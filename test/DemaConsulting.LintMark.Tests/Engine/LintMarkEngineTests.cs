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
using DemaConsulting.LintMark.Document;
using DemaConsulting.LintMark.Engine;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Unit tests for the LintMarkEngine class.
/// </summary>
[Collection("Sequential")]
public class LintMarkEngineTests
{
    /// <summary>
    ///     Runs an engine against a temporary directory as the current directory and captures its report.
    /// </summary>
    /// <param name="directory">The temporary directory (becomes the current directory while running).</param>
    /// <param name="engine">The engine to run.</param>
    /// <param name="arguments">Command-line arguments (paths and options).</param>
    /// <returns>The exit code and the reported lines.</returns>
    internal static (int ExitCode, string[] Lines) Execute(TemporaryDirectory directory, LintMarkEngine engine, params string[] arguments)
    {
        var originalDirectory = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(directory.FullPath);
            var log = directory.PathOf("report.log");
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
        [.. results.Select(r => r.Path + "|" + string.Join(";", r.Failures.Select(f => $"{f.RuleId}:{f.Line}:{f.Column}:{f.Message}")))];

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
        Assert.All(results, r => Assert.Empty(r.Failures));
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
        var failure = Assert.Single(result.Failures);
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
        var failure = Assert.Single(Assert.Single(new LintMarkEngine(1).Process([path])).Failures);

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
        var failure = Assert.Single(Assert.Single(new LintMarkEngine(1).Process([path])).Failures);

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
        var failure = Assert.Single(Assert.Single(new LintMarkEngine(1).Process([directory.PathOf("missing.md")])).Failures);

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
        var failure = Assert.Single(Assert.Single(new LintMarkEngine(1).Process([directory.FullPath])).Failures);

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
        var failure = Assert.Single(results.Single(r => r.Path == bad).Failures);
        Assert.Equal(("file-parse-error", 1, 1, "Markdown parser failed: InvalidOperationException"), (failure.RuleId, failure.Line, failure.Column, failure.Message));
        Assert.Empty(results.Single(r => r.Path == good).Failures);
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
        var engine = new LintMarkEngine(8, path =>
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
        var engine = new LintMarkEngine(4, path =>
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
        var engine = new LintMarkEngine(1, path =>
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
        var engine = new LintMarkEngine(2, path => new FileResult(path,
        [
            new FileFailure("rule-b", 3, 1, "later"),
            new FileFailure("rule-z", 1, 5, "second"),
            new FileFailure("rule-a", 1, 5, "first"),
            new FileFailure("rule-y", 1, 2, "earliest")
        ]));

        // Act: run
        var (_, lines) = Execute(directory, engine, "a.md");

        // Assert: sorted by line, column, then rule id
        Assert.Equal(
            ["a.md:1:2: rule-y earliest", "a.md:1:5: rule-a first", "a.md:1:5: rule-z second", "a.md:3:1: rule-b later"],
            lines);
    }

    /// <summary>
    ///     Runs an action with a directory as the current directory.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="directory">The directory.</param>
    /// <param name="action">The action.</param>
    /// <returns>The result of the action.</returns>
    private static T InDirectory<T>(TemporaryDirectory directory, Func<T> action)
    {
        var originalDirectory = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(directory.FullPath);
            return action();
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
        }
    }

    /// <summary>
    ///     Test that a log file named as an input file is refused before it is touched.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Select_LogFileNamedAsInput_ThrowsAndLeavesFileUntouched()
    {
        // Arrange: a source file that is also given as the log file
        using var directory = new TemporaryDirectory();
        var source = directory.WriteFile("README.md", "# Precious\n");
        using var context = Context.Create(["--log", source, "README.md"]);

        // Act / Assert: the run is refused with a message naming the file
        var exception = Assert.Throws<InvalidOperationException>(() => InDirectory(directory, () => LintMarkEngine.Select(context)));
        Assert.Contains("README.md", exception.Message);

        // Assert: the source file is intact
        Assert.Equal("# Precious\n", File.ReadAllText(source));
    }

    /// <summary>
    ///     Test that a log file found by default discovery is refused, and that nothing was written to it.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_LogFileFoundByDiscovery_ThrowsAndLeavesFileUntouched()
    {
        // Arrange: a Markdown file under the current directory used as the log, with default discovery
        using var directory = new TemporaryDirectory();
        directory.WriteFile("a.md");
        var source = directory.WriteFile("docs/notes.md", "# Precious\n");
        using var context = Context.Create(["--log", source]);

        // Act / Assert: the run is refused
        Assert.Throws<InvalidOperationException>(() => InDirectory(directory, () =>
        {
            new LintMarkEngine(1).Run(context);
            return 0;
        }));

        // Assert: the file is intact, and no failure was reported
        Assert.Equal("# Precious\n", File.ReadAllText(source));
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test that excluding the log file lets the run proceed with the other files.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Select_LogFileExcluded_SelectsOtherFiles()
    {
        // Arrange: a log in the selected tree that is excluded by a pattern
        using var directory = new TemporaryDirectory();
        directory.WriteFile("a.md");
        var log = directory.WriteFile("out.md", "old log\n");
        using var context = Context.Create(["--log", log, "!out.md"]);

        // Act: select
        var files = InDirectory(directory, () => LintMarkEngine.Select(context));

        // Assert: only the other file is selected, and the log is untouched until written
        Assert.Equal(["a.md"], files.Select(static f => Path.GetFileName(f)));
        Assert.Equal("old log\n", File.ReadAllText(log));
    }

    /// <summary>
    ///     Test that a log file outside the selection does not prevent a run.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Select_LogFileNotSelected_SelectsFiles()
    {
        // Arrange: a log whose name does not match the default pattern
        using var directory = new TemporaryDirectory();
        directory.WriteFile("a.md");
        using var context = Context.Create(["--log", directory.PathOf("run.log")]);

        // Act: select
        var files = InDirectory(directory, () => LintMarkEngine.Select(context));

        // Assert: the Markdown file is selected and the log file does not exist yet
        Assert.Equal(["a.md"], files.Select(static f => Path.GetFileName(f)));
        Assert.False(File.Exists(directory.PathOf("run.log")));
    }

    /// <summary>
    ///     Test that a log file reached through a symbolic link to an input is refused.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Select_LogFileIsSymbolicLinkToInput_ThrowsAndLeavesFileUntouched()
    {
        // Arrange: an input and a link to it that serves as the log (creating links may need a privilege)
        using var directory = new TemporaryDirectory();
        var source = directory.WriteFile("README.md", "# Precious\n");
        var link = directory.PathOf("report.log");
        try
        {
            File.CreateSymbolicLink(link, source);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            Assert.Skip("Symbolic links cannot be created in this environment");
        }

        using var context = Context.Create(["--log", link, "README.md"]);

        // Act / Assert: refused, because the log would overwrite the input through the link
        Assert.Throws<InvalidOperationException>(() => InDirectory(directory, () => LintMarkEngine.Select(context)));
        Assert.Equal("# Precious\n", File.ReadAllText(source));
    }

    /// <summary>
    ///     Test that a log file reached through a linked directory is refused.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Select_LogFileThroughLinkedDirectory_ThrowsAndLeavesFileUntouched()
    {
        // Arrange: a directory with an input, and a link to the directory used to name the log
        using var directory = new TemporaryDirectory();
        var source = directory.WriteFile("real/README.md", "# Precious\n");
        var linkedDirectory = directory.PathOf("alias");
        try
        {
            Directory.CreateSymbolicLink(linkedDirectory, directory.PathOf("real"));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            Assert.Skip("Symbolic links cannot be created in this environment");
        }

        using var context = Context.Create(["--log", Path.Combine(linkedDirectory, "README.md"), "real/README.md"]);

        // Act / Assert: refused, because both paths name the same file
        Assert.Throws<InvalidOperationException>(() => InDirectory(directory, () => LintMarkEngine.Select(context)));
        Assert.Equal("# Precious\n", File.ReadAllText(source));
    }

    /// <summary>
    ///     Test that a null context is rejected by selection.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Select_NullContext_ThrowsArgumentNullException()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentNullException>(() => LintMarkEngine.Select(null!));
    }

    /// <summary>
    ///     Test that a file result cannot be changed through its failure list.
    /// </summary>
    [Fact]
    public void FileResult_Failures_CastToMutableTypes_CannotBeModified()
    {
        // Arrange: a result built from an array that the creator keeps
        var source = new[] { new FileFailure("rule-a", 1, 1, "first") };
        var result = new FileResult("a.md", [.. source]);

        // Act: the creator changes its array afterwards
        source[0] = new FileFailure("rule-b", 2, 2, "changed");
        IReadOnlyList<FileFailure> failures = result.Failures;

        // Assert: the result is unaffected and a caller cannot change it
        Assert.Equal("rule-a", failures[0].RuleId);
        ReadOnlyAssertions.AssertCannotBeModified(failures);
    }
}
