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
using DemaConsulting.LintMark.Formatting.Blocks;
using DemaConsulting.LintMark.Formatting.Inline;
using DemaConsulting.LintMark.Linting;
using DemaConsulting.LintMark.Verify.Checks;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Formats text with the production formatters and asserts the properties every rewrite must have: the text
///     is as expected and formatting it again changes nothing. Regression tests also assert that the result renders
///     the same and keeps its Pandoc structure.
/// </summary>
/// <remarks>
///     The safety checks are the ones the verification tool runs on every formatted file; the corpus run of the
///     verification tool is what proves them for real documents.
/// </remarks>
internal static class BlockFormatterAssertions
{
    /// <summary>
    ///     Creates the formatter the shipped tool uses: the block formatters, the inline normalizer, the paragraph
    ///     formatter and the document passes.
    /// </summary>
    /// <returns>The formatter.</returns>
    public static DocumentFormatter ProductionFormatter() =>
        new(
            [new HeadingFormatter(), new CodeBlockFormatter(), new ThematicBreakFormatter(), new ListFormatter(), new BlockQuoteFormatter(), new TableFormatter()],
            new InlineNormalizer(),
            new ParagraphFormatter(),
            new LinkRefactorer());

    /// <summary>
    ///     Parses LF-normalized text as a document named <c>a.md</c>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The parsed document.</returns>
    public static ParsedDocument Parse(string text) => DocumentParser.Parse(SourceDocument.FromText("a.md", text));

    /// <summary>
    ///     Finds the first block of a kind in the document.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="kind">The kind.</param>
    /// <returns>The block.</returns>
    public static BlockInfo FirstBlock(ParsedDocument document, BlockKind kind) => document.Blocks.First(block => block.Kind == kind);

    /// <summary>
    ///     Formats text and applies the edits, without any assertion.
    /// </summary>
    /// <param name="formatter">The formatter.</param>
    /// <param name="text">The LF-normalized text.</param>
    /// <param name="config">The configuration, or <see langword="null"/> for the defaults.</param>
    /// <returns>The result and the patched text.</returns>
    public static (FormatResult Result, string Formatted) Apply(DocumentFormatter formatter, string text, LintMarkConfig? config = null)
    {
        var document = Parse(text);
        var result = formatter.Format(document, config ?? LintMarkConfig.Default);
        return (result, SourcePatcher.Apply(document.Source, result.Edits));
    }

    /// <summary>
    ///     Asserts that text is formatted to the expected text and that formatting the result changes nothing.
    /// </summary>
    /// <param name="input">The LF-normalized input.</param>
    /// <param name="expected">The expected output.</param>
    /// <param name="config">The configuration, or <see langword="null"/> for the defaults.</param>
    /// <param name="formatter">The formatter, or <see langword="null"/> for <see cref="ProductionFormatter"/>.</param>
    public static void AssertFormats(string input, string expected, LintMarkConfig? config = null, DocumentFormatter? formatter = null)
    {
        var used = formatter ?? ProductionFormatter();
        var (_, formatted) = Apply(used, input, config);
        Assert.Equal(expected, formatted);
        var second = Apply(used, formatted, config);
        Assert.Empty(second.Result.Edits);
        Assert.DoesNotContain(second.Result.Diagnostics, static diagnostic => diagnostic.Kind == DiagnosticKind.Formatting);
    }

    /// <summary>
    ///     Asserts that text is formatted to the expected text, safely and stably: see <see cref="AssertSafeAndStable"/>.
    /// </summary>
    /// <param name="input">The LF-normalized input.</param>
    /// <param name="expected">The expected output.</param>
    /// <param name="config">The configuration, or <see langword="null"/> for the defaults.</param>
    public static void AssertFormatsSafely(string input, string expected, LintMarkConfig? config = null) =>
        Assert.Equal(expected, AssertSafeAndStable(input, config));

    /// <summary>
    ///     Formats text with the production formatters and checks the properties of a safe, stable rewrite: ordered
    ///     edits that match the reported violations, the same rendering and Pandoc structure, and nothing left to do
    ///     on a second run.
    /// </summary>
    /// <param name="text">The LF-normalized text.</param>
    /// <param name="config">The configuration, or <see langword="null"/> for the defaults.</param>
    /// <param name="formatter">The formatter, or <see langword="null"/> for <see cref="ProductionFormatter"/>.</param>
    /// <returns>The formatted text.</returns>
    public static string AssertSafeAndStable(string text, LintMarkConfig? config = null, DocumentFormatter? formatter = null)
    {
        var used = formatter ?? ProductionFormatter();
        var effective = config ?? LintMarkConfig.Default;
        var (result, formatted) = Apply(used, text, effective);

        // The edits are ordered, in range and report a violation exactly when they change something; the only finding
        // that a rewrite does not remove is a line that cannot be wrapped
        Assert.Equal(result.Edits.OrderBy(edit => edit.Start).ThenBy(edit => edit.Length), result.Edits);
        Assert.All(result.Edits, edit => Assert.True(edit.End <= text.Length));
        Assert.Equal(!string.Equals(text, formatted, StringComparison.Ordinal), result.Diagnostics.Any(static diagnostic => diagnostic.Kind == DiagnosticKind.Formatting));
        Assert.All(result.Diagnostics, diagnostic =>
            Assert.True(diagnostic.Kind == DiagnosticKind.Formatting || (diagnostic.Kind == DiagnosticKind.LintOnly && diagnostic.RuleId is RuleCatalog.LineLengthRuleId or RuleCatalog.ParagraphWrapRuleId)));

        // The rewrite renders the same and keeps its Pandoc structure; the comment that introduces new link definitions is
        // the one thing a page shows of the link refactoring and is left out of the comparison
        var visible = effective.LinkRefactor.Enabled
            ? MarkerLines.WithoutAddedMarker(text, formatted, effective.LinkRefactor.DefinitionsMarker)
            : formatted;
        AssertEquivalent("HTML", HtmlEquivalenceChecker.Check(text, visible), text, formatted);
        AssertEquivalent("structure", StructuralTokenChecker.Check(text, formatted), text, formatted);

        // Formatting the result again changes nothing and reports nothing that a rewrite could fix
        var second = Apply(used, formatted, effective);
        Assert.Empty(second.Result.Edits);
        Assert.DoesNotContain(second.Result.Diagnostics, static diagnostic => diagnostic.Kind == DiagnosticKind.Formatting);
        Assert.True(
            Parse(text).IsFormatUnsafe == Parse(formatted).IsFormatUnsafe,
            $"The rewrite changed whether the document is read with grid tables.\n--- before ---\n{text}\n--- after ---\n{formatted}");
        return formatted;
    }

    /// <summary>
    ///     Applies one block formatter to every block it accepts, without the document passes, and returns the edits and the text.
    /// </summary>
    /// <param name="formatter">The formatter under test.</param>
    /// <param name="text">The LF-normalized text.</param>
    /// <param name="config">The configuration, or <see langword="null"/> for the defaults.</param>
    /// <returns>The edits in order and the patched text.</returns>
    public static (System.Collections.Immutable.ImmutableArray<TextEdit> Edits, string Formatted) ApplyFormatter(IBlockFormatter formatter, string text, LintMarkConfig? config = null)
    {
        var document = Parse(text);
        var edits = new EditSet();
        var context = new FormatContext(document, config ?? LintMarkConfig.Default);
        foreach (var block in document.Blocks.Where(formatter.CanFormat))
        {
            formatter.Format(context, block, edits);
        }

        var ordered = edits.ToImmutable();
        return (ordered, SourcePatcher.Apply(document.Source, ordered));
    }

    /// <summary>
    ///     Asserts that one block formatter alone turns the input into the expected text and has nothing more to do
    ///     with the result.
    /// </summary>
    /// <param name="formatter">The formatter under test.</param>
    /// <param name="input">The LF-normalized input.</param>
    /// <param name="expected">The expected output.</param>
    /// <param name="config">The configuration, or <see langword="null"/> for the defaults.</param>
    public static void AssertFormatterOutput(IBlockFormatter formatter, string input, string expected, LintMarkConfig? config = null)
    {
        var (_, formatted) = ApplyFormatter(formatter, input, config);
        Assert.Equal(expected, formatted);
        Assert.Empty(ApplyFormatter(formatter, formatted, config).Edits);
    }

    /// <summary>
    ///     Asserts that text is already canonical: formatting proposes no edit and reports no violation.
    /// </summary>
    /// <param name="text">The LF-normalized text.</param>
    /// <param name="config">The configuration, or <see langword="null"/> for the defaults.</param>
    public static void AssertUnchanged(string text, LintMarkConfig? config = null)
    {
        var (result, formatted) = Apply(ProductionFormatter(), text, config);
        Assert.Empty(result.Edits);
        Assert.DoesNotContain(result.Diagnostics, static diagnostic => diagnostic.Kind == DiagnosticKind.Formatting);
        Assert.Equal(text, formatted);
    }

    /// <summary>
    ///     Asserts that an equivalence verdict is equivalent, with a message that shows the texts.
    /// </summary>
    /// <param name="check">The name of the check.</param>
    /// <param name="verdict">The verdict.</param>
    /// <param name="before">The text before.</param>
    /// <param name="after">The text after.</param>
    private static void AssertEquivalent(string check, EquivalenceResult verdict, string before, string after) =>
        Assert.True(verdict.IsEquivalent, $"The {check} check failed ({verdict.Description}).\n--- before ---\n{before}\n--- after ---\n{after}");
}
