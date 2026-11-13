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
using DemaConsulting.LintMark.Configuration;
using DemaConsulting.LintMark.Document;
using DemaConsulting.LintMark.Linting;
using Markdig.Syntax;

namespace DemaConsulting.LintMark.Formatting.Blocks;

/// <summary>
///     Writes thematic breaks in the canonical form: three characters of the configured kind, with no spaces
///     between them and no trailing whitespace.
/// </summary>
/// <remarks>
///     <para>
///         The break characters (and the spaces between them) are replaced by <c>---</c>, <c>***</c> or
///         <c>___</c> as configured; the indentation and any container prefix stay where they are, because only the
///         characters of one line change. Trailing whitespace after the break is removed.
///     </para>
///     <para>
///         Guards keep a conversion from changing the meaning of the document. A <c>---</c> line directly under
///         a line of text is a setext heading underline, so a break written another way is left as written
///         there. A <c>---</c> on the first line of the file could start YAML front matter, so it is not produced
///         there either. A break that follows a list marker of the same character (<c>- ***</c>) is left alone,
///         because the converted line (<c>- ---</c>) would be a thematic break by itself. Trailing whitespace is
///         still removed in all of these cases.
///     </para>
///     <para>
///         Thread safety: stateless; one instance serves every worker thread.
///     </para>
/// </remarks>
internal sealed class ThematicBreakFormatter : IBlockFormatter
{
    /// <summary>
    ///     Gets the identifiers of the rules this formatter enforces.
    /// </summary>
    public ImmutableArray<string> RuleIds { get; } =
    [
        RuleCatalog.ThematicBreakRuleId,
        RuleCatalog.TrailingWhitespaceRuleId
    ];

    /// <summary>
    ///     Determines whether a block is a thematic break.
    /// </summary>
    /// <param name="block">The block to test. Must not be null.</param>
    /// <returns><see langword="true"/> for a thematic break.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="block"/> is null.</exception>
    public bool CanFormat(BlockInfo block)
    {
        ArgumentNullException.ThrowIfNull(block);
        return block.Kind == BlockKind.ThematicBreak && block.Node is ThematicBreakBlock;
    }

    /// <summary>
    ///     Proposes the edits that bring a thematic break into canonical form.
    /// </summary>
    /// <param name="context">The document and settings. Must not be null.</param>
    /// <param name="block">The thematic break. Must be accepted by <see cref="CanFormat"/>.</param>
    /// <param name="edits">Receives the edits. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="block"/> is not a thematic break.</exception>
    /// <exception cref="InvalidOperationException">Thrown when an edit conflicts with another (a defect, not a property of the document).</exception>
    public void Format(FormatContext context, BlockInfo block, EditSet edits)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(block);
        ArgumentNullException.ThrowIfNull(edits);
        if (!CanFormat(block))
        {
            throw new ArgumentException($"The block must be a thematic break, not {block.Kind}.", nameof(block));
        }

        // Find the break characters on the line: the first is at the block start, the last is followed only by blanks
        var layout = new LineLayout(context.Document.Source);
        var text = layout.Text;
        var line = layout.LineOf(block.Start);
        var lineEnd = layout.LineEnd(line);
        var kind = text[block.Start];
        if (kind is not ('-' or '*' or '_'))
        {
            return;
        }

        var breakEnd = block.Start;
        for (var position = block.Start; position < lineEnd; position++)
        {
            if (text[position] == kind)
            {
                breakEnd = position + 1;
            }
            else if (!LineLayout.IsBlankChar(text[position]))
            {
                return;
            }
        }

        // Convert the break unless a guard says the new form would be read differently
        var target = context.Config.ThematicBreak switch
        {
            ThematicBreakStyle.Asterisks => "***",
            ThematicBreakStyle.Underscores => "___",
            _ => "---"
        };
        if (!text.AsSpan(block.Start, breakEnd - block.Start).SequenceEqual(target) && !IsGuarded(context.Document, layout, block, line, target))
        {
            edits.Add(new TextEdit(block.Start, breakEnd - block.Start, target, RuleCatalog.ThematicBreakRuleId));
        }

        if (lineEnd > breakEnd)
        {
            edits.Add(new TextEdit(breakEnd, lineEnd - breakEnd, string.Empty, RuleCatalog.TrailingWhitespaceRuleId));
        }
    }

    /// <summary>
    ///     Determines whether writing a break as <paramref name="target"/> would change how the document reads.
    /// </summary>
    /// <param name="document">The parsed document.</param>
    /// <param name="layout">The line arithmetic of the document.</param>
    /// <param name="block">The thematic break.</param>
    /// <param name="line">The line of the break.</param>
    /// <param name="target">The text the break would be written as.</param>
    /// <returns>
    ///     <see langword="true"/> when the line has a list marker of the same character before the break (the line
    ///     would become a break on its own), for the dash form when the line is the first line of the file (it could
    ///     open front matter), and for the dash form when text may be on the line above (the dashes would underline it).
    /// </returns>
    /// <remarks>
    ///     The decision must not depend on anything that another rule changes in the same run, or formatting the
    ///     result would decide differently. A break that follows a heading, a code block, another break, a list, a
    ///     quote or a table directly is never guarded: the blank-line rules put a blank line there anyway.
    /// </remarks>
    private static bool IsGuarded(ParsedDocument document, LineLayout layout, BlockInfo block, int line, string target)
    {
        // A break written as one kind of character, after a list marker of the same kind, would itself be a break (and the list is lost)
        var lineStart = layout.LineStart(line);
        if (layout.Text.AsSpan(lineStart, block.Start - lineStart).Contains(target[0]))
        {
            return true;
        }

        if (target != "---")
        {
            return false;
        }

        if (line == 0 && lineStart == block.Start)
        {
            return true;
        }

        // A new dashes line could close front matter that the first line of the document opens
        if (FrontMatterGuard.CouldOpenFrontMatter(document))
        {
            return true;
        }

        // Dashes directly under a line of text would underline it; a break that opens its container has no text above it in that container
        if (line == 0 || IsBlankInContainer(layout, line - 1) || PreviousSibling(document, block) is not { } previous)
        {
            return false;
        }

        // Only a block that is complete in itself may precede the dashes
        return previous.Kind is not (BlockKind.Heading or BlockKind.FencedCode or BlockKind.IndentedCode or BlockKind.ThematicBreak
            or BlockKind.List or BlockKind.BlockQuote or BlockKind.Table);
    }

    /// <summary>
    ///     Determines whether a line holds nothing but blanks and block quote markers.
    /// </summary>
    /// <param name="layout">The line arithmetic of the document.</param>
    /// <param name="line">The zero-based line.</param>
    /// <returns><see langword="true"/> for a blank line, also one inside a block quote (<c>&gt;</c> alone).</returns>
    private static bool IsBlankInContainer(LineLayout layout, int line) =>
        !layout.Text.AsSpan(layout.LineStart(line), layout.LineEnd(line) - layout.LineStart(line)).ContainsAnyExcept(' ', '\t', '>');

    /// <summary>
    ///     Finds the block that directly precedes a block in the same container.
    /// </summary>
    /// <param name="document">The parsed document.</param>
    /// <param name="block">The block.</param>
    /// <returns>The previous sibling, or <see langword="null"/> when the block is the first in its container.</returns>
    /// <remarks>
    ///     The flattened block list is used instead of the parser's tree: the tree keeps link reference
    ///     definitions in a group at the document root and would show that group as the neighbor.
    /// </remarks>
    private static BlockInfo? PreviousSibling(ParsedDocument document, BlockInfo block)
    {
        var blocks = document.Blocks;
        for (var i = blocks.IndexOf(block) - 1; i >= 0; i--)
        {
            if (ReferenceEquals(blocks[i].Parent, block.Parent))
            {
                return blocks[i];
            }
        }

        return null;
    }
}
