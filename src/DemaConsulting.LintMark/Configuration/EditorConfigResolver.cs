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
using System.Security;
using DemaConsulting.LintMark.Document;
using EditorConfig.Core;

namespace DemaConsulting.LintMark.Configuration;

/// <summary>
///     Determines the line ending that the editor settings (<c>.editorconfig</c> files) specify for a Markdown file.
/// </summary>
/// <remarks>
///     <para>
///         Only <c>end_of_line</c> is read. The raw property value is interpreted here rather than the
///         library's typed value, because the typed value does not honor <c>unset</c> and still reports a
///         value inherited from a farther file. <c>lf</c> and <c>crlf</c> (in any letter case) are
///         understood; <c>cr</c>, <c>unset</c>, any other word and a missing property all mean "not
///         specified", which is an ordinary outcome and never an error, because editor settings are shared
///         with other file types. Finding the files, nearest-file precedence, <c>root = true</c>, later
///         sections overriding earlier ones and section patterns are delegated to the library; its section
///         patterns are case-sensitive on every platform and the path is passed exactly as discovered.
///     </para>
///     <para>
///         The resolved chain of settings files is cached per directory. The result is not cached per
///         file, because sections can depend on the file name. A settings file that cannot be read is
///         reported as a message in the result; malformed content is ignored by the library and is never
///         an error.
///     </para>
///     <para>
///         Thread safety: one library parser is shared (it is safe for concurrent use) and the cache holds
///         <see cref="Lazy{T}"/> values with <see cref="LazyThreadSafetyMode.ExecutionAndPublication"/>, so
///         concurrent callers share one resolution per directory and always see the same result.
///     </para>
/// </remarks>
internal sealed class EditorConfigResolver
{
    /// <summary>
    ///     The raw property name that holds the line ending.
    /// </summary>
    private const string EndOfLineProperty = "end_of_line";

    /// <summary>
    ///     Resolves the chain of settings files for a directory.
    /// </summary>
    private readonly Func<string, EditorConfigResolvedChain> _resolveChain;

    /// <summary>
    ///     Evaluates a file against a resolved chain.
    /// </summary>
    private readonly Func<string, EditorConfigResolvedChain, FileConfiguration> _evaluate;

    /// <summary>
    ///     Resolved chains (or the reason they could not be read) by directory.
    /// </summary>
    private readonly ConcurrentDictionary<string, Lazy<(EditorConfigResolvedChain? Chain, string? Problem)>> _chains = new(StringComparer.Ordinal);

    /// <summary>
    ///     Initializes a new instance of the <see cref="EditorConfigResolver"/> class that reads the real file system.
    /// </summary>
    public EditorConfigResolver()
    {
        var parser = new EditorConfigParser();
        _resolveChain = parser.GetResolvedChainForDirectory;
        _evaluate = parser.Parse;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="EditorConfigResolver"/> class with replaceable library calls (a test seam).
    /// </summary>
    /// <param name="resolveChain">Resolves the chain of settings files for a directory. Must not be null.</param>
    /// <param name="evaluate">Evaluates a file against a chain. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    internal EditorConfigResolver(
        Func<string, EditorConfigResolvedChain> resolveChain,
        Func<string, EditorConfigResolvedChain, FileConfiguration> evaluate)
    {
        ArgumentNullException.ThrowIfNull(resolveChain);
        ArgumentNullException.ThrowIfNull(evaluate);
        _resolveChain = resolveChain;
        _evaluate = evaluate;
    }

    /// <summary>
    ///     Determines the line ending the editor settings specify for a Markdown file.
    /// </summary>
    /// <param name="markdownFile">Path of the Markdown file (it need not exist); relative paths are resolved against the current directory. Must not be null or empty.</param>
    /// <returns>
    ///     The specified line ending, "not specified", or a message when the settings could not be read.
    ///     Never throws for file-system or content problems.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="markdownFile"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="markdownFile"/> is empty.</exception>
    public EditorConfigResult Resolve(string markdownFile)
    {
        // Validate input
        ArgumentException.ThrowIfNullOrEmpty(markdownFile);

        // Resolve the full path exactly once; a path that cannot be resolved has no settings
        string fullPath;
        string? directory;
        try
        {
            fullPath = Path.GetFullPath(markdownFile.Replace('\\', '/'));
            directory = Path.GetDirectoryName(fullPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or SecurityException)
        {
            return EditorConfigResult.NotSpecified;
        }

        if (directory is null)
        {
            return EditorConfigResult.NotSpecified;
        }

        // The chain of settings files is shared by every file in the directory
        var (chain, problem) = _chains.GetOrAdd(
            directory,
            dir => new Lazy<(EditorConfigResolvedChain?, string?)>(() => ReadChain(dir), LazyThreadSafetyMode.ExecutionAndPublication)).Value;
        if (chain is null)
        {
            return new EditorConfigResult(null, problem);
        }

        // Evaluate this file against the chain and interpret the raw property
        try
        {
            var configuration = _evaluate(fullPath, chain);
            return configuration.Properties.TryGetValue(EndOfLineProperty, out var raw)
                ? new EditorConfigResult(Interpret(raw), null)
                : EditorConfigResult.NotSpecified;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or SecurityException)
        {
            return new EditorConfigResult(null, $"Cannot read .editorconfig settings: {ex.Message}");
        }
    }

    /// <summary>
    ///     Resolves the chain of settings files for a directory.
    /// </summary>
    /// <param name="directory">The full path of the directory.</param>
    /// <returns>The chain, or the reason it could not be read.</returns>
    private (EditorConfigResolvedChain? Chain, string? Problem) ReadChain(string directory)
    {
        try
        {
            return (_resolveChain(directory), null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or SecurityException)
        {
            return (null, $"Cannot read .editorconfig settings: {ex.Message}");
        }
    }

    /// <summary>
    ///     Interprets the raw value of <c>end_of_line</c>.
    /// </summary>
    /// <param name="raw">The raw value.</param>
    /// <returns>The line ending for <c>lf</c> and <c>crlf</c>; otherwise <see langword="null"/>.</returns>
    private static LineEnding? Interpret(string raw)
    {
        if (string.Equals(raw, "lf", StringComparison.OrdinalIgnoreCase))
        {
            return LineEnding.Lf;
        }

        return string.Equals(raw, "crlf", StringComparison.OrdinalIgnoreCase) ? LineEnding.CrLf : null;
    }
}
