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

namespace DemaConsulting.LintMark.Formatting.Inline;

/// <summary>
///     The words (atoms) of a paragraph between two hard line breaks, and how the segment ends.
/// </summary>
/// <remarks>
///     Supporting type of <see cref="AtomTokenizer"/>. A hard line break is a fixed point of the paragraph: the words
///     before it and the words after it are never joined, so each segment is wrapped on its own. Immutable; safe for
///     concurrent use.
/// </remarks>
internal sealed record ParagraphSegment
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ParagraphSegment"/> class.
    /// </summary>
    /// <param name="atoms">The atoms of the segment in order, none empty. Must not be a default array or contain null.</param>
    /// <param name="end">How the segment ends. Must be a defined value.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="atoms"/> is a default array or holds an empty atom.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="atoms"/> holds a null element.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="end"/> is not a defined value.</exception>
    public ParagraphSegment(ImmutableArray<string> atoms, HardBreakKind end)
    {
        // Validate every argument so that an invalid segment can never be created
        if (atoms.IsDefault)
        {
            throw new ArgumentException("The atoms must not be a default array.", nameof(atoms));
        }

        if (atoms.Any(static atom => atom is null))
        {
            throw new ArgumentNullException(nameof(atoms), "The atoms must not contain null.");
        }

        if (atoms.Any(static atom => atom.Length == 0))
        {
            throw new ArgumentException("The atoms must not contain an empty atom.", nameof(atoms));
        }

        if (!Enum.IsDefined(end))
        {
            throw new ArgumentOutOfRangeException(nameof(end), end, "Not a defined HardBreakKind value.");
        }

        Atoms = atoms;
        End = end;
    }

    /// <summary>
    ///     Gets the atoms of the segment in order.
    /// </summary>
    public ImmutableArray<string> Atoms { get; }

    /// <summary>
    ///     Gets how the segment ends.
    /// </summary>
    public HardBreakKind End { get; }
}
