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

using System.Text.RegularExpressions;
using Markdig.Syntax.Inlines;

namespace DemaConsulting.LintMark.Formatting.Links;

/// <summary>
///     The destination part of an inline link as it is written: <c>(destination "title")</c>, with the positions of
///     its first and last character.
/// </summary>
/// <param name="Start">Offset of the opening parenthesis.</param>
/// <param name="End">Offset just after the closing parenthesis.</param>
/// <param name="Destination">The destination as written (with its angle brackets when it has them).</param>
/// <param name="Title">The title as written (with its quotes or parentheses), or <see langword="null"/> when there is none.</param>
/// <param name="Url">The destination as the parser reports it.</param>
/// <param name="TitleText">The title as the parser reports it, or <see langword="null"/> when there is none.</param>
/// <remarks>
///     Immutable; safe for concurrent use.
/// </remarks>
internal sealed partial record InlineLinkTail(int Start, int End, string Destination, string? Title, string Url, string? TitleText)
{
    /// <summary>
    ///     Reads the destination part of an inline link, or finds out that the tool is not certain how it is written.
    /// </summary>
    /// <param name="text">The LF-normalized text of the document. Must not be null.</param>
    /// <param name="link">An inline link or image of the document. Must not be null.</param>
    /// <returns>
    ///     The destination part; <see langword="null"/> when the link is not an inline link, when its destination part is not
    ///     on one line, when it uses a backslash or an entity (the tool copies text it can compare with the parser's reading,
    ///     and nothing else), or when the parser's reading differs from the text.
    /// </returns>
    /// <remarks>
    ///     Only the text from the opening parenthesis to the closing one is read. The text of the link, with all its brackets,
    ///     emphasis and code, is never looked at, so it cannot confuse the reading.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public static InlineLinkTail? TryRead(string text, LinkInline link)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(link);

        // Only an inline link has a destination part of its own
        if (link.IsAutoLink || link.IsShortcut || link.Reference is not null || string.IsNullOrEmpty(link.Url) || link.UrlSpan.Start <= 0)
        {
            return null;
        }

        // The parser says where the destination is; the opening parenthesis (after "]") is found by stepping back over blanks and "<"
        var end = link.Span.End;
        if (end <= 0 || end >= text.Length || text[end] != ')' || link.UrlSpan.Start >= text.Length)
        {
            return null;
        }

        var open = SkipBlanksBackwards(text, link.UrlSpan.Start - 1);
        if (open >= 0 && text[open] == '<')
        {
            open = SkipBlanksBackwards(text, open - 1);
        }

        if (open < 1 || text[open] != '(' || text[open - 1] != ']')
        {
            return null;
        }

        var tail = text.AsSpan(open, end + 1 - open);
        return tail.Contains('\n') ? null : Read(tail.ToString(), open, link);
    }

    /// <summary>
    ///     Reads <c>(destination "title")</c> and compares it with what the parser says.
    /// </summary>
    /// <param name="tail">The text from the opening parenthesis to the closing parenthesis.</param>
    /// <param name="open">The offset of the opening parenthesis.</param>
    /// <param name="link">The link the parser read.</param>
    /// <returns>The destination part, or <see langword="null"/> when it is not certain.</returns>
    private static InlineLinkTail? Read(string tail, int open, LinkInline link)
    {
        var last = tail.Length - 1;
        var i = SkipBlanks(tail, 1);
        if (i >= last)
        {
            return null;
        }

        // The destination: in angle brackets, or a run without blanks whose parentheses balance
        int destinationEnd;
        string url;
        if (tail[i] == '<')
        {
            destinationEnd = tail.IndexOf('>', i + 1);
            if (destinationEnd < 0 || tail.AsSpan(i + 1, destinationEnd - i - 1).IndexOfAny('<', '\\') >= 0)
            {
                return null;
            }

            url = tail.Substring(i + 1, destinationEnd - i - 1);
            destinationEnd++;
        }
        else
        {
            destinationEnd = i;
            var depth = 0;
            while (destinationEnd < last && tail[destinationEnd] is not (' ' or '\t') && !char.IsControl(tail[destinationEnd]))
            {
                if (tail[destinationEnd] == '(')
                {
                    depth++;
                }
                else if (tail[destinationEnd] == ')')
                {
                    depth--;
                }

                destinationEnd++;
            }

            if (depth != 0 || destinationEnd == i || tail.AsSpan(i, destinationEnd - i).Contains('\\'))
            {
                return null;
            }

            url = tail[i..destinationEnd];
        }

        var destination = tail[i..destinationEnd];
        i = SkipBlanks(tail, destinationEnd);
        string? title = null;
        string? titleText = null;
        if (i < last)
        {
            // A title needs a blank before it, and is closed by the same delimiter
            if (i == destinationEnd || !TryReadTitle(tail, i, out var titleEnd))
            {
                return null;
            }

            title = tail[i..titleEnd];
            titleText = title[1..^1];
            i = SkipBlanks(tail, titleEnd);
        }

        // Nothing but the closing parenthesis may be left, and the parser must agree with what was read
        if (i != last || EntityRegex().IsMatch(url) || (titleText is not null && EntityRegex().IsMatch(titleText)))
        {
            return null;
        }

        if (!string.Equals(link.Url, url, StringComparison.Ordinal)
            || !string.Equals(link.Title ?? string.Empty, titleText ?? string.Empty, StringComparison.Ordinal))
        {
            return null;
        }

        return new InlineLinkTail(open, open + tail.Length, destination, title, url, titleText);
    }

    /// <summary>
    ///     Reads a title that starts at an index, in double quotes, single quotes or parentheses.
    /// </summary>
    /// <param name="tail">The text of the destination part, ending with its closing parenthesis.</param>
    /// <param name="start">The index of the opening delimiter.</param>
    /// <param name="end">Receives the index just after the closing delimiter.</param>
    /// <returns><see langword="true"/> when a title that holds no backslash and no nested parenthesis was read.</returns>
    private static bool TryReadTitle(string tail, int start, out int end)
    {
        end = -1;
        var closing = tail[start] switch
        {
            '"' => '"',
            '\'' => '\'',
            '(' => ')',
            _ => '\0'
        };
        if (closing == '\0')
        {
            return false;
        }

        // The last character is the parenthesis that closes the destination part, never the end of a title
        var close = tail.IndexOf(closing, start + 1, tail.Length - 1 - (start + 1));
        if (close < 0)
        {
            return false;
        }

        var inner = tail.AsSpan(start + 1, close - start - 1);
        if (inner.Contains('\\') || (closing == ')' && inner.Contains('(')))
        {
            return false;
        }

        end = close + 1;
        return true;
    }

    /// <summary>
    ///     Finds the first character at or after an index that is not a space or a tab.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The index to start at.</param>
    /// <returns>The index of the character; the length of the text when there is none.</returns>
    private static int SkipBlanks(string text, int index)
    {
        while (index < text.Length && text[index] is ' ' or '\t')
        {
            index++;
        }

        return index;
    }

    /// <summary>
    ///     Finds the first character at or before an index that is not a space or a tab.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The index to start at.</param>
    /// <returns>The index of the character; -1 when there is none.</returns>
    private static int SkipBlanksBackwards(string text, int index)
    {
        while (index >= 0 && text[index] is ' ' or '\t')
        {
            index--;
        }

        return index;
    }

    /// <summary>
    ///     Matches text that a parser may read as a character reference.
    /// </summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex(@"&[#A-Za-z0-9]+;", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex EntityRegex();
}
