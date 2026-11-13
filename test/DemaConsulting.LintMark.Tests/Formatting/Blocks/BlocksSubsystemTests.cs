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
using DemaConsulting.LintMark.Configuration;
using DemaConsulting.LintMark.Document;
using DemaConsulting.LintMark.Formatting;
using DemaConsulting.LintMark.Linting;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Test that the heading, code block and thematic break formatters and the document passes work together on
///     real documents: every rewrite renders the same, keeps its Pandoc structure, is stable when formatted again,
///     and leaves code, HTML, math, front matter and Pandoc constructs alone.
/// </summary>
public class BlocksSubsystemTests
{
    /// <summary>
    ///     A document that breaks every rule of the subsystem at least once.
    /// </summary>
    private const string DirtyDocument =
        "Title\n=====\nIntro text with trailing space \n- one\n- two\n\n\n\n## Closed ##\n```\ncode\n```\n   ***\n  #   Spaced   \n"
        + "~~~js\ntilde\n~~~\n    indented code\nSome\ttabbed text\n| a | b |\n|---|---|\n| 1 | 2 |";

    /// <summary>
    ///     The canonical form of <see cref="DirtyDocument"/>.
    /// </summary>
    private const string CleanDocument =
        "# Title\n\nIntro text with trailing space\n\n- one\n- two\n\n## Closed\n\n```\ncode\n```\n\n   ---\n\n# Spaced\n\n"
        + "```js\ntilde\n```\n\n```\nindented code\n```\n\nSome    tabbed text\n\n| a | b |\n|---|---|\n| 1 | 2 |\n";

    /// <summary>
    ///     A document that is already canonical and uses every construct the rules look at.
    /// </summary>
    private const string CanonicalDocument =
        "---\ntitle: Example\n---\n\n# Title\n\nParagraph with a hard break  \nand a link [text](http://example.com \"title\") and `code`.\n\n"
        + "## Section\n\n- one\n- two\n  - nested\n\n1. first\n2. second\n\n> quote\n>\n> - item in quote\n\n"
        + "```js\nconst x = 1;  \n```\n\n| a | b |\n|---|---|\n| 1 | 2 |\n\n---\n\n<!-- comment -->\n\n<div>\nraw  \n</div>\n\n"
        + "$$\nx^2\n$$\n\n[ref]: http://example.com\n\nNote[^1].\n\n[^1]: The note.\n";

    /// <summary>
    ///     Formats bytes the way the engine does and returns the bytes that would be written.
    /// </summary>
    /// <param name="bytes">The bytes of the file.</param>
    /// <returns>The bytes that would be written.</returns>
    private static byte[] FormatBytes(byte[] bytes)
    {
        var source = SourceDocument.Create("a.md", bytes);
        var result = BlockFormatterAssertions.ProductionFormatter().Format(DocumentParser.Parse(source), LintMarkConfig.Default);
        return source.ToBytes(SourcePatcher.Apply(source, result.Edits), result.LineEnding);
    }

    /// <summary>
    ///     Test that a document that breaks every rule is rewritten to its canonical form.
    /// </summary>
    [Fact]
    public void BlocksSubsystem_Format_DocumentBreakingEveryRule_ProducesTheCanonicalDocument() =>
        BlockFormatterAssertions.AssertFormats(DirtyDocument, CleanDocument);

    /// <summary>
    ///     Test that a canonical document produces no edit and no violation.
    /// </summary>
    [Fact]
    public void BlocksSubsystem_Format_CanonicalDocument_ProposesNothing() =>
        BlockFormatterAssertions.AssertUnchanged(CanonicalDocument);

    /// <summary>
    ///     Test that every violation of the dirty document is reported once per rule and line, at the place of its
    ///     change and with the description of its rule, and that all fourteen rules of the subsystem are among them.
    /// </summary>
    [Fact]
    public void BlocksSubsystem_Format_DocumentBreakingEveryRule_ReportsEveryRuleOnce()
    {
        // Act: format the dirty document
        var (result, _) = BlockFormatterAssertions.Apply(BlockFormatterAssertions.ProductionFormatter(), DirtyDocument);

        // Assert: the rule and the line of each violation (the document has 22 lines, and a rule is reported once per line)
        var reported = result.Diagnostics.OrderBy(d => d.Line).ThenBy(d => d.RuleId, StringComparer.Ordinal).Select(d => (d.RuleId, d.Line)).ToList();
        Assert.Equal(
            [
                ("heading-style", 1),
                ("heading-blank-lines", 3),
                ("trailing-whitespace", 3),
                ("list-blank-lines", 4),
                ("max-blank-lines", 7),
                ("heading-style", 9),
                ("heading-blank-lines", 10),
                ("fence-blank-lines", 13),
                ("thematic-break", 13),
                ("heading-blank-lines", 14),
                ("heading-space", 14),
                ("heading-start-left", 14),
                ("trailing-whitespace", 14),
                ("code-fence", 15),
                ("heading-blank-lines", 15),
                ("code-fence", 17),
                ("code-block-style", 18),
                ("fence-blank-lines", 18),
                ("fence-blank-lines", 19),
                ("hard-tabs", 19),
                ("table-blank-lines", 20),
                ("final-newline", 22)
            ],
            reported);

        // Each rule is reported once per line, all fourteen rules appear, and every message is the description of its rule
        Assert.Equal(reported.Count, reported.Distinct().Count());
        Assert.Equal(14, reported.Select(item => item.RuleId).Distinct().Count());
        Assert.All(result.Diagnostics, diagnostic =>
        {
            Assert.True(RuleCatalog.TryGet(diagnostic.RuleId, out var rule));
            Assert.Equal(rule.Description, diagnostic.Message);
            Assert.Equal("a.md", diagnostic.Path);
        });
    }

    /// <summary>
    ///     Test that the dirty document is rewritten the same way with CRLF line endings and a byte order mark.
    /// </summary>
    [Fact]
    public void BlocksSubsystem_Format_DirtyDocumentWithCrlfAndBom_KeepsLineEndingsAndMark() =>
        BlockFormatterAssertions.AssertFormatsOnDisk(DirtyDocument, CleanDocument);

    /// <summary>
    ///     Test that the dirty document gets the same edits and the same violations with CRLF or mixed line endings as with LF line endings.
    /// </summary>
    [Fact]
    public void BlocksSubsystem_Format_DirtyDocumentWithCrlf_ProposesTheSameEdits()
    {
        // Arrange: the edits and violations for the LF text
        var formatter = BlockFormatterAssertions.ProductionFormatter();
        var expected = formatter.Format(BlockFormatterAssertions.Parse(DirtyDocument), LintMarkConfig.Default);
        var crlf = DirtyDocument.Replace("\n", "\r\n", StringComparison.Ordinal);
        var mixed = BlockFormatterAssertions.MixEndings(DirtyDocument);

        // Act & Assert: the same edits come out of CRLF and mixed bytes; only the line ending diagnostic is added
        foreach (var text in new[] { crlf, mixed })
        {
            var result = formatter.Format(DocumentParser.Parse(SourceDocument.Create("a.md", Encoding.UTF8.GetBytes(text))), LintMarkConfig.Default);
            Assert.Equal(expected.Edits, result.Edits);
            Assert.Equal(expected.Diagnostics.Select(d => (d.RuleId, d.Line)), result.Diagnostics.Where(d => d.RuleId != "line-ending").Select(d => (d.RuleId, d.Line)));
        }
    }

    /// <summary>
    ///     Test that the dirty document gets the same edits and the same violations with a byte order mark as without one.
    /// </summary>
    [Fact]
    public void BlocksSubsystem_Format_DirtyDocumentWithByteOrderMark_ProposesTheSameEdits()
    {
        // Arrange: the edits and violations for the text without a mark
        var formatter = BlockFormatterAssertions.ProductionFormatter();
        var expected = formatter.Format(BlockFormatterAssertions.Parse(DirtyDocument), LintMarkConfig.Default);
        byte[] withMark = [.. new UTF8Encoding(true).GetPreamble(), .. Encoding.UTF8.GetBytes(DirtyDocument)];

        // Act: format the bytes with the mark
        var result = formatter.Format(DocumentParser.Parse(SourceDocument.Create("a.md", withMark)), LintMarkConfig.Default);

        // Assert: the same edits and violations
        Assert.Equal(expected.Edits, result.Edits);
        Assert.Equal(expected.Diagnostics.Select(d => (d.RuleId, d.Line)), result.Diagnostics.Select(d => (d.RuleId, d.Line)));
    }

    /// <summary>
    ///     Test that a file with mixed line endings is written with the majority ending.
    /// </summary>
    [Fact]
    public void BlocksSubsystem_Format_MixedLineEndings_AreWrittenWithTheMajorityEnding()
    {
        // Arrange: three CRLF and one LF ending, trailing whitespace, and extra blank lines
        var bytes = Encoding.UTF8.GetBytes("# Title  \r\n\r\ntext \nmore\r\n\r\n\r\n");

        // Act: format
        var written = FormatBytes(bytes);

        // Assert: canonical text with CRLF endings
        Assert.Equal("# Title\r\n\r\ntext\r\nmore\r\n", Encoding.UTF8.GetString(written));
    }

    /// <summary>
    ///     Test that headings, code, quotes and lists nested in each other are formatted without breaking the structure.
    /// </summary>
    [Theory]
    [InlineData("> # H  \n> text  \n>\n> ```\n> code  \n> ```\n", "> # H\n> text\n>\n> ```\n> code  \n> ```\n")]
    [InlineData("- item\n\n  ~~~\n  code\n  ~~~\n", "- item\n\n  ```\n  code\n  ```\n")]
    [InlineData("- a\n  ```\n  code\n  ```\n- b\n", "- a\n  ```\n  code\n  ```\n- b\n")]
    [InlineData("> - a \n>   b\n> - c\n", "> - a\n>   b\n> - c\n")]
    [InlineData("1. one\n   - two  \n     - three\n", "1. one\n   - two\n     - three\n")]
    [InlineData("> > deep  \n> > ***\n", "> > deep\n> > ***\n")]
    [InlineData("- item\n\n      code in item\n", "- item\n\n      code in item\n")]
    public void BlocksSubsystem_Format_NestedContainers_KeepTheStructure(string input, string expected) =>
        BlockFormatterAssertions.AssertFormats(input, expected);

    /// <summary>
    ///     Test that hard breaks are kept exactly as the author's intent: two spaces stay, more become two, one is not a break, a backslash stays.
    /// </summary>
    [Theory]
    [InlineData("a  \nb\n", "a  \nb\n")]
    [InlineData("a   \nb\n", "a  \nb\n")]
    [InlineData("a \nb\n", "a\nb\n")]
    [InlineData("a\\\nb\n", "a\\\nb\n")]
    [InlineData("a  \nb  \n", "a  \nb\n")]
    [InlineData("`a  \nb`\n", "`a  \nb`\n")]
    [InlineData("[a  \nb](http://x)\n", "[a  \nb](http://x)\n")]
    public void BlocksSubsystem_Format_HardBreaks_KeepTheirMeaning(string input, string expected) =>
        BlockFormatterAssertions.AssertFormats(input, expected);

    /// <summary>
    ///     Test that tabs are expanded only in plain text, never in indentation, code, link titles or tables.
    /// </summary>
    [Theory]
    [InlineData("\ttext\n", "```\ntext\n```\n")]
    [InlineData("a\tb\n", "a   b\n")]
    [InlineData("a `b\tc`\n", "a `b\tc`\n")]
    [InlineData("[a](http://x \"t\tt\")\n", "[a](http://x \"t\tt\")\n")]
    [InlineData("| a\tb |\n|---|\n| 1 |\n", "| a\tb |\n|---|\n| 1 |\n")]
    [InlineData("- a\n\t- b\n", "- a\n\t- b\n")]
    [InlineData("# a\tb\n", "# a b\n")]
    public void BlocksSubsystem_Format_Tabs_AreExpandedOnlyInPlainText(string input, string expected) =>
        BlockFormatterAssertions.AssertFormats(input, expected);

    /// <summary>
    ///     Test that Pandoc constructs are left alone, also when the lines around them are cleaned up.
    /// </summary>
    [Fact]
    public void BlocksSubsystem_Format_PandocConstructs_AreKept()
    {
        // Arrange: a fenced div, a LaTeX command, display math, an attribute group, a citation, a raw block and a grid table
        const string pandoc =
            "::: {.note}\ntext\n:::\n\n\\new" + "page\n\n$$\nx\n$$\n\nSee [@smith2020] and a [span]{#id .class}.\n\n"
            + "```{=html}\n<b>x</b>\n```\n\n+---+---+\n| a | b |\n+===+===+\n| 1 | 2 |\n+---+---+\n";

        // Act & Assert: canonical Pandoc text is unchanged
        BlockFormatterAssertions.AssertUnchanged(pandoc);
    }

    /// <summary>
    ///     Test that trailing whitespace next to Pandoc constructs is removed without changing the constructs.
    /// </summary>
    [Fact]
    public void BlocksSubsystem_Format_PandocConstructsWithTrailingWhitespace_AreCleaned() =>
        BlockFormatterAssertions.AssertFormats(
            "::: {.note}\ntext \n:::\t\n\n\\new" + "page \n\nSee [@smith2020] \n",
            "::: {.note}\ntext\n:::\n\n\\new" + "page\n\nSee [@smith2020]\n");

    /// <summary>
    ///     Test that a braced Pandoc fence keeps its backticks even when the configuration asks for tildes, and a tilde one keeps its tildes.
    /// </summary>
    [Theory]
    [InlineData("```{=html}\n<b>x</b>\n```\n")]
    [InlineData("~~~{.python}\nx = 1\n~~~\n")]
    public void BlocksSubsystem_Format_BracedFence_KeepsItsFenceCharacter(string input)
    {
        // Act & Assert: neither configuration changes the fence, so the structure tokens stay
        BlockFormatterAssertions.AssertFormats(input, input, LintMarkConfig.Default with { CodeFence = FenceMarker.Tilde });
        BlockFormatterAssertions.AssertFormats(input, input);
    }

    /// <summary>
    ///     Test that a grid table next to text is left alone and a document with an invalid grid table is never rewritten.
    /// </summary>
    [Fact]
    public void BlocksSubsystem_Format_DocumentWithInvalidGridTable_IsNeverRewritten()
    {
        // Arrange: text with trailing whitespace next to a grid table border that is not part of a valid table
        var document = BlockFormatterAssertions.Parse("para  \n+---+---+\nmore\t\n");

        // Act: format
        var result = BlockFormatterAssertions.ProductionFormatter().Format(document, LintMarkConfig.Default);

        // Assert: the document is format-unsafe, with no edit and no violation
        Assert.True(document.IsFormatUnsafe);
        Assert.Empty(result.Edits);
        Assert.Empty(result.Diagnostics);
    }

    /// <summary>
    ///     Test that front matter keeps its whitespace and that the first line of dashes is not turned into front matter.
    /// </summary>
    [Theory]
    [InlineData("---\ntitle: x  \nlist:\n\t- a\n---\n# H\n", "---\ntitle: x  \nlist:\n\t- a\n---\n\n# H\n")]
    [InlineData("---\n\ntext\n", "---\n\ntext\n")]
    [InlineData("***\n\ntext\n", "***\n\ntext\n")]
    [InlineData("---\n\ntext\n\n***\n", "---\n\ntext\n\n***\n")]
    [InlineData("---\n\n    ---\n", "---\n\n    ---\n")]
    public void BlocksSubsystem_Format_FrontMatterAndLeadingDashes_AreKept(string input, string expected) =>
        BlockFormatterAssertions.AssertFormats(input, expected);

    /// <summary>
    ///     Test that HTML blocks and comments keep their content and get blank lines around headings.
    /// </summary>
    [Theory]
    [InlineData("<div>\nx  \n\ty\n</div>\n\ntext\n", "<div>\nx  \n\ty\n</div>\n\ntext\n")]
    [InlineData("<!-- note -->\n# H\n", "<!-- note -->\n\n# H\n")]
    [InlineData("# H\n<!-- note -->\n", "# H\n\n<!-- note -->\n")]
    [InlineData("<!--\n\n\n  inner  \n-->\n", "<!--\n\n\n  inner  \n-->\n")]
    [InlineData("<details>\n<summary>S</summary>\n\ntext  \n\n</details>\n", "<details>\n<summary>S</summary>\n\ntext\n\n</details>\n")]
    public void BlocksSubsystem_Format_HtmlBlocks_KeepTheirContent(string input, string expected) =>
        BlockFormatterAssertions.AssertFormats(input, expected);

    /// <summary>
    ///     Test that an unclosed fence is left exactly as written, with its trailing blank lines.
    /// </summary>
    [Theory]
    [InlineData("text\n```\ncode  \n\n\n")]
    [InlineData("text\n~~~\ncode\n")]
    [InlineData("- item\n  ```\n  code  \n\n\n\n")]
    public void BlocksSubsystem_Format_UnclosedFence_StaysAsWritten(string input) =>
        BlockFormatterAssertions.AssertFormats(input, input);

    /// <summary>
    ///     Test that an indented block in its many forms becomes a fenced block that cannot be closed by its own content.
    /// </summary>
    [Theory]
    [InlineData("# H\n    code\n", "# H\n\n```\ncode\n```\n")]
    [InlineData("text\n\n    a\n\n    b\n\nmore\n", "text\n\n```\na\n\nb\n```\n\nmore\n")]
    [InlineData("    ~~~\n    x\n", "```\n~~~\nx\n```\n")]
    [InlineData("    ```\n    x\n    ```\n", "````\n```\nx\n```\n````\n")]
    [InlineData("    code\n- item\n", "```\ncode\n```\n\n- item\n")]
    public void BlocksSubsystem_Format_IndentedCodeVariants_BecomeFencedCode(string input, string expected) =>
        BlockFormatterAssertions.AssertFormats(input, expected);

    /// <summary>
    ///     Test that tilde fences with backtick fences inside, and an info string with a backtick, are converted correctly or left alone.
    /// </summary>
    [Theory]
    [InlineData("~~~\n```\ninner\n```\n~~~\n", "````\n```\ninner\n```\n````\n")]
    [InlineData("~~~ a`b\nx\n~~~\n", "~~~ a`b\nx\n~~~\n")]
    public void BlocksSubsystem_Format_TildeFenceEdgeCases_AreSafe(string input, string expected) =>
        BlockFormatterAssertions.AssertFormats(input, expected);

    /// <summary>
    ///     Test that thematic breaks in every form and place keep the document's meaning.
    /// </summary>
    [Theory]
    [InlineData("text\n\n***\n\nmore\n", "text\n\n---\n\nmore\n")]
    [InlineData("text\n***\nmore\n", "text\n***\nmore\n")]
    [InlineData("Title\n---\n\ntext\n", "## Title\n\ntext\n")]
    [InlineData("- a\n- b\n***\n", "- a\n- b\n\n---\n")]
    [InlineData("# H\n___\n", "# H\n\n---\n")]
    public void BlocksSubsystem_Format_ThematicBreakVariants_KeepTheMeaning(string input, string expected) =>
        BlockFormatterAssertions.AssertFormats(input, expected);

    /// <summary>
    ///     Test that setext headings in every form are converted only where that is safe.
    /// </summary>
    [Theory]
    [InlineData("Multi\nline\n===\n", "Multi\nline\n===\n")]
    [InlineData("C#\n==\n", "C#\n==\n")]
    [InlineData("[ref]: /x\n\nTitle\n===\n", "[ref]: /x\n\n# Title\n")]
    [InlineData("A\n===\nB\n---\n", "# A\n\n## B\n")]
    [InlineData("text\n\n  Indented\n  ===\n", "text\n\n# Indented\n")]
    public void BlocksSubsystem_Format_SetextVariants_AreConvertedOnlyWhereSafe(string input, string expected) =>
        BlockFormatterAssertions.AssertFormats(input, expected);

    /// <summary>
    ///     Test that closing number signs are removed from headings, and number signs that belong to the text are kept.
    /// </summary>
    [Theory]
    [InlineData("# Title #\n", "# Title\n")]
    [InlineData("## Title ##   \n", "## Title\n")]
    [InlineData("# Title   #\n", "# Title\n")]
    [InlineData("### ###\n", "###\n")]
    [InlineData("# C#\n", "# C#\n")]
    [InlineData("# foo \\#\n", "# foo \\#\n")]
    public void BlocksSubsystem_Format_ClosedHeadingVariants_LoseOnlyTheClosingSequence(string input, string expected) =>
        BlockFormatterAssertions.AssertFormats(input, expected);

    /// <summary>
    ///     Test that spacing is added next to lists, tables and fences in documents where it is a violation, and only there.
    /// </summary>
    [Theory]
    [InlineData("text\n- a\n- b\ntext2\n", "text\n\n- a\n- b\ntext2\n")]
    [InlineData("text\n2. a\n", "text\n2. a\n")]
    [InlineData("| a |\n|---|\n| 1 |\ntext\n", "| a |\n|---|\n| 1 |\ntext\n")]
    [InlineData("a\n```\nc\n```\nb\n", "a\n\n```\nc\n```\n\nb\n")]
    public void BlocksSubsystem_Format_Spacing_IsAddedOnlyForRealBlockBoundaries(string input, string expected) =>
        BlockFormatterAssertions.AssertFormats(input, expected);

    /// <summary>
    ///     Test that the configured settings select the character of fences and breaks, the blank line maximum and the final newline together.
    /// </summary>
    [Theory]
    [InlineData(
        "text\n\n\n\n```\ncode\n```\n\n---\n\n    indented\n\n",
        "text\n\n\n~~~\ncode\n~~~\n\n***\n\n~~~\nindented\n~~~",
        "Tilde",
        "Asterisks",
        2,
        false)]
    [InlineData(
        "text\n\n\n\n\n```\ncode\n```\n\n***\n\n~~~\ntilde\n~~~\n",
        "text\n\n\n\n```\ncode\n```\n\n___\n\n```\ntilde\n```\n",
        "Backtick",
        "Underscores",
        3,
        true)]
    [InlineData(
        "text\n\n\n```\ncode\n```\n\n___\n",
        "text\n\n```\ncode\n```\n\n---\n",
        "Backtick",
        "Dashes",
        1,
        true)]
    public void BlocksSubsystem_Format_ConfiguredSettings_SelectTheCanonicalForm(
        string input,
        string expected,
        string fence,
        string breakStyle,
        int maxBlankLines,
        bool finalNewline)
    {
        // Arrange: the fence character, the break style, the blank line maximum and the final newline of the configuration
        var config = LintMarkConfig.Default with
        {
            CodeFence = Enum.Parse<FenceMarker>(fence),
            ThematicBreak = Enum.Parse<ThematicBreakStyle>(breakStyle),
            MaxBlankLines = maxBlankLines,
            FinalNewline = finalNewline
        };

        // Act & Assert: the document follows every setting
        BlockFormatterAssertions.AssertFormats(input, expected, config);
    }

    /// <summary>
    ///     Test that a heading, a code block and a thematic break stay exactly as written inside a region where a rule
    ///     of their formatter is disabled, by either directive family, and that the blocks after the region are formatted.
    /// </summary>
    [Theory]
    [InlineData("markdownlint-disable MD003 MD046 MD035", "markdownlint-enable MD003 MD046 MD035")]
    [InlineData("lintmark-disable heading-style code-block-style thematic-break", "lintmark-enable heading-style code-block-style thematic-break")]
    [InlineData("lintmark-disable", "lintmark-enable")]
    public void BlocksSubsystem_Format_SuppressedBlocks_AreKeptAndTheOthersAreFormatted(string disable, string enable)
    {
        // Arrange: a setext heading, indented code and a break inside the region, and the same three after it
        var region = $"<!-- {disable} -->\n\nTitle\n=====\n\n    code\n\n***\n\n<!-- {enable} -->\n\n";
        var input = region + "Other\n=====\n\n    other\n\n***\n";

        // Act: format with the production formatters
        var formatted = BlockFormatterAssertions.AssertSafeAndStable(input);
        var (result, _) = BlockFormatterAssertions.Apply(BlockFormatterAssertions.ProductionFormatter(), input);

        // Assert: the region is unchanged, the rest is canonical, and no violation lies inside the region
        Assert.Equal(region + "# Other\n\n```\nother\n```\n\n---\n", formatted);
        Assert.All(result.Diagnostics, diagnostic => Assert.True(diagnostic.Line >= 12, $"{diagnostic.RuleId} at line {diagnostic.Line}"));
        Assert.NotEmpty(result.Diagnostics);
    }

    /// <summary>
    ///     Test that a block with a rule disabled only for the line after a next-line directive stays as written, while the other blocks are formatted.
    /// </summary>
    [Fact]
    public void BlocksSubsystem_Format_SuppressedWithNextLineDirective_KeepsOnlyThatBlock()
    {
        // Arrange: a closed heading whose own line is covered by a directive, and another closed heading
        const string input = "<!-- markdownlint-disable-next-line MD021 -->\n# One #\n\n# Two #\n";

        // Act & Assert: only the second heading loses its closing sequence
        Assert.Equal("<!-- markdownlint-disable-next-line MD021 -->\n# One #\n\n# Two\n", BlockFormatterAssertions.AssertSafeAndStable(input));
    }

    /// <summary>
    ///     Test that the blanks after the number signs of a heading become one space, and that text without a space after
    ///     the number signs, which is a paragraph, is kept.
    /// </summary>
    [Theory]
    [InlineData("#   Title\n", "# Title\n")]
    [InlineData("##\tTitle\n", "## Title\n")]
    [InlineData("text\n\n###    Spaced   out\n", "text\n\n### Spaced   out\n")]
    [InlineData("#Title\n", "#Title\n")]
    [InlineData("##Title##\n", "##Title##\n")]
    [InlineData("#hashtag and text\n", "#hashtag and text\n")]
    public void BlocksSubsystem_Format_BlanksAfterNumberSigns_BecomeOneSpaceAndMissingSpaceIsKept(string input, string expected) =>
        BlockFormatterAssertions.AssertFormats(input, expected);

    /// <summary>
    ///     Test that the indentation of a heading outside a quote or list is removed and that of a heading inside one is kept.
    /// </summary>
    [Theory]
    [InlineData(" # Title\n", "# Title\n")]
    [InlineData("text\n\n   ## Title\n", "text\n\n## Title\n")]
    [InlineData("> # Title\n", "> # Title\n")]
    [InlineData("- # Title\n", "- # Title\n")]
    [InlineData("-   # Title\n", "- # Title\n")]
    public void BlocksSubsystem_Format_HeadingIndentation_IsRemovedOnlyOutsideContainers(string input, string expected) =>
        BlockFormatterAssertions.AssertFormats(input, expected);

    /// <summary>
    ///     Test that the trailing whitespace of headings is removed, a backslash before it is kept, and a hard break inside a setext heading stays.
    /// </summary>
    [Theory]
    [InlineData("# Title   \n", "# Title\n")]
    [InlineData("Title  \n===\n", "# Title\n")]
    [InlineData("## Title \\ \n", "## Title \\\n")]
    [InlineData("One  \ntwo   \n===  \n", "One  \ntwo\n===\n")]
    [InlineData("One\ntwo \\  \n===\n", "One\ntwo \\  \n===\n")]
    public void BlocksSubsystem_Format_HeadingTrailingWhitespace_IsRemoved(string input, string expected) =>
        BlockFormatterAssertions.AssertFormats(input, expected);

    /// <summary>
    ///     Test that the trailing whitespace of the two fence lines is removed and that of the code between them is kept.
    /// </summary>
    [Fact]
    public void BlocksSubsystem_Format_FenceLineTrailingWhitespace_IsRemovedAndContentKept() =>
        BlockFormatterAssertions.AssertFormats("~~~js   \n  code  \n\ttab\t\n~~~   \n", "```js\n  code  \n\ttab\t\n```\n");

    /// <summary>
    ///     Test that the trailing whitespace of thematic breaks is removed, also where the characters of the break stay as written.
    /// </summary>
    [Theory]
    [InlineData("text\n\n***   \n", "text\n\n---\n")]
    [InlineData("text\n***  \n", "text\n***\n")]
    [InlineData("text\n\n- ***\t\n", "text\n\n- ***\n")]
    [InlineData("text\n\n+ ***  \n", "text\n\n+ ***\n")]
    public void BlocksSubsystem_Format_BreakTrailingWhitespace_IsRemoved(string input, string expected) =>
        BlockFormatterAssertions.AssertFormats(input, expected);

    /// <summary>
    ///     Test that a break after a plus sign list marker is kept when dashes are wanted, because the dashes would look like a grid table border.
    /// </summary>
    [Fact]
    public void BlocksSubsystem_Format_BreakAfterPlusListMarker_StaysAsWritten() =>
        BlockFormatterAssertions.AssertFormats("text\n\n+ ***\n", "text\n\n+ ***\n");

    /// <summary>
    ///     Test that an indented block that ends with a whitespace-only line wider than its indentation keeps the rest of that line in its fence.
    /// </summary>
    [Fact]
    public void BlocksSubsystem_Format_IndentedCodeEndingWithWideWhitespaceLine_KeepsItsContent() =>
        BlockFormatterAssertions.AssertFormats("x\n\n    code\n      \n", "x\n\n```\ncode\n  \n```\n");

    /// <summary>
    ///     Test that a next-line suppression directive keeps its place directly above the block it covers, because no blank line is added between them.
    /// </summary>
    [Theory]
    [InlineData("<!-- markdownlint-disable-next-line MD019 -->\n#  Heading  one\n")]
    [InlineData("Intro\n\n<!-- markdownlint-disable-next-line MD009 -->\n- a  \n- b\n")]
    [InlineData("Intro\n\n<!-- lintmark-disable-next-line trailing-whitespace -->\n- a   \n- b\n")]
    public void BlocksSubsystem_Format_NextLineDirectiveBeforeBlock_KeepsTheDirectiveWithItsLine(string input) =>
        Assert.Equal(input, BlockFormatterAssertions.AssertSafeAndStable(input));

    /// <summary>
    ///     Test that a heading with a tab after its number signs, on a later line of the document, is formatted safely and stably.
    /// </summary>
    [Theory]
    [InlineData("x\n\n#\tTitle\n", "x\n\n# Title\n")]
    [InlineData("para\n#\tTab\ttitle", "para\n\n# Tab   title\n")]
    public void BlocksSubsystem_Format_TabAfterNumberSigns_IsHandledSafely(string input, string expected) =>
        BlockFormatterAssertions.AssertFormats(input, expected);

    /// <summary>
    ///     Test that blocks whose lines do not have the shape the parser reported (a heading whose inline spans do not
    ///     fit its line) are left alone as a whole, with the document passes still working around them.
    /// </summary>
    [Fact]
    public void BlocksSubsystem_Format_BlocksWithUnexpectedShape_AreLeftAlone()
    {
        // Arrange: a heading with blanks and a tab in its text whose first inline span is moved
        var document = BlockFormatterAssertions.Parse("x\n\n#   a\tb\n");
        var heading = (Markdig.Syntax.HeadingBlock)BlockFormatterAssertions.FirstBlock(document, BlockKind.Heading).Node!;
        var first = heading.Inline!.FirstChild!;
        first.Span = new Markdig.Syntax.SourceSpan(first.Span.Start + 1, first.Span.End);

        // Act: format with the production formatters
        var result = BlockFormatterAssertions.ProductionFormatter().Format(document, LintMarkConfig.Default);

        // Assert: the heading has no edit at all
        Assert.Empty(result.Edits);
        Assert.Empty(result.Diagnostics);
    }

    /// <summary>
    ///     Test that formatting the same parsed document on many threads that start together, with one shared formatter, gives the same result.
    /// </summary>
    [Fact]
    public void BlocksSubsystem_Format_SameDocumentOnManyThreads_GivesTheSameResult()
    {
        // Arrange: one shared formatter, as the engine uses it, and one parsed document shared by every thread
        const int threadCount = 8;
        var formatter = BlockFormatterAssertions.ProductionFormatter();
        var document = BlockFormatterAssertions.Parse(DirtyDocument);
        var reference = formatter.Format(document, LintMarkConfig.Default);
        Assert.Equal(CleanDocument, SourcePatcher.Apply(document.Source, reference.Edits));
        var results = new FormatResult[threadCount];

        // Act: every thread formats the document many times after the common start signal
        BlockFormatterAssertions.RunConcurrently(
            threadCount,
            index =>
            {
                for (var round = 0; round < 25; round++)
                {
                    results[index] = formatter.Format(document, LintMarkConfig.Default);
                    Assert.Equal(reference.Edits, results[index].Edits);
                }
            });

        // Assert: every thread computed the edits and the violations of a single call
        Assert.All(results, result =>
        {
            Assert.Equal(reference.Edits, result.Edits);
            Assert.Equal(reference.Diagnostics, result.Diagnostics);
        });
    }

    /// <summary>
    ///     Test that formatting the result of a rewrite again changes nothing the second time.
    /// </summary>
    [Fact]
    public void BlocksSubsystem_Format_FormattedTwice_ChangesNothingTheSecondTime()
    {
        // Arrange: the dirty document, formatted once
        var once = BlockFormatterAssertions.AssertSafeAndStable(DirtyDocument);

        // Act: format the result again
        var (result, twice) = BlockFormatterAssertions.Apply(BlockFormatterAssertions.ProductionFormatter(), once);

        // Assert: nothing changes
        Assert.Equal(once, twice);
        Assert.Empty(result.Edits);
    }

    /// <summary>
    ///     Test that lists are written in the canonical form: one marker character, counting numbers, aligned items,
    ///     nested bullets indented two columns and one space after each marker.
    /// </summary>
    [Theory]
    [InlineData("* a\n* b\n", "- a\n- b\n")]
    [InlineData("1. a\n1. b\n", "1. a\n2. b\n")]
    [InlineData("  - a\n  - b\n", "- a\n- b\n")]
    [InlineData("- a\n    - b\n", "- a\n  - b\n")]
    [InlineData("-   a\n    b\n", "- a\n  b\n")]
    [InlineData("1.  a\n    - b\n", "1. a\n   - b\n")]
    [InlineData("* ---\n", "* ---\n")]
    public void BlocksSubsystem_Format_ListForms_AreCanonical(string input, string expected) =>
        BlockFormatterAssertions.AssertFormats(input, expected);

    /// <summary>
    ///     Test that block quotes are written with one space after each marker.
    /// </summary>
    [Theory]
    [InlineData(">  a\n", "> a\n")]
    [InlineData(">   # h\n", "> # h\n")]
    [InlineData(">  >  a\n", "> > a\n")]
    public void BlocksSubsystem_Format_QuoteForms_AreCanonical(string input, string expected) =>
        BlockFormatterAssertions.AssertFormats(input, expected);

    /// <summary>
    ///     Test that two lists are never merged into one and a list is never split, whichever markers are configured.
    /// </summary>
    [Theory]
    [InlineData("Dash")]
    [InlineData("Asterisk")]
    [InlineData("Plus")]
    public void BlocksSubsystem_Format_BulletLists_AreNeverMergedOrSplit(string bullet)
    {
        // Arrange: two lists that are only two lists because their markers differ, and a list with a nested list
        var config = LintMarkConfig.Default with { Bullet = Enum.Parse<BulletMarker>(bullet) };
        const string input = "- a\n\n* b\n\n+ c\n\n- d\n  * e\n  * f\n";

        // Act: format and check the result renders the same and keeps its list structure
        var formatted = BlockFormatterAssertions.AssertSafeAndStable(input, config);

        // Assert: the four lists at the margin are still four lists
        var lists = BlockFormatterAssertions.Parse(formatted).Blocks.Count(block => block.Kind == BlockKind.List && block.Parent is null);
        Assert.Equal(4, lists);
    }

    /// <summary>
    ///     Test that the structure of a list is kept: lazy lines, tabs and indented code stay as written, and code,
    ///     quotes and tables inside items move with them.
    /// </summary>
    [Theory]
    [InlineData("-   a\n  lazy\n", "-   a\n  lazy\n")]
    [InlineData("-\ta\n", "-\ta\n")]
    [InlineData("-     code\n", "-     code\n")]
    [InlineData(
        "-   a\n\n    ```\n    code\n    ```\n\n    > quote\n\n    | a | b |\n    |---|---|\n",
        "- a\n\n  ```\n  code\n  ```\n\n  > quote\n\n  | a | b |\n  |---|---|\n")]
    [InlineData("1. a\n   - b\n     1. c\n        text\n", "1. a\n   - b\n     1. c\n        text\n")]
    [InlineData("1. a\n   - b\n", "1. a\n   - b\n")]
    [InlineData("- a\n\n  b\n\n- c\n", "- a\n\n  b\n\n- c\n")]
    [InlineData("> -   a\n> -   b\n", "> -   a\n> -   b\n")]
    [InlineData("x[^n]\n\n[^n]: text\n\n    * a\n", "x[^n]\n\n[^n]: text\n\n    * a\n")]
    public void BlocksSubsystem_Format_ListStructure_IsKept(string input, string expected) =>
        BlockFormatterAssertions.AssertFormats(input, expected);

    /// <summary>
    ///     Test that the content of a block quote is kept: code, HTML, fences, lists, setext headings and tables keep their spaces.
    /// </summary>
    [Theory]
    [InlineData(">  ```\n>  code\n>  ```\n")]
    [InlineData(">  <div>\n>  html\n>  </div>\n")]
    [InlineData(">  - a\n")]
    [InlineData(">  Title\n>  ===\n")]
    [InlineData(">      code\n")]
    [InlineData("> a\n>     b\n")]
    [InlineData(">\ttext\n")]
    [InlineData("> a\nlazy\n")]
    [InlineData("- >  quote\n")]
    [InlineData(">  [^n]: note\n")]
    [InlineData(">  $$\n>  x = 1\n>  $$\n")]
    public void BlocksSubsystem_Format_QuoteContent_IsKept(string input) =>
        BlockFormatterAssertions.AssertFormats(input, input);

    /// <summary>
    ///     Test that formatting a document of lists and quotes again changes nothing the second time, in every line ending.
    /// </summary>
    [Fact]
    public void BlocksSubsystem_Format_ListsAndQuotes_ChangeNothingTheSecondTime()
    {
        // Arrange: lists and quotes that break the list and quote rules
        const string dirty = "* a\n  +   b\n1. c\n1. d\n\n>  >  e\n\n-   f\n\n    g\n";

        // Act: format, and format the result again, also as it is stored with other line endings
        var once = BlockFormatterAssertions.AssertSafeAndStable(dirty);
        BlockFormatterAssertions.AssertFormatsOnDisk(dirty, once);
        var (result, twice) = BlockFormatterAssertions.Apply(BlockFormatterAssertions.ProductionFormatter(), once);

        // Assert: nothing changes
        Assert.Equal(once, twice);
        Assert.Empty(result.Edits);
    }

    /// <summary>
    ///     Test that a list and a quote are left as written when the rule that owns them is suppressed, by identifier or by alias.
    /// </summary>
    [Theory]
    [InlineData("lintmark-disable bullet", "* a\n")]
    [InlineData("markdownlint-disable MD004", "* a\n")]
    [InlineData("markdownlint-disable MD027", ">  a\n")]
    [InlineData("lintmark-disable blockquote-space", ">  a\n")]
    public void BlocksSubsystem_Format_ListOrQuoteRuleSuppressed_LeavesThem(string directive, string block)
    {
        // Arrange: a block inside a region that disables its rule
        var text = $"<!-- {directive} -->\n\n{block}";

        // Act: format
        var (result, formatted) = BlockFormatterAssertions.Apply(BlockFormatterAssertions.ProductionFormatter(), text);

        // Assert: nothing changes and nothing is reported
        Assert.Equal(text, formatted);
        Assert.Empty(result.Edits);
        Assert.Empty(result.Diagnostics);
    }
}
