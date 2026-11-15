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

using DemaConsulting.LintMark.Configuration;
using DemaConsulting.LintMark.Formatting;
using DemaConsulting.LintMark.Formatting.Blocks;
using DemaConsulting.LintMark.Formatting.Inline;
using DemaConsulting.LintMark.Linting;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Test that the link refactoring converts long inline links to reference links, keeps the definitions in one block
///     at the end of the document, and leaves everything that it is not certain about as it is.
/// </summary>
public class LinkRefactorerTests
{
    /// <summary>
    ///     A long destination used by many tests (74 columns with the parentheses).
    /// </summary>
    private const string LongUrl = "https://example.com/some/very/long/path/to/a/page/that/goes/on";

    /// <summary>
    ///     Creates a configuration with the link refactoring on.
    /// </summary>
    /// <param name="width">The line length.</param>
    /// <param name="threshold">The width of a link that is converted.</param>
    /// <returns>The configuration.</returns>
    private static LintMarkConfig On(int width = 120, int threshold = 60) =>
        LintMarkConfig.Default with
        {
            LineLength = width,
            LinkRefactor = LinkRefactorOptions.Disabled with { Enabled = true, Threshold = threshold }
        };

    /// <summary>
    ///     Formats text with the shipped formatters and checks that the rewrite is safe and stable.
    /// </summary>
    /// <param name="text">The LF-normalized text.</param>
    /// <param name="config">The configuration, or <see langword="null"/> for the link refactoring switched on.</param>
    /// <returns>The formatted text.</returns>
    private static string Format(string text, LintMarkConfig? config = null) =>
        BlockFormatterAssertions.AssertSafeAndStable(text, config ?? On());

    /// <summary>
    ///     Test that nothing is converted unless the feature is switched on: the text stays byte for byte.
    /// </summary>
    [Fact]
    public void LinkRefactorer_Format_DisabledByDefault_LeavesLongLinksAsTheyAre()
    {
        // Arrange: a link that is far wider than the threshold, formatted with the default configuration
        var text = $"A [long link text that explains things]({LongUrl} \"A title\") in text.\n";

        // Act / Assert: the default configuration does not convert it
        Assert.Equal(text, BlockFormatterAssertions.AssertSafeAndStable(text, LintMarkConfig.Default with { LineLength = 200 }));
    }

    /// <summary>
    ///     Test that a long inline link becomes a reference link and its definition is written at the end of the document.
    /// </summary>
    [Fact]
    public void LinkRefactorer_Format_LongInlineLink_BecomesReferenceWithDefinitionAtTheEnd()
    {
        // Act: format a paragraph with a long link and a short one
        var formatted = Format($"A [long link text]({LongUrl} \"A title\") and [short](https://a.b) one.\n");

        // Assert: the long link is converted with its title, the short link stays, and the block follows one blank line
        Assert.Equal(
            "A [long link text][long-link-text] and [short](https://a.b) one.\n\n"
            + "<!-- Link References -->\n"
            + $"[long-link-text]: {LongUrl} \"A title\"\n",
            formatted);
    }

    /// <summary>
    ///     Test that two links to the same destination and title share one definition, written once.
    /// </summary>
    [Fact]
    public void LinkRefactorer_Format_SameDestinationTwice_ShareOneDefinition()
    {
        // Act: two long links with different texts and the same destination
        var formatted = Format($"A [first link text]({LongUrl}) and a [second link text]({LongUrl}) here.\n");

        // Assert: the label of the first link is used twice, and the destination is defined once
        Assert.Equal(
            "A [first link text][first-link-text] and a [second link text][first-link-text] here.\n\n"
            + "<!-- Link References -->\n"
            + $"[first-link-text]: {LongUrl}\n",
            formatted);
    }

    /// <summary>
    ///     Test that the same destination with another title is another definition.
    /// </summary>
    [Fact]
    public void LinkRefactorer_Format_SameDestinationOtherTitle_GetsAnotherDefinition()
    {
        // Act: two links that differ only by their title
        var formatted = Format($"A [first link]({LongUrl} \"One\") and a [first link]({LongUrl} \"Two\") here.\n", On(threshold: 20));

        // Assert: two definitions, the second with a number
        Assert.Equal(
            "A [first link][first-link] and a [first link][first-link-2] here.\n\n"
            + "<!-- Link References -->\n"
            + $"[first-link]: {LongUrl} \"One\"\n"
            + $"[first-link-2]: {LongUrl} \"Two\"\n",
            formatted);
    }

    /// <summary>
    ///     Test that a label that is taken by another destination gets the first free number.
    /// </summary>
    [Fact]
    public void LinkRefactorer_Format_LabelTakenByOtherDestination_GetsTheFirstFreeNumber()
    {
        // Act: three links with the same text and three destinations
        var formatted = Format(
            "A [Docs](https://example.com/one/aaaaaaaaaaaa), [Docs](https://example.com/two/bbbbbbbbbbbb) and [Docs](https://example.com/three/cccccccccc).\n",
            On(threshold: 20));

        // Assert: docs, docs-2 and docs-3, sorted
        Assert.Equal(
            "A [Docs][docs], [Docs][docs-2] and [Docs][docs-3].\n\n"
            + "<!-- Link References -->\n"
            + "[docs]: https://example.com/one/aaaaaaaaaaaa\n"
            + "[docs-2]: https://example.com/two/bbbbbbbbbbbb\n"
            + "[docs-3]: https://example.com/three/cccccccccc\n",
            formatted);
    }

    /// <summary>
    ///     Test that a bracketed text elsewhere in the document keeps its meaning: a label equal to it is never used.
    /// </summary>
    [Fact]
    public void LinkRefactorer_Format_LabelEqualToBracketedTextElsewhere_IsAvoided()
    {
        // Act: a literal [docs] in the text, and a link whose text gives the label docs
        var formatted = Format($"See [docs] in text.\n\n[Docs]({LongUrl}) end.\n", On(threshold: 20));

        // Assert: the label is docs-2, so the literal text is still not a link
        Assert.Equal(
            "See [docs] in text.\n\n[Docs][docs-2] end.\n\n"
            + "<!-- Link References -->\n"
            + $"[docs-2]: {LongUrl}\n",
            formatted);
    }

    /// <summary>
    ///     Test that an existing definition with the same destination is used and nothing is added.
    /// </summary>
    [Fact]
    public void LinkRefactorer_Format_ExistingDefinitionWithSameDestination_IsReused()
    {
        // Act: a definition and a long link to the same place
        var formatted = Format($"A [long link text]({LongUrl}) here.\n\n[home]: {LongUrl}\n");

        // Assert: the link uses the label of the definition and no block is added
        Assert.Equal($"A [long link text][home] here.\n\n[home]: {LongUrl}\n", formatted);
    }

    /// <summary>
    ///     Test that an existing definition with another title is not used.
    /// </summary>
    [Fact]
    public void LinkRefactorer_Format_ExistingDefinitionWithOtherTitle_IsNotReused()
    {
        // Act: the existing definition has a title, the link has none
        var formatted = Format($"A [home page]({LongUrl}) here.\n\n[home]: {LongUrl} \"Home\"\n", On(threshold: 20));

        // Assert: a new definition is made and the existing one is untouched
        Assert.Equal(
            "A [home page][home-page] here.\n\n"
            + $"[home]: {LongUrl} \"Home\"\n\n"
            + "<!-- Link References -->\n"
            + $"[home-page]: {LongUrl}\n",
            formatted);
    }

    /// <summary>
    ///     Test that an existing badge block stays where it is and the new block follows it, separated by a blank line.
    /// </summary>
    [Fact]
    public void LinkRefactorer_Format_ExistingBadgeBlock_IsLeftAndTheNewBlockFollowsIt()
    {
        // Act: a document that ends with a badge reference block
        var formatted = Format($"A [long link text]({LongUrl}) here.\n\n<!-- Badge References -->\n[badge]: https://example.com/badge\n");

        // Assert: the badge block is unchanged and a separate block comes after it
        Assert.Equal(
            "A [long link text][long-link-text] here.\n\n"
            + "<!-- Badge References -->\n[badge]: https://example.com/badge\n\n"
            + "<!-- Link References -->\n"
            + $"[long-link-text]: {LongUrl}\n",
            formatted);
    }

    /// <summary>
    ///     Test that new definitions are added after the last definition of an existing link references block, which keeps its order.
    /// </summary>
    [Fact]
    public void LinkRefactorer_Format_ExistingLinkReferencesBlock_GetsTheNewDefinitionsAfterItsLastDefinition()
    {
        // Act: an existing block whose definitions are not sorted, followed by more text
        var formatted = Format($"A [long link text]({LongUrl}) here.\n\n<!-- Link References -->\n[b]: https://example.com/b\n[a]: https://example.com/a\n\nMore text.\n");

        // Assert: the existing lines stay as they are and the new one follows them
        Assert.Equal(
            "A [long link text][long-link-text] here.\n\n"
            + "<!-- Link References -->\n[b]: https://example.com/b\n[a]: https://example.com/a\n"
            + $"[long-link-text]: {LongUrl}\n\nMore text.\n",
            formatted);
    }

    /// <summary>
    ///     Test that the marker of the configuration is used.
    /// </summary>
    [Fact]
    public void LinkRefactorer_Format_ConfiguredMarker_IsWritten()
    {
        // Arrange: a configuration with its own marker
        var config = On() with { LinkRefactor = On().LinkRefactor with { DefinitionsMarker = "<!-- Links -->" } };

        // Act
        var formatted = Format($"A [long link text]({LongUrl}) here.\n", config);

        // Assert
        Assert.EndsWith($"\n\n<!-- Links -->\n[long-link-text]: {LongUrl}\n", formatted, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that an image is converted like a link.
    /// </summary>
    [Fact]
    public void LinkRefactorer_Format_Image_BecomesReferenceImage()
    {
        // Act
        var formatted = Format($"![An image of things]({LongUrl}/image.png) after.\n");

        // Assert
        Assert.Equal(
            "![An image of things][an-image-of-things] after.\n\n"
            + "<!-- Link References -->\n"
            + $"[an-image-of-things]: {LongUrl}/image.png\n",
            formatted);
    }

    /// <summary>
    ///     Test that an image inside a link is converted as well as the link, each with its own destination.
    /// </summary>
    [Fact]
    public void LinkRefactorer_Format_BadgeImageInsideLink_ConvertsBothDestinations()
    {
        // Act
        var formatted = Format($"[![Build status](https://img.example.com/badge/build/passing/long/long/long.svg)]({LongUrl})\n", On(threshold: 20));

        // Assert: both parts become references and both are defined
        Assert.Equal(
            "[![Build status][build-status-2]][build-status]\n\n"
            + "<!-- Link References -->\n"
            + $"[build-status]: {LongUrl}\n"
            + "[build-status-2]: https://img.example.com/badge/build/passing/long/long/long.svg\n",
            formatted);
    }

    /// <summary>
    ///     Test that titles in quotes, apostrophes and parentheses and an angle-bracket destination are copied as they are written.
    /// </summary>
    [Fact]
    public void LinkRefactorer_Format_TitleAndDestinationForms_AreCopiedAsWritten()
    {
        // Act: one link per form
        var formatted = Format(
            "[One](https://example.com/aaaaaaaaaaaaaaaa 'single')\n\n"
            + "[Two](https://example.com/bbbbbbbbbbbbbbbbbb (paren))\n\n"
            + "[Three](<https://example.com/c c c cccccccc> \"angle\")\n",
            On(threshold: 20));

        // Assert: the definitions are sorted and keep their forms
        Assert.Equal(
            "[One][one]\n\n[Two][two]\n\n[Three][three]\n\n"
            + "<!-- Link References -->\n"
            + "[one]: https://example.com/aaaaaaaaaaaaaaaa 'single'\n"
            + "[three]: <https://example.com/c c c cccccccc> \"angle\"\n"
            + "[two]: https://example.com/bbbbbbbbbbbbbbbbbb (paren)\n",
            formatted);
    }

    /// <summary>
    ///     Test that the text of a link, with brackets, emphasis and code, stays byte for byte.
    /// </summary>
    [Fact]
    public void LinkRefactorer_Format_LinkTextWithBracketsEmphasisAndCode_IsKept()
    {
        // Act
        var formatted = Format($"A [a [b] *c* `d` e]({LongUrl}) here.\n");

        // Assert: only the destination part changes
        Assert.Equal(
            "A [a [b] *c* `d` e][a-b-c-d-e] here.\n\n"
            + "<!-- Link References -->\n"
            + $"[a-b-c-d-e]: {LongUrl}\n",
            formatted);
    }

    /// <summary>
    ///     Test that a text without letters or digits gets a label from the host and path of the destination.
    /// </summary>
    [Fact]
    public void LinkRefactorer_Format_TextWithoutLettersOrDigits_UsesTheDestinationForTheLabel()
    {
        // Act
        var formatted = Format($"See [***]({LongUrl}) here.\n", On(threshold: 10));

        // Assert: host and first path segment
        Assert.Contains("[example-com-some]: ", formatted, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a link below the threshold, a reference link, an autolink and a shortcut are not touched.
    /// </summary>
    [Fact]
    public void LinkRefactorer_Format_ShortAndNonInlineLinks_AreNotTouched()
    {
        // Arrange: only forms the refactoring must leave alone
        var text = $"[short](https://a.b), [long text here][ref], <{LongUrl}> and a [shortcut].\n\n[ref]: {LongUrl}\n[shortcut]: https://example.com/s\n";

        // Act / Assert
        Assert.Equal(text, Format(text, On(width: 200)));
    }

    /// <summary>
    ///     Test that links in a heading, a code span, a code block and a footnote are not touched.
    /// </summary>
    [Theory]
    [InlineData("# A [long heading link](https://example.com/some/very/long/path/to/a/page/that/goes/on)\n")]
    [InlineData("A `[a](https://example.com/some/very/long/path/to/a/page/that/goes/on)` span.\n")]
    [InlineData("```text\n[a](https://example.com/some/very/long/path/to/a/page/that/goes/on)\n```\n")]
    [InlineData("<div>[a](https://example.com/some/very/long/path/to/a/page/that/goes/on)</div>\n")]
    [InlineData("Text[^1].\n\n[^1]: A [long link](https://example.com/some/very/long/path/to/a/page/that/goes/on) in a note.\n")]
    public void LinkRefactorer_Format_LinkInHeadingCodeHtmlOrFootnote_IsNotTouched(string text) =>
        Assert.Equal(text, Format(text, On(threshold: 20)));

    /// <summary>
    ///     Test that a destination that is not read with certainty (a backslash, an entity, a line break) is left as written.
    /// </summary>
    [Theory]
    [InlineData("A [a](https://example.com/some/very/long/path/to/a/page/that/goes\\_on/and/on) here.\n")]
    [InlineData("A [a](https://example.com/some/very/long/path?a=1&amp;b=2&c=3-long-long-long-long-long) here.\n")]
    [InlineData("A [a](\nhttps://example.com/some/very/long/path/to/a/page/that/goes/on/and/on) here.\n")]
    [InlineData("A [a](https://example.com/some/very/long/path/to/a/page/that/goes/on \"a \\\" b\") here.\n")]
    public void LinkRefactorer_Format_DestinationNotReadWithCertainty_IsLeftAsWritten(string text) =>
        Assert.Equal(text, Format(text, On(threshold: 20)));

    /// <summary>
    ///     Test that the text of a link may span lines and the link is still converted.
    /// </summary>
    [Fact]
    public void LinkRefactorer_Format_LinkTextSplitAcrossLines_IsConverted()
    {
        // Act
        var formatted = Format($"A [long link\ntext]({LongUrl}) here.\n");

        // Assert: the lines of the text are kept
        Assert.Equal(
            "A [long link\ntext][long-link-text] here.\n\n"
            + "<!-- Link References -->\n"
            + $"[long-link-text]: {LongUrl}\n",
            formatted);
    }

    /// <summary>
    ///     Test that links in list items and block quotes, nested, are converted without touching the container markers.
    /// </summary>
    [Fact]
    public void LinkRefactorer_Format_ListsAndQuotes_AreConverted()
    {
        // Act
        var formatted = Format(
            $"- Item with [a long link text]({LongUrl}) here.\n  - Nested [another one](https://example.com/n/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa) there.\n\n"
            + $"> Quote with [yet another long link]({LongUrl}) and\n> > a nested [deep link](https://example.com/q/bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb) here.\n");

        // Assert
        Assert.Equal(
            "- Item with [a long link text][a-long-link-text] here.\n  - Nested [another one][another-one] there.\n\n"
            + "> Quote with [yet another long link][a-long-link-text] and\n> > a nested [deep link][deep-link] here.\n\n"
            + "<!-- Link References -->\n"
            + $"[a-long-link-text]: {LongUrl}\n"
            + "[another-one]: https://example.com/n/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\n"
            + "[deep-link]: https://example.com/q/bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\n",
            formatted);
    }

    /// <summary>
    ///     Test that the conversion happens before the wrap, so that the wrap works on the final text.
    /// </summary>
    [Fact]
    public void LinkRefactorer_Format_LongLinkInOverlongParagraph_IsConvertedBeforeTheWrap()
    {
        // Act: a paragraph that is too wide only because of the destination
        var formatted = Format($"This is a paragraph with a [long link text]({LongUrl} \"A title\") in the middle of the text and more words after it.\n", On(width: 80));

        // Assert: the wrap sees the short reference and breaks at 80 columns
        Assert.Equal(
            "This is a paragraph with a [long link text][long-link-text] in the middle of the\n"
            + "text and more words after it.\n\n"
            + "<!-- Link References -->\n"
            + $"[long-link-text]: {LongUrl} \"A title\"\n",
            formatted);
    }

    /// <summary>
    ///     Test that a table row that is too wide in the compact layout gets its links converted, and the others keep theirs.
    /// </summary>
    [Fact]
    public void LinkRefactorer_Format_TableRowTooWide_ConvertsTheLinksOfThatRowOnly()
    {
        // Act
        var formatted = Format(
            $"| Name | Link |\n| --- | --- |\n| a | [docs]({LongUrl}) |\n| b | [x](https://s.io) |\n",
            On(width: 60));

        // Assert: the wide row is short, the other row is untouched, and the definition is at the end
        Assert.Equal(
            "| Name | Link |\n| --- | --- |\n| a | [docs][docs] |\n| b | [x](https://s.io) |\n\n"
            + "<!-- Link References -->\n"
            + $"[docs]: {LongUrl}\n",
            formatted);
    }

    /// <summary>
    ///     Test that a table that is laid out aligned after its links were converted is already in that layout after one run.
    /// </summary>
    [Fact]
    public void LinkRefactorer_Format_AlignedTableThatFitsAfterConversion_IsAlignedInOneRun()
    {
        // Arrange: an aligned layout is wanted, and only the long link keeps the table from fitting
        var config = On(width: 60) with { Tables = TableStyle.Aligned };

        // Act: the safety assertions include that a second run changes nothing
        var formatted = Format($"| Name | Link |\n| --- | --- |\n| a | [docs]({LongUrl}) |\n", config);

        // Assert: the link is converted and the cells are padded to one width
        Assert.Contains("| a    | [docs][docs] |\n", formatted, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a table is left as it is when the conversion would not make its row fit, or when tables are excluded.
    /// </summary>
    [Fact]
    public void LinkRefactorer_Format_TableThatCannotFitOrIsExcluded_IsLeftAsWritten()
    {
        // Arrange
        var text = $"| Name | Link |\n| --- | --- |\n| a | [docs]({LongUrl}) |\n";

        // Act / Assert: with a width that even the short form does not fit, and with tables excluded
        Assert.Equal(text, Format(text, On(width: 12)));
        Assert.Equal(text, Format(text, On(width: 60) with { LinkRefactor = On().LinkRefactor with { InTables = false } }));
    }

    /// <summary>
    ///     Test that a suppression comment keeps the links of its paragraph, in the lintmark and the markdownlint form.
    /// </summary>
    [Fact]
    public void LinkRefactorer_Format_SuppressedParagraph_IsLeftAndTheNextOneIsConverted()
    {
        // Arrange: the first paragraph is suppressed by the markdownlint alias of the rule
        var text = $"<!-- markdownlint-disable-next-line MD054 -->\nA [kept link text]({LongUrl}) here.\n\nA [changed link text]({LongUrl}/2) here.\n";

        // Act
        var formatted = Format(text);

        // Assert
        Assert.Equal(
            $"<!-- markdownlint-disable-next-line MD054 -->\nA [kept link text]({LongUrl}) here.\n\nA [changed link text][changed-link-text] here.\n\n"
            + "<!-- Link References -->\n"
            + $"[changed-link-text]: {LongUrl}/2\n",
            formatted);
    }

    /// <summary>
    ///     Test that the rule switch turns the feature off even when it is enabled.
    /// </summary>
    [Fact]
    public void LinkRefactorer_Format_RuleSwitchedOff_LeavesLinksAlone()
    {
        // Arrange
        var text = $"A [long link text]({LongUrl}) here.\n";
        var config = On() with { Rules = On().Rules.SetItem(RuleCatalog.LinkRefactorRuleId, false) };

        // Act / Assert
        Assert.Equal(text, Format(text, config));
    }

    /// <summary>
    ///     Test that front matter is untouched and the definitions follow the last line of the document.
    /// </summary>
    [Fact]
    public void LinkRefactorer_Format_FrontMatter_IsKept()
    {
        // Act
        var formatted = Format($"---\ntitle: A [long](https://x.y/z)\n---\n\nA [long link text]({LongUrl}) here.\n");

        // Assert
        Assert.StartsWith("---\ntitle: A [long](https://x.y/z)\n---\n\nA [long link text][long-link-text] here.\n", formatted, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that brackets before a parenthesis that does not make an inline link count as text that a new definition would
    ///     turn into a link: the label of the converted link steps aside (rule L-1).
    /// </summary>
    [Fact]
    public void LinkRefactorer_Format_BracketsBeforeAnInvalidDestination_AreTakenAsALabel()
    {
        // Arrange: the first brackets have a blank in the destination, so they are text; the second pair is a real link
        var text = $"[X]({{% if a %}}/a) and [X]({LongUrl})\n";

        // Act
        var formatted = Format(text, On(width: 200, threshold: 20));

        // Assert: the label x is taken by the text, so the converted link gets x-2, and the text is not turned into a link
        Assert.Equal($"[X]({{% if a %}}/a) and [X][x-2]\n\n<!-- Link References -->\n[x-2]: {LongUrl}\n", formatted);
    }

    /// <summary>
    ///     Test that brackets that are the text of a link the parser read do not take the label (the usual case).
    /// </summary>
    [Fact]
    public void LinkRefactorer_Format_BracketsOfAnInlineLink_DoNotTakeTheLabel()
    {
        // Act
        var formatted = Format($"[X]({LongUrl}) and ![X]({LongUrl}2)\n", On(width: 200, threshold: 20));

        // Assert: the first link takes x, the image gets x-2
        Assert.StartsWith("[X][x] and ![X][x-2]\n", formatted, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the plan is empty when the document ends where a block cannot be added safely.
    /// </summary>
    [Fact]
    public void LinkRefactorer_Plan_UnclosedFenceAtTheEnd_ChangesNothing()
    {
        // Arrange: the end of the document is inside a code fence that is never closed
        var document = BlockFormatterAssertions.Parse($"A [long link text]({LongUrl}) here.\n\n```text\ncode\n");

        // Act / Assert
        Assert.True(new LinkRefactorer().Plan(document, On()).IsEmpty);
    }

    /// <summary>
    ///     Test that every conversion is reported as a formatting finding at the place of the link.
    /// </summary>
    [Fact]
    public void LinkRefactorer_Format_ConvertedLinks_AreReportedAtTheirPosition()
    {
        // Act
        var (result, _) = BlockFormatterAssertions.Apply(
            BlockFormatterAssertions.ProductionFormatter(),
            $"Text\n\nA [long link text]({LongUrl}) here.\n",
            On());

        // Assert
        var finding = Assert.Single(result.Diagnostics, static diagnostic => diagnostic.RuleId == "link-refactor");
        Assert.Equal(DiagnosticKind.Formatting, finding.Kind);
        Assert.Equal((3, 3), (finding.Line, finding.Column));
    }

    /// <summary>
    ///     Test the labels that are derived from a text, and from the destination when the text gives none.
    /// </summary>
    [Theory]
    [InlineData("Long Link Text", "https://a.b/c", 40, "long-link-text")]
    [InlineData("  --Hello,   World!!  ", "https://a.b/c", 40, "hello-world")]
    [InlineData("\u00DCn\u00EF" + "code T\u00EB" + "xt 2", "https://a.b/c", 40, "\u00FCn\u00EF" + "code-t\u00EB" + "xt-2")]
    [InlineData("alpha beta gamma delta", "https://a.b/c", 12, "alpha-beta")]
    [InlineData("abcdefghijklmnop", "https://a.b/c", 5, "abcde")]
    [InlineData("***", "https://www.example.com/docs/page?x=1", 40, "example-com-docs")]
    [InlineData("***", "docs/guide.md#top", 40, "docs-guide-md")]
    [InlineData("***", "#", 40, "link")]
    public void LinkLabelAllocator_DeriveBase_Text_GivesTheLabel(string text, string url, int max, string expected) =>
        Assert.Equal(expected, LinkLabelAllocator.DeriveBase(text, url, max));

    /// <summary>
    ///     Test that the command line can switch the feature on by naming its rule, and that the alias of the markdownlint rule is known.
    /// </summary>
    [Fact]
    public void RuleOverrides_Apply_EnableLinkRefactor_SwitchesTheFeatureOn()
    {
        // Arrange
        var overrides = RuleOverrides.Create(["MD054"], []);

        // Act
        var config = overrides.Apply(LintMarkConfig.Default);

        // Assert: the alias resolves to the rule, and the feature is on
        Assert.True(config.LinkRefactor.Enabled);
        Assert.True(config.Rules[RuleCatalog.LinkRefactorRuleId]);
        Assert.False(RuleOverrides.Create([], ["link-refactor"]).Apply(LintMarkConfig.Default).Rules[RuleCatalog.LinkRefactorRuleId]);
    }

    /// <summary>
    ///     Test that the text of an inline link may be broken when the option is on, while its destination part stays whole,
    ///     and that the link is one word otherwise.
    /// </summary>
    [Fact]
    public void LinkRefactorer_Format_BreakLinkText_BreaksTheTextAndKeepsTheDestinationWhole()
    {
        // Arrange: a paragraph that is too wide, with a link in it; the link is not converted (threshold above its width)
        const string text = "Words [link text with many words](https://example.com/x) after words.\n";
        var off = LintMarkConfig.Default with { LineLength = 30 };
        var on = off with { LinkRefactor = LinkRefactorOptions.Disabled with { BreakLinkText = true } };

        // Act / Assert: without the option the link is one word, with it the text is filled like any other text
        Assert.Equal("Words\n[link text with many words](https://example.com/x)\nafter words.\n", Format(text, off));
        Assert.Equal("Words [link text with many\nwords](https://example.com/x)\nafter words.\n", Format(text, on));
    }
}
