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
using DemaConsulting.LintMark.Document;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Unit tests for the SourceDocument class.
/// </summary>
public class SourceDocumentTests
{
    /// <summary>
    ///     Encodes text as UTF-8 without a byte order mark.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The bytes.</returns>
    private static byte[] Utf8(string text) => new UTF8Encoding(false).GetBytes(text);

    /// <summary>
    ///     Test that LF-only text is kept as is and counted.
    /// </summary>
    [Fact]
    public void SourceDocument_Create_LfOnly_KeepsTextAndCountsLf()
    {
        // Arrange: LF-only content
        var bytes = Utf8("a\nb\nc\n");

        // Act: create the document
        var document = SourceDocument.Create("a.md", bytes);

        // Assert: text is unchanged and only LF terminators are counted
        Assert.Equal("a\nb\nc\n", document.Text);
        Assert.Equal(new LineEndingInfo(3, 0), document.LineEndings);
        Assert.False(document.LineEndings.IsMixed);
        Assert.Equal(LineEnding.Lf, document.MajorityLineEnding);
        Assert.Equal("a.md", document.Path);
    }

    /// <summary>
    ///     Test that the exposed line table cannot be changed through a cast.
    /// </summary>
    [Fact]
    public void SourceDocument_LineStarts_CastToMutableTypes_CannotBeModified()
    {
        // Arrange: a three-line document
        var document = SourceDocument.Create("a.md", Utf8("a\nb\nc\n"));

        // Act: take the line table
        var lineStarts = document.LineStarts;

        // Assert: a caller cannot change it, and positions are still computed from the original table
        ReadOnlyAssertions.AssertCannotBeModified(lineStarts);
        Assert.Equal(new[] { 0, 2, 4, 6 }, lineStarts);
        Assert.Equal((3, 1), document.PositionOf(4));
    }

    /// <summary>
    ///     Test that CRLF content is normalized to LF and counted.
    /// </summary>
    [Fact]
    public void SourceDocument_Create_CrLf_NormalizesToLfAndCountsCrLf()
    {
        // Arrange: CRLF content
        var bytes = Utf8("a\r\nb\r\n");

        // Act: create the document
        var document = SourceDocument.Create("a.md", bytes);

        // Assert: text is LF-normalized, terminators are counted as CRLF
        Assert.Equal("a\nb\n", document.Text);
        Assert.Equal(new LineEndingInfo(0, 2), document.LineEndings);
        Assert.Equal(LineEnding.CrLf, document.MajorityLineEnding);
    }

    /// <summary>
    ///     Test that mixed terminators are detected and the majority is resolved.
    /// </summary>
    [Fact]
    public void SourceDocument_Create_MixedEndings_IsMixedAndMajorityResolved()
    {
        // Arrange: two CRLF terminators and one LF terminator
        var bytes = Utf8("a\r\nb\r\nc\n");

        // Act: create the document
        var document = SourceDocument.Create("a.md", bytes);

        // Assert: mixed, normalized, CRLF is the majority
        Assert.True(document.LineEndings.IsMixed);
        Assert.Equal("a\nb\nc\n", document.Text);
        Assert.Equal(new LineEndingInfo(1, 2), document.LineEndings);
        Assert.Equal(LineEnding.CrLf, document.MajorityLineEnding);
    }

    /// <summary>
    ///     Test that a tie between LF and CRLF resolves to LF.
    /// </summary>
    [Fact]
    public void SourceDocument_MajorityLineEnding_Tie_IsLf()
    {
        // Arrange: one LF and one CRLF terminator
        var document = SourceDocument.Create("a.md", Utf8("a\r\nb\n"));

        // Act: read the majority
        var majority = document.MajorityLineEnding;

        // Assert: ties resolve to LF
        Assert.Equal(LineEnding.Lf, majority);
    }

    /// <summary>
    ///     Test that a file without terminators resolves to LF.
    /// </summary>
    [Fact]
    public void SourceDocument_MajorityLineEnding_NoTerminators_IsLf()
    {
        // Arrange: a single line without terminator
        var document = SourceDocument.Create("a.md", Utf8("single line"));

        // Act: read the facts
        var majority = document.MajorityLineEnding;

        // Assert: LF majority and no final newline
        Assert.Equal(LineEnding.Lf, majority);
        Assert.False(document.HasFinalNewline);
        Assert.Equal(new LineEndingInfo(0, 0), document.LineEndings);
    }

    /// <summary>
    ///     Test that an empty file is a valid document with a single empty line.
    /// </summary>
    [Fact]
    public void SourceDocument_Create_EmptyFile_HasSingleEmptyLine()
    {
        // Arrange / Act: create from no bytes
        var document = SourceDocument.Create("a.md", []);

        // Assert: empty text, one line, no final newline
        Assert.Equal(string.Empty, document.Text);
        Assert.Equal([0], document.LineStarts);
        Assert.False(document.HasFinalNewline);
        Assert.Equal((1, 1), document.PositionOf(0));
    }

    /// <summary>
    ///     Test that the final newline is detected.
    /// </summary>
    [Fact]
    public void SourceDocument_Create_FinalNewline_IsDetected()
    {
        // Arrange / Act: documents with and without a final newline
        var with = SourceDocument.Create("a.md", Utf8("a\n"));
        var without = SourceDocument.Create("a.md", Utf8("a"));

        // Assert: the flag follows the text
        Assert.True(with.HasFinalNewline);
        Assert.False(without.HasFinalNewline);
    }

    /// <summary>
    ///     Test that a UTF-8 byte order mark is stripped from the text and remembered.
    /// </summary>
    [Fact]
    public void SourceDocument_Create_Utf8Bom_IsStrippedAndPreserved()
    {
        // Arrange: bytes starting with a UTF-8 BOM
        byte[] bytes = [0xEF, 0xBB, 0xBF, .. Utf8("# Title\n")];

        // Act: create the document
        var document = SourceDocument.Create("a.md", bytes);

        // Assert: BOM is not part of the text but is remembered; the encoding writes it back
        Assert.True(document.HasBom);
        Assert.Equal("# Title\n", document.Text);
        Assert.Equal([0xEF, 0xBB, 0xBF], document.Encoding.GetPreamble());
        Assert.Equal(bytes, document.ToBytes(document.Text, LineEnding.Lf));
    }

    /// <summary>
    ///     Test that a file without a byte order mark does not gain one.
    /// </summary>
    [Fact]
    public void SourceDocument_Create_NoBom_WritesNoBom()
    {
        // Arrange / Act: plain UTF-8 bytes
        var document = SourceDocument.Create("a.md", Utf8("x\n"));

        // Assert: no BOM is reported or written
        Assert.False(document.HasBom);
        Assert.Empty(document.Encoding.GetPreamble());
    }

    /// <summary>
    ///     Test that a UTF-16 byte order mark is reported as an encoding failure.
    /// </summary>
    [Fact]
    public void SourceDocument_Create_Utf16Bom_ThrowsEncodingError()
    {
        // Arrange: bytes starting with a UTF-16 LE BOM
        byte[] bytes = [0xFF, 0xFE, 0x61, 0x00];

        // Act: create the document
        var exception = Assert.Throws<SourceDocumentException>(() => SourceDocument.Create("a.md", bytes));

        // Assert: reported under the encoding rule at 1:1
        Assert.Equal("file-encoding-error", exception.RuleId);
        Assert.Equal((1, 1), (exception.Line, exception.Column));
    }

    /// <summary>
    ///     Test that invalid UTF-8 is reported with the position of the first bad byte.
    /// </summary>
    [Fact]
    public void SourceDocument_Create_InvalidUtf8_ThrowsWithPosition()
    {
        // Arrange: invalid byte (0xFF) after "ab" on the second line
        byte[] bytes = [.. Utf8("line1\nab"), 0xFF, .. Utf8("\n")];

        // Act: create the document
        var exception = Assert.Throws<SourceDocumentException>(() => SourceDocument.Create("a.md", bytes));

        // Assert: encoding rule, second line, third column
        Assert.Equal("file-encoding-error", exception.RuleId);
        Assert.Equal((2, 3), (exception.Line, exception.Column));
    }

    /// <summary>
    ///     Test that a bare carriage return is reported with its position.
    /// </summary>
    [Fact]
    public void SourceDocument_Create_LoneCr_ThrowsLineEndingErrorWithPosition()
    {
        // Arrange: a bare CR after "ab" on the second line
        var bytes = Utf8("one\r\nab\rcd\n");

        // Act: create the document
        var exception = Assert.Throws<SourceDocumentException>(() => SourceDocument.Create("a.md", bytes));

        // Assert: line-ending rule at line 2, column 3
        Assert.Equal("file-line-ending-error", exception.RuleId);
        Assert.Equal((2, 3), (exception.Line, exception.Column));
    }

    /// <summary>
    ///     Test that a bare carriage return at the very end of the text is reported.
    /// </summary>
    [Fact]
    public void SourceDocument_FromText_TrailingCr_ThrowsLineEndingError()
    {
        // Arrange / Act: text ending in a bare CR
        var exception = Assert.Throws<SourceDocumentException>(() => SourceDocument.FromText("a.md", "abc\r"));

        // Assert: line-ending rule at 1:4
        Assert.Equal("file-line-ending-error", exception.RuleId);
        Assert.Equal((1, 4), (exception.Line, exception.Column));
    }

    /// <summary>
    ///     Test that FromText normalizes the same way as Create.
    /// </summary>
    [Fact]
    public void SourceDocument_FromText_Crlf_NormalizesLikeCreate()
    {
        // Arrange / Act: text with CRLF terminators
        var document = SourceDocument.FromText("a.md", "a\r\nb\r\n");

        // Assert: normalized and counted
        Assert.Equal("a\nb\n", document.Text);
        Assert.Equal(new LineEndingInfo(0, 2), document.LineEndings);
    }

    /// <summary>
    ///     Test that null arguments are rejected.
    /// </summary>
    [Fact]
    public void SourceDocument_Create_NullPath_ThrowsArgumentNullException()
    {
        // Arrange / Act / Assert: a null path is rejected
        Assert.Throws<ArgumentNullException>(() => SourceDocument.Create(null!, []));
        Assert.Throws<ArgumentNullException>(() => SourceDocument.FromText("a.md", null!));
    }

    /// <summary>
    ///     Test that line starts are recorded for every line.
    /// </summary>
    [Fact]
    public void SourceDocument_LineStarts_RecordEveryLine()
    {
        // Arrange / Act: three lines and a final newline
        var document = SourceDocument.FromText("a.md", "ab\n\ncd\n");

        // Assert: a trailing empty line starts at the end of the text
        Assert.Equal([0, 3, 4, 7], document.LineStarts);
    }

    /// <summary>
    ///     Test that positions are 1-based and columns count UTF-16 units with a tab as one.
    /// </summary>
    [Fact]
    public void SourceDocument_PositionOf_TabsAndSurrogates_CountsUtf16UnitsWithTabAsOne()
    {
        // Arrange: a tab and an emoji (two UTF-16 units) before 'x'
        var document = SourceDocument.FromText("a.md", "first\n\t\U0001F600x\n");
        var offset = document.Text.IndexOf('x', StringComparison.Ordinal);

        // Act: convert the offset
        var (line, column) = document.PositionOf(offset);

        // Assert: tab = 1, emoji = 2 units, so 'x' is column 4
        Assert.Equal(2, line);
        Assert.Equal(4, column);
    }

    /// <summary>
    ///     Test that OffsetOf is the inverse of PositionOf for every offset.
    /// </summary>
    [Fact]
    public void SourceDocument_OffsetOf_RoundTripsWithPositionOf()
    {
        // Arrange: a document with several lines and an empty line
        var document = SourceDocument.FromText("a.md", "abc\n\ndef\nlast");

        // Act / Assert: every offset (including the end) round-trips
        for (var offset = 0; offset <= document.Text.Length; offset++)
        {
            var (line, column) = document.PositionOf(offset);
            Assert.Equal(offset, document.OffsetOf(line, column));
        }
    }

    /// <summary>
    ///     Test that out-of-range positions are rejected.
    /// </summary>
    [Fact]
    public void SourceDocument_PositionAndOffset_OutOfRange_Throw()
    {
        // Arrange: a two-line document
        var document = SourceDocument.FromText("a.md", "ab\ncd");

        // Act / Assert: offsets, lines and columns outside the document are rejected
        Assert.Throws<ArgumentOutOfRangeException>(() => document.PositionOf(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => document.PositionOf(6));
        Assert.Throws<ArgumentOutOfRangeException>(() => document.OffsetOf(0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => document.OffsetOf(3, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => document.OffsetOf(1, 5));
    }

    /// <summary>
    ///     Test that Slice returns the requested characters of the normalized text.
    /// </summary>
    [Fact]
    public void SourceDocument_Slice_ReturnsRequestedCharacters()
    {
        // Arrange: a document
        var document = SourceDocument.FromText("a.md", "hello world\n");

        // Act: slice the second word
        var slice = document.Slice(6, 5).ToString();

        // Assert: the slice matches
        Assert.Equal("world", slice);
    }

    /// <summary>
    ///     Test that LF content round-trips byte for byte.
    /// </summary>
    [Fact]
    public void SourceDocument_ToBytes_Lf_RestoresOriginalBytes()
    {
        // Arrange: LF-only content
        var bytes = Utf8("# T\n\ntext\n");
        var document = SourceDocument.Create("a.md", bytes);

        // Act: write back using the majority line ending
        var written = document.ToBytes(document.Text, document.MajorityLineEnding);

        // Assert: identical bytes
        Assert.Equal(bytes, written);
    }

    /// <summary>
    ///     Test that CRLF content round-trips byte for byte.
    /// </summary>
    [Fact]
    public void SourceDocument_ToBytes_CrLf_RestoresOriginalBytes()
    {
        // Arrange: CRLF content
        var bytes = Utf8("# T\r\n\r\ntext\r\n");
        var document = SourceDocument.Create("a.md", bytes);

        // Act: write back using the majority line ending
        var written = document.ToBytes(document.Text, document.MajorityLineEnding);

        // Assert: identical bytes
        Assert.Equal(bytes, written);
    }

    /// <summary>
    ///     Test that BOM plus CRLF content round-trips byte for byte.
    /// </summary>
    [Fact]
    public void SourceDocument_ToBytes_BomAndCrLf_RestoresOriginalBytes()
    {
        // Arrange: BOM and CRLF content
        byte[] bytes = [0xEF, 0xBB, 0xBF, .. Utf8("a\r\nb")];
        var document = SourceDocument.Create("a.md", bytes);

        // Act: write back using the majority line ending
        var written = document.ToBytes(document.Text, document.MajorityLineEnding);

        // Assert: identical bytes, including the missing final newline
        Assert.Equal(bytes, written);
        Assert.False(document.HasFinalNewline);
    }

    /// <summary>
    ///     Test that a mixed file is written back in its majority style, which is the documented limit of the round trip.
    /// </summary>
    [Fact]
    public void SourceDocument_ToBytes_MixedFile_WritesMajorityStyle()
    {
        // Arrange: a file with two LF terminators and one CRLF terminator, and one with the opposite majority
        var lfMajority = SourceDocument.Create("a.md", Utf8("a\nb\r\nc\n"));
        var crLfMajority = SourceDocument.Create("b.md", Utf8("a\r\nb\nc\r\n"));

        // Act: write back unchanged text in the majority style
        var lfBytes = lfMajority.ToBytes(lfMajority.Text, lfMajority.MajorityLineEnding);
        var crLfBytes = crLfMajority.ToBytes(crLfMajority.Text, crLfMajority.MajorityLineEnding);

        // Assert: consistent files in the majority style, not the original bytes (only the counts are kept)
        Assert.Equal(Utf8("a\nb\nc\n"), lfBytes);
        Assert.Equal(Utf8("a\r\nb\r\nc\r\n"), crLfBytes);
        Assert.True(lfMajority.LineEndings.IsMixed);
        Assert.True(crLfMajority.LineEndings.IsMixed);
    }

    /// <summary>
    ///     Test that ToDisk converts to the requested terminator style.
    /// </summary>
    [Fact]
    public void SourceDocument_ToDisk_Target_ConvertsLineEndings()
    {
        // Arrange: LF-normalized text
        const string text = "a\nb\n";

        // Act: convert to both styles
        var lf = SourceDocument.ToDisk(text, LineEnding.Lf);
        var crlf = SourceDocument.ToDisk(text, LineEnding.CrLf);

        // Assert: terminators follow the target
        Assert.Equal("a\nb\n", lf);
        Assert.Equal("a\r\nb\r\n", crlf);
    }
}
