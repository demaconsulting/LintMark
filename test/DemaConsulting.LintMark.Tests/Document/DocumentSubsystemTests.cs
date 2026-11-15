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
///     Subsystem tests of the Document subsystem: bytes go in through the source document and the parser, and the
///     block tree, the suppression ranges and the display width come out, with real collaborators and no substitutes.
/// </summary>
public class DocumentSubsystemTests
{
    /// <summary>
    ///     Test that UTF-8 bytes with a byte order mark and CRLF line endings become an LF-normalized block tree whose
    ///     blocks carry source ranges that tile the text, and that unreadable input is reported with a rule and position.
    /// </summary>
    [Fact]
    public void DocumentSubsystem_Parsing_Utf8CrlfBytes_BecomeBlockTreeAndUnreadableInputIsReported()
    {
        // Arrange: a UTF-8 file with a byte order mark and CRLF line endings, a UTF-16 file and a file with a bare carriage return
        byte[] bytes = [0xEF, 0xBB, 0xBF, .. new UTF8Encoding(false).GetBytes("# Title\r\n\r\ntext\r\n\r\n- a\r\n- b\r\n")];
        byte[] utf16 = [0xFF, 0xFE, 0x41, 0x00];
        byte[] bareCr = "one\rtwo\n"u8.ToArray();

        // Act: read and parse the first file, and try to read the other two
        var source = SourceDocument.Create("a.md", bytes);
        var document = DocumentParser.Parse(source);
        var encoding = Assert.Throws<SourceDocumentException>(() => SourceDocument.Create("b.md", utf16));
        var lineEnding = Assert.Throws<SourceDocumentException>(() => SourceDocument.Create("c.md", bareCr));

        // Assert: the text is LF-normalized, the endings are counted, the blocks are in source order and tile the text
        Assert.Equal("# Title\n\ntext\n\n- a\n- b\n", source.Text);
        Assert.True(source.HasBom);
        Assert.Equal(new LineEndingInfo(0, 6), source.LineEndings);
        Assert.Equal(
            [BlockKind.Heading, BlockKind.Paragraph, BlockKind.List, BlockKind.ListItem, BlockKind.Paragraph, BlockKind.ListItem, BlockKind.Paragraph],
            document.Blocks.Select(block => block.Kind));
        Assert.Equal(source.Text.IndexOf("- a", StringComparison.Ordinal), document.Blocks.First(block => block.Kind == BlockKind.List).Start);
        Assert.Empty(SpanTilingAssertions.Check(document));
        Assert.Equal((SourceDocumentException.EncodingRuleId, 1, 1), (encoding.RuleId, encoding.Line, encoding.Column));
        Assert.Equal((SourceDocumentException.LineEndingRuleId, 1, 4), (lineEnding.RuleId, lineEnding.Line, lineEnding.Column));
    }

    /// <summary>
    ///     Test that comments in the lintmark and markdownlint forms produce suppression ranges that cover exactly the
    ///     text between them, for the rule they name, whether it is named by id or by number.
    /// </summary>
    [Fact]
    public void DocumentSubsystem_Suppression_DisableEnableComments_CoverOnlyTheNamedRuleInsideTheRange()
    {
        // Arrange: three paragraphs, the middle one between a lintmark comment pair and the last one under a markdownlint pair
        const string text =
            "before\n\n<!-- lintmark-disable trailing-whitespace -->\ninside\n<!-- lintmark-enable trailing-whitespace -->\n\n"
            + "<!-- markdownlint-disable MD013 -->\nnumbered\n<!-- markdownlint-enable MD013 -->\n\nafter\n";

        // Act: parse the text
        var suppressions = DocumentParser.Parse(SourceDocument.FromText("a.md", text)).Suppressions;
        var before = text.IndexOf("before", StringComparison.Ordinal);
        var inside = text.IndexOf("inside", StringComparison.Ordinal);
        var numbered = text.IndexOf("numbered", StringComparison.Ordinal);
        var after = text.IndexOf("after", StringComparison.Ordinal);

        // Assert: the named rules are suppressed in their ranges only, the number resolves to the id, other rules are not suppressed
        Assert.False(suppressions.IsSuppressed(before, "trailing-whitespace"));
        Assert.True(suppressions.IsSuppressed(inside, "trailing-whitespace"));
        Assert.True(suppressions.IsSuppressed(inside, "MD009"));
        Assert.False(suppressions.IsSuppressed(inside, "line-length"));
        Assert.True(suppressions.IsSuppressed(numbered, "line-length"));
        Assert.True(suppressions.IsSuppressed(numbered, "MD013"));
        Assert.False(suppressions.IsSuppressed(after, "line-length"));
        Assert.True(suppressions.IsSuppressedRange(inside - 5, inside + 5, "trailing-whitespace"));
    }

    /// <summary>
    ///     Test that the display width counts wide characters as two columns and combining marks and tabs as none, and
    ///     that positions in a document with wide characters are counted in characters.
    /// </summary>
    [Fact]
    public void DocumentSubsystem_DisplayWidth_WideCombiningAndTabCharacters_AreMeasuredInColumns()
    {
        // Arrange: texts of narrow, wide, combining and tab characters, and a document with a wide character on its first line
        var source = SourceDocument.FromText("a.md", "日本\nx\n");

        // Act: measure the texts and map the offset of the second line
        var narrow = TextWidth.Of("abc");
        var wide = TextWidth.Of("日本語");
        var combining = TextWidth.Of("e\u0301");
        var tab = TextWidth.Of("a\tb");
        var position = source.PositionOf(source.Text.IndexOf('x'));

        // Assert: the widths are columns; positions are lines and characters
        Assert.Equal(3, narrow);
        Assert.Equal(6, wide);
        Assert.Equal(1, combining);
        Assert.Equal(2, tab);
        Assert.Equal((2, 1), position);
        Assert.Equal((1, 3), source.PositionOf(2));
    }
}
