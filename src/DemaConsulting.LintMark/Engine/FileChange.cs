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

namespace DemaConsulting.LintMark.Engine;

/// <summary>
///     A rewrite of one file that passed every safety check and is ready to be written.
/// </summary>
/// <remarks>
///     <para>
///         A change exists only for a rewrite that is safe: when any safety check fails, the result carries no
///         change, so nothing downstream can write the file by mistake. The original bytes travel with the
///         change so the writer can refuse to replace a file that changed after it was read. Immutable,
///         including the byte arrays.
///     </para>
///     <para>
///         The byte arrays are immutable copies, so the equality of two changes compares the array structures
///         by reference; compare the members to compare content.
///     </para>
/// </remarks>
internal sealed record FileChange
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="FileChange"/> class.
    /// </summary>
    /// <param name="originalLf">The LF-normalized text as it is on disk. Must not be null.</param>
    /// <param name="newLf">The LF-normalized text after rewriting. Must not be null.</param>
    /// <param name="originalEndings">The counts of the line terminators in the file as it is on disk.</param>
    /// <param name="targetEnding">The line ending the file is written with. Must be a defined value.</param>
    /// <param name="originalBytes">The exact bytes the rewrite was computed from. Must not be a default array.</param>
    /// <param name="newBytes">The exact bytes to write (byte order mark and line endings restored). Must not be a default array.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="originalLf"/> or <paramref name="newLf"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a byte array is a default array.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="targetEnding"/> is not a defined value.</exception>
    public FileChange(
        string originalLf,
        string newLf,
        LineEndingInfo originalEndings,
        LineEnding targetEnding,
        ImmutableArray<byte> originalBytes,
        ImmutableArray<byte> newBytes)
    {
        // Validate the arguments so that an incomplete change can never be created
        ArgumentNullException.ThrowIfNull(originalLf);
        ArgumentNullException.ThrowIfNull(newLf);
        if (!Enum.IsDefined(targetEnding))
        {
            throw new ArgumentOutOfRangeException(nameof(targetEnding), targetEnding, "The line ending is not a defined value.");
        }

        if (originalBytes.IsDefault)
        {
            throw new ArgumentException("The original bytes must not be a default array.", nameof(originalBytes));
        }

        if (newBytes.IsDefault)
        {
            throw new ArgumentException("The new bytes must not be a default array.", nameof(newBytes));
        }

        OriginalLf = originalLf;
        NewLf = newLf;
        OriginalEndings = originalEndings;
        TargetEnding = targetEnding;
        OriginalBytes = originalBytes;
        NewBytes = newBytes;
    }

    /// <summary>
    ///     Gets the LF-normalized text as it is on disk.
    /// </summary>
    public string OriginalLf { get; }

    /// <summary>
    ///     Gets the LF-normalized text after rewriting.
    /// </summary>
    public string NewLf { get; }

    /// <summary>
    ///     Gets the counts of the line terminators in the file as it is on disk.
    /// </summary>
    public LineEndingInfo OriginalEndings { get; }

    /// <summary>
    ///     Gets the line ending the file is written with.
    /// </summary>
    public LineEnding TargetEnding { get; }

    /// <summary>
    ///     Gets the exact bytes the rewrite was computed from.
    /// </summary>
    public ImmutableArray<byte> OriginalBytes { get; }

    /// <summary>
    ///     Gets the exact bytes to write (byte order mark and line endings restored).
    /// </summary>
    public ImmutableArray<byte> NewBytes { get; }
}
