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

namespace DemaConsulting.LintMark.Formatting.Blocks;

/// <summary>
///     What the paragraph formatter found out about one paragraph: the edit that wraps it, where its first line that
///     is too wide is, and whether a line that could be broken stays too wide.
/// </summary>
/// <remarks>
///     <para>
///         Supporting type of <see cref="ParagraphFormatter"/>. The offsets and the edit refer to the text that the
///         formatter was given (the document after the other rules have been applied), not to the original file; the
///         document formatter translates them. A paragraph that has no line that is too wide and could be broken, and
///         a paragraph that is exempt from the rule, give the outcome <see cref="None"/>.
///     </para>
///     <para>
///         Immutable; safe for concurrent use.
///     </para>
/// </remarks>
internal sealed record ParagraphOutcome
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ParagraphOutcome"/> class.
    /// </summary>
    /// <param name="edit">The edit that wraps the paragraph, or <see langword="null"/> when none is proposed.</param>
    /// <param name="overflowOffset">
    ///     The offset of the first character beyond the line length on the first line that is too wide and could be
    ///     broken, or -1 when there is none.
    /// </param>
    /// <param name="overflowWidth">The display width of that line; 0 when <paramref name="overflowOffset"/> is -1.</param>
    /// <param name="hasResidualOverflow">Whether a line that could be broken is still too wide (after the edit, when there is one).</param>
    /// <param name="wrapOffset">
    ///     The offset of the first line that is not laid out the way the sentence or semantic wrap mode asks for, or -1 when
    ///     the paragraph is laid out that way or the mode does not apply. With an edit the finding is one that the edit fixes;
    ///     without one it is a finding that cannot be fixed automatically.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="overflowOffset"/> or <paramref name="wrapOffset"/> is less than -1 or <paramref name="overflowWidth"/> is negative.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when a residual overflow is given without an overflow offset, a width without an offset, or an edit without
    ///     any offset.
    /// </exception>
    public ParagraphOutcome(TextEdit? edit, int overflowOffset, int overflowWidth, bool hasResidualOverflow, int wrapOffset = -1)
    {
        // Validate every argument so that an inconsistent outcome can never be created
        ArgumentOutOfRangeException.ThrowIfLessThan(overflowOffset, -1);
        ArgumentOutOfRangeException.ThrowIfLessThan(wrapOffset, -1);
        ArgumentOutOfRangeException.ThrowIfNegative(overflowWidth);
        if (overflowOffset < 0 && (hasResidualOverflow || overflowWidth != 0))
        {
            throw new ArgumentException("A residual overflow and a width need the offset of the overflow.", nameof(overflowOffset));
        }

        if (edit is not null && overflowOffset < 0 && wrapOffset < 0)
        {
            throw new ArgumentException("An edit needs the offset of the overflow or of the wrap finding.", nameof(overflowOffset));
        }

        if (edit is { } proposed && (proposed.Replacement is null || proposed.RuleId is null))
        {
            throw new ArgumentException("The edit must be created with its constructor.", nameof(edit));
        }

        Edit = edit;
        OverflowOffset = overflowOffset;
        OverflowWidth = overflowWidth;
        HasResidualOverflow = hasResidualOverflow;
        WrapOffset = wrapOffset;
    }

    /// <summary>
    ///     Gets the outcome for a paragraph that needs no report and no edit.
    /// </summary>
    public static ParagraphOutcome None { get; } = new(null, -1, 0, false);

    /// <summary>
    ///     Gets a value indicating whether the outcome has a finding or an edit: the paragraph is too wide somewhere or not laid out as the mode asks.
    /// </summary>
    public bool HasFinding => OverflowOffset >= 0 || WrapOffset >= 0;

    /// <summary>
    ///     Gets the offset of the first line that is not laid out the way the sentence or semantic wrap mode asks for, or -1 when
    ///     there is none.
    /// </summary>
    public int WrapOffset { get; }

    /// <summary>
    ///     Gets the edit that wraps the paragraph, or <see langword="null"/> when none is proposed.
    /// </summary>
    public TextEdit? Edit { get; }

    /// <summary>
    ///     Gets the offset of the first character beyond the line length on the first line that is too wide and could
    ///     be broken, or -1 when there is none.
    /// </summary>
    public int OverflowOffset { get; }

    /// <summary>
    ///     Gets the display width of the first line that is too wide and could be broken; 0 when there is none.
    /// </summary>
    public int OverflowWidth { get; }

    /// <summary>
    ///     Gets a value indicating whether a line that could be broken is still too wide after the edit (or without
    ///     one, when the paragraph is not rewritten).
    /// </summary>
    public bool HasResidualOverflow { get; }
}
