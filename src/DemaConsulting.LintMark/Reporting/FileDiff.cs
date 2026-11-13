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

namespace DemaConsulting.LintMark.Reporting;

/// <summary>
///     What the fix option would change in one file, for the diff option to display.
/// </summary>
/// <remarks>
///     Both texts are LF-normalized, so a change of line endings alone leaves them equal; the original counts
///     and the target let the reporter say so. Immutable value type; safe for concurrent use. A default
///     instance bypasses the validation of the constructor; the reporter rejects it.
/// </remarks>
internal readonly record struct FileDiff
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="FileDiff"/> struct.
    /// </summary>
    /// <param name="path">Full path of the file. Must not be null or empty.</param>
    /// <param name="beforeLf">The LF-normalized text as it is on disk. Must not be null.</param>
    /// <param name="afterLf">The LF-normalized text after rewriting. Must not be null.</param>
    /// <param name="originalEndings">The counts of the line terminators in the file as it is on disk.</param>
    /// <param name="targetEnding">The line ending the file would be written with. Must be a defined value.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/>, <paramref name="beforeLf"/> or <paramref name="afterLf"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="targetEnding"/> is not a defined value.</exception>
    public FileDiff(
        string path,
        string beforeLf,
        string afterLf,
        LineEndingInfo originalEndings,
        LineEnding targetEnding)
    {
        // Validate the arguments so that an incomplete difference can never be created
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(beforeLf);
        ArgumentNullException.ThrowIfNull(afterLf);
        if (!Enum.IsDefined(targetEnding))
        {
            throw new ArgumentOutOfRangeException(nameof(targetEnding), targetEnding, "The line ending is not a defined value.");
        }

        Path = path;
        BeforeLf = beforeLf;
        AfterLf = afterLf;
        OriginalEndings = originalEndings;
        TargetEnding = targetEnding;
    }

    /// <summary>
    ///     Gets the full path of the file.
    /// </summary>
    public string Path { get; }

    /// <summary>
    ///     Gets the LF-normalized text as it is on disk.
    /// </summary>
    public string BeforeLf { get; }

    /// <summary>
    ///     Gets the LF-normalized text after rewriting.
    /// </summary>
    public string AfterLf { get; }

    /// <summary>
    ///     Gets the counts of the line terminators in the file as it is on disk.
    /// </summary>
    public LineEndingInfo OriginalEndings { get; }

    /// <summary>
    ///     Gets the line ending the file would be written with.
    /// </summary>
    public LineEnding TargetEnding { get; }
}
