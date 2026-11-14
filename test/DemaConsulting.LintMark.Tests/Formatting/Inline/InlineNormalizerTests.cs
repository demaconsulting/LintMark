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
using DemaConsulting.LintMark.Document;
using DemaConsulting.LintMark.Formatting;
using DemaConsulting.LintMark.Formatting.Inline;
using DemaConsulting.LintMark.Linting;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Test that the InlineNormalizer class writes emphasis and strong emphasis with the configured delimiter and wraps bare
///     URLs in angle brackets, and that it leaves a paragraph alone when a rewrite could change how it renders.
/// </summary>
public class InlineNormalizerTests
{
    /// <summary>
    ///     The normalizer under test.
    /// </summary>
    private static readonly InlineNormalizer Normalizer = new();

    /// <summary>
    ///     A configuration that writes emphasis and strong emphasis with underscores.
    /// </summary>
    private static readonly LintMarkConfig UnderscoreConfig = LintMarkConfig.Default with
    {
        Emphasis = EmphasisMarker.Underscore,
        Strong = EmphasisMarker.Underscore
    };

    /// <summary>
    ///     Asserts that a document formatter that only has the normalizer turns the input into the expected text, and that the
    ///     result renders the same, keeps its structure and is stable.
    /// </summary>
    /// <param name="input">The LF-normalized input.</param>
    /// <param name="expected">The expected output.</param>
    /// <param name="config">The configuration, or <see langword="null"/> for the defaults.</param>
    private static void AssertFormats(string input, string expected, LintMarkConfig? config = null)
    {
        var formatter = new DocumentFormatter([], Normalizer);
        Assert.Equal(expected, BlockFormatterAssertions.AssertSafeAndStable(input, config, formatter));
        BlockFormatterAssertions.AssertFormats(input, expected, config);
    }

    /// <summary>
    ///     Test that a missing argument is rejected when adding the edits of a paragraph.
    /// </summary>
    [Fact]
    public void InlineNormalizer_AddBlockEdits_NullArguments_Throw()
    {
        // Arrange: a paragraph, the document, the configuration and an edit set
        var document = BlockFormatterAssertions.Parse("_a_\n");
        var block = BlockFormatterAssertions.FirstBlock(document, BlockKind.Paragraph);
        var edits = new EditSet();

        // Act & Assert: each argument is checked and named
        Assert.Equal("document", Assert.Throws<ArgumentNullException>(() => Normalizer.AddBlockEdits(null!, block, LintMarkConfig.Default, edits)).ParamName);
        Assert.Equal("block", Assert.Throws<ArgumentNullException>(() => Normalizer.AddBlockEdits(document, null!, LintMarkConfig.Default, edits)).ParamName);
        Assert.Equal("config", Assert.Throws<ArgumentNullException>(() => Normalizer.AddBlockEdits(document, block, null!, edits)).ParamName);
        Assert.Equal("edits", Assert.Throws<ArgumentNullException>(() => Normalizer.AddBlockEdits(document, block, LintMarkConfig.Default, null!)).ParamName);
    }

    /// <summary>
    ///     Test that a block that is not a paragraph is rejected.
    /// </summary>
    [Fact]
    public void InlineNormalizer_AddBlockEdits_HeadingBlock_ThrowsArgumentException()
    {
        // Arrange: a heading
        var document = BlockFormatterAssertions.Parse("# _a_\n");
        var block = BlockFormatterAssertions.FirstBlock(document, BlockKind.Heading);

        // Act & Assert: the block is rejected and the parameter is named
        var exception = Assert.Throws<ArgumentException>(() => Normalizer.AddBlockEdits(document, block, LintMarkConfig.Default, new EditSet()));
        Assert.Equal("block", exception.ParamName);
    }

    /// <summary>
    ///     Test that the normalizer lists the three rules it enforces and that the list cannot be changed.
    /// </summary>
    [Fact]
    public void InlineNormalizer_RuleIds_Read_ListsTheThreeRulesAndIsImmutable()
    {
        // Act: read the rule ids
        var ruleIds = Normalizer.RuleIds;

        // Assert: the three rules, each registered, in an immutable array
        Assert.Equal(["emphasis", "strong", "bare-urls"], ruleIds);
        Assert.All(ruleIds, id => Assert.True(RuleCatalog.TryGet(id, out _)));
        ReadOnlyAssertions.AssertCannotBeModified((IReadOnlyList<string>)ruleIds);
    }

    /// <summary>
    ///     Test that emphasis and strong emphasis written with underscores are written with asterisks by default.
    /// </summary>
    [Theory]
    [InlineData("_a_\n", "*a*\n")]
    [InlineData("__a__\n", "**a**\n")]
    [InlineData("a _b_ c\n", "a *b* c\n")]
    [InlineData("a __b__ c, _d_ and __e__.\n", "a **b** c, *d* and **e**.\n")]
    [InlineData("_a\nb_\n", "*a\nb*\n")]
    [InlineData("_a  \nb_\n", "*a  \nb*\n")]
    [InlineData("(_a_)\n", "(*a*)\n")]
    [InlineData("*a* **b**\n", "*a* **b**\n")]
    [InlineData("_a `code` b_\n", "*a `code` b*\n")]
    [InlineData("[_a_](u)\n", "[*a*](u)\n")]
    [InlineData("![_a_](u)\n", "![*a*](u)\n")]
    public void InlineNormalizer_AddBlockEdits_UnderscoreDelimiters_BecomeAsterisks(string input, string expected) =>
        AssertFormats(input, expected);

    /// <summary>
    ///     Test that emphasis and strong emphasis written with asterisks are written with underscores when that is configured.
    /// </summary>
    [Theory]
    [InlineData("*a*\n", "_a_\n")]
    [InlineData("**a**\n", "__a__\n")]
    [InlineData("a *b* c and **d** e.\n", "a _b_ c and __d__ e.\n")]
    [InlineData("(*a*)\n", "(_a_)\n")]
    public void InlineNormalizer_AddBlockEdits_AsteriskDelimiters_BecomeUnderscoresWhenConfigured(string input, string expected) =>
        AssertFormats(input, expected, UnderscoreConfig);

    /// <summary>
    ///     Test that emphasis inside a word keeps its asterisks when underscores are configured, because an underscore does not emphasize inside a word.
    /// </summary>
    [Theory]
    [InlineData("a*b*c\n")]
    [InlineData("é*a*\n")]
    [InlineData("*a*b\n")]
    [InlineData("**a**b\n")]
    public void InlineNormalizer_AddBlockEdits_WithinWordEmphasis_KeepsAsterisks(string input) =>
        AssertFormats(input, input, UnderscoreConfig);

    /// <summary>
    ///     Test that emphasis with another emphasis next to it or inside it, a delimiter character in its content or
    ///     in a literal of the paragraph, or an escaped delimiter, keeps its delimiters.
    /// </summary>
    [Theory]
    [InlineData("_a *b* c_\n")]
    [InlineData("_a_*b*\n")]
    [InlineData("_a_ and snake_case\n")]
    [InlineData("_a_ and \\_\n")]
    [InlineData("\\_a\\_\n")]
    [InlineData("_a*b_\n")]
    [InlineData("**_a_**\n")]
    [InlineData("***a***\n")]
    [InlineData("_a_<b>x</b>\n")]
    [InlineData("_a_ *\n")]
    public void InlineNormalizer_AddBlockEdits_UnsafeEmphasis_KeepsTheParagraph(string input) =>
        AssertFormats(input, input);

    /// <summary>
    ///     Test that the delimiter characters in a code span keep their text.
    /// </summary>
    [Theory]
    [InlineData("`_a_`\n")]
    [InlineData("`__a__` and `*b*`\n")]
    public void InlineNormalizer_AddBlockEdits_DelimitersInCodeSpan_AreKept(string input) =>
        AssertFormats(input, input);

    /// <summary>
    ///     Test that the delimiter characters in inline HTML keep their text.
    /// </summary>
    [Theory]
    [InlineData("<span>_a_</span>\n")]
    [InlineData("<a title=\"__a__\">b</a>\n")]
    public void InlineNormalizer_AddBlockEdits_DelimitersInHtml_AreKept(string input) =>
        AssertFormats(input, input);

    /// <summary>
    ///     Test that the delimiter characters in inline math keep their text.
    /// </summary>
    [Theory]
    [InlineData("$_a_$\n")]
    [InlineData("$$__a__$$\n")]
    public void InlineNormalizer_AddBlockEdits_DelimitersInMath_AreKept(string input) =>
        AssertFormats(input, input);

    /// <summary>
    ///     Test that the delimiter characters in the destination of a link or an image keep their text.
    /// </summary>
    [Theory]
    [InlineData("[a](http://x.com/_b_)\n")]
    [InlineData("![a](http://x.com/_b_.png)\n")]
    public void InlineNormalizer_AddBlockEdits_DelimitersInLinkDestination_AreKept(string input) =>
        AssertFormats(input, input);

    /// <summary>
    ///     Test that the paragraphs inside lists and quotes are normalized.
    /// </summary>
    [Theory]
    [InlineData("- _a_\n", "- *a*\n")]
    [InlineData("1. __a__\n", "1. **a**\n")]
    [InlineData("> _a_\n", "> *a*\n")]
    [InlineData("> - _a_ http://x.com\n", "> - *a* <http://x.com>\n")]
    public void InlineNormalizer_AddBlockEdits_ParagraphInContainer_IsNormalized(string input, string expected) =>
        AssertFormats(input, expected);

    /// <summary>
    ///     Test that the paragraphs of a footnote are normalized like those of the top level.
    /// </summary>
    [Theory]
    [InlineData("a[^n]\n\n[^n]: x _b_ y\n", "a[^n]\n\n[^n]: x *b* y\n")]
    [InlineData("a[^n]\n\n[^n]: x _b_ y\n\n    z _c_ w\n", "a[^n]\n\n[^n]: x *b* y\n\n    z *c* w\n")]
    [InlineData("a[^n]\n\n[^n]: x http://x.com\n", "a[^n]\n\n[^n]: x <http://x.com>\n")]
    public void InlineNormalizer_AddBlockEdits_ParagraphInFootnote_IsNormalized(string input, string expected) =>
        AssertFormats(input, expected);

    /// <summary>
    ///     Test that emphasis or strong emphasis that ends the last paragraph of a footnote is left as written.
    /// </summary>
    [Theory]
    [InlineData("a[^n]\n\n[^n]: _b_\n")]
    [InlineData("a[^n]\n\n[^n]: x __b__\n")]
    [InlineData("a[^n]\n\n[^n]: x\n\n    _para_\n")]
    public void InlineNormalizer_AddBlockEdits_EmphasisEndingLastFootnoteParagraph_IsKept(string input) =>
        AssertFormats(input, input);

    /// <summary>
    ///     Test that headings and table cells keep their emphasis and URLs: the normalizer leaves the text as written, and
    ///     the whole tool changes only the heading style of a setext heading.
    /// </summary>
    [Theory]
    [InlineData("# _a_ http://x.com\n", "# _a_ http://x.com\n")]
    [InlineData("_a_\n===\n", "# _a_\n")]
    [InlineData("| _a_ | http://x.com |\n|---|---|\n", "| _a_ | http://x.com |\n|---|---|\n")]
    public void InlineNormalizer_AddBlockEdits_HeadingsAndTables_AreNotNormalized(string input, string toolExpected)
    {
        // Act & Assert: the normalizer alone changes nothing; the tool writes the setext heading in ATX style and nothing else
        Assert.Equal(input, BlockFormatterAssertions.AssertSafeAndStable(input, null, new DocumentFormatter([], Normalizer)));
        BlockFormatterAssertions.AssertFormats(input, toolExpected);
    }

    /// <summary>
    ///     Test that a bare http, https or ftp URL is wrapped in angle brackets.
    /// </summary>
    [Theory]
    [InlineData("see http://x.com for more\n", "see <http://x.com> for more\n")]
    [InlineData("see https://x.org/a/b?c=d.\n", "see <https://x.org/a/b?c=d>.\n")]
    [InlineData("ftp://f.org/x\n", "<ftp://f.org/x>\n")]
    [InlineData("(https://x.org)\n", "(<https://x.org>)\n")]
    [InlineData("a http://x.com, b\n", "a <http://x.com>, b\n")]
    [InlineData("http://x.com/a_b_c\n", "<http://x.com/a_b_c>\n")]
    [InlineData("http://x.com/a&b\n", "<http://x.com/a&b>\n")]
    [InlineData("a\nhttp://x.com\n", "a\n<http://x.com>\n")]
    [InlineData("http://a.com and http://b.com\n", "<http://a.com> and <http://b.com>\n")]
    [InlineData("see http://x.com/a_(b) now\n", "see <http://x.com/a_(b)> now\n")]
    [InlineData("see (http://x.com/a_(b))\n", "see (<http://x.com/a_(b)>)\n")]
    [InlineData("- see http://y.com\n", "- see <http://y.com>\n")]
    [InlineData("1. see http://y.com\n", "1. see <http://y.com>\n")]
    [InlineData("- [x] see http://y.com\n", "- [x] see <http://y.com>\n")]
    public void InlineNormalizer_AddBlockEdits_BareUrls_AreWrappedInAngleBrackets(string input, string expected) =>
        AssertFormats(input, expected);

    /// <summary>
    ///     Test that a URL at the start of a paragraph that needs a blank line before it is wrapped and separated.
    /// </summary>
    [Fact]
    public void InlineNormalizer_AddBlockEdits_UrlAtStartOfBlockAfterHeading_IsWrappedAndSeparated() =>
        BlockFormatterAssertions.AssertFormats("# T\nhttp://x.com\n", "# T\n\n<http://x.com>\n");

    /// <summary>
    ///     Test that a www address keeps its form.
    /// </summary>
    [Theory]
    [InlineData("www.x.com\n")]
    [InlineData("see www.x.com/a now\n")]
    public void InlineNormalizer_AddBlockEdits_WwwAddress_IsKept(string input) =>
        AssertFormats(input, input);

    /// <summary>
    ///     Test that an e-mail address and a mailto link keep their form.
    /// </summary>
    [Theory]
    [InlineData("a@b.com\n")]
    [InlineData("mailto:a@b.com\n")]
    [InlineData("<mailto:a@b.com>\n")]
    public void InlineNormalizer_AddBlockEdits_MailAddress_IsKept(string input) =>
        AssertFormats(input, input);

    /// <summary>
    ///     Test that a URL that is already a link keeps its form, in an angle-bracket autolink, in link text, and as the
    ///     destination of a link, also where the link stands in a list item and starts with text that looks like a task
    ///     list marker.
    /// </summary>
    [Theory]
    [InlineData("<http://x.com>\n")]
    [InlineData("[http://x.com](http://y.com)\n")]
    [InlineData("[a](http://x.com)\n")]
    [InlineData("[x](http://y.com)\n")]
    [InlineData("- [a](http://y.com)\n")]
    [InlineData("- [x](http://y.com)\n")]
    [InlineData("- [X](http://y.com)\n")]
    [InlineData("- [ ](http://y.com)\n")]
    [InlineData("1. [x](http://y.com)\n")]
    [InlineData("- foo [x](http://y.com)\n")]
    [InlineData("> - [x](http://y.com)\n")]
    public void InlineNormalizer_AddBlockEdits_UrlAlreadyLinked_IsKept(string input) =>
        AssertFormats(input, input);

    /// <summary>
    ///     Test that a URL in a code span keeps its form.
    /// </summary>
    [Fact]
    public void InlineNormalizer_AddBlockEdits_UrlInCodeSpan_IsKept() =>
        AssertFormats("`http://x.com`\n", "`http://x.com`\n");

    /// <summary>
    ///     Test that a URL in inline HTML keeps its form.
    /// </summary>
    [Fact]
    public void InlineNormalizer_AddBlockEdits_UrlInHtml_IsKept() =>
        AssertFormats("<a href=\"http://x.com\">a</a>\n", "<a href=\"http://x.com\">a</a>\n");

    /// <summary>
    ///     Test that a URL that is not set off from the text around it keeps its form: it follows or precedes a quote
    ///     mark, an emphasis delimiter, or a pipe.
    /// </summary>
    [Theory]
    [InlineData("\"http://x.com\"\n")]
    [InlineData("*http://x.com*\n")]
    [InlineData("a|http://x.com\n")]
    public void InlineNormalizer_AddBlockEdits_UrlAttachedToOtherText_IsKept(string input) =>
        AssertFormats(input, input);

    /// <summary>
    ///     Test that an address whose scheme is not written in lower case keeps its form, because the parser does not
    ///     read it as a bare URL.
    /// </summary>
    [Theory]
    [InlineData("HTTP://X.COM/a\n")]
    [InlineData("see Http://x.com/a.\n")]
    public void InlineNormalizer_AddBlockEdits_UpperCaseScheme_IsKept(string input) =>
        AssertFormats(input, input);

    /// <summary>
    ///     Test that bare URLs are left as written when that is configured, while emphasis is still normalized.
    /// </summary>
    [Fact]
    public void InlineNormalizer_AddBlockEdits_LeaveUrlsConfigured_KeepsUrlsAndNormalizesEmphasis() =>
        AssertFormats("_a_ http://x.com\n", "*a* http://x.com\n", LintMarkConfig.Default with { BareUrls = BareUrlStyle.Leave });

    /// <summary>
    ///     Test that a paragraph is left alone when the document defines link references and the paragraph holds a bracket.
    /// </summary>
    [Theory]
    [InlineData("[_a_]\n\n[_a_]: http://u.com\n")]
    [InlineData("[x] _a_ http://x.com\n\n[x]: http://u.com\n")]
    public void InlineNormalizer_AddBlockEdits_BracketAndReferenceDefinitions_KeepsTheParagraph(string input) =>
        AssertFormats(input, input);

    /// <summary>
    ///     Test that a paragraph without a bracket is still normalized when the document defines link references.
    /// </summary>
    [Fact]
    public void InlineNormalizer_AddBlockEdits_ReferenceDefinitionsAndNoBracket_StillNormalizes() =>
        AssertFormats("_a_\n\n[x]: http://u.com\n", "*a*\n\n[x]: http://u.com\n");

    /// <summary>
    ///     Test that a paragraph that cannot be proved to render the same is left as written while the rest of the file is normalized.
    /// </summary>
    [Fact]
    public void InlineNormalizer_AddBlockEdits_ParagraphThatMayRenderDifferently_IsLeftAndTheRestIsNormalized()
    {
        // Arrange: a paragraph with an unclosed declaration before a URL (the inserted angle bracket would be swallowed) and a plain one
        const string input = "a <!x http://x.com\n\n_b_\n";

        // Act: format
        var formatted = BlockFormatterAssertions.AssertSafeAndStable(input, null, new DocumentFormatter([], Normalizer));

        // Assert: the first paragraph is exactly as written, and the plain paragraph is normalized
        Assert.Equal("a <!x http://x.com\n\n*b*\n", formatted);
    }

    /// <summary>
    ///     Test that a rule is not applied to a paragraph when it is suppressed in it, by identifier or by alias.
    /// </summary>
    [Theory]
    [InlineData("lintmark-disable emphasis", "_a_ **b** http://x.com", "_a_ **b** <http://x.com>", "bare-urls")]
    [InlineData("markdownlint-disable MD049", "_a_ **b** http://x.com", "_a_ **b** <http://x.com>", "bare-urls")]
    [InlineData("lintmark-disable strong", "_a_ __b__ http://x.com", "*a* __b__ <http://x.com>", "bare-urls,emphasis")]
    [InlineData("markdownlint-disable MD050", "_a_ __b__ http://x.com", "*a* __b__ <http://x.com>", "bare-urls,emphasis")]
    [InlineData("lintmark-disable bare-urls", "_a_ __b__ http://x.com", "*a* **b** http://x.com", "emphasis,strong")]
    [InlineData("markdownlint-disable MD034", "_a_ __b__ http://x.com", "*a* **b** http://x.com", "emphasis,strong")]
    public void InlineNormalizer_AddBlockEdits_RuleSuppressed_LeavesThatRule(string directive, string paragraph, string expected, string appliedRules)
    {
        // Arrange: a paragraph inside a region that disables one rule
        var input = $"<!-- {directive} -->\n\n{paragraph}\n";
        var formatter = new DocumentFormatter([], Normalizer);

        // Act: format
        var (result, formatted) = BlockFormatterAssertions.Apply(formatter, input);

        // Assert: only the other rules are applied, and each edit and each violation names one of them
        Assert.Equal($"<!-- {directive} -->\n\n{expected}\n", formatted);
        Assert.Equal(appliedRules, string.Join(',', result.Edits.Select(edit => edit.RuleId).Distinct().Order(StringComparer.Ordinal)));
        Assert.Equal(appliedRules, string.Join(',', result.Diagnostics.Select(diagnostic => diagnostic.RuleId).Distinct().Order(StringComparer.Ordinal)));
    }

    /// <summary>
    ///     Test that a rule is still applied to a paragraph when only a different rule is suppressed in it.
    /// </summary>
    [Theory]
    [InlineData("lintmark-disable emphasis", "_a_ __b__", "_a_ **b**", "strong")]
    [InlineData("markdownlint-disable MD050", "_a_ __b__", "*a* __b__", "emphasis")]
    [InlineData("lintmark-disable bare-urls", "_a_ http://x.com", "*a* http://x.com", "emphasis")]
    public void InlineNormalizer_AddBlockEdits_OtherRuleSuppressed_StillAppliesTheRule(string directive, string paragraph, string expected, string appliedRule)
    {
        // Arrange: a paragraph inside a region that disables one rule only
        var input = $"<!-- {directive} -->\n\n{paragraph}\n";
        var formatter = new DocumentFormatter([], Normalizer);

        // Act: format
        var (result, formatted) = BlockFormatterAssertions.Apply(formatter, input);

        // Assert: the rule that is not disabled is applied, and its edit is credited to it
        Assert.Equal($"<!-- {directive} -->\n\n{expected}\n", formatted);
        Assert.Contains(result.Edits, edit => edit.RuleId == appliedRule);
    }

    /// <summary>
    ///     Test that every edit is reported under the rule of the violation it removes.
    /// </summary>
    [Fact]
    public void InlineNormalizer_AddBlockEdits_NonCanonicalParagraph_ReportsEachEditUnderItsRule()
    {
        // Arrange: a paragraph with all three violations
        var document = BlockFormatterAssertions.Parse("_a_ __b__ http://x.com\n");
        var block = BlockFormatterAssertions.FirstBlock(document, BlockKind.Paragraph);
        var edits = new EditSet();

        // Act: add the edits
        Normalizer.AddBlockEdits(document, block, LintMarkConfig.Default, edits);

        // Assert: each rule of the normalizer reports
        var rules = edits.ToImmutable().Select(edit => edit.RuleId).Distinct().Order(StringComparer.Ordinal).ToList();
        Assert.Equal(["bare-urls", "emphasis", "strong"], rules);
    }

    /// <summary>
    ///     Test that the edits are replacements of existing characters and never insertions.
    /// </summary>
    [Fact]
    public void InlineNormalizer_AddBlockEdits_Edits_ReplaceExistingCharacters()
    {
        // Arrange: a paragraph with emphasis and a URL
        var document = BlockFormatterAssertions.Parse("_a_ http://x.com\n");
        var edits = new EditSet();

        // Act: add the edits
        Normalizer.AddBlockEdits(document, BlockFormatterAssertions.FirstBlock(document, BlockKind.Paragraph), LintMarkConfig.Default, edits);

        // Assert: none is an insertion
        Assert.NotEmpty(edits.ToImmutable());
        Assert.All(edits.ToImmutable(), edit => Assert.True(edit.Length > 0));
    }

    /// <summary>
    ///     Test that the same edits are proposed for CRLF and mixed line endings, and with a byte order mark.
    /// </summary>
    [Fact]
    public void InlineNormalizer_AddBlockEdits_LineEndingsAndByteOrderMark_ProposeTheSameEdits()
    {
        // Arrange: a text with emphasis, strong emphasis and a URL
        const string input = "_a_ __b__\nhttp://x.com\nend\n";
        var formatter = new DocumentFormatter([], Normalizer);
        var expected = BlockFormatterAssertions.Apply(formatter, input).Result.Edits;

        // Act & Assert: the edits do not depend on how the file is stored
        var preamble = new System.Text.UTF8Encoding(true).GetPreamble();
        foreach (var bytes in new[]
        {
            System.Text.Encoding.UTF8.GetBytes(input.Replace("\n", "\r\n", StringComparison.Ordinal)),
            System.Text.Encoding.UTF8.GetBytes(BlockFormatterAssertions.MixEndings(input)),
            [.. preamble, .. System.Text.Encoding.UTF8.GetBytes(input)]
        })
        {
            var document = DocumentParser.Parse(SourceDocument.Create("a.md", bytes));
            Assert.Equal(expected, formatter.Format(document, LintMarkConfig.Default).Edits);
        }
    }

    /// <summary>
    ///     Test that the result is the same when several threads format the same text with one normalizer.
    /// </summary>
    [Fact]
    public void InlineNormalizer_AddBlockEdits_SharedInstanceOnManyThreads_GivesTheSameResult()
    {
        // Arrange: one formatter that holds the shared normalizer and a text
        const string input = "_a_ __b__ http://x.com\n\n> _c_ ftp://f.org\n\n- *d* https://x.org.\n";
        var formatter = new DocumentFormatter([], Normalizer);
        var expected = BlockFormatterAssertions.Apply(formatter, input).Formatted;
        var results = new string[8];

        // Act: format on several threads at once
        BlockFormatterAssertions.RunConcurrently(results.Length, index =>
        {
            for (var round = 0; round < 25; round++)
            {
                results[index] = BlockFormatterAssertions.Apply(formatter, input).Formatted;
                Assert.Equal(expected, results[index]);
            }
        });

        // Assert: every thread saw the same text
        Assert.All(results, result => Assert.Equal(expected, result));
    }

    /// <summary>
    ///     Test that a document formatter without a normalizer proposes no inline edit.
    /// </summary>
    [Fact]
    public void InlineNormalizer_AddBlockEdits_NotRegistered_ProposesNoInlineEdit()
    {
        // Act: format a paragraph with a document formatter that has no normalizer
        var (result, formatted) = BlockFormatterAssertions.Apply(new DocumentFormatter(), "_a_ http://x.com\n");

        // Assert: the text is untouched
        Assert.Equal("_a_ http://x.com\n", formatted);
        Assert.Empty(result.Edits);
    }
}
