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

using System.Collections.Immutable;
using System.Security;
using System.Text;
using DemaConsulting.LintMark.Cli;
using DemaConsulting.LintMark.Configuration;
using DemaConsulting.LintMark.Document;
using DemaConsulting.LintMark.Formatting;
using DemaConsulting.LintMark.Formatting.Blocks;
using DemaConsulting.LintMark.Formatting.Inline;
using DemaConsulting.LintMark.Linting;
using DemaConsulting.LintMark.Reporting;
using DemaConsulting.LintMark.Safety;

namespace DemaConsulting.LintMark.Engine;

/// <summary>
///     Discovers the Markdown files named on the command line, processes them in parallel (read, parse,
///     configure, format and verify), optionally rewrites them, and reports every finding in a deterministic order.
/// </summary>
/// <remarks>
///     <para>
///         Each file is processed independently by a worker: its configuration is loaded first (so that the
///         <c>ignore</c> patterns can skip it before it is read), then the file is read, decoded and parsed, and
///         the document is formatted into a proposed rewrite that must pass every safety check. Workers never
///         write output, never write files and never touch the <see cref="Context"/>; they return a
///         <see cref="FileResult"/> stored by input index. After all workers finish, the files are rewritten
///         one after the other when the fix option is given, and all diagnostics are reported in order, so
///         console and log output, the files written and the exit code do not depend on scheduling. The degree
///         of parallelism defaults to the processor count and can be limited (to one for sequential
///         processing) for tests and diagnosis; it is not a user option.
///     </para>
///     <para>
///         The effective configuration of a file is the configuration that applies to it (the explicit file,
///         the nearest discovered file, or the built-in defaults, depending on the command line) with the line
///         ending taken from the editor settings when the configuration gives none. A file whose configuration
///         or editor settings are invalid is still read and parsed, so its own failures are still reported, but
///         it is never formatted and never rewritten. A configuration problem shared by many files is reported
///         once. The <c>ignore</c> patterns of the configuration skip the files that were found through a
///         directory, a glob or the default pattern; a file named on the command line is never ignored.
///     </para>
///     <para>
///         Safety is the central property of rewriting: a proposed rewrite is written only when the rendered
///         HTML is equivalent, the Pandoc structure is unchanged, the rewritten text parses as before and
///         formatting it again changes nothing. Any other outcome (including an exception in a check)
///         leaves the file untouched and is reported as a diagnostic. A document the parser could only read by
///         dropping grid tables is never rewritten and its offending line is reported. The checks run in check
///         mode too, so what is reported predicts what the fix option does; only the write is gated.
///     </para>
///     <para>
///         Every finding is reported, one per line, as <c>path:line:col: rule-id message</c> through
///         <see cref="Context.WriteError"/>; paths are relative to the current directory with forward slashes.
///         There is no summary output on success. With the fix option the files that were rewritten no longer
///         report their formatting violations. With the diff option a unified diff of the proposed rewrites is
///         written to standard output and nothing is written to disk.
///     </para>
///     <para>
///         Thread safety: an instance holds only immutable configuration and stateless collaborators;
///         <see cref="Process(IReadOnlyList{string})"/> and <see cref="Run(Context)"/> may be called concurrently.
///     </para>
/// </remarks>
internal sealed class LintMarkEngine
{
    /// <summary>
    ///     Rule identifier for a file that cannot be read (missing, permission, I/O error).
    /// </summary>
    public const string ReadErrorRuleId = RuleCatalog.FileReadErrorRuleId;

    /// <summary>
    ///     Rule identifier for a file whose content makes the Markdown parser throw.
    /// </summary>
    public const string ParseErrorRuleId = RuleCatalog.FileParseErrorRuleId;

    /// <summary>
    ///     Rule identifier for a file that could not be formatted (a formatter threw or proposed invalid edits).
    /// </summary>
    public const string FormatErrorRuleId = RuleCatalog.FileFormatErrorRuleId;

    /// <summary>
    ///     Rule identifier for a rewritten file that could not be written.
    /// </summary>
    public const string WriteErrorRuleId = RuleCatalog.FileWriteErrorRuleId;

    /// <summary>
    ///     Maximum number of files processed concurrently.
    /// </summary>
    private readonly int _maxDegreeOfParallelism;

    /// <summary>
    ///     Processes a single file given its formatting configuration; replaceable so tests can control timing.
    /// </summary>
    private readonly Func<string, LintMarkConfig?, FileResult> _fileProcessor;

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
    ///     Formats parsed documents; replaceable so tests can install block formatters, including broken ones.
    /// </summary>
    private readonly DocumentFormatter _formatter;

    /// <summary>
    ///     Replaces the content of a file given the resolved target captured when it was read, the bytes it was
    ///     computed from and the new bytes; replaceable so tests can simulate write failures and files that
    ///     change (or links that are redirected) after they were read.
    /// </summary>
    private readonly Action<string, string, ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> _fileWriter;

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
    ///     Optional replacement for the per-file processing (a test seam). It receives the path and the
    ///     configuration the file may be formatted under (<see langword="null"/> when the file must not be
    ///     formatted); <see langword="null"/> selects the real read, parse, format and verify.
    /// </param>
    /// <param name="documentParser">
    ///     Optional replacement for the Markdown parsing step of the real per-file processing, including the
    ///     parsing done by the safety checks (a test seam); <see langword="null"/> selects <see cref="DocumentParser.Parse"/>.
    /// </param>
    /// <param name="editorConfigFactory">
    ///     Optional replacement for the creation of the editor-settings resolver used by <see cref="Run(Context)"/>
    ///     (a test seam); <see langword="null"/> selects a resolver that reads the real file system.
    /// </param>
    /// <param name="formatter">
    ///     Optional replacement for the document formatter (a test seam that installs block formatters);
    ///     <see langword="null"/> selects the production formatter with the heading, code block and thematic break formatters.
    /// </param>
    /// <param name="fileWriter">
    ///     Optional replacement for the atomic file write (a test seam). It receives the path, the resolved
    ///     target captured when the file was read, the bytes the rewrite was computed from and the new bytes;
    ///     <see langword="null"/> selects <see cref="AtomicFileWriter"/>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxDegreeOfParallelism"/> is less than one.</exception>
    internal LintMarkEngine(
        int maxDegreeOfParallelism,
        Func<string, LintMarkConfig?, FileResult>? fileProcessor = null,
        Func<SourceDocument, ParsedDocument>? documentParser = null,
        Func<EditorConfigResolver>? editorConfigFactory = null,
        DocumentFormatter? formatter = null,
        Action<string, string, ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>? fileWriter = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxDegreeOfParallelism, 1);
        _maxDegreeOfParallelism = maxDegreeOfParallelism;
        _documentParser = documentParser ?? DocumentParser.Parse;
        _fileProcessor = fileProcessor ?? ReadAndRewrite;
        _editorConfigFactory = editorConfigFactory ?? (static () => new EditorConfigResolver());
        _formatter = formatter ?? new DocumentFormatter(
            [new HeadingFormatter(), new CodeBlockFormatter(), new ThematicBreakFormatter(), new ListFormatter(), new BlockQuoteFormatter()],
            new InlineNormalizer());
        _fileWriter = fileWriter ?? (static (path, target, original, content) => AtomicFileWriter.Write(path, target, original.Span, content.Span));
    }

    /// <summary>
    ///     Discovers, processes, optionally rewrites, and reports the files selected by the context.
    /// </summary>
    /// <param name="context">The context supplying the path and configuration arguments and receiving the reports. Must not be null.</param>
    /// <remarks>
    ///     Diagnostics and configuration problems are written through <see cref="Context.WriteError"/>, which
    ///     makes the exit code 1. Discovery runs against the current directory. <c>--no-config</c> selects
    ///     the built-in defaults and reads neither configuration source; <c>--config</c> selects exactly that
    ///     file (its problems are reported even when no Markdown file is selected); otherwise the nearest
    ///     <c>.lintmark.yaml</c> is discovered per file. The editor settings are consulted unless
    ///     <c>--no-config</c> is given. With <c>--fix</c> the rewrites that passed every check are written, one
    ///     file after the other in path order, once all workers have finished; a file that cannot be written is
    ///     reported and stays as it was. With <c>--diff</c> the unified diff is written to standard output.
    ///     Without either option nothing is written to disk.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a path argument is empty (reported by the caller as a usage error).</exception>
    public void Run(Context context)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(context);

        // Discover the files against the current directory
        var baseDirectory = Directory.GetCurrentDirectory();
        var files = FileDiscovery.DiscoverFiles(context.Paths, baseDirectory);

        // Choose the configuration sources from the command line, then process; no output happens until all workers are done
        var loader = CreateLoader(context);
        var editorConfig = context.NoConfig ? null : _editorConfigFactory();
        var results = Process(files, loader, editorConfig, baseDirectory);

        // Write the rewrites one after the other (never from a worker), then collect what remains to report
        List<Diagnostic> diagnostics = context.Fix ? WriteChanges(results) : [.. results.SelectMany(static r => r.Diagnostics)];
        diagnostics.AddRange(results
            .SelectMany(static r => r.ConfigErrors)
            .Concat(loader.InitialErrors)
            .Distinct()
            .Select(static e => new Diagnostic(
                ConfigLoader.ErrorRuleId, DiagnosticKind.LintOnly, e.Path, e.Line, e.Column, e.Message.ReplaceLineEndings(" "))));

        // Report in one deterministic order; the diff goes to standard output after the findings
        TextReporter.Report(diagnostics, baseDirectory, context);
        if (context.Diff)
        {
            DiffReporter.Report(
                results
                    .Where(static r => r.Change is not null)
                    .Select(static r => new FileDiff(r.Path, r.Change!.OriginalLf, r.Change.NewLf, r.Change.OriginalEndings, r.Change.TargetEnding)),
                baseDirectory,
                context);
        }
    }

    /// <summary>
    ///     Processes files with the built-in default configuration, possibly in parallel, and returns their results in path order.
    /// </summary>
    /// <param name="files">Paths of the files to process; every file is treated as named on the command line. Must not be null.</param>
    /// <returns>One result per input file, sorted by path (ordinal), independent of completion order.</returns>
    /// <remarks>
    ///     Equivalent to <see cref="Process(IReadOnlyList{string}, ConfigLoader, EditorConfigResolver?)"/> with a
    ///     defaults-only loader and no editor settings: no configuration source is read.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="files"/> is null.</exception>
    public IReadOnlyList<FileResult> Process(IReadOnlyList<string> files) => Process(files, ConfigLoader.ForDefaults(), null);

    /// <summary>
    ///     Processes files that were all named explicitly, attaches each file's effective configuration and returns the results in path order.
    /// </summary>
    /// <param name="files">Paths of the files to process; every file is treated as named on the command line, so no <c>ignore</c> pattern applies. Must not be null.</param>
    /// <param name="loader">Supplies the configuration that applies to each file. Must not be null.</param>
    /// <param name="editorConfig">Supplies the line ending from the editor settings, or <see langword="null"/> to ignore them.</param>
    /// <returns>One result per input file, sorted by path (ordinal), independent of completion order.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="files"/> (or an element) or <paramref name="loader"/> is null.</exception>
    internal IReadOnlyList<FileResult> Process(IReadOnlyList<string> files, ConfigLoader loader, EditorConfigResolver? editorConfig)
    {
        // Validate the list here so that the projection below cannot hide a null argument
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(loader);
        if (files.Any(static file => file is null))
        {
            throw new ArgumentNullException(nameof(files), "The files must not contain null.");
        }

        DiscoveredFile[] named = [.. files.Select(static file => new DiscoveredFile(file, isExplicit: true))];
        return Process(named, loader, editorConfig);
    }

    /// <summary>
    ///     Processes files, possibly in parallel, attaches each file's effective configuration and returns the results in path order.
    /// </summary>
    /// <param name="files">The files to process. Must not be null.</param>
    /// <param name="loader">Supplies the configuration that applies to each file. Must not be null.</param>
    /// <param name="editorConfig">Supplies the line ending from the editor settings, or <see langword="null"/> to ignore them.</param>
    /// <param name="baseDirectory">
    ///     The directory that <c>ignore</c> patterns are relative to when no configuration file applies, or
    ///     <see langword="null"/> for the current directory.
    /// </param>
    /// <returns>One result per input file, sorted by path (ordinal), independent of completion order.</returns>
    /// <remarks>
    ///     Results are stored by input index while workers run and sorted afterwards, which keeps the
    ///     outcome identical for any degree of parallelism and any scheduling. Workers must not write
    ///     output or files. The effective configuration is derived with a <c>with</c> expression on the immutable
    ///     configuration, so nothing shared is mutated. The editor settings are consulted only when the
    ///     configuration gives no line ending. If a replaced file processor throws, the exception is
    ///     propagated (wrapped in an <see cref="AggregateException"/> when run in parallel).
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="files"/> or <paramref name="loader"/> is null, or a file has no path.</exception>
    internal IReadOnlyList<FileResult> Process(
        IReadOnlyList<DiscoveredFile> files,
        ConfigLoader loader,
        EditorConfigResolver? editorConfig,
        string? baseDirectory = null)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(loader);
        if (files.Any(static file => file.Path is null))
        {
            throw new ArgumentNullException(nameof(files), "The files must not contain a file without a path.");
        }

        // Fix the base directory once, so every worker uses the same one
        var root = baseDirectory ?? Directory.GetCurrentDirectory();

        // Process every file; each worker writes only its own slot
        var results = new FileResult[files.Count];
        var options = new ParallelOptions { MaxDegreeOfParallelism = _maxDegreeOfParallelism };
        Parallel.For(0, files.Count, options, index => results[index] = ProcessFile(files[index], loader, editorConfig, root));

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
    ///     Writes the rewrites that are ready, one after the other in path order, and collects the diagnostics that remain.
    /// </summary>
    /// <param name="results">The results of all files, in path order.</param>
    /// <returns>
    ///     The diagnostics to report: for a file that was written, only the findings that rewriting does not
    ///     remove; for a file that could not be written, all findings plus a write failure; for other files, all findings.
    /// </returns>
    /// <remarks>
    ///     Called after every worker has finished, so no two writes can overlap (also not when two paths name
    ///     the same file, for instance through a hard link). A failure of one write never stops the others.
    /// </remarks>
    private List<Diagnostic> WriteChanges(IReadOnlyList<FileResult> results)
    {
        var remaining = new List<Diagnostic>();
        foreach (var result in results)
        {
            if (result.Change is not { } change)
            {
                remaining.AddRange(result.Diagnostics);
                continue;
            }

            // The writer refuses a file that changed, or a link that was redirected, after it was read; any failure leaves the file as it was
            try
            {
                _fileWriter(result.Path, change.ResolvedTarget, change.OriginalBytes.AsMemory(), change.NewBytes.AsMemory());
                remaining.AddRange(result.Diagnostics.Where(static d => d.Kind != DiagnosticKind.Formatting));
            }
            catch (Exception ex)
            {
                remaining.AddRange(result.Diagnostics);
                remaining.Add(Problem(result.Path, WriteErrorRuleId, 1, 1, $"Cannot write file: {ex.Message}"));
            }
        }

        return remaining;
    }

    /// <summary>
    ///     Processes one file: loads its configuration, skips it when ignored, and otherwise reads, parses, formats and verifies it.
    /// </summary>
    /// <param name="file">The file.</param>
    /// <param name="loader">Supplies the configuration that applies to the file.</param>
    /// <param name="editorConfig">Supplies the line ending from the editor settings, or <see langword="null"/> to ignore them.</param>
    /// <param name="baseDirectory">The directory that <c>ignore</c> patterns are relative to when no configuration file applies.</param>
    /// <returns>The result with its configuration or configuration problems.</returns>
    /// <remarks>
    ///     An invalid configuration leaves <see cref="FileResult.Config"/> null and carries the problems; the
    ///     file's own failures are kept and the file is never formatted. Editor settings that cannot be read
    ///     add one problem at the start of the Markdown file, keep the configuration with an unspecified line
    ///     ending, and also prevent formatting: the line ending the file should get is not known.
    /// </remarks>
    private FileResult ProcessFile(DiscoveredFile file, ConfigLoader loader, EditorConfigResolver? editorConfig, string baseDirectory)
    {
        // The configuration comes first, because its ignore patterns decide whether the file is read at all
        var load = loader.Load(file.Path);
        if (load.Config is { } loaded
            && !file.IsExplicit
            && FileDiscovery.IsIgnored(file.Path, GetIgnoreRoot(load, baseDirectory), loaded.Ignore))
        {
            return new FileResult(file.Path, []) { Config = loaded, Ignored = true };
        }

        // An invalid configuration yields no settings, only its problems
        if (load.Config is not { } config)
        {
            return _fileProcessor(file.Path, null) with { Config = null, ConfigErrors = load.Errors };
        }

        // The configuration's own line ending wins; the editor settings are consulted only when it gives none
        var errors = ImmutableArray<ConfigError>.Empty;
        if (config.EndOfLine is null && editorConfig is not null)
        {
            var editor = editorConfig.Resolve(file.Path);
            if (editor.ErrorMessage is not null)
            {
                errors = [new ConfigError(file.Path, 1, 1, editor.ErrorMessage)];
            }
            else if (editor.EndOfLine is { } endOfLine)
            {
                config = config with { EndOfLine = endOfLine };
            }
        }

        // A file with a configuration problem is never formatted
        return _fileProcessor(file.Path, errors.IsEmpty ? config : null) with { Config = config, ConfigErrors = errors };
    }

    /// <summary>
    ///     Gets the directory that the <c>ignore</c> patterns of a configuration are relative to.
    /// </summary>
    /// <param name="load">The loaded configuration.</param>
    /// <param name="baseDirectory">The directory to use when the configuration is not from a file.</param>
    /// <returns>The directory of the configuration file, or the base directory for the built-in defaults.</returns>
    private static string GetIgnoreRoot(ConfigLoadResult load, string baseDirectory) =>
        load.SourcePath is null ? baseDirectory : Path.GetDirectoryName(Path.GetFullPath(load.SourcePath)) ?? baseDirectory;

    /// <summary>
    ///     Reads, parses, formats and verifies one file, converting every expected failure into a diagnostic.
    /// </summary>
    /// <param name="path">Path of the file.</param>
    /// <param name="config">The configuration to format under, or <see langword="null"/> when the file must not be formatted.</param>
    /// <returns>The result; its diagnostic list is empty when the file was read, parsed and is compliant.</returns>
    private FileResult ReadAndRewrite(string path, LintMarkConfig? config)
    {
        // Resolve the links first and read the bytes from the resolved file, so the identity that is captured is the
        // one the bytes come from; read problems are reported rather than thrown
        string target;
        byte[] bytes;
        try
        {
            target = AtomicFileWriter.ResolveTarget(path);
            bytes = File.ReadAllBytes(target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or SecurityException)
        {
            return Failed(path, ReadErrorRuleId, 1, 1, DescribeReadFailure(ex));
        }

        // Decode and parse; encoding and line-ending problems carry a precise position
        ParsedDocument document;
        try
        {
            document = _documentParser(SourceDocument.Create(path, bytes));
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

        // A file that must not be formatted is only checked for problems it has on its own
        return config is null ? new FileResult(path, []) : Rewrite(path, target, bytes, document, config);
    }

    /// <summary>
    ///     Formats a parsed document and verifies the proposed rewrite.
    /// </summary>
    /// <param name="path">Path of the file.</param>
    /// <param name="target">The full path of the file the bytes were read from, with symbolic links resolved.</param>
    /// <param name="bytes">The exact bytes that were read.</param>
    /// <param name="document">The parsed document.</param>
    /// <param name="config">The configuration to format under.</param>
    /// <returns>
    ///     The result: the findings, and a change only when the proposed rewrite differs from the file and passed
    ///     every safety check. A document that must not be rewritten yields the grid table finding and no change.
    /// </returns>
    private FileResult Rewrite(string path, string target, byte[] bytes, ParsedDocument document, LintMarkConfig config)
    {
        var source = document.Source;

        // A document the parser could only read by dropping grid tables is never formatted; its offending line is reported
        if (document.GridFallback is { } grid)
        {
            return new FileResult(path, ReportGridFallback(path, document, config, grid));
        }

        // Format, then apply the edits; a formatter that throws or proposes invalid edits fails this file only
        FormatResult format;
        string newLf;
        try
        {
            format = _formatter.Format(document, config);
            newLf = SourcePatcher.Apply(source, format.Edits);
            if (newLf.Contains('\r'))
            {
                return Failed(path, FormatErrorRuleId, 1, 1, "Formatting failed: the result contains a carriage return");
            }
        }
        catch (Exception ex)
        {
            return Failed(path, FormatErrorRuleId, 1, 1, $"Formatting failed: {ex.GetType().Name}");
        }

        // Nothing to write when the bytes would not change
        var newBytes = source.ToBytes(newLf, format.LineEnding);
        if (newBytes.AsSpan().SequenceEqual(bytes))
        {
            return new FileResult(path, format.Diagnostics);
        }

        // Every rewrite that changes bytes must pass the safety checks, also one that changes the line endings alone:
        // the Markdown parser renders some text differently for CRLF and LF
        if (CheckSafety(path, source, newLf, bytes, newBytes, config) is { } refusal)
        {
            return new FileResult(path, format.Diagnostics.Add(refusal.Diagnostic)) { Safety = refusal.Status };
        }

        var change = new FileChange(
            source.Text,
            newLf,
            source.LineEndings,
            format.LineEnding,
            [.. bytes],
            [.. newBytes],
            target);
        return new FileResult(path, format.Diagnostics) { Change = change };
    }

    /// <summary>
    ///     Creates the finding for a document that was read without grid tables, unless the rule is off or suppressed there.
    /// </summary>
    /// <param name="path">Path of the file.</param>
    /// <param name="document">The parsed document.</param>
    /// <param name="config">The configuration of the file.</param>
    /// <param name="grid">Where the offending line is.</param>
    /// <returns>One diagnostic, or none when the rule is switched off or suppressed at the offending line.</returns>
    /// <remarks>
    ///     Switching the report off never lets the file be rewritten: that guard is a safety property and is
    ///     applied by the caller regardless.
    /// </remarks>
    private static ImmutableArray<Diagnostic> ReportGridFallback(string path, ParsedDocument document, LintMarkConfig config, GridTableFallback grid)
    {
        var rule = RuleCatalog.GridTableInvalidRuleId;
        var offset = document.Source.OffsetOf(grid.Line, grid.Column);
        if (!config.Rules[rule] || document.Suppressions.IsSuppressed(offset, rule))
        {
            return [];
        }

        return
        [
            Problem(path, rule, grid.Line, grid.Column, "Line looks like a grid table border but the table is not valid; the file is never rewritten")
        ];
    }

    /// <summary>
    ///     Runs the safety checks on a proposed rewrite.
    /// </summary>
    /// <param name="path">Path of the file.</param>
    /// <param name="source">The original document.</param>
    /// <param name="newLf">The proposed LF-normalized text.</param>
    /// <param name="bytes">The exact bytes of the file.</param>
    /// <param name="newBytes">The exact bytes that would be written; differ from <paramref name="bytes"/>.</param>
    /// <param name="config">The configuration the rewrite was computed under.</param>
    /// <returns>The status and diagnostic of the first check that failed, or <see langword="null"/> when every check passed.</returns>
    /// <remarks>
    ///     Order: rendered HTML of the LF-normalized text, rendered HTML of the text with the line endings it has
    ///     on disk, Pandoc structure, that the rewritten text is still read with grid tables, and idempotency.
    ///     A rewrite that changes the line endings alone leaves the LF-normalized text as it is, so only the
    ///     rendering of the text on disk is checked for it, because the parser renders some text differently for
    ///     CRLF and LF. A check that throws is a failed check (fail closed). The diagnostic is placed at the
    ///     first line where the original and the proposed text differ.
    /// </remarks>
    private (SafetyStatus Status, Diagnostic Diagnostic)? CheckSafety(
        string path,
        SourceDocument source,
        string newLf,
        byte[] bytes,
        byte[] newBytes,
        LintMarkConfig config)
    {
        // Each check yields a verdict; the first that is not equivalent decides
        var textChanged = !string.Equals(newLf, source.Text, StringComparison.Ordinal);
        var checks = new List<(SafetyStatus Status, string RuleId, string Sentence, Func<EquivalenceResult> Run)>();

        // The checks on the LF-normalized text are decided in advance when only the line endings change
        if (textChanged)
        {
            checks.Add((SafetyStatus.HtmlMismatch, RuleCatalog.HtmlMismatchRuleId, "The rewrite would change how the document renders",
                () => HtmlEquivalenceChecker.Check(source.Text, newLf)));
        }

        // The text as it is on disk, with its real line endings, always has to render the same
        checks.Add((SafetyStatus.HtmlMismatch, RuleCatalog.HtmlMismatchRuleId, "The rewrite would change how the document renders because of its line endings",
            () => HtmlEquivalenceChecker.CheckOnDisk(DecodeContent(bytes, source), DecodeContent(newBytes, source))));
        if (textChanged)
        {
            checks.Add((SafetyStatus.StructureMismatch, RuleCatalog.StructureMismatchRuleId, "The rewrite would change the Pandoc structure",
                () => StructuralTokenChecker.Check(source.Text, newLf)));
            checks.Add((SafetyStatus.StructureMismatch, RuleCatalog.StructureMismatchRuleId, "The rewrite would not be read as before",
                () => CheckReparse(path, newLf)));
            checks.Add((SafetyStatus.NotIdempotent, RuleCatalog.NotIdempotentRuleId, "Formatting is not stable",
                () => IdempotencyChecker.Check(newLf, text => Reformat(path, text, config))));
        }

        foreach (var (status, ruleId, sentence, run) in checks)
        {
            var verdict = Verdict(run);
            if (verdict.IsEquivalent)
            {
                continue;
            }

            // Position the finding where the proposed text first differs from the file; a change of the line endings alone is at 1:1
            var index = textChanged ? Math.Min(EquivalenceResult.FirstDifference(source.Text, newLf), source.Text.Length) : 0;
            var (line, column) = source.PositionOf(index);
            var message = $"{sentence} ({verdict.Description}); the file is not rewritten";
            return (status, Problem(path, ruleId, line, column, message));
        }

        return null;
    }

    /// <summary>
    ///     Decodes the bytes of a file, or of its proposed rewrite, to text with the line endings it has on disk.
    /// </summary>
    /// <param name="content">The bytes, starting with the byte order mark when the document had one.</param>
    /// <param name="source">The original document, which says whether there is a byte order mark.</param>
    /// <returns>The text without a byte order mark and with its line terminators unchanged.</returns>
    private static string DecodeContent(byte[] content, SourceDocument source)
    {
        var skip = source.HasBom ? source.Encoding.GetPreamble().Length : 0;
        return Encoding.UTF8.GetString(content, skip, content.Length - skip);
    }

    /// <summary>
    ///     Runs one check and treats an exception as a failed check.
    /// </summary>
    /// <param name="check">The check.</param>
    /// <returns>The verdict, or a failed verdict when the check threw.</returns>
    private static EquivalenceResult Verdict(Func<EquivalenceResult> check)
    {
        try
        {
            return check();
        }

        // A check that cannot be completed must refuse the rewrite, whatever the cause
        catch (Exception ex)
        {
            return EquivalenceResult.Different($"the check failed: {ex.GetType().Name}");
        }
    }

    /// <summary>
    ///     Checks that the proposed text is read with grid tables, as a document the formatter may touch.
    /// </summary>
    /// <param name="path">Path of the file.</param>
    /// <param name="newLf">The proposed LF-normalized text.</param>
    /// <returns>An equivalent result when the proposed text parses without the grid table fallback.</returns>
    /// <remarks>
    ///     An edit must not create a grid-table-looking line that is not a valid table: the rewritten file would
    ///     then be one that this tool refuses to format, and rendered output cannot show the damage.
    /// </remarks>
    private EquivalenceResult CheckReparse(string path, string newLf) =>
        _documentParser(SourceDocument.FromText(path, newLf)).IsFormatUnsafe
            ? EquivalenceResult.Different("the rewritten text contains a grid table border line that is not part of a valid grid table")
            : EquivalenceResult.Equivalent;

    /// <summary>
    ///     Formats LF-normalized text the way the engine formats a file; the formatting step of the idempotency check.
    /// </summary>
    /// <param name="path">Path of the file, for the document.</param>
    /// <param name="text">The LF-normalized text.</param>
    /// <param name="config">The configuration to format under.</param>
    /// <returns>The formatted text.</returns>
    private string Reformat(string path, string text, LintMarkConfig config)
    {
        var document = _documentParser(SourceDocument.FromText(path, text));
        var format = _formatter.Format(document, config);
        return SourcePatcher.Apply(document.Source, format.Edits);
    }

    /// <summary>
    ///     Creates a result with a single failure.
    /// </summary>
    /// <param name="path">Path of the file.</param>
    /// <param name="ruleId">Rule identifier.</param>
    /// <param name="line">1-based line.</param>
    /// <param name="column">1-based column.</param>
    /// <param name="message">Description (line breaks are replaced so the report stays one line per diagnostic).</param>
    /// <returns>The result.</returns>
    private static FileResult Failed(string path, string ruleId, int line, int column, string message) =>
        new(path, [Problem(path, ruleId, line, column, message)]);

    /// <summary>
    ///     Creates a diagnostic that is only reported.
    /// </summary>
    /// <param name="path">Path of the file.</param>
    /// <param name="ruleId">Rule identifier.</param>
    /// <param name="line">1-based line.</param>
    /// <param name="column">1-based column.</param>
    /// <param name="message">Description (line breaks are replaced so the report stays one line per diagnostic).</param>
    /// <returns>The diagnostic.</returns>
    private static Diagnostic Problem(string path, string ruleId, int line, int column, string message) =>
        new(ruleId, DiagnosticKind.LintOnly, path, line, column, message.ReplaceLineEndings(" "));

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
}
