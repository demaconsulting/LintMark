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

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Security;
using DemaConsulting.LintMark.Document;

namespace DemaConsulting.LintMark.Configuration;

/// <summary>
///     Finds, reads and validates the <c>.lintmark.yaml</c> configuration that applies to a Markdown file.
/// </summary>
/// <remarks>
///     <para>
///         A loader works in one of three modes chosen by its factory: built-in defaults only (nothing is
///         read), discovery (the nearest <c>.lintmark.yaml</c> for each file), or one explicit file. In
///         discovery mode the search starts in the file's own directory and walks upward; the nearest file
///         wins and files are never merged. The walk stops after the first directory that contains a
///         <c>.git</c> entry (a directory, or a file as used by linked working trees and submodules), so the repository
///         root itself is searched and nothing above it is. A directory that merely has the config file's
///         name is not a configuration.
///     </para>
///     <para>
///         Loading never throws for problems in the environment or the content: they come back as
///         <see cref="ConfigError"/> entries in the <see cref="ConfigLoadResult"/>. Content is read once per
///         configuration file per loader; results are cached per directory and per file, so edits made
///         while a run is in progress are not seen.
///     </para>
///     <para>
///         Thread safety: the caches are <see cref="ConcurrentDictionary{TKey, TValue}"/> instances holding
///         <see cref="Lazy{T}"/> values with <see cref="LazyThreadSafetyMode.ExecutionAndPublication"/>, so
///         concurrent callers share one parse and every caller observes the same immutable result. Results
///         are pure functions of the file-system content, independent of scheduling.
///     </para>
/// </remarks>
internal sealed class ConfigLoader
{
    /// <summary>
    ///     The rule identifier under which every configuration problem is reported.
    /// </summary>
    public const string ErrorRuleId = "config-error";

    /// <summary>
    ///     The name of the configuration file that is discovered.
    /// </summary>
    public const string FileName = ".lintmark.yaml";

    /// <summary>
    ///     The largest configuration file that is read, in bytes (1 MiB); a guard against reading an unrelated huge file.
    /// </summary>
    public const int MaxFileBytes = 1_048_576;

    /// <summary>
    ///     The name of the entry that marks the repository root.
    /// </summary>
    private const string RepositoryMarker = ".git";

    /// <summary>
    ///     Size of the buffer used to read a configuration file.
    /// </summary>
    private const int ReadBufferSize = 8192;

    /// <summary>
    ///     The result shared by every load that finds no configuration.
    /// </summary>
    private static readonly ConfigLoadResult DefaultResult = new(LintMarkConfig.Default, null, []);

    /// <summary>
    ///     The result returned for every file (defaults or explicit file), or <see langword="null"/> in discovery mode.
    /// </summary>
    private readonly ConfigLoadResult? _fixedResult;

    /// <summary>
    ///     Results by start directory (discovery mode).
    /// </summary>
    private readonly ConcurrentDictionary<string, Lazy<ConfigLoadResult>> _directories = new(StringComparer.Ordinal);

    /// <summary>
    ///     Results by configuration file path (discovery mode).
    /// </summary>
    private readonly ConcurrentDictionary<string, Lazy<ConfigLoadResult>> _files = new(StringComparer.Ordinal);

    /// <summary>
    ///     Initializes a new instance of the <see cref="ConfigLoader"/> class.
    /// </summary>
    /// <param name="fixedResult">The result for every file, or <see langword="null"/> for discovery mode.</param>
    private ConfigLoader(ConfigLoadResult? fixedResult)
    {
        _fixedResult = fixedResult;
        InitialErrors = fixedResult is { Config: null } ? fixedResult.Errors : ImmutableArray<ConfigError>.Empty;
    }

    /// <summary>
    ///     Gets the problems of an explicitly named configuration file, so they can be reported even when no Markdown
    ///     file is processed; empty in the other modes.
    /// </summary>
    public ImmutableArray<ConfigError> InitialErrors { get; }

    /// <summary>
    ///     Creates a loader that always returns the built-in defaults and never touches the file system.
    /// </summary>
    /// <returns>The loader.</returns>
    public static ConfigLoader ForDefaults() => new(DefaultResult);

    /// <summary>
    ///     Creates a loader that discovers the nearest <c>.lintmark.yaml</c> for each file.
    /// </summary>
    /// <returns>The loader.</returns>
    public static ConfigLoader ForDiscovery() => new((ConfigLoadResult?)null);

    /// <summary>
    ///     Creates a loader that returns exactly one configuration file for every file.
    /// </summary>
    /// <param name="configFile">Path of the file, relative to the current directory or absolute; both separator styles are accepted. Must not be null or blank.</param>
    /// <returns>The loader. The file is read once, immediately; a problem with it is available as <see cref="InitialErrors"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configFile"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="configFile"/> is empty or blank.</exception>
    public static ConfigLoader ForFile(string configFile)
    {
        // Validate input, then load once; discovery is not consulted
        ArgumentException.ThrowIfNullOrWhiteSpace(configFile);
        return new ConfigLoader(LoadFile(configFile));
    }

    /// <summary>
    ///     Gets the configuration that applies to a Markdown file.
    /// </summary>
    /// <param name="markdownFile">Path of the Markdown file (it need not exist); relative paths are resolved against the current directory. Must not be null or empty.</param>
    /// <returns>The configuration or the problems found; never throws for environment or content problems.</returns>
    /// <remarks>
    ///     In discovery mode the result is cached per start directory, so every file in a directory shares one
    ///     result instance.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="markdownFile"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="markdownFile"/> is empty.</exception>
    public ConfigLoadResult Load(string markdownFile)
    {
        // Validate input
        ArgumentException.ThrowIfNullOrEmpty(markdownFile);

        // Defaults and explicit-file loaders answer the same for every file
        if (_fixedResult is not null)
        {
            return _fixedResult;
        }

        // Find the directory the search starts in; a path that cannot be resolved has no configuration
        string? directory;
        try
        {
            directory = Path.GetDirectoryName(Path.GetFullPath(Normalize(markdownFile)));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or SecurityException)
        {
            return DefaultResult;
        }

        if (directory is null)
        {
            return DefaultResult;
        }

        // One search per directory, shared by every thread that asks
        return _directories.GetOrAdd(directory, ResolveDirectoryLazy).Value;
    }

    /// <summary>
    ///     Finds the configuration file that applies to files in a directory, without any caching.
    /// </summary>
    /// <param name="directory">The directory to start in (relative to the current directory or absolute). Must not be null or empty.</param>
    /// <returns>The full path of the nearest configuration file, or <see langword="null"/> when there is none within the repository.</returns>
    /// <remarks>
    ///     In each directory the configuration file is checked first, then the repository marker. A config file
    ///     in the repository root is therefore found, and nothing above the root is ever searched. Without a
    ///     repository marker the search ends at the file-system root.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="directory"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="directory"/> is empty.</exception>
    public static string? FindConfigFile(string directory)
    {
        // Validate input
        ArgumentException.ThrowIfNullOrEmpty(directory);

        // Resolve the start; a path that cannot be resolved has no configuration
        string? current;
        try
        {
            current = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Normalize(directory)));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or SecurityException)
        {
            return null;
        }

        // Walk upward until a config file is found, the repository root is passed, or the file-system root is reached
        while (!string.IsNullOrEmpty(current))
        {
            var candidate = Path.Combine(current, FileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            var marker = Path.Combine(current, RepositoryMarker);
            if (Directory.Exists(marker) || File.Exists(marker))
            {
                return null;
            }

            var parent = Path.GetDirectoryName(current);
            current = string.Equals(parent, current, StringComparison.Ordinal) ? null : parent;
        }

        return null;
    }

    /// <summary>
    ///     Reads and validates one configuration file, without any caching.
    /// </summary>
    /// <param name="configFile">Path of the file, relative to the current directory or absolute. Must not be null or blank.</param>
    /// <returns>The configuration or the problems found; a missing, unreadable or oversized file is a problem at line 1, column 1.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configFile"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="configFile"/> is empty or blank.</exception>
    public static ConfigLoadResult LoadFile(string configFile)
    {
        // Validate input
        ArgumentException.ThrowIfNullOrWhiteSpace(configFile);

        // Resolve the path, reporting an unusable one against the text as given
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(Normalize(configFile));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or SecurityException)
        {
            return Failure(configFile, "Cannot read configuration file: the path is not valid");
        }

        // Read at most one byte more than the limit, so an oversized file is detected without loading all of it
        var content = ReadFile(fullPath, out var problem);
        return content is null ? Failure(fullPath, problem!) : Parse(fullPath, content);
    }

    /// <summary>
    ///     Decodes and validates the content of a configuration file.
    /// </summary>
    /// <param name="path">Path reported in every error. Must not be null or empty.</param>
    /// <param name="content">The raw bytes of the file.</param>
    /// <returns>The configuration or the problems found.</returns>
    /// <remarks>
    ///     A UTF-8 byte order mark is removed silently; a UTF-16 or UTF-32 mark and invalid UTF-8 are reported at
    ///     the offending position (1-based, UTF-16 code units, a tab counts as one); line endings may be LF or
    ///     CRLF. Content above <see cref="MaxFileBytes"/> is rejected. Pure: no I/O and no shared state.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is empty.</exception>
    public static ConfigLoadResult Parse(string path, ReadOnlySpan<byte> content)
    {
        // Validate input
        ArgumentException.ThrowIfNullOrEmpty(path);

        if (content.Length > MaxFileBytes)
        {
            return Failure(path, "The configuration file is larger than 1 MiB");
        }

        // Strip the byte order mark and decode strictly, like a Markdown file
        string text;
        try
        {
            text = SourceDocument.Decode(content, out _);
        }
        catch (SourceDocumentException ex)
        {
            return new ConfigLoadResult(null, path, [new ConfigError(path, ex.Line, ex.Column, ex.Message)]);
        }

        return ConfigParser.Parse(path, text);
    }

    /// <summary>
    ///     Creates the lazy search for one start directory.
    /// </summary>
    /// <param name="directory">The start directory.</param>
    /// <returns>The lazy result.</returns>
    private Lazy<ConfigLoadResult> ResolveDirectoryLazy(string directory) =>
        new(() => ResolveDirectory(directory), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    ///     Searches for the configuration of one start directory and loads it through the per-file cache.
    /// </summary>
    /// <param name="directory">The start directory.</param>
    /// <returns>The result.</returns>
    private ConfigLoadResult ResolveDirectory(string directory)
    {
        var configFile = FindConfigFile(directory);
        if (configFile is null)
        {
            return DefaultResult;
        }

        // Several directories can share one file; it is read and parsed once
        return _files.GetOrAdd(
            configFile,
            static path => new Lazy<ConfigLoadResult>(() => LoadFile(path), LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    /// <summary>
    ///     Replaces backslashes with forward slashes so both separator styles work on every platform.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The path with forward slashes.</returns>
    private static string Normalize(string path) => path.Replace('\\', '/');

    /// <summary>
    ///     Creates a result holding one problem at the start of the file.
    /// </summary>
    /// <param name="path">The path reported.</param>
    /// <param name="message">The message.</param>
    /// <returns>The failed result.</returns>
    private static ConfigLoadResult Failure(string path, string message) =>
        new(null, path, [new ConfigError(path, 1, 1, message)]);

    /// <summary>
    ///     Reads a file, up to the size limit plus one byte.
    /// </summary>
    /// <param name="fullPath">The full path.</param>
    /// <param name="problem">Receives the message when the file cannot be read.</param>
    /// <returns>The bytes, or <see langword="null"/> when the file could not be read.</returns>
    private static byte[]? ReadFile(string fullPath, out string? problem)
    {
        problem = null;

        // A directory cannot be opened as a file on every platform, so check it explicitly for one stable message
        if (Directory.Exists(fullPath))
        {
            problem = "Cannot read configuration file: the path is a directory";
            return null;
        }

        try
        {
            using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var buffer = new MemoryStream();
            var chunk = new byte[ReadBufferSize];
            int read;
            while (buffer.Length <= MaxFileBytes && (read = stream.Read(chunk, 0, chunk.Length)) > 0)
            {
                buffer.Write(chunk, 0, read);
            }

            return buffer.ToArray();
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            problem = "Configuration file not found";
        }
        catch (UnauthorizedAccessException)
        {
            problem = "Access to the configuration file is denied";
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or SecurityException)
        {
            problem = $"Cannot read configuration file: {ex.Message}";
        }

        return null;
    }
}

/// <summary>
///     The outcome of loading a configuration: either a valid configuration or the list of problems found.
/// </summary>
/// <remarks>
///     <para>
///         Supporting type of <see cref="ConfigLoader"/>. A load never yields a half-valid configuration:
///         <see cref="Config"/> is set exactly when <see cref="Errors"/> is empty, which makes it
///         impossible to use settings from a file that was reported as invalid. The errors are stored
///         sorted by line, column and message so the report does not depend on discovery order.
///     </para>
///     <para>
///         Thread safety: instances are immutable and may be shared between threads.
///     </para>
/// </remarks>
internal sealed record ConfigLoadResult
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ConfigLoadResult"/> class.
    /// </summary>
    /// <param name="config">The valid configuration, or <see langword="null"/> when there are errors.</param>
    /// <param name="sourcePath">Full path of the file the configuration came from; <see langword="null"/> for built-in defaults.</param>
    /// <param name="errors">The problems found; empty exactly when <paramref name="config"/> is set.</param>
    public ConfigLoadResult(LintMarkConfig? config, string? sourcePath, ImmutableArray<ConfigError> errors)
    {
        // Store the errors in report order
        Config = config;
        SourcePath = sourcePath;
        Errors =
        [
            .. errors
                .OrderBy(static e => e.Line)
                .ThenBy(static e => e.Column)
                .ThenBy(static e => e.Message, StringComparer.Ordinal)
        ];
    }

    /// <summary>
    ///     Gets the valid configuration, or <see langword="null"/> when the source had errors.
    /// </summary>
    public LintMarkConfig? Config { get; }

    /// <summary>
    ///     Gets the full path of the configuration file, or <see langword="null"/> when built-in defaults apply.
    /// </summary>
    public string? SourcePath { get; }

    /// <summary>
    ///     Gets the problems found, sorted by line, column and message (empty when <see cref="Config"/> is set).
    /// </summary>
    public ImmutableArray<ConfigError> Errors { get; }
}

/// <summary>
///     One problem found in a configuration source, with the position it is reported at.
/// </summary>
/// <remarks>
///     Supporting type of <see cref="ConfigLoader"/>. Immutable. The message is kept on one line (line
///     breaks are replaced by spaces) so that one problem is always one line of output.
/// </remarks>
internal sealed record ConfigError
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ConfigError"/> class.
    /// </summary>
    /// <param name="path">Path of the file the problem is reported against. Must not be null or empty.</param>
    /// <param name="line">1-based line.</param>
    /// <param name="column">1-based column.</param>
    /// <param name="message">Description of the problem without the path or position. Must not be null or blank.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> or <paramref name="message"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is empty or <paramref name="message"/> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="line"/> or <paramref name="column"/> is less than one.</exception>
    public ConfigError(string path, int line, int column, string message)
    {
        // Validate input
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentOutOfRangeException.ThrowIfLessThan(line, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(column, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Path = path;
        Line = line;
        Column = column;
        Message = message.ReplaceLineEndings(" ");
    }

    /// <summary>
    ///     Gets the path of the file the problem is reported against.
    /// </summary>
    public string Path { get; }

    /// <summary>
    ///     Gets the 1-based line of the problem.
    /// </summary>
    public int Line { get; }

    /// <summary>
    ///     Gets the 1-based column of the problem (UTF-16 code units; a tab counts as one).
    /// </summary>
    public int Column { get; }

    /// <summary>
    ///     Gets the single-line description of the problem.
    /// </summary>
    public string Message { get; }
}
