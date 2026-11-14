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

using DemaConsulting.LintMark.Document;
using DemaConsulting.LintMark.Formatting.Proof;
using DemaConsulting.LintMark.Reporting;
using Markdig;

namespace DemaConsulting.LintMark.Verify.Reporting;

/// <summary>
///     Builds the bounded diffs that the reports show for a failure.
/// </summary>
/// <remarks>
///     The unified diff of the formatted text is made by the product's own diff reporter, so it is what
///     <c>lintmark --diff</c> would show. The HTML difference is the same kind of diff of the normalized HTML of
///     both sides, with a line break after every tag so that the difference is easy to see. Stateless and thread-safe.
/// </remarks>
internal static class DiffText
{
    /// <summary>
    ///     The marker the normalizer puts around verbatim parts; it is invisible and is not shown.
    /// </summary>
    private const char VerbatimMarker = '\u0001';

    /// <summary>
    ///     Creates the unified diff of the original and the formatted text.
    /// </summary>
    /// <param name="path">The path to show.</param>
    /// <param name="beforeLf">The original LF-normalized text.</param>
    /// <param name="afterLf">The formatted LF-normalized text.</param>
    /// <param name="maxLines">The longest diff kept, in lines.</param>
    /// <returns>The diff lines, capped; empty when the texts are equal.</returns>
    public static IReadOnlyList<string> Text(string path, string beforeLf, string afterLf, int maxLines)
    {
        var diff = new FileDiff(path, beforeLf, afterLf, new LineEndingInfo(0, 0), LineEnding.Lf);
        return Cap(DiffReporter.Format(diff, path), maxLines);
    }

    /// <summary>
    ///     Creates the unified diff of the normalized HTML of two texts.
    /// </summary>
    /// <param name="before">The original text (it may contain carriage returns).</param>
    /// <param name="after">The formatted text.</param>
    /// <param name="maxLines">The longest diff kept, in lines.</param>
    /// <returns>The diff lines, capped; a single line naming the problem when the rendering fails.</returns>
    public static IReadOnlyList<string> Html(string before, string after, int maxLines)
    {
        try
        {
            var left = Lines(before);
            var right = Lines(after);
            var diff = new FileDiff("normalized.html", left, right, new LineEndingInfo(0, 0), LineEnding.Lf);
            return Cap(DiffReporter.Format(diff, "normalized.html"), maxLines);
        }
        catch (Exception ex)
        {
            return [$"(the HTML difference is not available: {ex.GetType().Name})"];
        }
    }

    /// <summary>
    ///     Renders a text, normalizes the HTML and puts every tag on a line of its own.
    /// </summary>
    /// <param name="text">The Markdown text.</param>
    /// <returns>The lines, joined with LF.</returns>
    private static string Lines(string text)
    {
        var html = HtmlEquivalenceChecker.Normalize(Markdown.ToHtml(text, DocumentParser.Pipeline));
        return html.Replace(VerbatimMarker.ToString(), string.Empty, StringComparison.Ordinal).Replace("\r", "\\r", StringComparison.Ordinal).Replace(">", ">\n", StringComparison.Ordinal);
    }

    /// <summary>
    ///     Cuts a diff to a number of lines.
    /// </summary>
    /// <param name="lines">The lines.</param>
    /// <param name="maxLines">The largest number of lines to keep.</param>
    /// <returns>The lines, with a closing line that says how many were left out.</returns>
    private static IReadOnlyList<string> Cap(IReadOnlyList<string> lines, int maxLines)
    {
        if (lines.Count <= maxLines)
        {
            return lines;
        }

        return [.. lines.Take(maxLines), $"... ({lines.Count - maxLines} more lines)"];
    }
}
