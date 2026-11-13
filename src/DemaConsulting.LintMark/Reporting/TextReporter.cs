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

using DemaConsulting.LintMark.Cli;
using DemaConsulting.LintMark.Linting;

namespace DemaConsulting.LintMark.Reporting;

/// <summary>
///     Writes diagnostics as text, one per line, in a deterministic order.
/// </summary>
/// <remarks>
///     <para>
///         Every diagnostic is written as <c>path:line:col: rule-id message</c> through
///         <see cref="Context.WriteError"/>, which also makes the exit code 1; so a run that reports anything
///         fails, whether the finding is a formatting violation, a lint finding or a failure. Paths are shown
///         relative to the base directory with forward slashes (a path outside it keeps its <c>../</c> parts).
///     </para>
///     <para>
///         The report is deterministic: diagnostics that are equal are written once, and the rest are ordered
///         by display path (ordinal), line, column, rule identifier (ordinal) and message (ordinal), so the
///         output never depends on the order in which files were processed or findings were produced. Writing
///         happens on the calling thread; the unit has no state.
///     </para>
///     <para>
///         Thread safety: static and stateless; safe for concurrent use (the context serializes its own output).
///     </para>
/// </remarks>
internal static class TextReporter
{
    /// <summary>
    ///     Writes diagnostics to the error channel of a context.
    /// </summary>
    /// <param name="diagnostics">The diagnostics to report, in any order. Must not be null or contain null.</param>
    /// <param name="baseDirectory">The directory that paths are shown relative to. Must not be null or empty.</param>
    /// <param name="context">The context that receives the lines. Must not be null.</param>
    /// <remarks>
    ///     Nothing is written (and the exit code is not changed) when there are no diagnostics. On Windows, a
    ///     path on a different drive than the base directory is shown as an absolute path.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when an argument, or an element of <paramref name="diagnostics"/>, is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="baseDirectory"/> is empty and there is a diagnostic.</exception>
    public static void Report(IEnumerable<Diagnostic> diagnostics, string baseDirectory, Context context)
    {
        // Validate input; the elements are checked before anything is written
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(baseDirectory);
        ArgumentNullException.ThrowIfNull(context);
        var items = diagnostics.ToList();
        if (items.Any(static diagnostic => diagnostic is null))
        {
            throw new ArgumentNullException(nameof(diagnostics), "The diagnostics must not contain null.");
        }

        // Remove repeated findings, then order by what the reader sees
        var lines = items
            .Distinct()
            .Select(diagnostic => (Display: GetDisplayPath(diagnostic.Path, baseDirectory), Diagnostic: diagnostic))
            .OrderBy(static item => item.Display, StringComparer.Ordinal)
            .ThenBy(static item => item.Diagnostic.Line)
            .ThenBy(static item => item.Diagnostic.Column)
            .ThenBy(static item => item.Diagnostic.RuleId, StringComparer.Ordinal)
            .ThenBy(static item => item.Diagnostic.Message, StringComparer.Ordinal);

        // Write sequentially from the calling thread
        foreach (var (display, diagnostic) in lines)
        {
            context.WriteError($"{display}:{diagnostic.Line}:{diagnostic.Column}: {diagnostic.RuleId} {diagnostic.Message}");
        }
    }

    /// <summary>
    ///     Converts a path to the form used in reports: relative to the base directory with forward slashes.
    /// </summary>
    /// <param name="path">The path as processed (normally a full path).</param>
    /// <param name="baseDirectory">The directory to relativize against. Must not be empty.</param>
    /// <returns>The display path; on Windows, the absolute path when it is on a different drive.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="baseDirectory"/> is empty.</exception>
    internal static string GetDisplayPath(string path, string baseDirectory)
    {
        var relative = Path.GetRelativePath(baseDirectory, path);
        return Path.DirectorySeparatorChar == '/' ? relative : relative.Replace(Path.DirectorySeparatorChar, '/');
    }
}
