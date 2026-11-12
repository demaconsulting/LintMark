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
using DemaConsulting.LintMark.Document;

namespace DemaConsulting.LintMark.Engine;

/// <summary>
///     Discovers the Markdown files named on the command line, processes them in parallel and
///     reports read and parse failures in a deterministic order.
/// </summary>
/// <remarks>
///     <para>
///         Each file is processed independently (read, decode, parse) by a worker. Workers never
///         write output and never touch the <see cref="Context"/>; they return a <see cref="FileResult"/>
///         stored by input index. After all workers finish, results are sorted by path (ordinal) and
///         reported sequentially, so console/log output and the exit code do not depend on scheduling.
///         The degree of parallelism defaults to the processor count and can be limited (to one for
///         sequential processing) for tests and diagnosis; it is not a user option.
///     </para>
///     <para>
///         Only failures are reported, one per line, as <c>path:line:col: rule-id message</c> through
///         <see cref="Context.WriteError"/>; paths are relative to the current directory with forward
///         slashes. There is no summary output on success. Phase 1 never writes files, so the
///         <c>--check</c> option does not change behavior.
///     </para>
///     <para>
///         Thread safety: an instance holds only immutable configuration; <see cref="Process"/> and
///         <see cref="Run(Context)"/> may be called concurrently.
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
    ///     Maximum number of link targets followed when resolving a path (a guard against pathological link structures).
    /// </summary>
    private const int MaxLinkDepth = 32;

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
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxDegreeOfParallelism"/> is less than one.</exception>
    internal LintMarkEngine(
        int maxDegreeOfParallelism,
        Func<string, FileResult>? fileProcessor = null,
        Func<SourceDocument, ParsedDocument>? documentParser = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxDegreeOfParallelism, 1);
        _maxDegreeOfParallelism = maxDegreeOfParallelism;
        _documentParser = documentParser ?? DocumentParser.Parse;
        _fileProcessor = fileProcessor ?? ProcessFile;
    }

    /// <summary>
    ///     Discovers, processes and reports the files selected by the context.
    /// </summary>
    /// <param name="context">The context supplying the path arguments and receiving the failure reports. Must not be null.</param>
    /// <remarks>
    ///     Failures are written through <see cref="Context.WriteError"/>, which makes the exit code 1.
    ///     Discovery runs against the current directory (see <see cref="Select"/>).
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a path argument is empty (reported by the caller as a usage error).</exception>
    /// <exception cref="InvalidOperationException">Thrown when the log file is one of the selected files.</exception>
    public void Run(Context context)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(context);

        Run(context, Select(context));
    }

    /// <summary>
    ///     Processes and reports the given files.
    /// </summary>
    /// <param name="context">The context receiving the failure reports. Must not be null.</param>
    /// <param name="files">The files to process, as returned by <see cref="Select"/>. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public void Run(Context context, IReadOnlyList<string> files)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(files);

        // Process the files; no output happens until all workers are done
        var baseDirectory = Directory.GetCurrentDirectory();
        var results = Process(files);

        // Emit in display-path order, and within a file by position, so output is deterministic
        foreach (var (displayPath, result) in results
                     .Select(r => (DisplayPath: GetDisplayPath(r.Path, baseDirectory), Result: r))
                     .OrderBy(static r => r.DisplayPath, StringComparer.Ordinal))
        {
            foreach (var failure in result.Failures
                         .OrderBy(static f => f.Line)
                         .ThenBy(static f => f.Column)
                         .ThenBy(static f => f.RuleId, StringComparer.Ordinal))
            {
                context.WriteError($"{displayPath}:{failure.Line}:{failure.Column}: {failure.RuleId} {failure.Message}");
            }
        }
    }

    /// <summary>
    ///     Selects the files named by the context and refuses a run that would overwrite one of them with the log.
    /// </summary>
    /// <param name="context">The context supplying the path arguments and the log file path. Must not be null.</param>
    /// <returns>The selected files (distinct full paths in ordinal order).</returns>
    /// <remarks>
    ///     Nothing is read or written, and the log file is not touched. A caller that has output to write
    ///     should call this before the first write, because the first write opens the log file and so
    ///     replaces its content.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a path argument is empty (reported by the caller as a usage error).</exception>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when <see cref="Context.LogFile"/> is one of the selected files; the log would replace that
    ///     input, so the run is refused instead.
    /// </exception>
    public static IReadOnlyList<string> Select(Context context)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(context);

        // Discover the files against the current directory
        var files = FileDiscovery.Discover(context.Paths, Directory.GetCurrentDirectory());

        // Opening the log file replaces its content, so a log that is also an input would destroy that input.
        // The comparison is by real path: a path that reaches a file through a link (for example the /var to
        // /private/var link of macOS temporary folders, or a symbolic link to a source file) names the same file
        if (context.LogFile is { } logFile)
        {
            var cache = new Dictionary<string, string>(StringComparer.Ordinal);
            var realLog = ResolveLinks(logFile, cache);
            if (files.Any(file => string.Equals(ResolveLinks(file, cache), realLog, FileDiscovery.PathComparison)))
            {
                throw new InvalidOperationException(
                    $"Log file '{logFile}' is also selected as an input file; refusing to overwrite it. " +
                    "Choose a different --log path or exclude the file with a ! pattern.");
            }
        }

        return files;
    }

    /// <summary>
    ///     Resolves every symbolic link or junction along a full path.
    /// </summary>
    /// <param name="path">The full path.</param>
    /// <param name="cache">Resolved paths by path, shared between calls to save repeated work.</param>
    /// <param name="depth">The number of link targets followed so far (guards against pathological link structures).</param>
    /// <returns>The full path with every link replaced by its target; the path itself when nothing is a link.</returns>
    /// <remarks>
    ///     The parent is resolved first, then the last segment, which may itself be a link. The target of a
    ///     link is resolved again, because it may be written through further links (a link to
    ///     <c>/var/x</c> still lies below the <c>/private/var</c> link). Hard links cannot be recognized this
    ///     way; a log file that is a hard link to an input is not detected.
    /// </remarks>
    private static string ResolveLinks(string path, Dictionary<string, string> cache, int depth = 0)
    {
        if (cache.TryGetValue(path, out var known))
        {
            return known;
        }

        var parent = Path.GetDirectoryName(path);
        string resolved;
        if (parent is null)
        {
            resolved = path;
        }
        else
        {
            var combined = Path.Combine(ResolveLinks(parent, cache, depth), Path.GetFileName(path));
            var target = GetLinkTarget(combined);
            resolved = target is null || depth >= MaxLinkDepth ? combined : ResolveLinks(target, cache, depth + 1);
        }

        cache[path] = resolved;
        return resolved;
    }

    /// <summary>
    ///     Gets the final target of a link at the end of a path.
    /// </summary>
    /// <param name="path">The full path whose parent directories are already resolved.</param>
    /// <returns>The full path of the final target, or <see langword="null"/> when the path is not a link.</returns>
    private static string? GetLinkTarget(string path)
    {
        try
        {
            return new FileInfo(path).ResolveLinkTarget(returnFinalTarget: true)?.FullName;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or SecurityException)
        {
            // A path that cannot be examined (or a link loop) is compared as it is
            return null;
        }
    }

    /// <summary>
    ///     Processes files, possibly in parallel, and returns their results in path order.
    /// </summary>
    /// <param name="files">Paths of the files to process. Must not be null.</param>
    /// <returns>One result per input file, sorted by path (ordinal), independent of completion order.</returns>
    /// <remarks>
    ///     Results are stored by input index while workers run and sorted afterwards, which keeps the
    ///     outcome identical for any degree of parallelism and any scheduling. Workers must not write
    ///     output. If a replaced file processor throws, the exception is propagated (wrapped in an
    ///     <see cref="AggregateException"/> when run in parallel).
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="files"/> is null.</exception>
    public IReadOnlyList<FileResult> Process(IReadOnlyList<string> files)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(files);

        // Process every file; each worker writes only its own slot
        var results = new FileResult[files.Count];
        var options = new ParallelOptions { MaxDegreeOfParallelism = _maxDegreeOfParallelism };
        Parallel.For(0, files.Count, options, index => results[index] = _fileProcessor(files[index]));

        // Order by path (stable) so the caller never observes scheduling order
        return [.. results.OrderBy(static r => r.Path, StringComparer.Ordinal)];
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
