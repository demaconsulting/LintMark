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

namespace DemaConsulting.LintMark.Configuration;

/// <summary>
///     The editor-settings outcome for one Markdown file: the line ending the editor settings specify, or why they could not be read.
/// </summary>
/// <remarks>
///     <para>
///         Supporting type of <see cref="EditorConfigResolver"/>. "Not specified" (both members
///         <see langword="null"/>) is an ordinary outcome, not an error: editor settings are shared with
///         other file types and often say nothing about line endings.
///     </para>
///     <para>
///         Thread safety: instances are immutable and may be shared between threads.
///     </para>
/// </remarks>
internal sealed record EditorConfigResult
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="EditorConfigResult"/> class.
    /// </summary>
    /// <param name="endOfLine">The line ending the editor settings specify, or <see langword="null"/>.</param>
    /// <param name="errorMessage">Why the settings could not be read, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentException">
    ///     Thrown when both values are set, when <paramref name="errorMessage"/> is blank, or when
    ///     <paramref name="endOfLine"/> is not a defined member.
    /// </exception>
    public EditorConfigResult(LineEnding? endOfLine, string? errorMessage)
    {
        // Validate input: a failed read cannot also carry a value
        if (endOfLine.HasValue && errorMessage is not null)
        {
            throw new ArgumentException("A line ending and an error message are mutually exclusive.", nameof(errorMessage));
        }

        if (errorMessage is not null && string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new ArgumentException("The error message must not be blank.", nameof(errorMessage));
        }

        if (endOfLine.HasValue && !Enum.IsDefined(endOfLine.Value))
        {
            throw new ArgumentException("The line ending is not a defined value.", nameof(endOfLine));
        }

        EndOfLine = endOfLine;
        ErrorMessage = errorMessage?.ReplaceLineEndings(" ");
    }

    /// <summary>
    ///     Gets the outcome in which the editor settings say nothing about the line ending.
    /// </summary>
    public static EditorConfigResult NotSpecified { get; } = new(null, null);

    /// <summary>
    ///     Gets the line ending the editor settings specify, or <see langword="null"/> when they do not.
    /// </summary>
    public LineEnding? EndOfLine { get; }

    /// <summary>
    ///     Gets why the editor settings could not be read, or <see langword="null"/> when they could.
    /// </summary>
    public string? ErrorMessage { get; }
}
