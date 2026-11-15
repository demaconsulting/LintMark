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
using DemaConsulting.LintMark.Cli;
using DemaConsulting.LintMark.Configuration;
using DemaConsulting.LintMark.Document;
using DemaConsulting.LintMark.Formatting;
using DemaConsulting.LintMark.Formatting.Blocks;
using DemaConsulting.LintMark.Formatting.Inline;
using DemaConsulting.LintMark.Linting;
using DemaConsulting.LintMark.Reporting;

namespace DemaConsulting.LintMark.Engine;

/// <summary>
///     Discovers the Markdown files named on the command line, processes them in parallel (read, parse,
///     configure and format), optionally rewrites them, and reports every finding in a deterministic order.
/// </summary>
/// <remarks>
///     <para>
///         Each file is processed independently by a worker: its configuration is loaded first (so that the
///         <c>ignore</c> patterns can skip it before it is read), then the file is read, decoded and parsed, and
///         the document is formatted into a proposed rewrite. Workers never
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
///         directory, a glob or the default pattern; a file named on the command line is never ignored. The
///         patterns of the configuration that applies at each scan root (the current directory, a directory
///         argument, or the directory a glob starts in) also prune discovery: an ignored directory is not
///         entered, nested repositories included, while each file still uses its own nearest configuration.
///     </para>
///     <para>
///         A document the parser could only read by dropping grid tables is never rewritten and its offending
///         line is reported. A formatter that throws or proposes invalid edits fails its file only, which is
///         left untouched. The block formatters decide what is safe to change with explicit rules; the whole-file
///         properties (same page, same structure, stable second pass) are verified outside the product by
///         the verification tool.
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
///         <see cref="Run(Context)"/> may be called concurrently.
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
    ///     Formats parsed documents; replaceable so the verification tool and tests can install block formatters.
    /// </summary>
    private readonly DocumentFormatter _formatter;

    /// <summary>
    ///     Runs the lint-only rules.
    /// </summary>
    private readonly LintRuleEngine _lintRules = LintRuleEngine.CreateDefault();

    /// <summary>
    ///     Replaces the content of a file given the resolved target captured when it was read, the bytes it was
    ///     computed from and the new bytes; replaceable so tests can simulate write failures.
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
    /// <param name="formatter">
    ///     Optional replacement for the document formatter; <see langword="null"/> selects <see cref="CreateDefaultFormatter"/>.
    /// </param>
    /// <param name="fileWriter">
    ///     Optional replacement for the atomic file write (a test seam). It receives the path, the resolved
    ///     target captured when the file was read, the bytes the rewrite was computed from and the new bytes;
    ///     <see langword="null"/> selects <see cref="AtomicFileWriter"/>.
    /// </param>
    internal LintMarkEngine(
        int maxDegreeOfParallelism,
        DocumentFormatter? formatter = null,
        Action<string, string, ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>? fileWriter = null)
    {
        _maxDegreeOfParallelism = maxDegreeOfParallelism;
        _formatter = formatter ?? CreateDefaultFormatter();
        _fileWriter = fileWriter ?? (static (path, target, original, content) => AtomicFileWriter.Write(path, target, original.Span, content.Span));
    }

    /// <summary>
    ///     Creates the production document formatter with every block formatter.
    /// </summary>
    /// <returns>A new formatter; the verification tool uses it to format a second time.</returns>
    internal static DocumentFormatter CreateDefaultFormatter() => new(
        [new HeadingFormatter(), new CodeBlockFormatter(), new ThematicBreakFormatter(), new ListFormatter(), new BlockQuoteFormatter(), new TableFormatter()],
        new InlineNormalizer(),
        new ParagraphFormatter(),
        new LinkRefactorer());

    /// <summary>
    ///     Discovers, processes, optionally rewrites, and reports the files selected by the context.
    /// </summary>
    /// <param name="context">The context supplying the path and configuration arguments and receiving the reports.</param>
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
    /// <exception cref="ArgumentException">Thrown when a path argument is empty (reported by the caller as a usage error).</exception>
    public void Run(Context context)
    {
        // Discover the files against the current directory
        var baseDirectory = Directory.GetCurrentDirectory();
        // Choose the configuration sources from the command line; discovery prunes by the ignore patterns that apply at each scan root
        var loader = CreateLoader(context);
        var files = FileDiscovery.DiscoverFiles(context.Paths, baseDirectory, scanRoot => GetScanRootIgnore(loader, scanRoot, baseDirectory));

        // Process; no output happens until all workers are done
        var editorConfig = context.NoConfig ? null : new EditorConfigResolver();
        var results = Process(files, loader, editorConfig, baseDirectory, context.RuleOverrides, context.Fix);

        // Write the rewrites one after the other (never from a worker), then collect what remains to report
        List<Diagnostic> diagnostics = context.Fix ? WriteChanges(results) : [.. results.SelectMany(static r => r.Diagnostics)];
        diagnostics.AddRange(results
            .SelectMany(static r => r.ConfigErrors)
            .Concat(loader.InitialErrors)
            .Distinct()
            .Select(static e => Diagnostic.LintOnly(e.Path, ConfigLoader.ErrorRuleId, e.Line, e.Column, e.Message)));

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
    ///     Processes files, possibly in parallel, attaches each file's effective configuration and returns the results in path order.
    /// </summary>
    /// <param name="files">The files to process.</param>
    /// <param name="loader">Supplies the configuration that applies to each file.</param>
    /// <param name="editorConfig">Supplies the line ending from the editor settings, or <see langword="null"/> to ignore them.</param>
    /// <param name="baseDirectory">
    ///     The directory that <c>ignore</c> patterns are relative to when no configuration file applies, or
    ///     <see langword="null"/> for the current directory.
    /// </param>
    /// <param name="overrides">The rules forced on or off by the command line; <see langword="null"/> for none.</param>
    /// <param name="fix">
    ///     Whether the rewrites will be written; each rewrite then also carries the findings that remain in the
    ///     rewritten text, so they can be reported at the positions of the file as it is afterwards.
    /// </param>
    /// <returns>One result per input file, sorted by path (ordinal), independent of completion order.</returns>
    /// <remarks>
    ///     Results are stored by input index while workers run and sorted afterwards, which keeps the
    ///     outcome identical for any degree of parallelism and any scheduling. Workers must not write
    ///     output or files. The effective configuration is derived with a <c>with</c> expression on the immutable
    ///     configuration, so nothing shared is mutated. The editor settings are consulted only when the
    ///     configuration gives no line ending.
    /// </remarks>
    internal IReadOnlyList<FileResult> Process(
        IReadOnlyList<DiscoveredFile> files,
        ConfigLoader loader,
        EditorConfigResolver? editorConfig,
        string? baseDirectory = null,
        RuleOverrides? overrides = null,
        bool fix = false)
    {
        // Fix the base directory once, so every worker uses the same one
        var root = baseDirectory ?? Directory.GetCurrentDirectory();
        var forced = overrides ?? RuleOverrides.None;

        // Process every file; each worker writes only its own slot
        var results = new FileResult[files.Count];
        var options = new ParallelOptions { MaxDegreeOfParallelism = _maxDegreeOfParallelism };
        Parallel.For(0, files.Count, options, index => results[index] = ProcessFile(files[index], loader, editorConfig, root, forced, fix));

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
                remaining.AddRange(change.Remaining ?? [.. result.Diagnostics.Where(static d => d.Kind != DiagnosticKind.Formatting)]);
            }
            catch (Exception ex)
            {
                remaining.AddRange(result.Diagnostics);
                remaining.Add(Diagnostic.LintOnly(result.Path, WriteErrorRuleId, 1, 1, $"Cannot write file: {ex.Message}"));
            }
        }

        return remaining;
    }

    /// <summary>
    ///     Processes one file: loads its configuration, skips it when ignored, and otherwise reads, parses and formats it.
    /// </summary>
    /// <param name="file">The file.</param>
    /// <param name="loader">Supplies the configuration that applies to the file.</param>
    /// <param name="editorConfig">Supplies the line ending from the editor settings, or <see langword="null"/> to ignore them.</param>
    /// <param name="baseDirectory">The directory that <c>ignore</c> patterns are relative to when no configuration file applies.</param>
    /// <param name="overrides">The rules forced on or off by the command line, applied over the configuration of the file.</param>
    /// <param name="fix">Whether a rewrite also carries the findings that remain in the rewritten text.</param>
    /// <returns>The result with its configuration or configuration problems.</returns>
    /// <remarks>
    ///     An invalid configuration leaves <see cref="FileResult.Config"/> null and carries the problems; the
    ///     file's own failures are kept and the file is never formatted. Editor settings that cannot be read
    ///     add one problem at the start of the Markdown file, keep the configuration with an unspecified line
    ///     ending, and also prevent formatting: the line ending the file should get is not known.
    /// </remarks>
    private FileResult ProcessFile(DiscoveredFile file, ConfigLoader loader, EditorConfigResolver? editorConfig, string baseDirectory, RuleOverrides overrides, bool fix)
    {
        // The configuration comes first, because its ignore patterns decide whether the file is read at all
        var load = loader.Load(file.Path);
        if (load.Config is { } loaded
            && !file.IsExplicit
            && FileDiscovery.IsIgnored(file.Path, GetIgnoreRoot(load, baseDirectory), loaded.Ignore))
        {
            return new FileResult(file.Path, []) { Config = loaded, Ignored = true };
        }

        // An invalid configuration yields no settings, only its problems; the rules forced on the command line win over the file
        if (load.Config is not { } loadedConfig)
        {
            return ReadAndRewrite(file.Path, null, fix) with { Config = null, ConfigErrors = load.Errors };
        }

        var config = overrides.Apply(loadedConfig);

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
        return ReadAndRewrite(file.Path, errors.IsEmpty ? config : null, fix) with { Config = config, ConfigErrors = errors };
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
    ///     Gets the <c>ignore</c> scope of the configuration that applies at a scan root.
    /// </summary>
    /// <param name="loader">Supplies the configuration that applies at the scan root.</param>
    /// <param name="scanRoot">The full path of the scan root directory.</param>
    /// <param name="baseDirectory">The directory to use when the configuration is not from a file.</param>
    /// <returns>The patterns and the directory they are relative to; <see langword="null"/> when the configuration is invalid (its problems are reported per file).</returns>
    private static IgnoreScope? GetScanRootIgnore(ConfigLoader loader, string scanRoot, string baseDirectory)
    {
        // The loader resolves per file, so ask for a hypothetical file in the scan root
        var load = loader.Load(Path.Combine(scanRoot, "scan-root.md"));
        return load.Config is { } config ? new IgnoreScope(GetIgnoreRoot(load, baseDirectory), config.Ignore) : null;
    }

    /// <summary>
    ///     Reads, parses and formats one file, converting every expected failure into a diagnostic.
    /// </summary>
    /// <param name="path">Path of the file.</param>
    /// <param name="config">The configuration to format under, or <see langword="null"/> when the file must not be formatted.</param>
    /// <param name="fix">Whether a rewrite also carries the findings that remain in the rewritten text.</param>
    /// <returns>The result; its diagnostic list is empty when the file was read, parsed and is compliant.</returns>
    private FileResult ReadAndRewrite(string path, LintMarkConfig? config, bool fix)
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
            document = DocumentParser.Parse(SourceDocument.Create(path, bytes));
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
        return config is null ? new FileResult(path, []) : Rewrite(path, target, bytes, document, config, fix);
    }

    /// <summary>
    ///     Formats a parsed document and prepares the proposed rewrite.
    /// </summary>
    /// <param name="path">Path of the file.</param>
    /// <param name="target">The full path of the file the bytes were read from, with symbolic links resolved.</param>
    /// <param name="bytes">The exact bytes that were read.</param>
    /// <param name="document">The parsed document.</param>
    /// <param name="config">The configuration to format under.</param>
    /// <param name="fix">Whether a change also carries the findings that remain in the rewritten text.</param>
    /// <returns>
    ///     The result: the findings, and a change only when the proposed rewrite differs from the file. A document
    ///     that must not be rewritten yields the grid table finding and no change.
    /// </returns>
    private FileResult Rewrite(string path, string target, byte[] bytes, ParsedDocument document, LintMarkConfig config, bool fix)
    {
        var source = document.Source;

        // A document the parser could only read by dropping grid tables is never formatted; its offending line is reported,
        // and the lint-only rules still run on the reading without grid tables
        if (document.GridFallback is { } grid)
        {
            try
            {
                return new FileResult(path, ReportGridFallback(path, document, config, grid).AddRange(_lintRules.Run(document, config)));
            }
            catch (Exception ex)
            {
                return Failed(path, FormatErrorRuleId, 1, 1, $"Linting failed: {ex.GetType().Name}");
            }
        }

        // Format, then apply the edits; a formatter that throws or proposes invalid edits fails this file only. The lint-only
        // rules read the document as written, so what they report does not depend on what the formatter changes
        FormatResult format;
        ImmutableArray<Diagnostic> diagnostics;
        string newLf;
        ImmutableArray<Diagnostic>? remaining = null;
        try
        {
            format = _formatter.Format(document, config);
            diagnostics = format.Diagnostics.AddRange(_lintRules.Run(document, config));
            newLf = SourcePatcher.Apply(source, format.Edits);

            // Under fix, the findings that rewriting does not remove are reported against the rewritten text, so their positions
            // match the file on disk: the lint-only rules and the formatter's own lint-only findings are computed again on it
            if (fix && newLf != source.Text)
            {
                var rewritten = DocumentParser.Parse(SourceDocument.FromText(path, newLf));
                remaining = [.. _formatter.Format(rewritten, config).Diagnostics.Where(static d => d.Kind != DiagnosticKind.Formatting), .. _lintRules.Run(rewritten, config)];
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
            return new FileResult(path, diagnostics);
        }

        var change = new FileChange(
            source.Text,
            newLf,
            source.LineEndings,
            format.LineEnding,
            [.. bytes],
            [.. newBytes],
            target)
        {
            Remaining = remaining
        };
        return new FileResult(path, diagnostics) { Change = change };
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
    ///     Switching the report off never lets the file be rewritten: the guard is applied by the caller
    ///     regardless (the formatters cannot edit a document that was read without its grid tables).
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
            Diagnostic.LintOnly(path, rule, grid.Line, grid.Column, "Line looks like a grid table border but the table is not valid; the file is never rewritten")
        ];
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
        new(path, [Diagnostic.LintOnly(path, ruleId, line, column, message)]);

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

/// <summary>
///     The outcome of processing one file.
/// </summary>
/// <param name="Path">The path of the file as it was given to the engine.</param>
/// <param name="Diagnostics">The findings about the file (empty when it was read, parsed and found compliant).</param>
/// <param name="Config">
///     The effective configuration of the file (the configuration that applies to it, with the line ending
///     filled in from the editor settings when the configuration gives none), or <see langword="null"/> when
///     the file's configuration is invalid or has not been attached.
/// </param>
/// <remarks>
///     Workers return results and never write output; the engine emits them later in path order so
///     output does not depend on scheduling. Immutable once created.
/// </remarks>
internal sealed record FileResult(string Path, ImmutableArray<Diagnostic> Diagnostics, LintMarkConfig? Config = null)
{
    /// <summary>
    ///     Gets the configuration problems that apply to the file (empty when its configuration is valid).
    /// </summary>
    /// <remarks>
    ///     The same problem is shared by every file that uses the same configuration; the engine reports each
    ///     distinct problem once.
    /// </remarks>
    public ImmutableArray<ConfigError> ConfigErrors { get; init; } = ImmutableArray<ConfigError>.Empty;

    /// <summary>
    ///     Gets the rewrite that is ready to be written, or <see langword="null"/> when the file is not to be rewritten.
    /// </summary>
    /// <remarks>
    ///     Set only for a rewrite that changes the bytes of the file; never set for a file with an invalid
    ///     configuration, a format-unsafe document or a failure.
    /// </remarks>
    public FileChange? Change { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the file was skipped because an <c>ignore</c> pattern of its configuration matches it.
    /// </summary>
    /// <remarks>
    ///     An ignored file is not read, produces no diagnostic and is never rewritten.
    /// </remarks>
    public bool Ignored { get; init; }
}

/// <summary>
///     A rewrite of one file that is ready to be written.
/// </summary>
/// <param name="OriginalLf">The LF-normalized text as it is on disk.</param>
/// <param name="NewLf">The LF-normalized text after rewriting.</param>
/// <param name="OriginalEndings">The counts of the line terminators in the file as it is on disk.</param>
/// <param name="TargetEnding">The line ending the file is written with.</param>
/// <param name="OriginalBytes">The exact bytes the rewrite was computed from.</param>
/// <param name="NewBytes">The exact bytes to write (byte order mark and line endings restored).</param>
/// <param name="ResolvedTarget">The full path of the file the original bytes were read from, with symbolic links resolved at read time.</param>
/// <remarks>
///     A change exists only for a rewrite that differs from the file: for a file that must not be rewritten
///     (an invalid configuration, a format-unsafe document, a failure) the result carries no change, so
///     nothing downstream can write the file by mistake. The original bytes travel with the change so the
///     writer can refuse to replace a file that changed after it was read, and so does the resolved target
///     (the file that the path referred to through symbolic links when it was read), so the writer can also
///     refuse when a link was redirected to a file with the same bytes. Immutable, including the byte arrays.
/// </remarks>
internal sealed record FileChange(
    string OriginalLf,
    string NewLf,
    LineEndingInfo OriginalEndings,
    LineEnding TargetEnding,
    ImmutableArray<byte> OriginalBytes,
    ImmutableArray<byte> NewBytes,
    string ResolvedTarget)
{
    /// <summary>
    ///     Gets the findings that remain in the rewritten text, with positions in the rewritten text, or
    ///     <see langword="null"/> when they were not computed (the rewrite is not going to be written).
    /// </summary>
    /// <remarks>
    ///     Reported instead of the findings of the original text once the rewrite is written, because the
    ///     rewrite moves lines; the formatting findings are not among them, since rewriting resolves them.
    /// </remarks>
    public ImmutableArray<Diagnostic>? Remaining { get; init; }
}
