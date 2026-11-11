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

using System.Text;

namespace DemaConsulting.LintMark.Document;

/// <summary>
///     An immutable, LF-normalized view of one Markdown file together with the facts needed to
///     write it back exactly as it was found (encoding, byte order mark and line terminators).
/// </summary>
/// <remarks>
///     <para>
///         The Markdown parser renders multi-line code spans differently for LF and CRLF input
///         (spike quirk Q-4), so all parsing and offset arithmetic is done on LF-normalized
///         <see cref="Text"/> and the original terminators are only recorded (as counts) so they can be
///         restored on write. Mixed files are supported; a bare carriage return is rejected rather than
///         silently rewritten.
///     </para>
///     <para>
///         Positions are 1-based. A column counts UTF-16 code units and counts a tab as a single
///         unit; display-width concerns (East-Asian width, tab stops) are deliberately out of scope.
///     </para>
///     <para>
///         Thread safety: instances are fully immutable after construction and may be shared freely
///         between threads. Every file is nevertheless processed by exactly one worker.
///     </para>
/// </remarks>
internal sealed class SourceDocument
{
    /// <summary>
    ///     Strict UTF-8 decoder used for reading; invalid byte sequences throw instead of being replaced.
    /// </summary>
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    ///     UTF-8 encoder that writes a byte order mark, used when the source file had one.
    /// </summary>
    private static readonly UTF8Encoding Utf8WithBom = new(encoderShouldEmitUTF8Identifier: true);

    /// <summary>
    ///     UTF-8 encoder that writes no byte order mark, used when the source file had none.
    /// </summary>
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    ///     Initializes a new instance of the <see cref="SourceDocument"/> class from already-validated parts.
    /// </summary>
    /// <param name="path">Path the document was read from.</param>
    /// <param name="text">LF-normalized text.</param>
    /// <param name="lineEndings">Original terminator counts.</param>
    /// <param name="hasBom">Whether the original bytes started with a UTF-8 byte order mark.</param>
    private SourceDocument(string path, string text, LineEndingInfo lineEndings, bool hasBom)
    {
        Path = path;
        Text = text;
        LineEndings = lineEndings;
        HasBom = hasBom;
        HasFinalNewline = text.Length > 0 && text[^1] == '\n';

        // Record the offset of the first character of every line; a final newline therefore
        // yields a trailing empty line starting at Text.Length, matching editor conventions.
        var starts = new List<int>(capacity: lineEndings.LfCount + lineEndings.CrLfCount + 1) { 0 };
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                starts.Add(i + 1);
            }
        }

        LineStarts = starts;
    }

    /// <summary>
    ///     Gets the path the document was read from (used verbatim for reporting).
    /// </summary>
    public string Path { get; }

    /// <summary>
    ///     Gets the document text with every line terminator normalized to <c>\n</c>.
    /// </summary>
    public string Text { get; }

    /// <summary>
    ///     Gets the counts of the original line terminators.
    /// </summary>
    public LineEndingInfo LineEndings { get; }

    /// <summary>
    ///     Gets a value indicating whether the text ends with a line terminator.
    /// </summary>
    public bool HasFinalNewline { get; }

    /// <summary>
    ///     Gets a value indicating whether the original bytes started with a UTF-8 byte order mark.
    /// </summary>
    public bool HasBom { get; }

    /// <summary>
    ///     Gets the encoding to use when writing the document back (UTF-8, with a byte order mark only if the source had one).
    /// </summary>
    public Encoding Encoding => HasBom ? Utf8WithBom : Utf8WithoutBom;

    /// <summary>
    ///     Gets the offset (into <see cref="Text"/>) of the first character of every line.
    /// </summary>
    public IReadOnlyList<int> LineStarts { get; }

    /// <summary>
    ///     Gets the line terminator that occurs most often in the original file; ties and files
    ///     without terminators resolve to <see cref="LineEnding.Lf"/>.
    /// </summary>
    public LineEnding MajorityLineEnding => LineEndings.CrLfCount > LineEndings.LfCount ? LineEnding.CrLf : LineEnding.Lf;

    /// <summary>
    ///     Creates a document from the raw bytes of a file.
    /// </summary>
    /// <param name="path">Path of the file, kept for reporting. Must not be null.</param>
    /// <param name="bytes">Complete file content.</param>
    /// <returns>The LF-normalized document.</returns>
    /// <remarks>
    ///     Strict decoding is used so that damaged files are reported instead of being silently
    ///     "repaired" by a later rewrite. Pure with respect to its inputs; thread-safe.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    /// <exception cref="SourceDocumentException">
    ///     Thrown (rule <see cref="SourceDocumentException.EncodingRuleId"/>) for a UTF-16/UTF-32 byte order mark
    ///     or invalid UTF-8, and (rule <see cref="SourceDocumentException.LineEndingRuleId"/>) for a bare carriage return.
    /// </exception>
    public static SourceDocument Create(string path, ReadOnlySpan<byte> bytes)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(path);

        // Detect and strip the byte order mark; only the UTF-8 mark is supported
        var content = StripBom(bytes, out var hasBom);

        // Decode strictly, translating a decoder failure into a positioned failure
        string decoded;
        try
        {
            decoded = StrictUtf8.GetString(content);
        }
        catch (DecoderFallbackException ex)
        {
            var (line, column) = LocateByte(content, ex.Index);
            throw new SourceDocumentException(
                SourceDocumentException.EncodingRuleId, line, column, "Invalid UTF-8 byte sequence");
        }

        return Build(path, decoded, hasBom);
    }

    /// <summary>
    ///     Creates a document from text that has already been decoded.
    /// </summary>
    /// <param name="path">Path of the document, kept for reporting. Must not be null.</param>
    /// <param name="text">Document text; <c>\r\n</c> terminators are normalized to <c>\n</c>.</param>
    /// <returns>The LF-normalized document (without a byte order mark).</returns>
    /// <remarks>
    ///     Test and tooling seam: it applies exactly the same normalization and validation as
    ///     <see cref="Create"/> without requiring bytes. Pure; thread-safe.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> or <paramref name="text"/> is null.</exception>
    /// <exception cref="SourceDocumentException">Thrown when the text contains a bare carriage return.</exception>
    public static SourceDocument FromText(string path, string text)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(text);

        return Build(path, text, hasBom: false);
    }

    /// <summary>
    ///     Converts an offset into <see cref="Text"/> to a 1-based line and column.
    /// </summary>
    /// <param name="offset">Offset in [0, <c>Text.Length</c>]; <c>Text.Length</c> addresses the end of the document.</param>
    /// <returns>The 1-based line and the 1-based column (UTF-16 code units, tab counted as one).</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="offset"/> is outside the document.</exception>
    public (int Line, int Column) PositionOf(int offset)
    {
        // Validate input
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset, Text.Length);

        var index = LineIndexOf(offset);
        return (index + 1, offset - LineStarts[index] + 1);
    }

    /// <summary>
    ///     Converts a 1-based line and column back to an offset into <see cref="Text"/>.
    /// </summary>
    /// <param name="line">1-based line number.</param>
    /// <param name="column">1-based column; may address the line terminator (one past the last visible character).</param>
    /// <returns>The offset of the addressed character.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the line or column does not exist in the document.</exception>
    public int OffsetOf(int line, int column)
    {
        // Validate the line and column against the document
        ArgumentOutOfRangeException.ThrowIfLessThan(line, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(line, LineStarts.Count);
        ArgumentOutOfRangeException.ThrowIfLessThan(column, 1);

        // The last addressable offset of a line is its terminator (or the end of the document)
        var offset = LineStarts[line - 1] + column - 1;
        var lastOffset = line < LineStarts.Count ? LineStarts[line] - 1 : Text.Length;
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset, lastOffset);
        return offset;
    }

    /// <summary>
    ///     Gets a slice of the normalized text.
    /// </summary>
    /// <param name="start">Offset of the first character.</param>
    /// <param name="length">Number of characters.</param>
    /// <returns>A read-only view over <see cref="Text"/>; no allocation is performed.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the slice is not entirely inside the text.</exception>
    public ReadOnlySpan<char> Slice(int start, int length) => Text.AsSpan(start, length);

    /// <summary>
    ///     Converts LF-normalized text to the requested line terminator style.
    /// </summary>
    /// <param name="normalizedText">Text whose terminators are all <c>\n</c>. Must not be null.</param>
    /// <param name="target">Terminator style to produce.</param>
    /// <returns>The text with every <c>\n</c> replaced as requested.</returns>
    /// <remarks>
    ///     Static because the conversion needs no document state; it is the inverse of the
    ///     normalization performed on read. Pure; thread-safe.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="normalizedText"/> is null.</exception>
    public static string ToDisk(string normalizedText, LineEnding target)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(normalizedText);

        return target == LineEnding.CrLf ? normalizedText.Replace("\n", "\r\n", StringComparison.Ordinal) : normalizedText;
    }

    /// <summary>
    ///     Encodes LF-normalized text into the bytes to write to disk, restoring the byte order mark and terminator style.
    /// </summary>
    /// <param name="normalizedText">Text whose terminators are all <c>\n</c>. Must not be null.</param>
    /// <param name="target">Terminator style to produce.</param>
    /// <returns>The encoded bytes, starting with the UTF-8 byte order mark when the source file had one.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="normalizedText"/> is null.</exception>
    public byte[] ToBytes(string normalizedText, LineEnding target)
    {
        // Convert the terminators first, then prepend the preamble when the file had a BOM
        var body = Encoding.GetBytes(ToDisk(normalizedText, target));
        var preamble = Encoding.GetPreamble();
        if (preamble.Length == 0)
        {
            return body;
        }

        var result = new byte[preamble.Length + body.Length];
        preamble.CopyTo(result, 0);
        body.CopyTo(result, preamble.Length);
        return result;
    }

    /// <summary>
    ///     Validates terminators, normalizes them and constructs the document.
    /// </summary>
    /// <param name="path">Path of the document.</param>
    /// <param name="text">Decoded text, possibly containing CRLF terminators.</param>
    /// <param name="hasBom">Whether the source had a UTF-8 byte order mark.</param>
    /// <returns>The normalized document.</returns>
    /// <exception cref="SourceDocumentException">Thrown when the text contains a bare carriage return.</exception>
    private static SourceDocument Build(string path, string text, bool hasBom)
    {
        // Count terminators; a carriage return that is not part of CRLF cannot be represented
        var lfCount = 0;
        var crLfCount = 0;
        var line = 1;
        var lineStart = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\n')
            {
                // A line feed preceded by a carriage return is one CRLF terminator, otherwise a bare LF
                if (i > lineStart && text[i - 1] == '\r')
                {
                    crLfCount++;
                }
                else
                {
                    lfCount++;
                }

                line++;
                lineStart = i + 1;
            }
            else if (c == '\r' && (i + 1 >= text.Length || text[i + 1] != '\n'))
            {
                throw new SourceDocumentException(
                    SourceDocumentException.LineEndingRuleId,
                    line,
                    i - lineStart + 1,
                    "Bare carriage return (only LF and CRLF line endings are supported)");
            }
        }

        // Normalize CRLF to LF only when there is something to normalize
        var normalized = crLfCount > 0 ? text.Replace("\r\n", "\n", StringComparison.Ordinal) : text;
        return new SourceDocument(path, normalized, new LineEndingInfo(lfCount, crLfCount), hasBom);
    }

    /// <summary>
    ///     Removes a UTF-8 byte order mark and rejects the other Unicode byte order marks.
    /// </summary>
    /// <param name="bytes">Complete file content.</param>
    /// <param name="hasBom">Receives whether a UTF-8 mark was present.</param>
    /// <returns>The content without the UTF-8 mark.</returns>
    /// <exception cref="SourceDocumentException">Thrown for a UTF-16 or UTF-32 byte order mark.</exception>
    private static ReadOnlySpan<byte> StripBom(ReadOnlySpan<byte> bytes, out bool hasBom)
    {
        hasBom = false;

        // UTF-8 mark: EF BB BF
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            hasBom = true;
            return bytes[3..];
        }

        // UTF-16 LE/BE (FF FE / FE FF; also covers UTF-32 LE) and UTF-32 BE (00 00 FE FF)
        var isUtf16 = bytes.Length >= 2 && ((bytes[0] == 0xFF && bytes[1] == 0xFE) || (bytes[0] == 0xFE && bytes[1] == 0xFF));
        var isUtf32Be = bytes.Length >= 4 && bytes[0] == 0 && bytes[1] == 0 && bytes[2] == 0xFE && bytes[3] == 0xFF;
        if (isUtf16 || isUtf32Be)
        {
            throw new SourceDocumentException(
                SourceDocumentException.EncodingRuleId, 1, 1, "Unsupported byte order mark (only UTF-8 is supported)");
        }

        return bytes;
    }

    /// <summary>
    ///     Finds the line and column of a byte offset in UTF-8 content.
    /// </summary>
    /// <param name="content">Decoded content (without byte order mark).</param>
    /// <param name="index">Byte index reported by the decoder; out-of-range values fall back to 1:1.</param>
    /// <returns>The 1-based line and column of the byte.</returns>
    private static (int Line, int Column) LocateByte(ReadOnlySpan<byte> content, int index)
    {
        if (index < 0 || index > content.Length)
        {
            return (1, 1);
        }

        // Count line feeds before the bad byte, then measure the partial line in UTF-16 units
        var before = content[..index];
        var line = 1;
        var lineStartByte = 0;
        for (var i = 0; i < before.Length; i++)
        {
            if (before[i] == (byte)'\n')
            {
                line++;
                lineStartByte = i + 1;
            }
        }

        return (line, Encoding.UTF8.GetCharCount(before[lineStartByte..]) + 1);
    }

    /// <summary>
    ///     Finds the zero-based index of the line containing an offset.
    /// </summary>
    /// <param name="offset">A valid offset into the text.</param>
    /// <returns>Index into <see cref="LineStarts"/> of the last line start that is not greater than the offset.</returns>
    private int LineIndexOf(int offset)
    {
        // Binary search over the sorted line starts; an inexact match yields the insertion point
        var low = 0;
        var high = LineStarts.Count - 1;
        while (low < high)
        {
            var mid = (low + high + 1) >>> 1;
            if (LineStarts[mid] <= offset)
            {
                low = mid;
            }
            else
            {
                high = mid - 1;
            }
        }

        return low;
    }
}
