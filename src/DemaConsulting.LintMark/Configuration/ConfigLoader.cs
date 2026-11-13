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
using System.Text;

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
    ///     Strict UTF-8 decoding: invalid bytes are reported instead of replaced.
    /// </summary>
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

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

        // Strip the byte order mark and decode strictly
        if (HasUnsupportedByteOrderMark(content))
        {
            return Failure(path, "Unsupported byte order mark (only UTF-8 is supported)");
        }

        if (content.Length >= 3 && content[0] == 0xEF && content[1] == 0xBB && content[2] == 0xBF)
        {
            content = content[3..];
        }

        string text;
        try
        {
            text = StrictUtf8.GetString(content);
        }
        catch (DecoderFallbackException ex)
        {
            var (line, column) = LocateByte(content, ex.Index);
            return new ConfigLoadResult(null, path, [new ConfigError(path, line, column, "Invalid UTF-8 byte sequence")]);
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

    /// <summary>
    ///     Determines whether content starts with a UTF-16 or UTF-32 byte order mark.
    /// </summary>
    /// <param name="bytes">The content.</param>
    /// <returns><see langword="true"/> for an unsupported mark.</returns>
    private static bool HasUnsupportedByteOrderMark(ReadOnlySpan<byte> bytes)
    {
        // UTF-16 LE/BE (FF FE / FE FF; the first also covers UTF-32 LE) and UTF-32 BE (00 00 FE FF)
        var isUtf16 = bytes.Length >= 2 && ((bytes[0] == 0xFF && bytes[1] == 0xFE) || (bytes[0] == 0xFE && bytes[1] == 0xFF));
        var isUtf32Be = bytes.Length >= 4 && bytes[0] == 0 && bytes[1] == 0 && bytes[2] == 0xFE && bytes[3] == 0xFF;
        return isUtf16 || isUtf32Be;
    }

    /// <summary>
    ///     Finds the line and column of a byte offset in UTF-8 content.
    /// </summary>
    /// <param name="content">The content without byte order mark.</param>
    /// <param name="index">The byte index reported by the decoder; out-of-range values fall back to 1:1.</param>
    /// <returns>The 1-based line and column of the byte.</returns>
    private static (int Line, int Column) LocateByte(ReadOnlySpan<byte> content, int index)
    {
        if (index < 0 || index > content.Length)
        {
            return (1, 1);
        }

        // Count line breaks before the bad byte (LF, CRLF or a lone CR, as in the nesting scan and in YAML),
        // then measure the partial line in UTF-16 units
        var before = content[..index];
        var line = 1;
        var lineStart = 0;
        for (var i = 0; i < before.Length; i++)
        {
            // A CR directly before an LF belongs to the same line break; the LF is the one counted
            var isBreak = before[i] == (byte)'\n'
                          || (before[i] == (byte)'\r' && (i + 1 >= content.Length || content[i + 1] != (byte)'\n'));
            if (isBreak)
            {
                line++;
                lineStart = i + 1;
            }
        }

        return (line, Encoding.UTF8.GetCharCount(before[lineStart..]) + 1);
    }
}
