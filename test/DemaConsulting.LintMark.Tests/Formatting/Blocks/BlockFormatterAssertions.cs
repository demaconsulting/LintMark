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
    public static (System.Collections.Immutable.ImmutableArray<TextEdit> Edits, string Formatted) ApplyFormatter(IBlockFormatter formatter, string text, LintMarkConfig? config = null) =>
        ApplyFormatter(formatter, Parse(text), config);

    /// <summary>
    ///     Applies one block formatter to every block it accepts in a parsed document, without the document passes.
    /// </summary>
    /// <param name="formatter">The formatter under test.</param>
    /// <param name="document">The parsed document.</param>
    /// <param name="config">The configuration, or <see langword="null"/> for the defaults.</param>
    /// <returns>The edits in order and the patched text.</returns>
    public static (System.Collections.Immutable.ImmutableArray<TextEdit> Edits, string Formatted) ApplyFormatter(IBlockFormatter formatter, ParsedDocument document, LintMarkConfig? config = null)
    {
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
    ///     Asserts that text is formatted to the expected text when it is stored with CRLF line endings and a
    ///     byte order mark, with CRLF line endings without one, and with mixed line endings that are mostly CRLF, and
    ///     that the mark is kept and every line ends the way the majority of the lines did.
    /// </summary>
    /// <param name="input">The LF-normalized input.</param>
    /// <param name="expected">The expected LF-normalized output.</param>
    /// <param name="config">The configuration, or <see langword="null"/> for the defaults.</param>
    public static void AssertFormatsOnDisk(string input, string expected, LintMarkConfig? config = null)
    {
        var crlfInput = input.Replace("\n", "\r\n", StringComparison.Ordinal);
        var crlfExpected = expected.Replace("\n", "\r\n", StringComparison.Ordinal);
        AssertDiskVariant(input, expected, config, crlfInput, crlfExpected, bom: true);
        AssertDiskVariant(input, expected, config, crlfInput, crlfExpected, bom: false);

        // Mixed endings: every third line ending is a bare line feed (when the text has that many), so CRLF is the majority and is written back
        AssertDiskVariant(input, expected, config, MixEndings(input), crlfExpected, bom: false);
    }

    /// <summary>
    ///     Asserts that a block formatter proposes the same edits for the text with CRLF line endings, and with mixed
    ///     line endings, as for the LF text.
    /// </summary>
    /// <param name="formatter">The formatter under test.</param>
    /// <param name="input">The LF-normalized input.</param>
    /// <param name="config">The configuration, or <see langword="null"/> for the defaults.</param>
    /// <remarks>
    ///     The parser works on LF-normalized text, so the edits are offsets into that text and must not depend on the
    ///     terminators of the file; this is checked on the formatter alone, with no document pass to hide a difference.
    /// </remarks>
    public static void AssertSameEditsWithCrlf(IBlockFormatter formatter, string input, LintMarkConfig? config = null) =>
        AssertSameEdits(
            formatter,
            input,
            config,
            System.Text.Encoding.UTF8.GetBytes(input.Replace("\n", "\r\n", StringComparison.Ordinal)),
            System.Text.Encoding.UTF8.GetBytes(MixEndings(input)));

    /// <summary>
    ///     Asserts that a block formatter proposes the same edits for the text with a byte order mark, with LF and with
    ///     CRLF line endings, as for the LF text without one.
    /// </summary>
    /// <param name="formatter">The formatter under test.</param>
    /// <param name="input">The LF-normalized input.</param>
    /// <param name="config">The configuration, or <see langword="null"/> for the defaults.</param>
    public static void AssertSameEditsWithByteOrderMark(IBlockFormatter formatter, string input, LintMarkConfig? config = null)
    {
        var preamble = new System.Text.UTF8Encoding(true).GetPreamble();
        AssertSameEdits(
            formatter,
            input,
            config,
            [.. preamble, .. System.Text.Encoding.UTF8.GetBytes(input)],
            [.. preamble, .. System.Text.Encoding.UTF8.GetBytes(input.Replace("\n", "\r\n", StringComparison.Ordinal))]);
    }

    /// <summary>
    ///     Asserts that a block formatter proposes the same edits for each stored form of a text as for the LF text.
    /// </summary>
    /// <param name="formatter">The formatter under test.</param>
    /// <param name="input">The LF-normalized input, which the formatter must change.</param>
    /// <param name="config">The configuration, or <see langword="null"/> for the defaults.</param>
    /// <param name="variants">The bytes of the same text as it may be stored.</param>
    private static void AssertSameEdits(IBlockFormatter formatter, string input, LintMarkConfig? config, params byte[][] variants)
    {
        var (expectedEdits, _) = ApplyFormatter(formatter, input, config);
        Assert.NotEmpty(expectedEdits);
        foreach (var bytes in variants)
        {
            var document = DocumentParser.Parse(SourceDocument.Create("a.md", bytes));
            Assert.Equal(input, document.Source.Text);
            Assert.Equal(expectedEdits, ApplyFormatter(formatter, document, config).Edits);
        }
    }

    /// <summary>
    ///     Writes every third line ending of a text as a bare line feed and the others as CRLF.
    /// </summary>
    /// <param name="input">The LF-normalized text.</param>
    /// <returns>The text with mixed line endings (CRLF only when it has fewer than three line endings).</returns>
    public static string MixEndings(string input)
    {
        var builder = new System.Text.StringBuilder(input.Length + 8);
        var count = 0;
        foreach (var c in input)
        {
            if (c == '\n')
            {
                builder.Append(count++ % 3 == 2 ? "\n" : "\r\n");
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Asserts that one instance of a block formatter gives the same edits when several threads format the same
    ///     parsed document at the same time.
    /// </summary>
    /// <param name="formatter">The formatter under test.</param>
    /// <param name="input">The LF-normalized input.</param>
    /// <param name="expected">The expected output.</param>
    /// <param name="config">The configuration, or <see langword="null"/> for the defaults.</param>
    /// <remarks>
    ///     The document is parsed once and shared, with one context; every thread has its own edit set. The threads
    ///     wait at a barrier so that they start together, and each formats the document many times to keep them
    ///     overlapping. Every thread's edits must equal those of a single call and must patch the text to the
    ///     expected result.
    /// </remarks>
    public static void AssertConcurrentFormatting(IBlockFormatter formatter, string input, string expected, LintMarkConfig? config = null)
    {
        // The single-threaded result is the reference
        const int threadCount = 8;
        const int rounds = 50;
        var document = Parse(input);
        var context = new FormatContext(document, config ?? LintMarkConfig.Default);
        var blocks = document.Blocks.Where(formatter.CanFormat).ToList();
        Assert.NotEmpty(blocks);
        var reference = FormatBlocks(formatter, context, blocks);
        Assert.Equal(expected, SourcePatcher.Apply(document.Source, reference));

        // Every thread formats the shared document from the same starting signal
        var results = new System.Collections.Immutable.ImmutableArray<TextEdit>[threadCount];
        RunConcurrently(
            threadCount,
            index =>
            {
                for (var round = 0; round < rounds; round++)
                {
                    results[index] = FormatBlocks(formatter, context, blocks);
                    Assert.Equal(reference, results[index]);
                }
            });

        // No thread saw anything different
        Assert.All(results, result => Assert.Equal(reference, result));
    }

    /// <summary>
    ///     Runs a body on several dedicated threads that start together, and rethrows the first failure on the calling thread.
    /// </summary>
    /// <param name="threadCount">The number of threads.</param>
    /// <param name="body">The body, called once per thread with the index of the thread after every thread is ready.</param>
    /// <remarks>
    ///     Dedicated threads (not pool threads) are used so that all of them can wait at the barrier at once, which
    ///     makes the bodies overlap.
    /// </remarks>
    public static void RunConcurrently(int threadCount, Action<int> body)
    {
        using var gate = new Barrier(threadCount);
        var failures = new Exception?[threadCount];
        var threads = Enumerable.Range(0, threadCount).Select(index => new Thread(() =>
        {
            try
            {
                gate.SignalAndWait();
                body(index);
            }
            catch (Exception exception)
            {
                failures[index] = exception;
            }
        })).ToList();
        threads.ForEach(thread => thread.Start());
        threads.ForEach(thread => thread.Join());
        var failure = failures.FirstOrDefault(exception => exception is not null);
        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    /// <summary>
    ///     Formats blocks with a formatter into a new edit set.
    /// </summary>
    /// <param name="formatter">The formatter.</param>
    /// <param name="context">The context.</param>
    /// <param name="blocks">The blocks to format.</param>
    /// <returns>The edits in order.</returns>
    private static System.Collections.Immutable.ImmutableArray<TextEdit> FormatBlocks(IBlockFormatter formatter, FormatContext context, IEnumerable<BlockInfo> blocks)
    {
        var edits = new EditSet();
        foreach (var block in blocks)
        {
            formatter.Format(context, block, edits);
        }

        return edits.ToImmutable();
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
