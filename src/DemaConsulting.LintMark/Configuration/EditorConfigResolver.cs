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
    ///     The library parser; safe for concurrent use.
    /// </summary>
    private readonly EditorConfigParser _parser = new();

    /// <summary>
    ///     Resolved chains (or the reason they could not be read) by directory.
    /// </summary>
    private readonly ConcurrentDictionary<string, Lazy<(EditorConfigResolvedChain? Chain, string? Problem)>> _chains = new(StringComparer.Ordinal);

    /// <summary>
    ///     Determines the line ending the editor settings specify for a Markdown file.
    /// </summary>
    /// <param name="markdownFile">Path of the Markdown file (it need not exist); relative paths are resolved against the current directory.</param>
    /// <returns>
    ///     The specified line ending, "not specified", or a message when the settings could not be read.
    ///     Never throws for file-system or content problems.
    /// </returns>
    public EditorConfigResult Resolve(string markdownFile)
    {
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
            var configuration = _parser.Parse(fullPath, chain);
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
            return (_parser.GetResolvedChainForDirectory(directory), null);
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

/// <summary>
///     The editor-settings outcome for one Markdown file: the line ending the editor settings specify, or why they could not be read.
/// </summary>
/// <remarks>
///     <para>
///         Supporting type of <see cref="EditorConfigResolver"/>. "Not specified" (both members
///         <see langword="null"/>) is an ordinary outcome, not an error: editor settings are shared with
///         other file types and often say nothing about line endings.
///     </para>
///     <para>
///         Thread safety: instances are immutable and may be shared between threads.
///     </para>
/// </remarks>
internal sealed record EditorConfigResult
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="EditorConfigResult"/> class.
    /// </summary>
    /// <param name="endOfLine">The line ending the editor settings specify, or <see langword="null"/>.</param>
    /// <param name="errorMessage">Why the settings could not be read, or <see langword="null"/>.</param>
    public EditorConfigResult(LineEnding? endOfLine, string? errorMessage)
    {
        EndOfLine = endOfLine;
        ErrorMessage = errorMessage?.ReplaceLineEndings(" ");
    }

    /// <summary>
    ///     Gets the outcome in which the editor settings say nothing about the line ending.
    /// </summary>
    public static EditorConfigResult NotSpecified { get; } = new(null, null);

    /// <summary>
    ///     Gets the line ending the editor settings specify, or <see langword="null"/> when they do not.
    /// </summary>
    public LineEnding? EndOfLine { get; }

    /// <summary>
    ///     Gets why the editor settings could not be read, or <see langword="null"/> when they could.
    /// </summary>
    public string? ErrorMessage { get; }
}
