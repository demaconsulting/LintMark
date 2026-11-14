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
///     The lines that a wrap produced for a run of atoms, and whether a line is still wider than allowed although it could be broken.
/// </summary>
/// <remarks>
///     Supporting type of <see cref="LineWrapper"/>. Immutable, including its collection; safe for concurrent use.
/// </remarks>
internal sealed record WrappedLines
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="WrappedLines"/> class.
    /// </summary>
    /// <param name="lines">The lines in order, none empty. Must not be a default array, empty or contain null.</param>
    /// <param name="hasBreakableOverflow">
    ///     Whether a line holds more than one atom and is still wider than the width available to it, because the
    ///     block-syntax guard allowed no better break.
    /// </param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="lines"/> is a default array, is empty or holds an empty line.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="lines"/> holds a null element.</exception>
    public WrappedLines(ImmutableArray<string> lines, bool hasBreakableOverflow)
    {
        // Validate every argument so that an invalid result can never be created
        if (lines.IsDefault || lines.IsEmpty)
        {
            throw new ArgumentException("The lines must not be a default or empty array.", nameof(lines));
        }

        if (lines.Any(static line => line is null))
        {
            throw new ArgumentNullException(nameof(lines), "The lines must not contain null.");
        }

        if (lines.Any(static line => line.Length == 0))
        {
            throw new ArgumentException("The lines must not contain an empty line.", nameof(lines));
        }

        Lines = lines;
        HasBreakableOverflow = hasBreakableOverflow;
    }

    /// <summary>
    ///     Gets the lines in order, without container prefix or trailing whitespace.
    /// </summary>
    public ImmutableArray<string> Lines { get; }

    /// <summary>
    ///     Gets a value indicating whether a line that holds more than one atom is wider than the width available to it.
    /// </summary>
    public bool HasBreakableOverflow { get; }
}
