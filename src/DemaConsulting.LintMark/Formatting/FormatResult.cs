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
using DemaConsulting.LintMark.Linting;

namespace DemaConsulting.LintMark.Formatting;

/// <summary>
///     The outcome of formatting one document: the text edits, the line ending to write, and the formatting
///     violations that rewriting would remove.
/// </summary>
/// <remarks>
///     Immutable, including its collections.
/// </remarks>
internal sealed record FormatResult
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="FormatResult"/> class.
    /// </summary>
    /// <param name="edits">The edits to the LF-normalized text, ordered and conflict-free; empty when nothing changes. Must not be a default array or contain a default edit.</param>
    /// <param name="lineEnding">The line ending the file is written with. Must be a defined value.</param>
    /// <param name="diagnostics">The formatting violations found (the findings that applying the result removes). Must not be a default array or contain null.</param>
    /// <exception cref="ArgumentException">Thrown when an array is a default array or <paramref name="edits"/> contains a default edit.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="diagnostics"/> contains a null element.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="lineEnding"/> is not a defined value.</exception>
    public FormatResult(
        ImmutableArray<TextEdit> edits,
        LineEnding lineEnding,
        ImmutableArray<Diagnostic> diagnostics)
    {
        // Validate the arguments so that an incomplete result can never be created
        if (edits.IsDefault)
        {
            throw new ArgumentException("The edits must not be a default array.", nameof(edits));
        }

        if (edits.Any(static edit => edit.RuleId is null))
        {
            throw new ArgumentException("The edits must not contain a default edit.", nameof(edits));
        }

        if (!Enum.IsDefined(lineEnding))
        {
            throw new ArgumentOutOfRangeException(nameof(lineEnding), lineEnding, "The line ending is not a defined value.");
        }

        if (diagnostics.IsDefault)
        {
            throw new ArgumentException("The diagnostics must not be a default array.", nameof(diagnostics));
        }

        if (diagnostics.Any(static diagnostic => diagnostic is null))
        {
            throw new ArgumentNullException(nameof(diagnostics), "The diagnostics must not contain null.");
        }

        Edits = edits;
        LineEnding = lineEnding;
        Diagnostics = diagnostics;
    }

    /// <summary>
    ///     Gets the edits to the LF-normalized text.
    /// </summary>
    public ImmutableArray<TextEdit> Edits { get; }

    /// <summary>
    ///     Gets the line ending the file is written with.
    /// </summary>
    public LineEnding LineEnding { get; }

    /// <summary>
    ///     Gets the formatting violations found.
    /// </summary>
    public ImmutableArray<Diagnostic> Diagnostics { get; }
}
