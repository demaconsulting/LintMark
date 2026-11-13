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
using DemaConsulting.LintMark.Linting;
using DemaConsulting.LintMark.Safety;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Formats text with the production formatters and asserts the properties every rewrite must have: the text
///     is as expected, it renders the same, it keeps its Pandoc structure, formatting it again changes nothing, and
///     the reported violations match the edits.
/// </summary>
/// <remarks>
///     The safety checks used here are the ones the engine runs before it writes a file. A formatter that
///     fails one of them is wrong; the assertions are never relaxed to make a case pass.
/// </remarks>
internal static class BlockFormatterAssertions
{
    /// <summary>
    ///     Creates the formatter the shipped tool uses: the three block formatters and the document passes.
    /// </summary>
    /// <returns>The formatter.</returns>
    public static DocumentFormatter ProductionFormatter() =>
        new([new HeadingFormatter(), new CodeBlockFormatter(), new ThematicBreakFormatter()]);

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
    ///     Formats text with the production formatters and checks every property of a safe, stable rewrite.
    /// </summary>
    /// <param name="text">The LF-normalized text.</param>
    /// <param name="config">The configuration, or <see langword="null"/> for the defaults.</param>
    /// <param name="formatter">The formatter, or <see langword="null"/> for <see cref="ProductionFormatter"/>.</param>
    /// <param name="checkParallel">Whether to also compute the result on several threads (skipped for bulk corpora to save time).</param>
    /// <returns>The formatted text.</returns>
    public static string AssertSafeAndStable(string text, LintMarkConfig? config = null, DocumentFormatter? formatter = null, bool checkParallel = true)
    {
        var used = formatter ?? ProductionFormatter();
        var effective = config ?? LintMarkConfig.Default;
        var (result, formatted) = Apply(used, text, effective);

        // The edits are ordered, in range and report a violation exactly when they change something
        Assert.Equal(result.Edits.OrderBy(edit => edit.Start).ThenBy(edit => edit.Length), result.Edits);
        Assert.All(result.Edits, edit => Assert.True(edit.End <= text.Length));
        Assert.Equal(!string.Equals(text, formatted, StringComparison.Ordinal), result.Diagnostics.Length > 0);
        Assert.All(result.Diagnostics, diagnostic =>
        {
            Assert.Equal(DiagnosticKind.Formatting, diagnostic.Kind);
            Assert.True(diagnostic.Line >= 1 && diagnostic.Column >= 1);
        });

        // The rewrite renders the same, with LF and with CRLF line endings, and keeps its Pandoc structure
        AssertEquivalent("HTML", HtmlEquivalenceChecker.Check(text, formatted), text, formatted);
        AssertEquivalent(
            "HTML on disk",
            HtmlEquivalenceChecker.CheckOnDisk(text.Replace("\n", "\r\n", StringComparison.Ordinal), formatted.Replace("\n", "\r\n", StringComparison.Ordinal)),
            text,
            formatted);
        AssertEquivalent("structure", StructuralTokenChecker.Check(text, formatted), text, formatted);

        // Formatting the result again changes nothing and reports nothing
        AssertEquivalent("idempotency", IdempotencyChecker.Check(formatted, again => Apply(used, again, effective).Formatted), text, formatted);
        var second = Apply(used, formatted, effective);
        Assert.Empty(second.Result.Edits);
        Assert.Empty(second.Result.Diagnostics);
        Assert.True(
            Parse(text).IsFormatUnsafe == Parse(formatted).IsFormatUnsafe,
            $"The rewrite changed whether the document is read with grid tables.\n--- before ---\n{text}\n--- after ---\n{formatted}");

        // The result does not depend on the thread that computes it
        if (checkParallel)
        {
            var parallel = Enumerable.Range(0, 4).AsParallel().Select(_ => Apply(used, text, effective).Formatted).ToList();
            Assert.All(parallel, other => Assert.Equal(formatted, other));
        }

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
    ///     Asserts that one block formatter alone turns the input into the expected text, that the result renders the
    ///     same and keeps its Pandoc structure, and that the formatter has nothing more to do with the result.
    /// </summary>
    /// <param name="formatter">The formatter under test.</param>
    /// <param name="input">The LF-normalized input.</param>
    /// <param name="expected">The expected output.</param>
    /// <param name="config">The configuration, or <see langword="null"/> for the defaults.</param>
    public static void AssertFormatterOutput(IBlockFormatter formatter, string input, string expected, LintMarkConfig? config = null)
    {
        var (_, formatted) = ApplyFormatter(formatter, input, config);
        Assert.Equal(expected, formatted);
        AssertEquivalent("HTML", HtmlEquivalenceChecker.Check(input, formatted), input, formatted);
        AssertEquivalent("structure", StructuralTokenChecker.Check(input, formatted), input, formatted);
        Assert.Empty(ApplyFormatter(formatter, formatted, config).Edits);
    }

    /// <summary>
    ///     Asserts that text is formatted to the expected text, safely and stably.
    /// </summary>
    /// <param name="input">The LF-normalized input.</param>
    /// <param name="expected">The expected output.</param>
    /// <param name="config">The configuration, or <see langword="null"/> for the defaults.</param>
    public static void AssertFormats(string input, string expected, LintMarkConfig? config = null) =>
        Assert.Equal(expected, AssertSafeAndStable(input, config));

    /// <summary>
    ///     Asserts that text is formatted to the expected text when it is stored with CRLF line endings, with a
    ///     byte order mark, and with mixed line endings, and that the line endings and the mark are kept.
    /// </summary>
    /// <param name="input">The LF-normalized input.</param>
    /// <param name="expected">The expected LF-normalized output.</param>
    /// <param name="config">The configuration, or <see langword="null"/> for the defaults.</param>
    public static void AssertFormatsOnDisk(string input, string expected, LintMarkConfig? config = null)
    {
        // CRLF with a byte order mark; then every other line ending as LF (the majority ending decides what is written back)
        AssertDiskVariant(input, expected, config, input.Replace("\n", "\r\n", StringComparison.Ordinal), expected.Replace("\n", "\r\n", StringComparison.Ordinal), bom: true);
        AssertDiskVariant(input, expected, config, input.Replace("\n", "\r\n", StringComparison.Ordinal), expected.Replace("\n", "\r\n", StringComparison.Ordinal), bom: false);
    }

    /// <summary>
    ///     Formats the bytes of a file and compares the written bytes with the expected text.
    /// </summary>
    /// <param name="input">The LF-normalized input.</param>
    /// <param name="expected">The expected LF-normalized output.</param>
    /// <param name="config">The configuration.</param>
    /// <param name="onDiskInput">The input as it is stored.</param>
    /// <param name="onDiskExpected">The expected output as it is stored.</param>
    /// <param name="bom">Whether the file has a byte order mark.</param>
    private static void AssertDiskVariant(string input, string expected, LintMarkConfig? config, string onDiskInput, string onDiskExpected, bool bom)
    {
        var preamble = bom ? new System.Text.UTF8Encoding(true).GetPreamble() : [];
        var source = SourceDocument.Create("a.md", [.. preamble, .. System.Text.Encoding.UTF8.GetBytes(onDiskInput)]);
        var result = ProductionFormatter().Format(DocumentParser.Parse(source), config ?? LintMarkConfig.Default);
        var formatted = SourcePatcher.Apply(source, result.Edits);
        var written = source.ToBytes(formatted, result.LineEnding);

        // The text is as expected, the mark is kept, and the line endings are the ones the file had
        Assert.Equal(input, source.Text);
        Assert.Equal(expected, formatted);
        Assert.Equal([.. preamble, .. System.Text.Encoding.UTF8.GetBytes(onDiskExpected)], written);
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
        Assert.Empty(result.Diagnostics);
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
