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

namespace DemaConsulting.LintMark.Formatting;

/// <summary>
///     Line arithmetic over the LF-normalized text of one document: where a line starts and ends, whether it
///     is blank, and how tab stops fall.
/// </summary>
/// <remarks>
///     <para>
///         Every whitespace rule works on whole lines of the original text, because the parser's spans of
///         headings, lists and trailing whitespace are not reliable enough to derive an edit from (see the
///         design of the formatting subsystem). Collecting the line arithmetic here keeps the rules short and
///         makes sure that all of them agree on what a line, a blank line and a tab stop are.
///     </para>
///     <para>
///         Lines are numbered from zero. A line ends at its line feed, which is not part of the line; a text
///         that ends with a line feed has no line after it (the empty line that <see cref="SourceDocument.LineStarts"/>
///         lists there is not counted). Blank means empty or only spaces and tabs, as in Markdown.
///     </para>
///     <para>
///         Thread safety: immutable apart from reads of the immutable source; safe for concurrent use.
///     </para>
/// </remarks>
internal sealed class LineLayout
{
    /// <summary>
    ///     The width of a tab stop in columns, the value that Markdown itself uses to expand tabs.
    /// </summary>
    public const int TabWidth = 4;

    /// <summary>
    ///     The source whose lines are described.
    /// </summary>
    private readonly SourceDocument _source;

    /// <summary>
    ///     Initializes a new instance of the <see cref="LineLayout"/> class.
    /// </summary>
    /// <param name="source">The document whose LF-normalized text is described. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
    public LineLayout(SourceDocument source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
        Text = source.Text;

        // The empty line that the line table lists after a final line feed is not a line of the text
        if (Text.Length == 0)
        {
            LineCount = 0;
        }
        else
        {
            LineCount = Text[^1] == '\n' ? source.LineStarts.Count - 1 : source.LineStarts.Count;
        }
    }

    /// <summary>
    ///     Gets the LF-normalized text.
    /// </summary>
    public string Text { get; }

    /// <summary>
    ///     Gets the number of lines; zero for an empty text.
    /// </summary>
    public int LineCount { get; }

    /// <summary>
    ///     Determines whether a character is a space or a tab.
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns><see langword="true"/> for a space or a tab.</returns>
    public static bool IsBlankChar(char c) => c is ' ' or '\t';

    /// <summary>
    ///     Gets the offset of the first character of a line.
    /// </summary>
    /// <param name="line">The zero-based line; may equal <see cref="LineCount"/> only when the text ends with a line feed (the start of the empty text after it).</param>
    /// <returns>The offset.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="line"/> is negative or beyond the lines of the text.</exception>
    public int LineStart(int line)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(line);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(line, _source.LineStarts.Count - 1);
        return _source.LineStarts[line];
    }

    /// <summary>
    ///     Gets the offset where a line ends: its line feed, or the end of the text for the last line.
    /// </summary>
    /// <param name="line">The zero-based line.</param>
    /// <returns>The offset of the line feed, or the text length.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="line"/> is negative or beyond the lines of the text.</exception>
    public int LineEnd(int line)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(line);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(line, _source.LineStarts.Count - 1);
        return line + 1 < _source.LineStarts.Count ? _source.LineStarts[line + 1] - 1 : Text.Length;
    }

    /// <summary>
    ///     Gets the line that holds an offset.
    /// </summary>
    /// <param name="offset">The offset; may equal the text length.</param>
    /// <returns>The zero-based line.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="offset"/> is outside the text.</exception>
    public int LineOf(int offset) => _source.PositionOf(offset).Line - 1;

    /// <summary>
    ///     Determines whether a line is blank (empty, or only spaces and tabs).
    /// </summary>
    /// <param name="line">The zero-based line.</param>
    /// <returns><see langword="true"/> when the line has no other character.</returns>
    public bool IsBlank(int line)
    {
        var end = LineEnd(line);
        for (var i = LineStart(line); i < end; i++)
        {
            if (!IsBlankChar(Text[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Finds the last line of a range that is not blank.
    /// </summary>
    /// <param name="firstLine">The first line of the range.</param>
    /// <param name="lastLine">The last line of the range.</param>
    /// <returns>The last line of the range that has content; <paramref name="firstLine"/> when every line is blank.</returns>
    /// <remarks>
    ///     The parser's spans of lists and code blocks may run over trailing lines that hold only whitespace.
    ///     Rules that must not depend on whether such a line is empty or holds spaces measure a block up to its
    ///     last line with content instead.
    /// </remarks>
    public int LastContentLine(int firstLine, int lastLine)
    {
        var line = lastLine;
        while (line > firstLine && IsBlank(line))
        {
            line--;
        }

        return line;
    }

    /// <summary>
    ///     Determines whether a line starts with a plus sign after any quote markers and indentation, as the border of a grid table does.
    /// </summary>
    /// <param name="line">The zero-based line.</param>
    /// <returns><see langword="true"/> when the first character that is not a quote marker or blank is a plus sign.</returns>
    /// <remarks>
    ///     Pandoc reads such a line as part of a grid table, and the structural safety check watches the lines that
    ///     have the exact shape of a border, so edits that could turn a line into one or move it are avoided.
    /// </remarks>
    public bool LooksLikeGridBorder(int line)
    {
        // Skip the block quote markers, list markers and blanks that can stand before the border
        var end = LineEnd(line);
        var position = LineStart(line);
        while (position < end)
        {
            var c = Text[position];
            if (c is ' ' or '\t' or '>')
            {
                position++;
            }
            else if (c is '-' or '*' && position + 1 < end && IsBlankChar(Text[position + 1]))
            {
                position++;
            }
            else if (char.IsAsciiDigit(c))
            {
                var digitsEnd = position;
                while (digitsEnd < end && char.IsAsciiDigit(Text[digitsEnd]))
                {
                    digitsEnd++;
                }

                if (digitsEnd + 1 < end && Text[digitsEnd] is '.' or ')' && IsBlankChar(Text[digitsEnd + 1]))
                {
                    position = digitsEnd + 1;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                break;
            }
        }

        return position < end && Text[position] == '+';
    }

    /// <summary>
    ///     Gets the column of the next tab stop after a column.
    /// </summary>
    /// <param name="column">The zero-based column.</param>
    /// <returns>The column that a tab at <paramref name="column"/> advances to.</returns>
    public static int NextTabStop(int column) => column + (TabWidth - (column % TabWidth));
}
