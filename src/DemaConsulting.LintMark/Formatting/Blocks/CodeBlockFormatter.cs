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
using DemaConsulting.LintMark.Configuration;
using DemaConsulting.LintMark.Document;
using DemaConsulting.LintMark.Linting;
using Markdig.Syntax;

namespace DemaConsulting.LintMark.Formatting.Blocks;

/// <summary>
///     Writes code blocks in the canonical form: fenced with the configured fence character, never indented.
/// </summary>
/// <remarks>
///     <para>
///         A fenced code block keeps its place, its indentation and its content; only the fence characters
///         change to the configured character (backticks or tildes) and trailing whitespace on the two fence
///         lines goes. The new fence is made longer when the content holds a run of the new character that would
///         close it. A block whose info string holds a backtick is never converted to backticks (the info string
///         would no longer be valid), and a braced fence (<c>```{=html}</c>, Pandoc structure) keeps its
///         character. A top-level indented code block becomes a fenced block with the
///         content shifted left by the four columns of indentation; a whitespace-only line at its end that is
///         wider than the indentation keeps what is left of it as the last line of the code. An indented block
///         inside a list or quote stays as it is: its indentation also carries the container.
///     </para>
///     <para>
///         The content of a code block is never changed: every edit lies on a fence or replaces the indentation
///         of an indented block. An unclosed fence is not accepted by this formatter (the parser's view of where it ends is
///         not trusted), so it stays exactly as written. A line that does not have the shape the parser promised
///         produces no edit at all.
///     </para>
///     <para>
///         Thread safety: stateless; one instance serves every worker thread.
///     </para>
/// </remarks>
internal sealed class CodeBlockFormatter : IBlockFormatter
{
    /// <summary>
    ///     The shortest fence that Markdown allows.
    /// </summary>
    private const int MinimumFenceLength = 3;

    /// <summary>
    ///     The number of columns of indentation that make a line indented code.
    /// </summary>
    private const int IndentColumns = 4;

    /// <summary>
    ///     Gets the identifiers of the rules this formatter enforces.
    /// </summary>
    public ImmutableArray<string> RuleIds { get; } =
    [
        RuleCatalog.CodeFenceRuleId,
        RuleCatalog.CodeBlockStyleRuleId,
        RuleCatalog.TrailingWhitespaceRuleId
    ];

    /// <summary>
    ///     Determines whether a block is a code block this formatter handles.
    /// </summary>
    /// <param name="block">The block to test. Must not be null.</param>
    /// <returns><see langword="true"/> for a closed fenced code block and for an indented code block; <see langword="false"/> for an unclosed fence.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="block"/> is null.</exception>
    public bool CanFormat(BlockInfo block)
    {
        ArgumentNullException.ThrowIfNull(block);
        return (block.Kind == BlockKind.FencedCode && block.Node is FencedCodeBlock)
            || (block.Kind == BlockKind.IndentedCode && block.Node is CodeBlock and not FencedCodeBlock);
    }

    /// <summary>
    ///     Proposes the edits that bring a code block into canonical form.
    /// </summary>
    /// <param name="context">The document and settings. Must not be null.</param>
    /// <param name="block">The code block. Must be accepted by <see cref="CanFormat"/>.</param>
    /// <param name="edits">Receives the edits. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="block"/> is not a closed fenced or an indented code block.</exception>
    /// <exception cref="InvalidOperationException">Thrown when an edit conflicts with another (a defect, not a property of the document).</exception>
    public void Format(FormatContext context, BlockInfo block, EditSet edits)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(block);
        ArgumentNullException.ThrowIfNull(edits);
        if (!CanFormat(block))
        {
            throw new ArgumentException($"The block must be a closed fenced or an indented code block, not {block.Kind}.", nameof(block));
        }

        var layout = new LineLayout(context.Document.Source);
        var target = context.Config.CodeFence == FenceMarker.Tilde ? '~' : '`';
        if (block.Node is FencedCodeBlock fenced)
        {
            FormatFenced(layout, block, fenced, target, edits);
        }
        else if (block.Parent is null)
        {
            FormatIndented(context.Document, layout, block, target, edits);
        }
    }

    /// <summary>
    ///     Formats a closed fenced code block.
    /// </summary>
    /// <param name="layout">The line arithmetic of the document.</param>
    /// <param name="block">The code block.</param>
    /// <param name="fenced">The parser's fenced code block.</param>
    /// <param name="target">The fence character of the configuration.</param>
    /// <param name="edits">Receives the edits.</param>
    private static void FormatFenced(LineLayout layout, BlockInfo block, FencedCodeBlock fenced, char target, EditSet edits)
    {
        var text = layout.Text;
        var openLine = layout.LineOf(block.Start);
        var closeLine = layout.LineOf(block.EndInclusive);
        if (closeLine <= openLine)
        {
            return;
        }

        // Locate both fences from the source; anything unexpected means the block is left as it is
        var openStart = block.Start;
        var openLength = RunLength(text, openStart, layout.LineEnd(openLine), fenced.FencedChar);
        var closeStart = layout.LineStart(closeLine);
        while (closeStart < layout.LineEnd(closeLine) && text[closeStart] is ' ' or '\t' or '>')
        {
            closeStart++;
        }

        var closeLength = RunLength(text, closeStart, layout.LineEnd(closeLine), fenced.FencedChar);
        if (openLength != fenced.OpeningFencedCharCount
            || closeLength != fenced.ClosingFencedCharCount
            || text[closeStart..layout.LineEnd(closeLine)].Skip(closeLength).Any(static c => !LineLayout.IsBlankChar(c)))
        {
            return;
        }

        // Convert the fence character when it differs from the configured one
        if (fenced.FencedChar != target && CanUseFence(text, openStart + openLength, layout.LineEnd(openLine), target))
        {
            var length = Math.Max(openLength, LongerThanContent(layout, openLine + 1, closeLine - 1, target));
            edits.Add(new TextEdit(openStart, openLength, new string(target, length), RuleCatalog.CodeFenceRuleId));
            edits.Add(new TextEdit(closeStart, closeLength, new string(target, Math.Max(closeLength, length)), RuleCatalog.CodeFenceRuleId));
        }

        // Trailing whitespace on the two fence lines (the content lines are code and stay as written)
        AddTrailing(layout, openStart + openLength, layout.LineEnd(openLine), edits);
        AddTrailing(layout, closeStart + closeLength, layout.LineEnd(closeLine), edits);
    }

    /// <summary>
    ///     Counts the run of a character at an offset.
    /// </summary>
    /// <param name="text">The document text.</param>
    /// <param name="start">Offset of the first character of the run.</param>
    /// <param name="end">Offset where the line ends.</param>
    /// <param name="c">The character.</param>
    /// <returns>The number of consecutive occurrences of <paramref name="c"/> from <paramref name="start"/>.</returns>
    private static int RunLength(string text, int start, int end, char c)
    {
        var length = 0;
        while (start + length < end && text[start + length] == c)
        {
            length++;
        }

        return length;
    }

    /// <summary>
    ///     Determines whether the new fence character may be used given the rest of the opening line.
    /// </summary>
    /// <param name="text">The document text.</param>
    /// <param name="infoStart">Offset after the opening fence.</param>
    /// <param name="lineEnd">Offset where the line ends.</param>
    /// <param name="target">The new fence character.</param>
    /// <returns>
    ///     <see langword="false"/> when the target is the backtick and the info string holds a backtick (invalid for a
    ///     backtick fence), and when the info string starts with a brace: a braced fence (<c>```{=html}</c>) is
    ///     Pandoc structure that is only recognized with backticks, so its fence character is never changed.
    /// </returns>
    private static bool CanUseFence(string text, int infoStart, int lineEnd, char target)
    {
        var info = text.AsSpan(infoStart, lineEnd - infoStart).TrimStart(" \t");
        return !info.StartsWith("{", StringComparison.Ordinal) && (target != '`' || !info.Contains('`'));
    }

    /// <summary>
    ///     Finds the shortest fence length that no content line can close.
    /// </summary>
    /// <param name="layout">The line arithmetic of the document.</param>
    /// <param name="firstLine">First content line.</param>
    /// <param name="lastLine">Last content line (may be before the first when there is no content).</param>
    /// <param name="target">The fence character.</param>
    /// <returns>One more than the longest run of the fence character at the start of a content line, at least the minimum fence length.</returns>
    private static int LongerThanContent(LineLayout layout, int firstLine, int lastLine, char target)
    {
        var longest = 0;
        for (var line = firstLine; line <= lastLine; line++)
        {
            var position = layout.LineStart(line);
            var end = layout.LineEnd(line);
            while (position < end && layout.Text[position] is ' ' or '\t' or '>')
            {
                position++;
            }

            longest = Math.Max(longest, RunLength(layout.Text, position, end, target));
        }

        return Math.Max(MinimumFenceLength, longest + 1);
    }

    /// <summary>
    ///     Adds the edit that removes trailing whitespace from a fence line.
    /// </summary>
    /// <param name="layout">The line arithmetic of the document.</param>
    /// <param name="from">Offset after the fence (or the info string) from which only whitespace may follow.</param>
    /// <param name="lineEnd">Offset where the line ends.</param>
    /// <param name="edits">Receives the edit.</param>
    private static void AddTrailing(LineLayout layout, int from, int lineEnd, EditSet edits)
    {
        var runStart = lineEnd;
        while (runStart > from && LineLayout.IsBlankChar(layout.Text[runStart - 1]))
        {
            runStart--;
        }

        if (runStart < lineEnd)
        {
            edits.Add(new TextEdit(runStart, lineEnd - runStart, string.Empty, RuleCatalog.TrailingWhitespaceRuleId));
        }
    }

    /// <summary>
    ///     Replaces a top-level indented code block by a fenced block.
    /// </summary>
    /// <param name="document">The parsed document.</param>
    /// <param name="layout">The line arithmetic of the document.</param>
    /// <param name="block">The code block.</param>
    /// <param name="target">The fence character.</param>
    /// <param name="edits">Receives the edit.</param>
    /// <remarks>
    ///     Each content line loses exactly four columns of leading whitespace (a tab counts up to the next tab
    ///     stop); a blank line with fewer columns becomes empty. The blank lines after the last line with content
    ///     are not part of the block, so they are not part of the replacement, unless the last one is wider than the
    ///     indentation: what is left of it is a line of the code and stays in the fence. The block is left as written when
    ///     an unindented line would be a dashes line that could close front matter opened by the first line.
    /// </remarks>
    private static void FormatIndented(ParsedDocument document, LineLayout layout, BlockInfo block, char target, EditSet edits)
    {
        var (first, last) = LineOwnership.LinesOf(layout, block);
        if (layout.LineStart(first) != block.Start)
        {
            return;
        }

        // Remove the indentation line by line; a line that is not indented enough is not what the parser promised
        var lines = new List<string>(last - first + 1);
        for (var line = first; line <= last; line++)
        {
            if (!TryDedent(layout.Text, layout.LineStart(line), layout.LineEnd(line), out var stripped))
            {
                return;
            }

            lines.Add(stripped);
        }

        // The fence must be longer than any run of its character at the start of a line
        var longest = lines.Max(line => RunLength(line, line.Length - line.TrimStart(' ').Length, line.Length, target));

        // An unindented dashes line in the new fenced block could close front matter that the first line opens
        if (lines.Any(static line => line.TrimEnd(' ', '\t') is "---" or "...") && FrontMatterGuard.CouldOpenFrontMatter(document))
        {
            return;
        }

        var fence = new string(target, Math.Max(MinimumFenceLength, longest + 1));
        var replacement = new StringBuilder(fence).Append('\n').AppendJoin('\n', lines).Append('\n').Append(fence).ToString();
        var start = layout.LineStart(first);
        edits.Add(new TextEdit(start, layout.LineEnd(last) - start, replacement, RuleCatalog.CodeBlockStyleRuleId));
    }

    /// <summary>
    ///     Removes four columns of leading whitespace from a line of an indented code block.
    /// </summary>
    /// <param name="text">The document text.</param>
    /// <param name="start">Offset of the start of the line.</param>
    /// <param name="end">Offset where the line ends.</param>
    /// <param name="stripped">Receives the line without the indentation.</param>
    /// <returns><see langword="false"/> when a line with content has less than four columns of indentation.</returns>
    private static bool TryDedent(string text, int start, int end, out string stripped)
    {
        var column = 0;
        var position = start;
        while (position < end && column < IndentColumns && LineLayout.IsBlankChar(text[position]))
        {
            column = text[position] == '\t' ? LineLayout.NextTabStop(column) : column + 1;
            position++;
        }

        // A blank line with less indentation is an empty line of the code; a line with content needs the full indentation
        var rest = text[position..end];
        if (column < IndentColumns)
        {
            stripped = string.Empty;
            return rest.Length == 0;
        }

        // The loop stops at the first column that is four or more; a tab stop is four columns wide, so a tab never
        // crosses the fourth column and what is left of the line is exactly its content
        stripped = rest;
        return true;
    }
}
