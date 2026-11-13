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

using System.Security;
using DemaConsulting.LintMark.Cli;
using DemaConsulting.LintMark.Configuration;
using DemaConsulting.LintMark.Document;

namespace DemaConsulting.LintMark.Engine;

/// <summary>
///     Discovers the Markdown files named on the command line, processes them in parallel, attaches each
///     file's effective configuration, and reports read, parse and configuration failures in a deterministic order.
/// </summary>
/// <remarks>
///     <para>
///         Each file is processed independently (read, decode, parse, resolve configuration) by a worker.
///         Workers never write output and never touch the <see cref="Context"/>; they return a
///         <see cref="FileResult"/> stored by input index. After all workers finish, the failures and the
///         distinct configuration problems are merged, sorted by path (ordinal), then position, and reported
///         sequentially, so console/log output and the exit code do not depend on scheduling. The degree of
///         parallelism defaults to the processor count and can be limited (to one for sequential processing)
///         for tests and diagnosis; it is not a user option.
///     </para>
///     <para>
///         The effective configuration of a file is the configuration that applies to it (the explicit file,
///         the nearest discovered file, or the built-in defaults, depending on the command line) with the line
///         ending taken from the editor settings when the configuration gives none. A file whose configuration
///         is invalid is still read and parsed, so its own failures are still reported. A configuration problem
///         shared by many files is reported once.
///     </para>
///     <para>
///         Only failures are reported, one per line, as <c>path:line:col: rule-id message</c> through
///         <see cref="Context.WriteError"/>; paths are relative to the current directory with forward
///         slashes. There is no summary output on success. Phase 2 never writes files, so the
///         <c>--check</c> option does not change behavior.
///     </para>
///     <para>
///         Thread safety: an instance holds only immutable configuration; <see cref="Process(IReadOnlyList{string})"/>
///         and <see cref="Run(Context)"/> may be called concurrently.
///     </para>
/// </remarks>
internal sealed class LintMarkEngine
{
    /// <summary>
    ///     Rule identifier for a file that cannot be read (missing, permission, I/O error).
    /// </summary>
    public const string ReadErrorRuleId = "file-read-error";

    /// <summary>
    ///     Rule identifier for a file whose content makes the Markdown parser throw.
    /// </summary>
    public const string ParseErrorRuleId = "file-parse-error";

    /// <summary>
    ///     Maximum number of files processed concurrently.
    /// </summary>
    private readonly int _maxDegreeOfParallelism;

    /// <summary>
    ///     Processes a single file; replaceable so tests can control timing.
    /// </summary>
    private readonly Func<string, FileResult> _fileProcessor;

    /// <summary>
    ///     Parses a decoded document; replaceable so tests can simulate parser failures.
    /// </summary>
    private readonly Func<SourceDocument, ParsedDocument> _documentParser;

    /// <summary>
    ///     Creates the editor-settings resolver used by <see cref="Run(Context)"/>; replaceable so tests can
    ///     simulate unreadable editor settings.
    /// </summary>
    private readonly Func<EditorConfigResolver> _editorConfigFactory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="LintMarkEngine"/> class with default settings
    ///     (parallelism equal to the processor count).
    /// </summary>
    public LintMarkEngine()
        : this(Environment.ProcessorCount)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="LintMarkEngine"/> class.
    /// </summary>
    /// <param name="maxDegreeOfParallelism">Maximum number of files processed concurrently; 1 means sequential.</param>
    /// <param name="fileProcessor">
    ///     Optional replacement for the per-file processing (a test seam); <see langword="null"/> selects the real read-and-parse.
    /// </param>
    /// <param name="documentParser">
    ///     Optional replacement for the Markdown parsing step of the real per-file processing (a test seam);
    ///     <see langword="null"/> selects <see cref="DocumentParser.Parse"/>.
    /// </param>
    /// <param name="editorConfigFactory">
    ///     Optional replacement for the creation of the editor-settings resolver used by <see cref="Run(Context)"/>
    ///     (a test seam); <see langword="null"/> selects a resolver that reads the real file system.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxDegreeOfParallelism"/> is less than one.</exception>
    internal LintMarkEngine(
        int maxDegreeOfParallelism,
        Func<string, FileResult>? fileProcessor = null,
        Func<SourceDocument, ParsedDocument>? documentParser = null,
        Func<EditorConfigResolver>? editorConfigFactory = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxDegreeOfParallelism, 1);
        _maxDegreeOfParallelism = maxDegreeOfParallelism;
        _documentParser = documentParser ?? DocumentParser.Parse;
        _fileProcessor = fileProcessor ?? ProcessFile;
        _editorConfigFactory = editorConfigFactory ?? (static () => new EditorConfigResolver());
    }

    /// <summary>
    ///     Discovers, processes and reports the files selected by the context.
    /// </summary>
    /// <param name="context">The context supplying the path and configuration arguments and receiving the reports. Must not be null.</param>
    /// <remarks>
    ///     Failures and configuration problems are written through <see cref="Context.WriteError"/>, which
    ///     makes the exit code 1. Discovery runs against the current directory. <c>--no-config</c> selects
    ///     the built-in defaults and reads neither configuration source; <c>--config</c> selects exactly that
    ///     file (its problems are reported even when no Markdown file is selected); otherwise the nearest
    ///     <c>.lintmark.yaml</c> is discovered per file. The editor settings are consulted unless
    ///     <c>--no-config</c> is given.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a path argument is empty (reported by the caller as a usage error).</exception>
    public void Run(Context context)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(context);

        // Discover the files against the current directory
        var baseDirectory = Directory.GetCurrentDirectory();
        var files = FileDiscovery.Discover(context.Paths, baseDirectory);

        // Choose the configuration sources from the command line, then process; no output happens until all workers are done
        var loader = CreateLoader(context);
        var editorConfig = context.NoConfig ? null : _editorConfigFactory();
        var results = Process(files, loader, editorConfig);

        // Merge failures and the distinct configuration problems into one deterministic report
        var lines = results
            .SelectMany(r => r.Failures.Select(f => new ReportLine(GetDisplayPath(r.Path, baseDirectory), f.Line, f.Column, f.RuleId, f.Message)))
            .Concat(results
                .SelectMany(static r => r.ConfigErrors)
                .Concat(loader.InitialErrors)
                .Distinct()
                .Select(e => new ReportLine(GetDisplayPath(e.Path, baseDirectory), e.Line, e.Column, ConfigLoader.ErrorRuleId, e.Message)))
            .OrderBy(static l => l.Path, StringComparer.Ordinal)
            .ThenBy(static l => l.Line)
            .ThenBy(static l => l.Column)
            .ThenBy(static l => l.RuleId, StringComparer.Ordinal)
            .ThenBy(static l => l.Message, StringComparer.Ordinal);
        foreach (var line in lines)
        {
            context.WriteError($"{line.Path}:{line.Line}:{line.Column}: {line.RuleId} {line.Message}");
        }
    }

    /// <summary>
    ///     Processes files with the built-in default configuration, possibly in parallel, and returns their results in path order.
    /// </summary>
    /// <param name="files">Paths of the files to process. Must not be null.</param>
    /// <returns>One result per input file, sorted by path (ordinal), independent of completion order.</returns>
    /// <remarks>
    ///     Equivalent to <see cref="Process(IReadOnlyList{string}, ConfigLoader, EditorConfigResolver?)"/> with a
    ///     defaults-only loader and no editor settings: no configuration source is read.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="files"/> is null.</exception>
    public IReadOnlyList<FileResult> Process(IReadOnlyList<string> files) => Process(files, ConfigLoader.ForDefaults(), null);

    /// <summary>
    ///     Processes files, possibly in parallel, attaches each file's effective configuration and returns the results in path order.
    /// </summary>
    /// <param name="files">Paths of the files to process. Must not be null.</param>
    /// <param name="loader">Supplies the configuration that applies to each file. Must not be null.</param>
    /// <param name="editorConfig">Supplies the line ending from the editor settings, or <see langword="null"/> to ignore them.</param>
    /// <returns>One result per input file, sorted by path (ordinal), independent of completion order.</returns>
    /// <remarks>
    ///     Results are stored by input index while workers run and sorted afterwards, which keeps the
    ///     outcome identical for any degree of parallelism and any scheduling. Workers must not write
    ///     output. The effective configuration is derived with a <c>with</c> expression on the immutable
    ///     configuration, so nothing shared is mutated. The editor settings are consulted only when the
    ///     configuration gives no line ending. If a replaced file processor throws, the exception is
    ///     propagated (wrapped in an <see cref="AggregateException"/> when run in parallel).
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="files"/> or <paramref name="loader"/> is null.</exception>
    internal IReadOnlyList<FileResult> Process(IReadOnlyList<string> files, ConfigLoader loader, EditorConfigResolver? editorConfig)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(loader);

        // Process every file; each worker writes only its own slot
        var results = new FileResult[files.Count];
        var options = new ParallelOptions { MaxDegreeOfParallelism = _maxDegreeOfParallelism };
        Parallel.For(0, files.Count, options, index => results[index] = AttachConfiguration(_fileProcessor(files[index]), loader, editorConfig));

        // Order by path (stable) so the caller never observes scheduling order
        return [.. results.OrderBy(static r => r.Path, StringComparer.Ordinal)];
    }

    /// <summary>
    ///     Chooses the configuration loader from the command line.
    /// </summary>
    /// <param name="context">The context holding the configuration options.</param>
    /// <returns>A defaults-only loader for <c>--no-config</c>, an explicit-file loader for <c>--config</c>, otherwise a discovering loader.</returns>
    private static ConfigLoader CreateLoader(Context context)
    {
        if (context.NoConfig)
        {
            return ConfigLoader.ForDefaults();
        }

        return context.ConfigFile is null ? ConfigLoader.ForDiscovery() : ConfigLoader.ForFile(context.ConfigFile);
    }

    /// <summary>
    ///     Attaches the effective configuration of a file to its result.
    /// </summary>
    /// <param name="result">The result of reading and parsing the file.</param>
    /// <param name="loader">Supplies the configuration that applies to the file.</param>
    /// <param name="editorConfig">Supplies the line ending from the editor settings, or <see langword="null"/> to ignore them.</param>
    /// <returns>The result with its configuration or configuration problems.</returns>
    /// <remarks>
    ///     An invalid configuration leaves <see cref="FileResult.Config"/> null and carries the problems; the
    ///     file's own failures are kept. Editor settings that cannot be read add one problem at the start of the
    ///     Markdown file and leave the line ending unspecified.
    /// </remarks>
    private static FileResult AttachConfiguration(FileResult result, ConfigLoader loader, EditorConfigResolver? editorConfig)
    {
        // An invalid configuration yields no settings, only its problems
        var load = loader.Load(result.Path);
        if (load.Config is not { } config)
        {
            return result with { Config = null, ConfigErrors = load.Errors };
        }

        // The configuration's own line ending wins; the editor settings are consulted only when it gives none
        if (config.EndOfLine is null && editorConfig is not null)
        {
            var editor = editorConfig.Resolve(result.Path);
            if (editor.ErrorMessage is not null)
            {
                return result with { Config = config, ConfigErrors = [new ConfigError(result.Path, 1, 1, editor.ErrorMessage)] };
            }

            if (editor.EndOfLine is { } endOfLine)
            {
                config = config with { EndOfLine = endOfLine };
            }
        }

        return result with { Config = config };
    }

    /// <summary>
    ///     Reads and parses one file, converting every expected failure into a reported failure.
    /// </summary>
    /// <param name="path">Path of the file.</param>
    /// <returns>The result; its failure list is empty when the file was read and parsed.</returns>
    private FileResult ProcessFile(string path)
    {
        // Read the bytes; read problems are reported rather than thrown
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or SecurityException)
        {
            return Failed(path, ReadErrorRuleId, 1, 1, DescribeReadFailure(ex));
        }

        // Decode and parse; encoding and line-ending problems carry a precise position
        try
        {
            _documentParser(SourceDocument.Create(path, bytes));
            return new FileResult(path, []);
        }
        catch (SourceDocumentException ex)
        {
            return Failed(path, ex.RuleId, ex.Line, ex.Column, ex.Message);
        }

        // Any other exception comes from the Markdown parser; it must not abort the other files
        catch (Exception ex)
        {
            return Failed(path, ParseErrorRuleId, 1, 1, $"Markdown parser failed: {ex.GetType().Name}");
        }
    }

    /// <summary>
    ///     Creates a result with a single failure.
    /// </summary>
    /// <param name="path">Path of the file.</param>
    /// <param name="ruleId">Rule identifier.</param>
    /// <param name="line">1-based line.</param>
    /// <param name="column">1-based column.</param>
    /// <param name="message">Description (line breaks are replaced so the report stays one line per failure).</param>
    /// <returns>The result.</returns>
    private static FileResult Failed(string path, string ruleId, int line, int column, string message) =>
        new(path, [new FileFailure(ruleId, line, column, message.ReplaceLineEndings(" "))]);

    /// <summary>
    ///     Describes a read failure without leaking machine-specific text where a stable message exists.
    /// </summary>
    /// <param name="ex">The exception.</param>
    /// <returns>The message to report.</returns>
    private static string DescribeReadFailure(Exception ex) => ex switch
    {
        FileNotFoundException or DirectoryNotFoundException => "File not found",
        UnauthorizedAccessException => "Access to the file is denied",
        _ => $"Cannot read file: {ex.Message}"
    };

    /// <summary>
    ///     One line of the final report before formatting.
    /// </summary>
    /// <param name="Path">Display path of the file the line is reported against.</param>
    /// <param name="Line">1-based line.</param>
    /// <param name="Column">1-based column.</param>
    /// <param name="RuleId">Rule identifier.</param>
    /// <param name="Message">Single-line message.</param>
    private readonly record struct ReportLine(string Path, int Line, int Column, string RuleId, string Message);

    /// <summary>
    ///     Converts a path to the form used in reports: relative to the base directory with forward slashes.
    /// </summary>
    /// <param name="path">The path as processed (normally a full path).</param>
    /// <param name="baseDirectory">The directory to relativize against.</param>
    /// <returns>The display path.</returns>
    private static string GetDisplayPath(string path, string baseDirectory)
    {
        var relative = Path.GetRelativePath(baseDirectory, path);
        return Path.DirectorySeparatorChar == '/' ? relative : relative.Replace(Path.DirectorySeparatorChar, '/');
    }
}
