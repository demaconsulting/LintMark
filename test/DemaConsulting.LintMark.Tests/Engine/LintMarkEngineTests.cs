// Copyright (c) DEMA Consulting
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System.Globalization;
using System.Text;
using DemaConsulting.LintMark.Cli;
using DemaConsulting.LintMark.Configuration;
using DemaConsulting.LintMark.Document;
using DemaConsulting.LintMark.Engine;
using DemaConsulting.LintMark.Linting;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Unit tests for the LintMarkEngine class.
/// </summary>
[Collection("Sequential")]
public partial class LintMarkEngineTests
{
    /// <summary>
    ///     Runs an engine against a temporary directory as the current directory and captures its report.
    /// </summary>
    /// <param name="directory">The temporary directory (becomes the current directory while running).</param>
    /// <param name="engine">The engine to run.</param>
    /// <param name="arguments">Command-line arguments (paths and options).</param>
    /// <returns>The exit code and the reported lines.</returns>
    internal static (int ExitCode, string[] Lines) Execute(TemporaryDirectory directory, LintMarkEngine engine, params string[] arguments) =>
        ExecuteIn(directory, ".", engine, arguments);

    /// <summary>
    ///     Runs an engine with a folder of a temporary directory as the current directory and captures its report.
    /// </summary>
    /// <param name="directory">The temporary directory that holds the working folder and the log.</param>
    /// <param name="workingDirectory">Path of the working folder relative to <paramref name="directory"/>.</param>
    /// <param name="engine">The engine to run.</param>
    /// <param name="arguments">Command-line arguments (paths and options).</param>
    /// <returns>The exit code and the reported lines.</returns>
    internal static (int ExitCode, string[] Lines) ExecuteIn(TemporaryDirectory directory, string workingDirectory, LintMarkEngine engine, params string[] arguments)
    {
        var originalDirectory = Directory.GetCurrentDirectory();
        try
        {
            var log = directory.PathOf("report.log");
            Directory.SetCurrentDirectory(directory.PathOf(workingDirectory));
            int exitCode;
            using (var context = Context.Create(["--silent", "--log", log, .. arguments]))
            {
                engine.Run(context);
                exitCode = context.ExitCode;
            }

            // The log file is created by the first line written, so a run that reports nothing leaves none
            return (exitCode, File.Exists(log) ? File.ReadAllLines(log) : []);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
        }
    }

    /// <summary>
    ///     Projects results to comparable strings (the result records hold lists, which compare by reference).
    /// </summary>
    /// <param name="results">The results.</param>
    /// <returns>One string per result.</returns>
    private static List<string> Project(IReadOnlyList<FileResult> results) =>
        [.. results.Select(r => r.Path + "|" + string.Join(";", r.Diagnostics.Select(f => $"{f.RuleId}:{f.Line}:{f.Column}:{f.Message}")))];

    /// <summary>
    ///     Test that valid files produce no failures.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Process_ValidFiles_ReturnsNoFailures()
    {
        // Arrange: two valid Markdown files
        using var directory = new TemporaryDirectory();
        var files = new[] { directory.WriteFile("a.md"), directory.WriteFile("b.md", "# B\n\n- item\n") };

        // Act: process them
        var results = new LintMarkEngine(1).Process(files);

        // Assert: two results without failures
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Empty(r.Diagnostics));
    }

    /// <summary>
    ///     Test that invalid UTF-8 is reported with rule and position.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Process_InvalidUtf8_ReportsEncodingError()
    {
        // Arrange: an invalid byte on the second line
        using var directory = new TemporaryDirectory();
        var path = directory.WriteBytes("bad.md", [.. "ok\nab"u8, 0xFF, .. "\n"u8]);

        // Act: process
        var result = Assert.Single(new LintMarkEngine(1).Process([path]));

        // Assert: one encoding failure at 2:3
        var failure = Assert.Single(result.Diagnostics);
        Assert.Equal(("file-encoding-error", 2, 3), (failure.RuleId, failure.Line, failure.Column));
    }

    /// <summary>
    ///     Test that a bare carriage return is reported with rule and position.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Process_LoneCr_ReportsLineEndingError()
    {
        // Arrange: a bare CR
        using var directory = new TemporaryDirectory();
        var path = directory.WriteBytes("cr.md", "ab\rcd\n"u8.ToArray());

        // Act: process
        var failure = Assert.Single(Assert.Single(new LintMarkEngine(1).Process([path])).Diagnostics);

        // Assert: line-ending failure at 1:3
        Assert.Equal(("file-line-ending-error", 1, 3), (failure.RuleId, failure.Line, failure.Column));
    }

    /// <summary>
    ///     Test that a UTF-16 file is reported as an encoding error.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Process_Utf16File_ReportsEncodingError()
    {
        // Arrange: UTF-16 content with a BOM
        using var directory = new TemporaryDirectory();
        var path = directory.WriteBytes("u16.md", [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes("# T\n")]);

        // Act: process
        var failure = Assert.Single(Assert.Single(new LintMarkEngine(1).Process([path])).Diagnostics);

        // Assert: encoding failure at 1:1
        Assert.Equal(("file-encoding-error", 1, 1), (failure.RuleId, failure.Line, failure.Column));
    }

    /// <summary>
    ///     Test that a missing file is reported as a read error.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Process_MissingFile_ReportsReadError()
    {
        // Arrange: a path that does not exist
        using var directory = new TemporaryDirectory();

        // Act: process
        var failure = Assert.Single(Assert.Single(new LintMarkEngine(1).Process([directory.PathOf("missing.md")])).Diagnostics);

        // Assert: read failure at 1:1 with a stable message
        Assert.Equal(("file-read-error", 1, 1, "File not found"), (failure.RuleId, failure.Line, failure.Column, failure.Message));
    }

    /// <summary>
    ///     Test that a path that cannot be read as a file (a directory) is reported as a read error.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Process_DirectoryAsFile_ReportsReadError()
    {
        // Arrange: a directory passed as a file
        using var directory = new TemporaryDirectory();

        // Act: process
        var failure = Assert.Single(Assert.Single(new LintMarkEngine(1).Process([directory.FullPath])).Diagnostics);

        // Assert: reported as a read failure
        Assert.Equal("file-read-error", failure.RuleId);
    }

    /// <summary>
    ///     Test that an exception from the Markdown parser is reported as a parse failure and does not stop other files.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Process_ParserThrows_ReportsParseErrorForThatFileOnly()
    {
        // Arrange: a parser that fails for one document only
        using var directory = new TemporaryDirectory();
        var bad = directory.WriteFile("bad.md", "# bad\n");
        var good = directory.WriteFile("good.md", "# good\n");
        var engine = new LintMarkEngine(
            2,
            documentParser: source => source.Text.Contains("bad", StringComparison.Ordinal)
                ? throw new InvalidOperationException("boom")
                : DocumentParser.Parse(source));

        // Act: process both
        var results = engine.Process([bad, good]);

        // Assert: only the bad file fails, with a stable message
        var failure = Assert.Single(results.Single(r => r.Path == bad).Diagnostics);
        Assert.Equal(("file-parse-error", 1, 1, "Markdown parser failed: InvalidOperationException"), (failure.RuleId, failure.Line, failure.Column, failure.Message));
        Assert.Empty(results.Single(r => r.Path == good).Diagnostics);
    }

    /// <summary>
    ///     Test that processing no files returns no results.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Process_NoFiles_ReturnsEmpty()
    {
        // Arrange / Act: process an empty list
        var results = new LintMarkEngine().Process([]);

        // Assert: nothing
        Assert.Empty(results);
    }

    /// <summary>
    ///     Test that results are sorted by path whatever the input order.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Process_InputOrder_DoesNotAffectResultOrder()
    {
        // Arrange: files given in reverse order
        using var directory = new TemporaryDirectory();
        var files = new[] { "c.md", "a.md", "b.md" }.Select(f => directory.WriteFile(f)).ToArray();

        // Act: process
        var results = new LintMarkEngine(4).Process(files);

        // Assert: results are in ordinal path order
        Assert.Equal(files.Order(StringComparer.Ordinal).ToList(), results.Select(r => r.Path).ToList());
    }

    /// <summary>
    ///     Test that the result order follows paths, not completion order, even when later files finish first.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Process_CompletionOrderInverted_ResultsStillPathSorted()
    {
        // Arrange: a processor in which every file waits (bounded) for the next one to finish first,
        // which makes completion order the reverse of path order whenever the workers run concurrently
        var files = Enumerable.Range(0, 8).Select(i => $"file{i}.md").ToArray();
        var finished = files.Select(_ => new ManualResetEventSlim(false)).ToArray();
        var completion = new List<string>();
        var engine = new LintMarkEngine(8, (path, _) =>
        {
            var index = int.Parse(path[4..5], CultureInfo.InvariantCulture);
            if (index < 7)
            {
                finished[index + 1].Wait(TimeSpan.FromSeconds(2));
            }

            lock (completion)
            {
                completion.Add(path);
            }

            finished[index].Set();
            return new FileResult(path, []);
        });

        // Act: process
        var results = engine.Process(files);

        // Assert: results are in path order regardless of the order in which workers completed
        Assert.Equal(files, results.Select(r => r.Path).ToList());
        Assert.Equal(8, completion.Count);
        Assert.All(finished, e => e.Dispose());
    }

    /// <summary>
    ///     Test that workers really run files at the same time when parallelism allows it.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Process_ManyWorkers_RunFilesConcurrently()
    {
        // Arrange: a processor in which the first two files to start wait (bounded) for each other;
        // if the engine ran files one at a time the first one would time out
        using var gate = new ManualResetEventSlim(false);
        var arrivals = 0;
        var timeouts = 0;
        var current = 0;
        var maximum = 0;
        var engine = new LintMarkEngine(4, (path, _) =>
        {
            var now = Interlocked.Increment(ref current);
            InterlockedMax(ref maximum, now);
            if (Interlocked.Increment(ref arrivals) >= 2)
            {
                gate.Set();
            }

            if (!gate.Wait(TimeSpan.FromSeconds(30)))
            {
                Interlocked.Increment(ref timeouts);
            }

            Interlocked.Decrement(ref current);
            return new FileResult(path, []);
        });

        // Act: process several files
        var results = engine.Process(Enumerable.Range(0, 8).Select(i => $"f{i}.md").ToArray());

        // Assert: nobody timed out, at least two files were in flight together, and every file has a result
        Assert.Equal(0, timeouts);
        Assert.True(maximum >= 2, $"maximum concurrency was {maximum}");
        Assert.Equal(8, results.Count);
    }

    /// <summary>
    ///     Test that a degree of parallelism of one never runs two files at the same time.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Process_ParallelismOne_RunsSequentially()
    {
        // Arrange: a processor that records the highest concurrency
        var current = 0;
        var maximum = 0;
        var engine = new LintMarkEngine(1, (path, _) =>
        {
            var now = Interlocked.Increment(ref current);
            InterlockedMax(ref maximum, now);
            Thread.SpinWait(2000);
            Interlocked.Decrement(ref current);
            return new FileResult(path, []);
        });

        // Act: process many files
        engine.Process(Enumerable.Range(0, 20).Select(i => $"f{i}.md").ToArray());

        // Assert: never more than one at a time
        Assert.Equal(1, maximum);
    }

    /// <summary>
    ///     Atomically raises a value to at least a candidate.
    /// </summary>
    /// <param name="target">The value to raise.</param>
    /// <param name="candidate">The candidate maximum.</param>
    private static void InterlockedMax(ref int target, int candidate)
    {
        int observed;
        do
        {
            observed = Volatile.Read(ref target);
        }
        while (candidate > observed && Interlocked.CompareExchange(ref target, candidate, observed) != observed);
    }

    /// <summary>
    ///     Test that parallel and sequential processing of a large mixed set give identical results.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Process_ParallelVersusSequential_ProduceIdenticalResults()
    {
        // Arrange: 240 files: valid, invalid UTF-8, bare CR and missing, and a reversed input order
        using var directory = new TemporaryDirectory();
        var files = new List<string>();
        for (var i = 0; i < 240; i++)
        {
            var name = $"d{i % 7}/file{i:D3}.md";
            if (i % 11 == 0)
            {
                files.Add(directory.PathOf(name));
            }
            else if (i % 5 == 0)
            {
                files.Add(directory.WriteBytes(name, [.. "x\ny"u8, 0xFF, .. "\n"u8]));
            }
            else if (i % 7 == 0)
            {
                files.Add(directory.WriteBytes(name, "line\rbreak\n"u8.ToArray()));
            }
            else
            {
                files.Add(directory.WriteFile(name, $"# File {i}\n\n- a\n- b\n\n```\nunclosed {i}\n"));
            }
        }

        var shuffled = files.AsEnumerable().Reverse().ToList();

        // Act: process sequentially, in parallel, and in parallel from the reversed order
        var sequential = Project(new LintMarkEngine(1).Process(files));
        var parallel = Project(new LintMarkEngine(8).Process(files));
        var parallelShuffled = Project(new LintMarkEngine(8).Process(shuffled));

        // Assert: identical, and the failing files really failed
        Assert.Equal(sequential, parallel);
        Assert.Equal(sequential, parallelShuffled);
        Assert.Contains(sequential, s => s.Contains("file-encoding-error", StringComparison.Ordinal));
        Assert.Contains(sequential, s => s.Contains("file-line-ending-error", StringComparison.Ordinal));
        Assert.Contains(sequential, s => s.Contains("file-read-error", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Test that an invalid degree of parallelism is rejected.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Constructor_ParallelismBelowOne_ThrowsArgumentOutOfRangeException()
    {
        // Arrange / Act / Assert: zero is rejected
        Assert.Throws<ArgumentOutOfRangeException>(() => new LintMarkEngine(0));
    }

    /// <summary>
    ///     Test that null arguments are rejected.
    /// </summary>
    [Fact]
    public void LintMarkEngine_RunAndProcess_NullArguments_Throw()
    {
        // Arrange: an engine
        var engine = new LintMarkEngine(1);

        // Act / Assert: nulls are rejected
        Assert.Throws<ArgumentNullException>(() => engine.Run(null!));
        Assert.Throws<ArgumentNullException>(() => engine.Process(null!));
    }

    /// <summary>
    ///     Test that a clean run writes nothing and leaves the exit code at zero.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_ValidFiles_WritesNothingAndExitsZero()
    {
        // Arrange: valid files
        using var directory = new TemporaryDirectory();
        directory.WriteFile("a.md");
        directory.WriteFile("docs/b.md");

        // Act: run with the default pattern
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(2));

        // Assert: no output, exit code zero
        Assert.Equal(0, exitCode);
        Assert.Empty(lines);
    }

    /// <summary>
    ///     Test that failures are written as path:line:col: rule-id message, in path order, and set the exit code.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_Failures_WritesFormattedLinesInPathOrder()
    {
        // Arrange: failing files whose directory order differs from path order
        using var directory = new TemporaryDirectory();
        directory.WriteBytes("z.md", "ab\rcd\n"u8.ToArray());
        directory.WriteBytes("docs/a.md", [.. "x"u8, 0xFF]);
        directory.WriteFile("m.md");

        // Act: run with the default pattern
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(4));

        // Assert: one formatted line per failure, sorted by display path, forward slashes, exit code one
        Assert.Equal(1, exitCode);
        Assert.Equal(
            [
                "docs/a.md:1:2: file-encoding-error Invalid UTF-8 byte sequence",
                "z.md:1:3: file-line-ending-error Bare carriage return (only LF and CRLF line endings are supported)"
            ],
            lines);
    }

    /// <summary>
    ///     Test that a missing explicit file is reported with its relative path.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_MissingExplicitFile_ReportsReadError()
    {
        // Arrange: an empty directory
        using var directory = new TemporaryDirectory();

        // Act: run with a file that does not exist
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(2), "missing.md");

        // Assert: read error at 1:1 and exit code one
        Assert.Equal(1, exitCode);
        Assert.Equal(["missing.md:1:1: file-read-error File not found"], lines);
    }

    /// <summary>
    ///     Test that a glob without matches is not an error.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_GlobWithoutMatches_WritesNothingAndExitsZero()
    {
        // Arrange: a directory without Markdown files
        using var directory = new TemporaryDirectory();
        directory.WriteFile("notes.txt", "x");

        // Act: run with a glob that matches nothing
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(2), "**/*.md");

        // Assert: no output, exit code zero
        Assert.Equal(0, exitCode);
        Assert.Empty(lines);
    }

    /// <summary>
    ///     Test that the check flag does not change the result in this phase.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_CheckFlag_BehavesLikeDefault()
    {
        // Arrange: a failing file
        using var directory = new TemporaryDirectory();
        directory.WriteBytes("bad.md", [0xFF]);

        // Act: run with and without --check
        var plain = Execute(directory, new LintMarkEngine(2), "bad.md");
        var check = Execute(directory, new LintMarkEngine(2), "--check", "bad.md");

        // Assert: identical outcome
        Assert.Equal(plain, check, (a, b) => a.ExitCode == b.ExitCode && a.Lines.SequenceEqual(b.Lines));
    }

    /// <summary>
    ///     Test that a parser failure is reported under the parse error rule through the report.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_ParserFailure_ReportsParseError()
    {
        // Arrange: a parser that always fails
        using var directory = new TemporaryDirectory();
        directory.WriteFile("a.md");
        var engine = new LintMarkEngine(2, documentParser: _ => throw new InvalidOperationException("boom"));

        // Act: run
        var (exitCode, lines) = Execute(directory, engine);

        // Assert: parse failure line and exit code one
        Assert.Equal(1, exitCode);
        Assert.Equal(["a.md:1:1: file-parse-error Markdown parser failed: InvalidOperationException"], lines);
    }

    /// <summary>
    ///     Test that the failures of one file are written ordered by line, column and rule.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_MultipleFailuresInFile_AreOrderedByPosition()
    {
        // Arrange: a processor that returns failures in scrambled order
        using var directory = new TemporaryDirectory();
        directory.WriteFile("a.md");
        var engine = new LintMarkEngine(2, (path, _) => new FileResult(path,
        [
            new Diagnostic("rule-b", DiagnosticKind.LintOnly, path, 3, 1, "later"),
            new Diagnostic("rule-z", DiagnosticKind.LintOnly, path, 1, 5, "second"),
            new Diagnostic("rule-a", DiagnosticKind.LintOnly, path, 1, 5, "first"),
            new Diagnostic("rule-y", DiagnosticKind.LintOnly, path, 1, 2, "earliest")
        ]));

        // Act: run
        var (_, lines) = Execute(directory, engine, "a.md");

        // Assert: sorted by line, column, then rule id
        Assert.Equal(
            ["a.md:1:2: rule-y earliest", "a.md:1:5: rule-a first", "a.md:1:5: rule-z second", "a.md:3:1: rule-b later"],
            lines);
    }

    /// <summary>
    ///     Test that a file result cannot be changed through its failure list.
    /// </summary>
    [Fact]
    public void FileResult_Diagnostics_CastToMutableTypes_CannotBeModified()
    {
        // Arrange: a result built from an array that the creator keeps
        var source = new[] { new Diagnostic("rule-a", DiagnosticKind.LintOnly, "a.md", 1, 1, "first") };
        var result = new FileResult("a.md", [.. source]);

        // Act: the creator changes its array afterwards
        source[0] = new Diagnostic("rule-b", DiagnosticKind.LintOnly, "a.md", 2, 2, "changed");
        IReadOnlyList<Diagnostic> diagnostics = result.Diagnostics;

        // Assert: the result is unaffected and a caller cannot change it
        Assert.Equal("rule-a", diagnostics[0].RuleId);
        ReadOnlyAssertions.AssertCannotBeModified(diagnostics);
    }

    /// <summary>
    ///     Writes an <c>.editorconfig</c> that stops the search and gives every Markdown file a line ending.
    /// </summary>
    /// <param name="directory">The directory to write into.</param>
    /// <param name="value">The value of <c>end_of_line</c>.</param>
    private static void WriteEditorConfig(TemporaryDirectory directory, string value) =>
        directory.WriteFile(".editorconfig", $"root = true\n\n[*.md]\nend_of_line = {value}\n");

    /// <summary>
    ///     Test that processing without a loader attaches the default configuration to every file.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Process_Files_AttachDefaultConfig()
    {
        // Arrange: two files, and a configuration file that must not be read by this overload
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "line-length: 50\n");
        var files = new[] { directory.WriteFile("a.md"), directory.WriteFile("b.md") };

        // Act: process them
        var results = new LintMarkEngine(1).Process(files);

        // Assert: every result carries the defaults and no configuration problem
        Assert.All(results, r => Assert.Equal(LintMarkConfig.Default, r.Config));
        Assert.All(results, r => Assert.Empty(r.ConfigErrors));
    }

    /// <summary>
    ///     Test that each file gets the settings of its own nearest configuration.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Process_DiscoveredConfigs_EachFileGetsItsNearestConfig()
    {
        // Arrange: a root configuration, a nested configuration and a folder without its own
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "line-length: 100\n");
        directory.WriteFile("nested/.lintmark.yaml", "line-length: 80\n");
        var root = directory.WriteFile("a.md");
        var nested = directory.WriteFile("nested/deeper/b.md");
        var plain = directory.WriteFile("plain/c.md");

        // Act: process them with discovery
        var results = new LintMarkEngine(4).Process([root, nested, plain], ConfigLoader.ForDiscovery(), null);

        // Assert: each result carries the settings of its nearest file
        Assert.Equal(100, results.Single(r => r.Path == root).Config!.LineLength);
        Assert.Equal(80, results.Single(r => r.Path == nested).Config!.LineLength);
        Assert.Equal(100, results.Single(r => r.Path == plain).Config!.LineLength);
    }

    /// <summary>
    ///     Test that the line ending of the configuration wins over the editor settings.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Process_ConfigEndOfLine_WinsOverEditorConfig()
    {
        // Arrange: a configuration that chooses lf and editor settings that choose crlf
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "end-of-line: lf\n");
        WriteEditorConfig(directory, "crlf");
        var file = directory.WriteFile("a.md");

        // Act: process with both sources
        var result = Assert.Single(new LintMarkEngine(1).Process([file], ConfigLoader.ForDiscovery(), new EditorConfigResolver()));

        // Assert: the configuration's line ending is used
        Assert.Equal(LineEnding.Lf, result.Config!.EndOfLine);
    }

    /// <summary>
    ///     Test that the editor settings are not consulted when the configuration gives a line ending.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Process_ConfigEndOfLine_EditorConfigNotConsulted()
    {
        // Arrange: a configuration that chooses crlf and a resolver that fails when consulted
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "end-of-line: crlf\n");
        var file = directory.WriteFile("a.md");
        var resolver = new EditorConfigResolver(_ => throw new IOException("consulted"), (_, _) => throw new InvalidOperationException());

        // Act: process with both sources
        var result = Assert.Single(new LintMarkEngine(1).Process([file], ConfigLoader.ForDiscovery(), resolver));

        // Assert: the configuration's line ending is used and the failing resolver was never asked
        Assert.Equal(LineEnding.CrLf, result.Config!.EndOfLine);
        Assert.Empty(result.ConfigErrors);
    }

    /// <summary>
    ///     Test that the editor settings supply the line ending when the configuration gives none.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Process_ConfigSilent_FallsBackToEditorConfig()
    {
        // Arrange: a configuration without a line ending and editor settings that choose crlf
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "line-length: 90\n");
        WriteEditorConfig(directory, "crlf");
        var file = directory.WriteFile("a.md");

        // Act: process with both sources
        var result = Assert.Single(new LintMarkEngine(1).Process([file], ConfigLoader.ForDiscovery(), new EditorConfigResolver()));

        // Assert: the editor settings' line ending is used and the rest of the configuration is kept
        Assert.Equal(LineEnding.CrLf, result.Config!.EndOfLine);
        Assert.Equal(90, result.Config.LineLength);
    }

    /// <summary>
    ///     Test that a line ending specified nowhere stays unspecified.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Process_NeitherSpecifiesEndOfLine_StaysNull()
    {
        // Arrange: editor settings that say nothing about line endings
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".editorconfig", "root = true\n\n[*]\nindent_style = space\n");
        var file = directory.WriteFile("a.md");

        // Act: process with both sources
        var result = Assert.Single(new LintMarkEngine(1).Process([file], ConfigLoader.ForDiscovery(), new EditorConfigResolver()));

        // Assert: still unspecified, and no problem
        Assert.Null(result.Config!.EndOfLine);
        Assert.Empty(result.ConfigErrors);
    }

    /// <summary>
    ///     Test that an unsupported editor value leaves the line ending unspecified.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Process_EditorConfigCr_EndOfLineStaysNull()
    {
        // Arrange: editor settings that choose cr
        using var directory = new TemporaryDirectory();
        WriteEditorConfig(directory, "cr");
        var file = directory.WriteFile("a.md");

        // Act: process with both sources
        var result = Assert.Single(new LintMarkEngine(1).Process([file], ConfigLoader.ForDiscovery(), new EditorConfigResolver()));

        // Assert: unspecified, with no problem
        Assert.Null(result.Config!.EndOfLine);
        Assert.Empty(result.ConfigErrors);
    }

    /// <summary>
    ///     Test that without an editor-settings resolver the editor settings are not read.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Process_NoEditorConfigResolver_EndOfLineStaysNull()
    {
        // Arrange: editor settings that choose crlf
        using var directory = new TemporaryDirectory();
        WriteEditorConfig(directory, "crlf");
        var file = directory.WriteFile("a.md");

        // Act: process without a resolver
        var result = Assert.Single(new LintMarkEngine(1).Process([file], ConfigLoader.ForDiscovery(), null));

        // Assert: the editor settings had no effect
        Assert.Null(result.Config!.EndOfLine);
    }

    /// <summary>
    ///     Test that an explicit configuration still consults the editor settings.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Process_ExplicitConfig_StillConsultsEditorConfig()
    {
        // Arrange: an explicit configuration without a line ending and editor settings that choose crlf
        using var directory = new TemporaryDirectory();
        var config = directory.WriteFile("custom.yaml", "line-length: 70\n");
        WriteEditorConfig(directory, "crlf");
        var file = directory.WriteFile("a.md");

        // Act: process with the explicit file
        var result = Assert.Single(new LintMarkEngine(1).Process([file], ConfigLoader.ForFile(config), new EditorConfigResolver()));

        // Assert: the editor settings' line ending is used
        Assert.Equal(LineEnding.CrLf, result.Config!.EndOfLine);
        Assert.Equal(70, result.Config.LineLength);
    }

    /// <summary>
    ///     Test that an explicit configuration is used instead of a discovered one.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Process_ExplicitConfig_UsedInsteadOfDiscoveredConfig()
    {
        // Arrange: an invalid discoverable configuration and a valid explicit one
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "bogus: 1\n");
        var config = directory.WriteFile("custom.yaml", "line-length: 77\n");
        var file = directory.WriteFile("a.md");

        // Act: process with the explicit file
        var result = Assert.Single(new LintMarkEngine(1).Process([file], ConfigLoader.ForFile(config), null));

        // Assert: the explicit file's settings are used and the discoverable file's problem is not reported
        Assert.Equal(77, result.Config!.LineLength);
        Assert.Empty(result.ConfigErrors);
    }

    /// <summary>
    ///     Test that a file with an invalid configuration is still read and parsed.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Process_InvalidConfig_FileStillParsedAndConfigNull()
    {
        // Arrange: an invalid configuration and a file that cannot be decoded
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "bogus: 1\n");
        var bad = directory.WriteBytes("bad.md", [0xFF]);
        var good = directory.WriteFile("good.md");

        // Act: process both
        var results = new LintMarkEngine(2).Process([bad, good], ConfigLoader.ForDiscovery(), new EditorConfigResolver());

        // Assert: no configuration, the configuration problem on both files, and the decoding failure still found
        Assert.All(results, r => Assert.Null(r.Config));
        Assert.All(results, r => Assert.Single(r.ConfigErrors));
        Assert.Equal("file-encoding-error", Assert.Single(results.Single(r => r.Path == bad).Diagnostics).RuleId);
        Assert.Empty(results.Single(r => r.Path == good).Diagnostics);
    }

    /// <summary>
    ///     Test that editor settings that cannot be read add a problem at the Markdown file and keep the configuration.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Process_EditorConfigUnreadable_AddsConfigErrorAtMarkdownFileAndKeepsConfig()
    {
        // Arrange: a resolver whose settings cannot be read
        using var directory = new TemporaryDirectory();
        var file = directory.WriteFile("a.md");
        var resolver = new EditorConfigResolver(_ => throw new IOException("disk failure"), (_, _) => throw new InvalidOperationException());

        // Act: process with it
        var result = Assert.Single(new LintMarkEngine(1).Process([file], ConfigLoader.ForDefaults(), resolver));

        // Assert: one problem at the start of the Markdown file; the configuration is kept with an unspecified line ending
        var error = Assert.Single(result.ConfigErrors);
        Assert.Equal((file, 1, 1, "Cannot read .editorconfig settings: disk failure"), (error.Path, error.Line, error.Column, error.Message));
        Assert.NotNull(result.Config);
        Assert.Null(result.Config.EndOfLine);
    }

    /// <summary>
    ///     Test that parallel processing with configurations gives the same results as sequential processing.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Process_ParallelWithConfigs_EqualsSequential()
    {
        // Arrange: many files below several configurations, one of them invalid, and editor settings
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "line-length: 100\n");
        directory.WriteFile("a/.lintmark.yaml", "end-of-line: lf\n");
        directory.WriteFile("b/.lintmark.yaml", "bogus: 1\n");
        WriteEditorConfig(directory, "crlf");
        var files = Enumerable.Range(0, 60).Select(i => directory.WriteFile($"{(char)('a' + (i % 4))}/f{i:D2}.md")).ToArray();

        // Act: process sequentially and in parallel
        var sequential = new LintMarkEngine(1).Process(files, ConfigLoader.ForDiscovery(), new EditorConfigResolver());
        var parallel = new LintMarkEngine(8).Process(files, ConfigLoader.ForDiscovery(), new EditorConfigResolver());

        // Assert: identical results in identical order
        Assert.Equal(Describe(sequential), Describe(parallel));
        return;

        static List<string> Describe(IReadOnlyList<FileResult> results) =>
            [.. results.Select(r => $"{r.Path}|{r.Config?.LineLength}|{r.Config?.EndOfLine}|{string.Join(";", r.ConfigErrors.Select(e => e.Message))}")];
    }

    /// <summary>
    ///     Test that the configuration problems of a result cannot be modified through a cast.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Process_ConfigErrors_CastToMutableTypes_CannotBeModified()
    {
        // Arrange: a file with an invalid configuration
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "bogus: 1\n");
        var file = directory.WriteFile("a.md");

        // Act: process it
        var result = Assert.Single(new LintMarkEngine(1).Process([file], ConfigLoader.ForDiscovery(), null));

        // Assert: a caller cannot change the list
        ReadOnlyAssertions.AssertCannotBeModified((IReadOnlyList<ConfigError>)result.ConfigErrors);
    }

    /// <summary>
    ///     Test that a null loader is rejected.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Process_NullLoader_Throws()
    {
        // Act & Assert: the loader is validated
        Assert.Throws<ArgumentNullException>(() => new LintMarkEngine(1).Process(Array.Empty<string>(), null!, null));
    }

    /// <summary>
    ///     Test that an invalid configuration is reported as a positioned configuration error and gives exit code one.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_InvalidConfig_ReportsErrorLineAndExitsOne()
    {
        // Arrange: a valid file beside an invalid configuration
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "line-length: abc\n");
        directory.WriteFile("a.md");

        // Act: run
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(2));

        // Assert: one line in path:line:col: rule-id message form
        Assert.Equal(1, exitCode);
        Assert.Equal(
            [".lintmark.yaml:1:14: config-error Invalid value 'abc' for setting 'line-length'; expected a whole number without quotes from 0 to 100000"],
            lines);
    }

    /// <summary>
    ///     Test that a configuration problem shared by many files is reported once.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_ConfigErrorSharedByManyFiles_ReportedOnce()
    {
        // Arrange: one invalid configuration used by files in many folders
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "bogus: 1\n");
        foreach (var i in Enumerable.Range(0, 12))
        {
            directory.WriteFile($"d{i % 3}/f{i}.md");
        }

        // Act: run in parallel
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(8));

        // Assert: reported once
        Assert.Equal(1, exitCode);
        Assert.Equal([".lintmark.yaml:1:1: config-error Unknown setting 'bogus'"], lines);
    }

    /// <summary>
    ///     Test that configuration problems and file failures are merged in path then position order.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_ConfigErrorsAndFileFailures_MergedInPathThenPositionOrder()
    {
        // Arrange: a root configuration with two problems, a nested configuration with one, and failing files
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "wrap: nope\nbogus: 1\n");
        directory.WriteFile("docs/.lintmark.yaml", "tables: grid\n");
        directory.WriteBytes("a.md", [0xFF]);
        directory.WriteBytes("docs/x.md", [0xFF]);

        // Act: run
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(4));

        // Assert: ordinal path order, so the dotted file names come first, then position within a file
        Assert.Equal(1, exitCode);
        Assert.Equal(
            [
                ".lintmark.yaml:1:7: config-error Invalid value 'nope' for setting 'wrap'; expected one of: fill, sentence, semantic",
                ".lintmark.yaml:2:1: config-error Unknown setting 'bogus'",
                "a.md:1:1: file-encoding-error Invalid UTF-8 byte sequence",
                "docs/.lintmark.yaml:1:9: config-error Invalid value 'grid' for setting 'tables'; expected one of: compact, aligned",
                "docs/x.md:1:1: file-encoding-error Invalid UTF-8 byte sequence"
            ],
            lines);
    }

    /// <summary>
    ///     Test that an invalid configuration does not hide a read failure of a Markdown file.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_InvalidConfig_StillReportsFileReadFailures()
    {
        // Arrange: an invalid configuration and a missing file named explicitly
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "bogus: 1\n");

        // Act: run with the missing file
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(2), "missing.md");

        // Assert: both problems are reported
        Assert.Equal(1, exitCode);
        Assert.Equal(
            [".lintmark.yaml:1:1: config-error Unknown setting 'bogus'", "missing.md:1:1: file-read-error File not found"],
            lines);
    }

    /// <summary>
    ///     Test that a missing explicit configuration is reported even when no Markdown file is selected.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_ExplicitConfigMissing_ReportsErrorEvenWithNoFiles()
    {
        // Arrange: an empty directory
        using var directory = new TemporaryDirectory();

        // Act: run with a configuration file that does not exist
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(2), "--config", "missing.yaml");

        // Assert: reported at 1:1
        Assert.Equal(1, exitCode);
        Assert.Equal(["missing.yaml:1:1: config-error Configuration file not found"], lines);
    }

    /// <summary>
    ///     Test that an explicit relative configuration is reported with a display path relative to the current directory.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_ExplicitConfigRelative_ReportedWithRelativeDisplayPath()
    {
        // Arrange: an invalid configuration in a sub folder, and a Markdown file
        using var directory = new TemporaryDirectory();
        directory.WriteFile("conf/custom.yaml", "bogus: 1\n");
        directory.WriteFile("a.md");

        // Act: run with the file named with backslashes
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(2), "--config", "conf\\custom.yaml");

        // Assert: reported once, with forward slashes
        Assert.Equal(1, exitCode);
        Assert.Equal(["conf/custom.yaml:1:1: config-error Unknown setting 'bogus'"], lines);
    }

    /// <summary>
    ///     Test that a configuration outside the current directory is reported with a parent-relative path.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_ConfigOutsideCurrentDirectory_ReportedWithParentRelativePath()
    {
        // Arrange: the current directory is a sub folder, the invalid configuration sits beside it
        using var directory = new TemporaryDirectory();
        directory.WriteFile("other.yaml", "bogus: 1\n");
        directory.WriteFile("sub/a.md");

        // Act: run inside the sub folder with a configuration in the parent
        var (exitCode, lines) = ExecuteIn(directory, "sub", new LintMarkEngine(2), "--config", "../other.yaml");

        // Assert: reported relative to the current directory
        Assert.Equal(1, exitCode);
        Assert.Equal(["../other.yaml:1:1: config-error Unknown setting 'bogus'"], lines);
    }

    /// <summary>
    ///     Test that unreadable editor settings are reported at the Markdown file with exit code one.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_EditorConfigUnreadable_ReportsConfigErrorAtMarkdownFile()
    {
        // Arrange: a Markdown file and a resolver whose settings cannot be read
        using var directory = new TemporaryDirectory();
        directory.WriteFile("a.md");
        var engine = new LintMarkEngine(
            1,
            editorConfigFactory: () => new EditorConfigResolver(_ => throw new IOException("disk failure"), (_, _) => throw new InvalidOperationException()));

        // Act: run
        var (exitCode, lines) = Execute(directory, engine);

        // Assert: reported against the Markdown file at 1:1
        Assert.Equal(1, exitCode);
        Assert.Equal(["a.md:1:1: config-error Cannot read .editorconfig settings: disk failure"], lines);
    }

    /// <summary>
    ///     Test that no-config ignores both an invalid configuration and the editor settings.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_NoConfig_InvalidConfigAndUnreadableEditorConfig_ReportsNothing()
    {
        // Arrange: an invalid configuration, and a resolver that would fail if it were consulted
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "bogus: 1\n");
        directory.WriteFile("a.md");
        var engine = new LintMarkEngine(
            1,
            editorConfigFactory: () => throw new InvalidOperationException("The editor settings must not be consulted"));

        // Act: run with --no-config
        var (exitCode, lines) = Execute(directory, engine, "--no-config");

        // Assert: silent success
        Assert.Equal(0, exitCode);
        Assert.Empty(lines);
    }

    /// <summary>
    ///     Test that an explicit configuration still consults the editor settings during a run.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_ExplicitConfig_StillConsultsEditorConfig()
    {
        // Arrange: a valid explicit configuration and a resolver that fails when consulted
        using var directory = new TemporaryDirectory();
        directory.WriteFile("custom.yaml", "line-length: 90\n");
        directory.WriteFile("a.md");
        var engine = new LintMarkEngine(
            1,
            editorConfigFactory: () => new EditorConfigResolver(_ => throw new IOException("consulted"), (_, _) => throw new InvalidOperationException()));

        // Act: run with --config
        var (exitCode, lines) = Execute(directory, engine, "--config", "custom.yaml");

        // Assert: the editor settings were consulted, which is visible through the failure they report
        Assert.Equal(1, exitCode);
        Assert.Equal(["a.md:1:1: config-error Cannot read .editorconfig settings: consulted"], lines);
    }

    /// <summary>
    ///     Test that parallel and sequential runs produce identical output when configurations have problems.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_ConfigErrors_ParallelAndSequentialProduceIdenticalOutput()
    {
        // Arrange: invalid configurations in several folders and many files below them
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "wrap: nope\n");
        directory.WriteFile("b/.lintmark.yaml", "bogus: 1\n");
        directory.WriteFile("c/.lintmark.yaml", "line-length: 0\nrules:\n  MD013: false\n  line-length: true\n");
        foreach (var i in Enumerable.Range(0, 40))
        {
            directory.WriteFile($"{(char)('a' + (i % 4))}/f{i:D2}.md");
        }

        directory.WriteBytes("a/bad.md", [0xFF]);

        // Act: run sequentially and in parallel
        var sequential = Execute(directory, new LintMarkEngine(1));
        var parallel = Execute(directory, new LintMarkEngine(16));

        // Assert: identical output and exit code, with each problem reported once
        Assert.Equal(1, sequential.ExitCode);
        Assert.Equal(sequential.ExitCode, parallel.ExitCode);
        Assert.Equal(sequential.Lines, parallel.Lines);
        Assert.Equal(sequential.Lines.Length, sequential.Lines.Distinct().Count());
        Assert.Equal(4, sequential.Lines.Length);
    }

    /// <summary>
    ///     Creates a valid diagnostic for a file named a.md.
    /// </summary>
    /// <returns>The diagnostic.</returns>
    private static Diagnostic SampleDiagnostic() =>
        new("blank-lines", DiagnosticKind.LintOnly, "a.md", 1, 1, "Message");

    /// <summary>
    ///     Test that a file result keeps its values when they are valid.
    /// </summary>
    [Fact]
    public void FileResult_Constructor_ValidArguments_KeepsValues()
    {
        // Arrange / Act: a result with one diagnostic
        var diagnostic = SampleDiagnostic();
        var result = new FileResult("a.md", [diagnostic]);

        // Assert: the values are kept
        Assert.Equal("a.md", result.Path);
        Assert.Equal(diagnostic, Assert.Single(result.Diagnostics));
        Assert.Null(result.Config);
    }

    /// <summary>
    ///     Test that a file result without a path is rejected.
    /// </summary>
    [Fact]
    public void FileResult_Constructor_MissingPath_Throws()
    {
        // Arrange / Act / Assert: the path may be neither null nor empty
        Assert.Throws<ArgumentNullException>(() => new FileResult(null!, []));
        Assert.Throws<ArgumentException>(() => new FileResult(string.Empty, []));
    }

    /// <summary>
    ///     Test that a file result with default or null diagnostics is rejected.
    /// </summary>
    [Fact]
    public void FileResult_Constructor_InvalidDiagnostics_Throws()
    {
        // Arrange / Act / Assert: a default array and a null element are rejected
        Assert.Throws<ArgumentException>(() => new FileResult("a.md", default));
        Assert.Throws<ArgumentNullException>(() => new FileResult("a.md", [null!]));
    }

    /// <summary>
    ///     Test that a file change keeps its values when they are valid.
    /// </summary>
    [Fact]
    public void FileChange_Constructor_ValidArguments_KeepsValues()
    {
        // Arrange / Act: a change with both byte arrays
        var change = new FileChange("a\n", "b\n", new LineEndingInfo(1, 0), LineEnding.Lf, [1], [2]);

        // Assert: the values are kept
        Assert.Equal("a\n", change.OriginalLf);
        Assert.Equal("b\n", change.NewLf);
        Assert.Equal(LineEnding.Lf, change.TargetEnding);
        Assert.Equal([(byte)1], change.OriginalBytes);
        Assert.Equal([(byte)2], change.NewBytes);
    }

    /// <summary>
    ///     Test that a file change with a null text is rejected.
    /// </summary>
    [Fact]
    public void FileChange_Constructor_NullText_Throws()
    {
        // Arrange / Act / Assert: neither text may be null
        Assert.Throws<ArgumentNullException>(() => new FileChange(null!, "b\n", default, LineEnding.Lf, [1], [2]));
        Assert.Throws<ArgumentNullException>(() => new FileChange("a\n", null!, default, LineEnding.Lf, [1], [2]));
    }

    /// <summary>
    ///     Test that a file change with default byte arrays is rejected.
    /// </summary>
    [Fact]
    public void FileChange_Constructor_DefaultBytes_Throws()
    {
        // Arrange / Act / Assert: neither byte array may be a default array
        Assert.Throws<ArgumentException>(() => new FileChange("a\n", "b\n", default, LineEnding.Lf, default, [2]));
        Assert.Throws<ArgumentException>(() => new FileChange("a\n", "b\n", default, LineEnding.Lf, [1], default));
    }

    /// <summary>
    ///     Test that a file change with an undefined line ending is rejected.
    /// </summary>
    [Fact]
    public void FileChange_Constructor_UndefinedLineEnding_Throws()
    {
        // Arrange / Act / Assert: the target ending must be a defined value
        Assert.Throws<ArgumentOutOfRangeException>(() => new FileChange("a\n", "b\n", default, (LineEnding)99, [1], [2]));
    }
}
