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

using DemaConsulting.LintMark.Configuration;
using DemaConsulting.LintMark.Document;
using DemaConsulting.LintMark.Linting;

namespace DemaConsulting.LintMark.Formatting;

/// <summary>
///     The document-level pass that decides how the file ends: with exactly one line ending, or with none.
/// </summary>
/// <remarks>
///     <para>
///         Everything after the last line with content is the trailer. With <c>final-newline</c> on, the trailer
///         becomes a single line feed (this adds a missing one and removes extra blank lines and whitespace-only
///         lines); with it off, the trailer is removed. A document without any content is left alone.
///     </para>
///     <para>
///         When the last content belongs to a block whose trailing blank lines are content (an unclosed fence, an
///         HTML block that only the end of the file closes) the trailer is left as it is: changing it would change
///         what the block contains. Thread safety: static and stateless; safe for concurrent use.
///     </para>
/// </remarks>
internal static class TrailerPass
{
    /// <summary>
    ///     Adds the edit that gives the file its final line ending, when it needs one.
    /// </summary>
    /// <param name="document">The parsed document (for suppressions). Must not be null.</param>
    /// <param name="layout">The line arithmetic of the document. Must not be null.</param>
    /// <param name="ownership">Which lines belong to which block. Must not be null.</param>
    /// <param name="config">The effective configuration. Must not be null.</param>
    /// <param name="edits">Receives the edit. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public static void Apply(ParsedDocument document, LineLayout layout, LineOwnership ownership, LintMarkConfig config, EditSet edits)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(ownership);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(edits);

        // Find the last line with content; a document without content has no trailer to fix
        var lastContent = layout.LineCount - 1;
        while (lastContent >= 0 && layout.IsBlank(lastContent))
        {
            lastContent--;
        }

        if (lastContent < 0)
        {
            return;
        }

        // Blank lines that are part of a block's content are never touched, nor are those after a footnote body that is not plain text
        var owner = ownership.LeafAt(lastContent);
        if (owner is not null && LineOwnership.KeepsTrailingBlankLines(owner, contentFollows: false))
        {
            return;
        }

        if (ownership.IsInFootnote(lastContent) && owner?.Kind != BlockKind.Paragraph)
        {
            return;
        }

        // The trailer starts where the last content line ends
        var text = layout.Text;
        var trailerStart = layout.LineEnd(lastContent);
        var wanted = config.FinalNewline ? "\n" : string.Empty;
        if (text.AsSpan(trailerStart).SequenceEqual(wanted))
        {
            return;
        }

        if (document.Suppressions.IsSuppressedRange(trailerStart, text.Length + 1, RuleCatalog.FinalNewlineRuleId))
        {
            return;
        }

        edits.Add(new TextEdit(trailerStart, text.Length - trailerStart, wanted, RuleCatalog.FinalNewlineRuleId));
    }
}
