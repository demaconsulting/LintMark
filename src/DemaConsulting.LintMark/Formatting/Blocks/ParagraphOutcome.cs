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
/// <param name="Edit">The edit that wraps the paragraph, or <see langword="null"/> when none is proposed.</param>
/// <param name="OverflowOffset">
///     The offset of the first character beyond the line length on the first line that is too wide and could be
///     broken, or -1 when there is none.
/// </param>
/// <param name="OverflowWidth">The display width of that line; 0 when <paramref name="OverflowOffset"/> is -1.</param>
/// <param name="HasResidualOverflow">Whether a line that could be broken is still too wide (after the edit, when there is one).</param>
/// <param name="WrapOffset">
///     The offset of the first line that is not laid out the way the sentence or semantic wrap mode asks for, or -1 when
///     the paragraph is laid out that way or the mode does not apply. With an edit the finding is one that the edit fixes;
///     without one it is a finding that cannot be fixed automatically.
/// </param>
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
internal sealed record ParagraphOutcome(TextEdit? Edit, int OverflowOffset, int OverflowWidth, bool HasResidualOverflow, int WrapOffset = -1)
{
    /// <summary>
    ///     Gets the outcome for a paragraph that needs no report and no edit.
    /// </summary>
    public static ParagraphOutcome None { get; } = new(null, -1, 0, false);

    /// <summary>
    ///     Gets a value indicating whether the outcome has a finding or an edit: the paragraph is too wide somewhere or not laid out as the mode asks.
    /// </summary>
    public bool HasFinding => OverflowOffset >= 0 || WrapOffset >= 0;
}
