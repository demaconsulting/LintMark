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
using System.Text;
using DemaConsulting.LintMark.Document;
using Markdig;
using Markdig.Syntax;

namespace DemaConsulting.LintMark.Formatting.Inline;

/// <summary>
///     Proves, for one paragraph, that a set of inline edits does not change how the paragraph renders.
/// </summary>
/// <remarks>
///     <para>
///         The guards of the inline normalizer keep the known ways in which a delimiter or a URL conversion can
///         change the meaning of a paragraph out of the candidates, but guards alone were measured to be
///         insufficient (adjacent delimiter runs that pair differently once one of them is rewritten, an earlier
///         unclosed construct that swallows an inserted <c>&gt;</c>). The proof closes that gap: the content of
///         the paragraph is rebuilt as a stand-alone text (the content of each line from the first inline
///         element on, joined with line feeds, which is what the inline parser reads), the edits are applied to
///         a copy, both texts are rendered with the fixed pipeline of the tool, and the edits stand only when the
///         two HTML strings are byte for byte equal.
///     </para>
///     <para>
///         This is a local copy of the safety property and never a replacement for the file-level checks, which
///         still run unchanged on every rewrite. A paragraph whose proof fails is left exactly as written.
///     </para>
///     <para>
///         Thread safety: static and stateless; safe for concurrent use (the pipeline is immutable).
///     </para>
/// </remarks>
internal static class ParagraphProof
{
    /// <summary>
    ///     Determines whether a set of edits leaves the rendering of a paragraph unchanged.
    /// </summary>
    /// <param name="text">The LF-normalized text of the document. Must not be null.</param>
    /// <param name="lines">The lines of the paragraph (see <see cref="DocumentParser.GetLines"/>). Must not be null or contain null.</param>
    /// <param name="edits">The edits to prove, as offsets into <paramref name="text"/>. Must not be the default array.</param>
    /// <returns>
    ///     <see langword="true"/> when the stand-alone content of the paragraph is read as exactly one paragraph, every edit
    ///     lies inside the content of one line, and the edited content renders to the same HTML as the original;
    ///     <see langword="false"/> otherwise.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null or <paramref name="lines"/> holds a null element.</exception>
    public static bool Holds(string text, IReadOnlyList<LeafLine> lines, ImmutableArray<TextEdit> edits)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(lines);
        if (lines.Any(static line => line is null))
        {
            throw new ArgumentNullException(nameof(lines), "The lines must not contain null.");
        }

        if (edits.IsDefault)
        {
            throw new ArgumentNullException(nameof(edits));
        }

        // Rebuild the content of the paragraph and remember where each line starts in it
        var content = new StringBuilder();
        var starts = new List<(int DocumentStart, int DocumentEnd, int ContentStart)>(lines.Count);
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.ContentStart < 0 || line.ContentEnd < line.ContentStart || line.ContentEnd > text.Length)
            {
                return false;
            }

            if (i > 0)
            {
                content.Append('\n');
            }

            starts.Add((line.ContentStart, line.ContentEnd, content.Length));
            content.Append(text, line.ContentStart, line.ContentEnd - line.ContentStart);
        }

        var original = content.ToString();

        // Translate the edits into the stand-alone content; an edit outside the content of a line cannot be proved
        var edited = new StringBuilder(original);
        foreach (var edit in edits.OrderByDescending(static edit => edit.Start))
        {
            var mapped = Map(starts, edit);
            if (mapped < 0)
            {
                return false;
            }

            edited.Remove(mapped, edit.Length).Insert(mapped, edit.Replacement);
        }

        // The content must be read as one paragraph, and both texts must render alike
        var pipeline = DocumentParser.Pipeline;
        var parsed = Markdown.Parse(original, pipeline);
        if (parsed.Count != 1 || parsed[0] is not ParagraphBlock)
        {
            return false;
        }

        return string.Equals(Markdown.ToHtml(original, pipeline), Markdown.ToHtml(edited.ToString(), pipeline), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Finds the offset in the stand-alone content that corresponds to the start of an edit.
    /// </summary>
    /// <param name="lines">The content ranges of the lines in the document and their start in the content.</param>
    /// <param name="edit">The edit.</param>
    /// <returns>The offset, or -1 when the edit does not lie inside the content of one line.</returns>
    private static int Map(List<(int DocumentStart, int DocumentEnd, int ContentStart)> lines, TextEdit edit)
    {
        foreach (var (documentStart, documentEnd, contentStart) in lines)
        {
            if (edit.Start >= documentStart && edit.End <= documentEnd)
            {
                return contentStart + edit.Start - documentStart;
            }
        }

        return -1;
    }
}
