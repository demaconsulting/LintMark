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
        + "~~~js\ntilde\n~~~\n    indented code\nSome\ttabbed text";

    /// <summary>
    ///     The canonical form of <see cref="DirtyDocument"/>.
    /// </summary>
    private const string CleanDocument =
        "# Title\n\nIntro text with trailing space\n\n- one\n- two\n\n## Closed\n\n```\ncode\n```\n\n   ---\n\n# Spaced\n\n"
        + "```js\ntilde\n```\n\n```\nindented code\n```\n\nSome    tabbed text\n";

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
    ///     Test that every violation of the dirty document is reported, each under the rule it breaks.
    /// </summary>
    [Fact]
    public void BlocksSubsystem_Format_DocumentBreakingEveryRule_ReportsEveryRuleOnce()
    {
        // Act: format the dirty document
        var (result, _) = BlockFormatterAssertions.Apply(BlockFormatterAssertions.ProductionFormatter(), DirtyDocument);

        // Assert: all fourteen rules are reported
        var reported = result.Diagnostics.Select(diagnostic => diagnostic.RuleId).Distinct().Order(StringComparer.Ordinal).ToList();
        Assert.Equal(
            [
                "code-block-style",
                "code-fence",
                "fence-blank-lines",
                "final-newline",
                "hard-tabs",
                "heading-blank-lines",
                "heading-space",
                "heading-start-left",
                "heading-style",
                "list-blank-lines",
                "max-blank-lines",
                "thematic-break",
                "trailing-whitespace"
            ],
            reported);
    }

    /// <summary>
    ///     Test that the dirty document is rewritten the same way with CRLF line endings and a byte order mark.
    /// </summary>
    [Fact]
    public void BlocksSubsystem_Format_DirtyDocumentWithCrlfAndBom_KeepsLineEndingsAndMark() =>
        BlockFormatterAssertions.AssertFormatsOnDisk(DirtyDocument, CleanDocument);

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
    [Fact]
    public void BlocksSubsystem_Format_ConfiguredSettings_SelectTheCanonicalForm()
    {
        // Arrange: tildes, asterisks, two blank lines allowed, no final newline
        var config = LintMarkConfig.Default with
        {
            CodeFence = FenceMarker.Tilde,
            ThematicBreak = ThematicBreakStyle.Asterisks,
            MaxBlankLines = 2,
            FinalNewline = false
        };

        // Act & Assert: the document follows every setting
        BlockFormatterAssertions.AssertFormats(
            "text\n\n\n\n```\ncode\n```\n\n---\n\n    indented\n\n",
            "text\n\n\n~~~\ncode\n~~~\n\n***\n\n~~~\nindented\n~~~",
            config);
    }

    /// <summary>
    ///     Test that formatting the same document on many threads gives the same result.
    /// </summary>
    [Fact]
    public void BlocksSubsystem_Format_SameDocumentOnManyThreads_GivesTheSameResult()
    {
        // Arrange: one shared formatter, as the engine uses it
        var formatter = BlockFormatterAssertions.ProductionFormatter();

        // Act: format the dirty document on several threads
        var results = Enumerable.Range(0, 16).AsParallel().Select(_ => BlockFormatterAssertions.Apply(formatter, DirtyDocument).Formatted).ToList();

        // Assert: every thread computed the canonical document
        Assert.All(results, result => Assert.Equal(CleanDocument, result));
    }

    /// <summary>
    ///     Test that the rewrite of the repository's own style guide samples stays stable when applied in sequence.
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
}
