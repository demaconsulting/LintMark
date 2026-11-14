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

namespace DemaConsulting.LintMark.Formatting.Links;

/// <summary>
///     What the link refactoring decided for a document: the edits, where each link was converted and whether a table changed.
/// </summary>
/// <param name="Edits">The edits against the text the plan was made for, ordered and without conflicts.</param>
/// <param name="Conversions">One entry for each link that is converted, in document order.</param>
/// <param name="ChangesTables">Whether a link in a table cell is converted (the table must be laid out again).</param>
/// <remarks>
///     Immutable; safe for concurrent use.
/// </remarks>
internal sealed record LinkRefactorPlan(ImmutableArray<TextEdit> Edits, ImmutableArray<LinkConversion> Conversions, bool ChangesTables)
{
    /// <summary>
    ///     Gets the plan that changes nothing.
    /// </summary>
    public static LinkRefactorPlan Empty { get; } = new([], [], false);

    /// <summary>
    ///     Gets a value indicating whether the plan changes anything.
    /// </summary>
    public bool IsEmpty => Edits.IsDefaultOrEmpty;
}

/// <summary>
///     One link that is converted to a reference link.
/// </summary>
/// <param name="Offset">Offset of the link in the text the plan was made for.</param>
/// <param name="Message">What is reported about the link.</param>
/// <remarks>
///     Immutable; safe for concurrent use.
/// </remarks>
internal sealed record LinkConversion(int Offset, string Message);
