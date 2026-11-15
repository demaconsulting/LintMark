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
using System.Text;

namespace DemaConsulting.LintMark.Document;

/// <summary>
///     An immutable, LF-normalized view of one Markdown file together with the facts needed to
///     write it back as it was found (encoding, byte order mark and line terminators; a file whose terminators
///     are mixed is written back in its majority style, see the remarks).
/// </summary>
/// <remarks>
///     <para>
///         The Markdown parser reads multi-line code spans differently for LF and CRLF input
///         (the CRLF code-span difference), so all parsing and offset arithmetic is done on LF-normalized
///         <see cref="Text"/> and the original terminators are only recorded (as counts) so they can be
///         restored on write. A file whose terminators are all of one style is written back byte for byte.
///         A mixed file is deliberately not: the counts do not say which line had which terminator, and
///         mixed endings are a violation that a formatter fixes by writing the majority style (see
///         <see cref="MajorityLineEnding"/>). A bare carriage return is rejected rather than
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

        LineStarts = starts.ToImmutableArray();
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
    /// <remarks>
    ///     The list is backed by an immutable array, so a caller cannot change the line table through
    ///     it, not even by casting.
    /// </remarks>
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

        // Detect and strip the byte order mark, then decode strictly
        var decoded = Decode(bytes, out var hasBom);
        return Build(path, decoded, hasBom);
    }

    /// <summary>
    ///     Decodes the bytes of a file as strict UTF-8: removes a UTF-8 byte order mark, and reports the other
    ///     Unicode byte order marks and invalid UTF-8 at their position.
    /// </summary>
    /// <param name="bytes">Complete file content.</param>
    /// <param name="hasBom">Receives whether a UTF-8 mark was present.</param>
    /// <returns>The decoded text, without the mark and with the original line terminators.</returns>
    /// <exception cref="SourceDocumentException">
    ///     Thrown (rule <see cref="SourceDocumentException.EncodingRuleId"/>) for a UTF-16 or UTF-32 byte order mark
    ///     or for invalid UTF-8; the configuration loader reuses it for the same checks of a configuration file.
    /// </exception>
    internal static string Decode(ReadOnlySpan<byte> bytes, out bool hasBom)
    {
        // UTF-8 mark: EF BB BF
        hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        var content = hasBom ? bytes[3..] : bytes;

        // UTF-16 LE/BE (FF FE / FE FF; also covers UTF-32 LE) and UTF-32 BE (00 00 FE FF)
        var isUtf16 = bytes.Length >= 2 && ((bytes[0] == 0xFF && bytes[1] == 0xFE) || (bytes[0] == 0xFE && bytes[1] == 0xFF));
        var isUtf32Be = bytes.Length >= 4 && bytes[0] == 0 && bytes[1] == 0 && bytes[2] == 0xFE && bytes[3] == 0xFF;
        if (isUtf16 || isUtf32Be)
        {
            throw new SourceDocumentException(
                SourceDocumentException.EncodingRuleId, 1, 1, "Unsupported byte order mark (only UTF-8 is supported)");
        }

        try
        {
            return StrictUtf8.GetString(content);
        }
        catch (DecoderFallbackException ex)
        {
            // Count the line breaks before the bad byte (LF, CRLF or a lone CR), then measure the partial line in UTF-16 units
            var before = content[..Math.Clamp(ex.Index, 0, content.Length)];
            var line = 1;
            var lineStart = 0;
            for (var i = 0; i < before.Length; i++)
            {
                // A CR directly before an LF belongs to the same line break; the LF is the one counted
                if (before[i] == (byte)'\n' || (before[i] == (byte)'\r' && (i + 1 >= content.Length || content[i + 1] != (byte)'\n')))
                {
                    line++;
                    lineStart = i + 1;
                }
            }

            throw new SourceDocumentException(
                SourceDocumentException.EncodingRuleId, line, Encoding.UTF8.GetCharCount(before[lineStart..]) + 1, "Invalid UTF-8 byte sequence");
        }
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
    /// <remarks>
    ///     For unchanged text and the original terminator style (<see cref="MajorityLineEnding"/> for a file whose
    ///     terminators are all of one style) the bytes equal the original file. A mixed file cannot be restored
    ///     byte for byte, because only the counts of its terminators are kept: it is written in the style chosen by
    ///     the caller, normally the majority style.
    /// </remarks>
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

/// <summary>
///     Line terminator styles that LintMark can write back to disk.
/// </summary>
/// <remarks>
///     Only LF and CRLF are modeled. A bare carriage return (classic Mac style) is deliberately
///     not representable: <see cref="SourceDocument"/> reports it as a failure instead of silently
///     rewriting it, because the Markdown parser treats it inconsistently.
/// </remarks>
internal enum LineEnding
{
    /// <summary>
    ///     Unix-style line feed (<c>\n</c>).
    /// </summary>
    Lf,

    /// <summary>
    ///     Windows-style carriage return followed by line feed (<c>\r\n</c>).
    /// </summary>
    CrLf
}

/// <summary>
///     Counts of the original line terminators found in a source file.
/// </summary>
/// <param name="LfCount">Number of bare line feeds (<c>\n</c> not preceded by <c>\r</c>).</param>
/// <param name="CrLfCount">Number of <c>\r\n</c> pairs.</param>
/// <remarks>
///     The counts (rather than a single style flag) are kept because mixed files exist in
///     practice; the formatter needs them to decide whether a file is mixed and which style is the
///     majority. Immutable value type; safe for concurrent use.
/// </remarks>
internal readonly record struct LineEndingInfo(int LfCount, int CrLfCount)
{
    /// <summary>
    ///     Gets a value indicating whether the file contains both LF and CRLF terminators.
    /// </summary>
    public bool IsMixed => LfCount > 0 && CrLfCount > 0;
}

/// <summary>
///     Describes a file that cannot be turned into a <see cref="SourceDocument"/>.
/// </summary>
/// <remarks>
///     A typed exception (rather than a message) is used so the engine can report the failure
///     as <c>path:line:col: rule-id message</c> without parsing text. The position is 1-based and
///     refers to the first offending byte or character when it is known, otherwise to 1:1.
///     Instances are immutable and safe to share between threads.
/// </remarks>
public sealed class SourceDocumentException : Exception
{
    /// <summary>
    ///     Rule identifier reported when the file is not valid UTF-8 or carries a non-UTF-8 byte order mark.
    /// </summary>
    public const string EncodingRuleId = "file-encoding-error";

    /// <summary>
    ///     Rule identifier reported when the file contains a bare carriage return.
    /// </summary>
    public const string LineEndingRuleId = "file-line-ending-error";

    /// <summary>
    ///     Initializes a new instance of the <see cref="SourceDocumentException"/> class.
    /// </summary>
    /// <param name="ruleId">Kebab-case rule identifier to report the failure under.</param>
    /// <param name="line">1-based line of the first offending character (1 when unknown).</param>
    /// <param name="column">1-based column of the first offending character (1 when unknown).</param>
    /// <param name="message">Human-readable description of the failure (single line, no path).</param>
    public SourceDocumentException(string ruleId, int line, int column, string message)
        : base(message)
    {
        RuleId = ruleId;
        Line = line;
        Column = column;
    }

    /// <summary>
    ///     Gets the kebab-case rule identifier the failure is reported under.
    /// </summary>
    public string RuleId { get; }

    /// <summary>
    ///     Gets the 1-based line of the first offending character.
    /// </summary>
    public int Line { get; }

    /// <summary>
    ///     Gets the 1-based column (UTF-16 code units, tab counted as one) of the first offending character.
    /// </summary>
    public int Column { get; }
}
