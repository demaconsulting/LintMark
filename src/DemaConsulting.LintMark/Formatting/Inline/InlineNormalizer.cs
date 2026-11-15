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
using DemaConsulting.LintMark.Linting;
using Markdig.Syntax;

namespace DemaConsulting.LintMark.Formatting.Inline;

/// <summary>
///     Writes the emphasis, strong emphasis, bare URLs and code spans of a paragraph in the canonical form: the configured
///     delimiter character, URLs wrapped in angle brackets, and code spans without redundant padding.
/// </summary>
/// <remarks>
///     <para>
///         Four rules are handled: <c>emphasis</c> (<c>_x_</c> and <c>*x*</c> are the same emphasis; the
///         <c>emphasis</c> setting decides which is written), <c>strong</c> (the same for <c>__x__</c> and
///         <c>**x**</c>), <c>bare-urls</c> (a bare <c>http://</c>, <c>https://</c> or <c>ftp://</c> URL becomes
///         <c>&lt;url&gt;</c>; the setting <c>leave</c> switches the rule off) and <c>code-span-space</c> (a code span
///         padded with one space on each side loses both spaces, unless its content begins or ends with a backtick;
///         the rule has no setting). Only paragraphs are handled: headings
///         and table cells keep their text as written, and so do <c>www.</c> addresses, e-mail addresses and
///         <c>mailto:</c> links, which read differently when they are wrapped.
///     </para>
///     <para>
///         The normalizer is not a block formatter (it claims no lines): <see cref="DocumentFormatter"/> asks it
///         for each paragraph that no block formatter claims, so the whitespace rules keep their lines. Its edits
///         replace characters inside the text (a delimiter, the first and the last character of a URL) or delete the
///         padding inside a code span, never the whitespace at the ends of a line and never insert at a position, so they
///         cannot conflict with the edits of those rules.
///     </para>
///     <para>
///         Candidates come from the inline syntax tree with strict guards (see <see cref="InlineCandidates"/>), which leave a
///         paragraph as written whenever the edits could change how it is read (for example when it holds a literal
///         <c>&lt;</c>). A paragraph is also left alone when the document defines link
///         references and the paragraph holds a square bracket: a rewritten label could then stop matching its definition.
///         A rule that is suppressed anywhere in the paragraph is not applied to it.
///     </para>
///     <para>
///         Thread safety: stateless; one instance serves every worker thread.
///     </para>
/// </remarks>
internal sealed class InlineNormalizer
{
    /// <summary>
    ///     Gets the identifiers of the rules this normalizer enforces.
    /// </summary>
    public ImmutableArray<string> RuleIds { get; } =
    [
        RuleCatalog.EmphasisRuleId,
        RuleCatalog.StrongRuleId,
        RuleCatalog.BareUrlsRuleId,
        RuleCatalog.CodeSpanSpaceRuleId
    ];

#pragma warning disable S2325 // An instance is registered with the document formatter, like a block formatter: the contract is instance-based
    /// <summary>
    ///     Proposes the edits that bring the emphasis, strong emphasis and bare URLs of a paragraph into canonical form.
    /// </summary>
    /// <param name="document">The parsed document the paragraph belongs to. Must not be null.</param>
    /// <param name="block">The paragraph. Must be a paragraph block of <paramref name="document"/>.</param>
    /// <param name="config">The effective configuration. Must not be null.</param>
    /// <param name="edits">Receives the edits. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="block"/> is not a paragraph.</exception>
    /// <exception cref="InvalidOperationException">Thrown when an edit conflicts with another (a defect, not a property of the document).</exception>
    public void AddBlockEdits(ParsedDocument document, BlockInfo block, LintMarkConfig config, EditSet edits)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(block);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(edits);
        if (block.Kind != BlockKind.Paragraph)
        {
            throw new ArgumentException($"The block must be a paragraph, not {block.Kind}.", nameof(block));
        }

        // Only a paragraph with inline content can be rewritten, and only by the rules that are not suppressed in it
        if (block.Node is not ParagraphBlock { Inline: { } inline })
        {
            return;
        }

        var emphasis = IsSuppressed(document, block, RuleCatalog.EmphasisRuleId) ? (EmphasisMarker?)null : config.Emphasis;
        var strong = IsSuppressed(document, block, RuleCatalog.StrongRuleId) ? (EmphasisMarker?)null : config.Strong;
        var urls = config.BareUrls == BareUrlStyle.Autolink && !IsSuppressed(document, block, RuleCatalog.BareUrlsRuleId);
        var codeSpaces = !IsSuppressed(document, block, RuleCatalog.CodeSpanSpaceRuleId);
        if (emphasis is null && strong is null && !urls && !codeSpaces)
        {
            return;
        }

        // A label that is rewritten could stop matching its definition, so such a paragraph is left alone
        var text = document.Source.Text;
        if (document.References.Definitions.Count > 0 && text.AsSpan(block.Start, block.EndInclusive + 1 - block.Start).Contains('['))
        {
            return;
        }

        var candidates = InlineCandidates.Find(text, inline, emphasis, strong, urls, codeSpaces);
        if (candidates.IsEmpty)
        {
            return;
        }

        foreach (var edit in candidates)
        {
            edits.Add(edit);
        }
    }
#pragma warning restore S2325

    /// <summary>
    ///     Determines whether a rule is suppressed anywhere in a block.
    /// </summary>
    /// <param name="document">The document that holds the suppressions.</param>
    /// <param name="block">The block.</param>
    /// <param name="ruleId">The rule.</param>
    /// <returns><see langword="true"/> when a suppression covers part of the block.</returns>
    private static bool IsSuppressed(ParsedDocument document, BlockInfo block, string ruleId) =>
        document.Suppressions.IsSuppressedRange(block.Start, block.EndInclusive + 1, ruleId);
}
