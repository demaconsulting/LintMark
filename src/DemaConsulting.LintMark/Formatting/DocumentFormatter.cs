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
using DemaConsulting.LintMark.Configuration;
using DemaConsulting.LintMark.Document;
using DemaConsulting.LintMark.Formatting.Inline;
using DemaConsulting.LintMark.Linting;

namespace DemaConsulting.LintMark.Formatting;

/// <summary>
///     Turns a parsed document into the edits and line ending that bring it into canonical form, without
///     writing anything.
/// </summary>
/// <remarks>
///     <para>
///         The formatter walks the blocks of the document from the last to the first and gives each block to the first
///         registered <see cref="IBlockFormatter"/> that accepts it (the order does not change the result: edits are ordered by
///         position, and a formatter that looks at the edits already proposed sees those of the blocks after its own). A
///         block is left verbatim when no formatter accepts it, or when any rule of the accepting formatter is suppressed
///         anywhere inside the block. A block that a formatter accepts is <em>claimed</em> (also when it is suppressed): only
///         that formatter edits its lines.
///     </para>
///     <para>
///         What no formatter claims is handled by the rules that apply to every document: trailing whitespace and
///         tabs in text (<see cref="LeafWhitespace"/>), runs of blank lines (<see cref="BlankLinePass"/>), the blank
///         lines that separate headings, fenced code, lists and tables from their neighbors
///         (<see cref="BlockSpacingPass"/>) and the end of the file (<see cref="TrailerPass"/>). They never touch
///         code, HTML, math, front matter, grid tables or blocks the tool does not model
///         (<see cref="LineOwnership"/> decides which lines those are), and they honor suppression comments at
///         block granularity. Every edit of such an always-applied rule is also reported as a formatting violation
///         at the place it changes, so that linting a file is formatting it and listing what would change. The
///         inline rules (emphasis, strong emphasis and bare URLs) are applied the same way: each paragraph that no
///         formatter claims is given to the <see cref="InlineNormalizer"/>, when one is registered.
///     </para>
///     <para>
///         The line ending is decided here, not by an edit, because the LF-normalized text has no line ending
///         to edit. The target is the configuration's line ending, or the file's majority ending when the
///         configuration gives none. A file with any terminator that differs from the target gets one
///         <c>line-ending</c> diagnostic at line 1, column 1. That rule is always applied: it cannot be
///         configured or suppressed, because a whole-file property has no partial form.
///     </para>
///     <para>
///         A document that is <see cref="ParsedDocument.IsFormatUnsafe"/> is never formatted: the result has no
///         edit and no diagnostic, not even for line endings, so rewriting leaves the file byte for byte as it was.
///     </para>
///     <para>
///         Thread safety: an instance holds an immutable list of formatters that must themselves be safe for
///         concurrent use (see <see cref="IBlockFormatter"/>); <see cref="Format"/> keeps its state in local
///         variables, so one instance may serve every worker.
///     </para>
/// </remarks>
internal sealed class DocumentFormatter
{
    /// <summary>
    ///     The identifier of the always-applied rule that makes line endings uniform.
    /// </summary>
    public const string LineEndingRuleId = RuleCatalog.LineEndingRuleId;

    /// <summary>
    ///     The registered formatters, in priority order.
    /// </summary>
    private readonly ImmutableArray<IBlockFormatter> _formatters;

    /// <summary>
    ///     The inline normalizer that handles the paragraphs no block formatter claims, or <see langword="null"/> for none.
    /// </summary>
    private readonly InlineNormalizer? _inline;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DocumentFormatter"/> class.
    /// </summary>
    /// <param name="formatters">
    ///     The block formatters in priority order (the first that accepts a block formats it), or
    ///     <see langword="null"/> for none. The list is copied.
    /// </param>
    /// <param name="inline">
    ///     The inline normalizer that is asked for each paragraph that no block formatter claims, or
    ///     <see langword="null"/> for none (then no inline rule is applied).
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when an element of <paramref name="formatters"/> is null.</exception>
    public DocumentFormatter(IReadOnlyList<IBlockFormatter>? formatters = null, InlineNormalizer? inline = null)
    {
        // Copy to an immutable array so a caller cannot change the list afterwards, and reject null elements up front
        ImmutableArray<IBlockFormatter> copy = formatters is null ? [] : ImmutableArray.CreateRange(formatters);
        if (copy.Any(static formatter => formatter is null))
        {
            throw new ArgumentNullException(nameof(formatters), "The formatters must not contain null.");
        }

        _formatters = copy;
        _inline = inline;
    }

    /// <summary>
    ///     Formats a document.
    /// </summary>
    /// <param name="document">The parsed document. Must not be null.</param>
    /// <param name="config">The effective configuration of the file. Must not be null.</param>
    /// <returns>The edits, the line ending to write and the formatting violations found.</returns>
    /// <remarks>
    ///     Pure: nothing is written and the document is not changed. The edits are not applied here, so the
    ///     caller can still verify the result before anything reaches a file.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when two formatters propose conflicting edits (a defect of a formatter).</exception>
    public FormatResult Format(ParsedDocument document, LintMarkConfig config)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(config);

        // A document that cannot be trusted is never touched
        var source = document.Source;
        if (document.IsFormatUnsafe)
        {
            return new FormatResult([], source.MajorityLineEnding, []);
        }

        // Find out which lines a block formatter claims, which are protected, and which lie in containers
        var layout = new LineLayout(source);
        var ownership = LineOwnership.Build(document, layout, block => FindFormatter(block) is not null);

        // The passes handle what no block formatter claims: whitespace, inline text, blank lines, block spacing and the end of the
        // file. They run first, so that a block formatter that depends on the lines around its block sees their edits too
        var edits = new EditSet();
        AddLeafEdits(document, config, layout, ownership, edits);
        BlankLinePass.Apply(document, layout, ownership, config, edits);
        BlockSpacingPass.Apply(document, layout, ownership, edits);
        TrailerPass.Apply(document, layout, ownership, config, edits);

        // Collect the edits of the first formatter that accepts each block, skipping blocks with suppressed rules. The
        // blocks are visited from the end of the document to its start, so that a formatter that depends on the lines
        // after its block (a list must not move over a line that would then belong to it) sees the edits that those
        // blocks propose; the result does not depend on the order in which edits are added
        var context = new FormatContext(document, config, layout, ownership);
        for (var i = document.Blocks.Length - 1; i >= 0; i--)
        {
            var block = document.Blocks[i];
            var formatter = FindFormatter(block);
            if (formatter is null || IsSuppressed(document, block, formatter))
            {
                continue;
            }

            formatter.Format(context, block, edits);
        }

        // The line ending is the configured one, or else the one the file uses most
        var target = config.EndOfLine ?? source.MajorityLineEnding;
        var result = edits.ToImmutable();
        return new FormatResult(result, target, [.. CheckLineEndings(source, target), .. ReportEdits(source, result)]);
    }

    /// <summary>
    ///     Finds the first registered formatter that accepts a block.
    /// </summary>
    /// <param name="block">The block.</param>
    /// <returns>The formatter, or <see langword="null"/> when none accepts the block.</returns>
    private IBlockFormatter? FindFormatter(BlockInfo block) =>
        _formatters.FirstOrDefault(candidate => candidate.CanFormat(block));

    /// <summary>
    ///     Adds the whitespace edits for the blocks that no block formatter claims.
    /// </summary>
    /// <param name="document">The parsed document.</param>
    /// <param name="config">The effective configuration of the file.</param>
    /// <param name="layout">The line arithmetic of the document.</param>
    /// <param name="ownership">Which lines belong to which block.</param>
    /// <param name="edits">Receives the edits.</param>
    /// <remarks>
    ///     Paragraphs and headings go through the inline structure (hard breaks and protected spans matter).
    ///     Table rows, single-line link reference definitions, thematic breaks and lines that hold only a
    ///     container marker only lose their trailing whitespace. Protected blocks and claimed blocks are skipped.
    ///     A paragraph is also given to the inline normalizer, when there is one, which edits the emphasis
    ///     delimiters and bare URLs inside its text (never the whitespace the other edits handle).
    /// </remarks>
    private void AddLeafEdits(ParsedDocument document, LintMarkConfig config, LineLayout layout, LineOwnership ownership, EditSet edits)
    {
        // Blocks first: each unclaimed, unprotected leaf block is handled by the kind of whitespace it can hold
        foreach (var block in document.Blocks)
        {
            if (LineOwnership.IsContainer(block.Kind) || FindFormatter(block) is not null)
            {
                continue;
            }

            var (first, last) = LineOwnership.LinesOf(layout, block);
            if (ownership.IsProtected(first))
            {
                continue;
            }

            switch (block.Kind)
            {
                case BlockKind.Paragraph or BlockKind.Heading:
                    var trailing = !IsSuppressedIn(document, block.Start, block.EndInclusive, RuleCatalog.TrailingWhitespaceRuleId);
                    var tabs = !IsSuppressedIn(document, block.Start, block.EndInclusive, RuleCatalog.HardTabsRuleId);
                    if (trailing || tabs)
                    {
                        LeafWhitespace.AddBlockEdits(layout, block, trailing, tabs, edits);
                    }

                    if (block.Kind == BlockKind.Paragraph)
                    {
                        _inline?.AddBlockEdits(document, block, config, edits);
                    }

                    break;

                case BlockKind.Table or BlockKind.ThematicBreak or BlockKind.LinkReferenceDefinition:
                    for (var line = first; line <= last; line++)
                    {
                        AddLineTrailing(document, layout, line, edits);
                    }

                    break;
            }
        }

        // Lines that no leaf block covers and that are not blank hold container markers only
        for (var line = 0; line < layout.LineCount; line++)
        {
            if (ownership.LeafAt(line) is null && !layout.IsBlank(line))
            {
                AddLineTrailing(document, layout, line, edits);
            }
        }
    }

    /// <summary>
    ///     Adds the trailing whitespace edit of one line unless the rule is suppressed there.
    /// </summary>
    /// <param name="document">The parsed document.</param>
    /// <param name="layout">The line arithmetic of the document.</param>
    /// <param name="line">The zero-based line.</param>
    /// <param name="edits">Receives the edit.</param>
    private static void AddLineTrailing(ParsedDocument document, LineLayout layout, int line, EditSet edits)
    {
        if (!IsSuppressedIn(document, layout.LineStart(line), layout.LineEnd(line), RuleCatalog.TrailingWhitespaceRuleId))
        {
            LeafWhitespace.AddTrailingEdit(layout, line, edits);
        }
    }

    /// <summary>
    ///     Determines whether a rule is suppressed anywhere in a range of offsets.
    /// </summary>
    /// <param name="document">The parsed document that holds the suppressions.</param>
    /// <param name="start">Offset of the first character of the range.</param>
    /// <param name="endInclusive">Offset of the last character of the range (or of the line feed that ends the last line).</param>
    /// <param name="ruleId">The rule.</param>
    /// <returns><see langword="true"/> when a suppression covers part of the range.</returns>
    private static bool IsSuppressedIn(ParsedDocument document, int start, int endInclusive, string ruleId) =>
        document.Suppressions.IsSuppressedRange(start, endInclusive + 1, ruleId);

    /// <summary>
    ///     Turns the edits of the always-applied formatting rules into diagnostics.
    /// </summary>
    /// <param name="source">The source document (for the path and the positions).</param>
    /// <param name="edits">The edits, ordered by position.</param>
    /// <returns>One diagnostic per rule and line, in the order of the edits; none for edits of rules that are not always applied.</returns>
    /// <remarks>
    ///     Linting a file is formatting it and reporting what would change, so every edit of an always-applied
    ///     rule is a violation at the place it changes. The position of an insertion is the line it is inserted
    ///     before. An edit of a configurable rule (a block formatter that a test installs, for instance) is not
    ///     reported here: reporting belongs to those rules.
    /// </remarks>
    private static ImmutableArray<Diagnostic> ReportEdits(SourceDocument source, ImmutableArray<TextEdit> edits)
    {
        var reported = new HashSet<(string RuleId, int Line)>();
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        foreach (var edit in edits)
        {
            if (!RuleCatalog.TryGet(edit.RuleId, out var rule)
                || rule.IsConfigurable
                || rule.Kind != DiagnosticKind.Formatting
                || rule.Id == LineEndingRuleId)
            {
                continue;
            }

            var (line, column) = source.PositionOf(edit.Start);
            if (reported.Add((rule.Id, line)))
            {
                diagnostics.Add(new Diagnostic(rule.Id, DiagnosticKind.Formatting, source.Path, line, column, rule.Description));
            }
        }

        return diagnostics.ToImmutable();
    }

    /// <summary>
    ///     Determines whether any rule of a formatter is suppressed inside a block.
    /// </summary>
    /// <param name="document">The document holding the suppressions.</param>
    /// <param name="block">The block.</param>
    /// <param name="formatter">The formatter that would format the block.</param>
    /// <returns><see langword="true"/> when the block must be left verbatim.</returns>
    private static bool IsSuppressed(ParsedDocument document, BlockInfo block, IBlockFormatter formatter) =>
        formatter.RuleIds.Any(rule => document.Suppressions.IsSuppressedRange(block.Start, block.EndInclusive + 1, rule));

    /// <summary>
    ///     Reports the line-ending violation of a file, if any.
    /// </summary>
    /// <param name="source">The source document with the counts of the original terminators.</param>
    /// <param name="target">The line ending the file is written with.</param>
    /// <returns>One diagnostic at 1:1 when a terminator differs from the target; otherwise none.</returns>
    private static ImmutableArray<Diagnostic> CheckLineEndings(SourceDocument source, LineEnding target)
    {
        // A terminator that differs from the target makes the file a violation; a file without terminators never is
        var counts = source.LineEndings;
        var other = target == LineEnding.Lf ? counts.CrLfCount : counts.LfCount;
        if (other == 0)
        {
            return [];
        }

        // Name what the file has now and what it should have
        var expected = Describe(target);
        string message;
        if (counts.IsMixed)
        {
            message = $"Mixed line endings ({counts.CrLfCount} CRLF, {counts.LfCount} LF); expected {expected}";
        }
        else
        {
            var actual = Describe(target == LineEnding.Lf ? LineEnding.CrLf : LineEnding.Lf);
            message = $"Line endings are {actual}; expected {expected}";
        }

        return [new Diagnostic(LineEndingRuleId, DiagnosticKind.Formatting, source.Path, 1, 1, message)];
    }

    /// <summary>
    ///     Names a line ending for messages.
    /// </summary>
    /// <param name="ending">The line ending.</param>
    /// <returns><c>CRLF</c> or <c>LF</c>.</returns>
    private static string Describe(LineEnding ending) => ending == LineEnding.CrLf ? "CRLF" : "LF";
}
