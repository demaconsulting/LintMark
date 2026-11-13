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
using DemaConsulting.LintMark.Document;
using DemaConsulting.LintMark.Formatting;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Base class of the test block formatters: they format paragraphs and are registered under the
///     <c>paragraph-wrap</c> rule, so suppression comments for that rule apply to them.
/// </summary>
/// <remarks>
///     The formatters in this file are deliberately broken (or deliberately simple) doubles that let the tests
///     prove that the safety checks refuse every kind of harmful rewrite. They are never used in production code.
/// </remarks>
internal abstract class ParagraphTestFormatter : IBlockFormatter
{
    /// <summary>
    ///     Gets the rule the formatter is registered under.
    /// </summary>
    public ImmutableArray<string> RuleIds { get; } = ["paragraph-wrap"];

    /// <summary>
    ///     Accepts paragraphs only.
    /// </summary>
    /// <param name="block">The block.</param>
    /// <returns><see langword="true"/> for a paragraph.</returns>
    public bool CanFormat(BlockInfo block) => block.Kind == BlockKind.Paragraph;

    /// <summary>
    ///     Proposes the edits for a paragraph.
    /// </summary>
    /// <param name="context">The document and settings.</param>
    /// <param name="block">The paragraph.</param>
    /// <param name="edits">Receives the edits.</param>
    public abstract void Format(FormatContext context, BlockInfo block, EditSet edits);

    /// <summary>
    ///     Gets the text of a block.
    /// </summary>
    /// <param name="context">The document and settings.</param>
    /// <param name="block">The block.</param>
    /// <returns>The LF-normalized text of the block.</returns>
    protected static string TextOf(FormatContext context, BlockInfo block) =>
        context.Document.Source.Text.Substring(block.Start, block.EndInclusive - block.Start + 1);
}

/// <summary>
///     Replaces every occurrence of a text inside paragraphs: a simple, correct formatter for the tests that need edits.
/// </summary>
internal sealed class ReplacingFormatter : ParagraphTestFormatter
{
    /// <summary>
    ///     The text to find.
    /// </summary>
    private readonly string _find;

    /// <summary>
    ///     The text to put in its place.
    /// </summary>
    private readonly string _replacement;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ReplacingFormatter"/> class.
    /// </summary>
    /// <param name="find">The text to find; must not be empty.</param>
    /// <param name="replacement">The replacement.</param>
    public ReplacingFormatter(string find, string replacement)
    {
        _find = find;
        _replacement = replacement;
    }

    /// <inheritdoc />
    public override void Format(FormatContext context, BlockInfo block, EditSet edits)
    {
        var text = TextOf(context, block);
        var index = text.IndexOf(_find, StringComparison.Ordinal);
        while (index >= 0)
        {
            edits.Add(new TextEdit(block.Start + index, _find.Length, _replacement, "paragraph-wrap"));
            index = text.IndexOf(_find, index + _find.Length, StringComparison.Ordinal);
        }
    }
}

/// <summary>
///     Removes the two spaces of a hard break, which changes how the paragraph renders.
/// </summary>
internal sealed class HardBreakDroppingFormatter : ParagraphTestFormatter
{
    /// <inheritdoc />
    public override void Format(FormatContext context, BlockInfo block, EditSet edits)
    {
        var text = TextOf(context, block);
        var index = text.IndexOf("  \n", StringComparison.Ordinal);
        while (index >= 0)
        {
            edits.Add(new TextEdit(block.Start + index, 2, string.Empty, "paragraph-wrap"));
            index = text.IndexOf("  \n", index + 3, StringComparison.Ordinal);
        }
    }
}

/// <summary>
///     Joins the lines of a paragraph with spaces: the rendered text is the same, but Pandoc fence lines
///     (such as a line that starts with three colons) are glued into prose.
/// </summary>
internal sealed class LineJoiningFormatter : ParagraphTestFormatter
{
    /// <inheritdoc />
    public override void Format(FormatContext context, BlockInfo block, EditSet edits)
    {
        var text = TextOf(context, block);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                edits.Add(new TextEdit(block.Start + i, 1, " ", "paragraph-wrap"));
            }
        }
    }
}

/// <summary>
///     Switches the words <c>alpha beta</c> between a space and a line break on every pass: the rendered text is
///     the same each time, but formatting the result changes it again.
/// </summary>
internal sealed class FlipFlopFormatter : ParagraphTestFormatter
{
    /// <inheritdoc />
    public override void Format(FormatContext context, BlockInfo block, EditSet edits)
    {
        var text = TextOf(context, block);
        var space = text.IndexOf("alpha beta", StringComparison.Ordinal);
        if (space >= 0)
        {
            edits.Add(new TextEdit(block.Start + space + "alpha".Length, 1, "\n", "paragraph-wrap"));
            return;
        }

        var newline = text.IndexOf("alpha\nbeta", StringComparison.Ordinal);
        if (newline >= 0)
        {
            edits.Add(new TextEdit(block.Start + newline + "alpha".Length, 1, " ", "paragraph-wrap"));
        }
    }
}

/// <summary>
///     Appends a space to every paragraph on every pass: the rendered text never changes, but the text keeps growing.
/// </summary>
internal sealed class GrowingFormatter : ParagraphTestFormatter
{
    /// <inheritdoc />
    public override void Format(FormatContext context, BlockInfo block, EditSet edits) =>
        edits.Add(new TextEdit(block.EndInclusive + 1, 0, " ", "paragraph-wrap"));
}

/// <summary>
///     Puts the text <c>+---</c> on a line of its own, which makes the line look like the border of a grid table.
/// </summary>
internal sealed class GridBorderCreatingFormatter : ParagraphTestFormatter
{
    /// <inheritdoc />
    public override void Format(FormatContext context, BlockInfo block, EditSet edits)
    {
        var text = TextOf(context, block);
        var index = text.IndexOf(" +--- ", StringComparison.Ordinal);
        if (index >= 0)
        {
            edits.Add(new TextEdit(block.Start + index, 1, "\n", "paragraph-wrap"));
            edits.Add(new TextEdit(block.Start + index + 5, 1, "\n", "paragraph-wrap"));
        }
    }
}

/// <summary>
///     Throws when asked to format a paragraph.
/// </summary>
internal sealed class ThrowingFormatter : ParagraphTestFormatter
{
    /// <inheritdoc />
    public override void Format(FormatContext context, BlockInfo block, EditSet edits) =>
        throw new InvalidOperationException("The formatter failed on purpose");
}

/// <summary>
///     Proposes two edits that change the same characters.
/// </summary>
internal sealed class OverlappingEditsFormatter : ParagraphTestFormatter
{
    /// <inheritdoc />
    public override void Format(FormatContext context, BlockInfo block, EditSet edits)
    {
        edits.Add(new TextEdit(block.Start, 2, "xx", "paragraph-wrap"));
        edits.Add(new TextEdit(block.Start + 1, 2, "yy", "paragraph-wrap"));
    }
}

/// <summary>
///     Proposes an edit that lies outside the text.
/// </summary>
internal sealed class OutOfRangeEditFormatter : ParagraphTestFormatter
{
    /// <inheritdoc />
    public override void Format(FormatContext context, BlockInfo block, EditSet edits) =>
        edits.Add(new TextEdit(context.Document.Source.Text.Length + 5, 1, "x", "paragraph-wrap"));
}

/// <summary>
///     Inserts a carriage return, which the tool must never write.
/// </summary>
internal sealed class CarriageReturnFormatter : ParagraphTestFormatter
{
    /// <inheritdoc />
    public override void Format(FormatContext context, BlockInfo block, EditSet edits) =>
        edits.Add(new TextEdit(block.Start, 0, "\r", "paragraph-wrap"));
}
