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

using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using DemaConsulting.LintMark.Configuration;

namespace DemaConsulting.LintMark.Linting;

/// <summary>
///     The single registry of every rule identifier the tool knows: kebab-case ids, their legacy rule-number
///     aliases (such as <c>MD013</c>), their kind and default state, and the aliases of rules that no longer exist.
/// </summary>
/// <remarks>
///     <para>
///         The catalog is the one place where a rule identifier is defined, so that a configuration can be
///         validated against it, a diagnostic can be traced to a registered rule, and the default state of every
///         configurable rule has one home. Three groups are registered: the configurable rules (the <c>rules</c>
///         setting of a configuration can switch them on or off), the always-applied formatting rules
///         (<c>line-ending</c>, the whitespace, blank-line, block-spacing, heading, code block, thematic break,
///         list, block quote, inline and final newline rules, some of which carry legacy rule-number aliases), and the identifiers under
///         which the tool reports its own failures (file, format and configuration problems). The last two
///         groups are known but not configurable, so a configuration that names one (by id or by alias) is
///         still rejected as an unknown rule; their aliases do resolve where a suppression comment names a rule.
///         Aliases of dropped rules are recognized (so a legacy entry is accepted and ignored) but never
///         resolve to a rule.
///     </para>
///     <para>
///         Names are compared with an ordinal comparison that ignores case, independent of the current culture, so
///         the result never depends on the machine's locale.
///     </para>
///     <para>
///         Thread safety: all data is immutable and initialized once; every member is safe for concurrent use.
///     </para>
/// </remarks>
internal static class RuleCatalog
{
    /// <summary>
    ///     The identifier of the always-applied rule that makes line endings uniform.
    /// </summary>
    public const string LineEndingRuleId = "line-ending";

    /// <summary>
    ///     The identifier of the always-applied rule that removes trailing whitespace.
    /// </summary>
    public const string TrailingWhitespaceRuleId = "trailing-whitespace";

    /// <summary>
    ///     The identifier of the always-applied rule that replaces tab characters in text by spaces.
    /// </summary>
    public const string HardTabsRuleId = "hard-tabs";

    /// <summary>
    ///     The identifier of the always-applied rule that limits consecutive blank lines.
    /// </summary>
    public const string MaxBlankLinesRuleId = "max-blank-lines";

    /// <summary>
    ///     The identifier of the always-applied rule that writes headings in the configured style.
    /// </summary>
    public const string HeadingStyleRuleId = "heading-style";

    /// <summary>
    ///     The identifier of the always-applied rule that puts exactly one space after the number signs of a heading.
    /// </summary>
    public const string HeadingSpaceRuleId = "heading-space";

    /// <summary>
    ///     The identifier of the always-applied rule that starts headings at the left margin.
    /// </summary>
    public const string HeadingStartLeftRuleId = "heading-start-left";

    /// <summary>
    ///     The identifier of the always-applied rule that surrounds headings with blank lines.
    /// </summary>
    public const string HeadingBlankLinesRuleId = "heading-blank-lines";

    /// <summary>
    ///     The identifier of the always-applied rule that surrounds fenced code blocks with blank lines.
    /// </summary>
    public const string FenceBlankLinesRuleId = "fence-blank-lines";

    /// <summary>
    ///     The identifier of the always-applied rule that surrounds lists with blank lines.
    /// </summary>
    public const string ListBlankLinesRuleId = "list-blank-lines";

    /// <summary>
    ///     The identifier of the always-applied rule that surrounds tables with blank lines.
    /// </summary>
    public const string TableBlankLinesRuleId = "table-blank-lines";

    /// <summary>
    ///     The identifier of the always-applied rule that puts a pipe at both ends of every line of a table.
    /// </summary>
    public const string TablePipeStyleRuleId = "table-pipe-style";

    /// <summary>
    ///     The identifier of the always-applied rule that writes the cells of a table in the configured layout (compact or aligned).
    /// </summary>
    public const string TableColumnStyleRuleId = "table-column-style";

    /// <summary>
    ///     The identifier of the configurable lint-only rule that reports a table that is wider than the line length even when compact.
    /// </summary>
    public const string TableTooWideRuleId = "table-too-wide";

    /// <summary>
    ///     The identifier of the configurable lint-only rule that reports a table row with another number of cells than the header.
    /// </summary>
    public const string TableColumnCountRuleId = "table-column-count";

    /// <summary>
    ///     The identifier of the configurable lint-only rule that reports a grid table, which is always left as written.
    /// </summary>
    public const string TableGridUnformattedRuleId = "table-grid-unformatted";

    /// <summary>
    ///     The identifier of the configurable lint-only rule that reports a heading that skips a level.
    /// </summary>
    public const string HeadingIncrementRuleId = "heading-increment";

    /// <summary>
    ///     The identifier of the configurable lint-only rule that reports a heading whose text another heading of the document has too.
    /// </summary>
    public const string HeadingDuplicateRuleId = "heading-duplicate";

    /// <summary>
    ///     The identifier of the configurable lint-only rule that reports a heading that ends with punctuation.
    /// </summary>
    public const string HeadingTrailingPunctuationRuleId = "heading-trailing-punctuation";

    /// <summary>
    ///     The identifier of the configurable lint-only rule that reports a paragraph that is only emphasis where a heading is meant.
    /// </summary>
    public const string EmphasisAsHeadingRuleId = "emphasis-as-heading";

    /// <summary>
    ///     The identifier of the configurable lint-only rule that reports a line that starts like a heading but has no space after the number signs.
    /// </summary>
    public const string HeadingMissingSpaceRuleId = "heading-missing-space";

    /// <summary>
    ///     The identifier of the configurable lint-only rule that reports a line written like a closed heading without the spaces inside the number signs.
    /// </summary>
    public const string HeadingClosedMissingSpaceRuleId = "heading-closed-missing-space";

    /// <summary>
    ///     The identifier of the configurable lint-only rule that reports an image without alternative text.
    /// </summary>
    public const string ImageAltTextRuleId = "image-alt-text";

    /// <summary>
    ///     The identifier of the configurable lint-only rule that reports a link without a destination.
    /// </summary>
    public const string LinkEmptyRuleId = "link-empty";

    /// <summary>
    ///     The identifier of the configurable lint-only rule that reports a link to a fragment that no heading or anchor provides.
    /// </summary>
    public const string LinkFragmentRuleId = "link-fragment";

    /// <summary>
    ///     The identifier of the configurable lint-only rule that reports a reference link whose label is not defined.
    /// </summary>
    public const string LinkReferenceUndefinedRuleId = "link-reference-undefined";

    /// <summary>
    ///     The identifier of the configurable lint-only rule that reports a link reference definition that is never used or is defined twice.
    /// </summary>
    public const string LinkDefinitionUnusedRuleId = "link-definition-unused";

    /// <summary>
    ///     The identifier of the configurable lint-only rule that reports a link written with the brackets and the parentheses reversed.
    /// </summary>
    public const string LinkReversedRuleId = "link-reversed";

    /// <summary>
    ///     The identifier of the configurable lint-only rule that reports a link whose text starts or ends with a space.
    /// </summary>
    public const string LinkSpaceRuleId = "link-space";

    /// <summary>
    ///     The identifier of the configurable lint-only rule that reports a link whose text says nothing about the destination.
    /// </summary>
    public const string DescriptiveLinkTextRuleId = "descriptive-link-text";

    /// <summary>
    ///     The identifier of the configurable lint-only rule that reports a fenced code block without a language.
    /// </summary>
    public const string CodeFenceLanguageRuleId = "code-fence-language";

    /// <summary>
    ///     The identifier of the configurable lint-only rule that reports a fenced code block that is never closed.
    /// </summary>
    public const string FenceUnclosedRuleId = "fence-unclosed";

    /// <summary>
    ///     The identifier of the configurable lint-only rule that reports a footnote definition that is never referenced.
    /// </summary>
    public const string FootnoteUnusedRuleId = "footnote-unused";

    /// <summary>
    ///     The identifier of the configurable lint-only rule that reports a blank line that separates two block quotes.
    /// </summary>
    public const string BlockQuoteBlankLineRuleId = "blockquote-blank-line";

    /// <summary>
    ///     The identifier of the configurable lint-only rule that reports a code block whose lines all start with a dollar sign.
    /// </summary>
    public const string DollarSignInShellCodeRuleId = "dollar-sign-in-shell-code";

    /// <summary>
    ///     The identifier of the configurable lint-only rule that reports emphasis with a space on one side only.
    /// </summary>
    public const string EmphasisSpaceRuleId = "emphasis-space";

    /// <summary>
    ///     The identifier of the always-applied rule that writes thematic breaks in the configured style.
    /// </summary>
    public const string ThematicBreakRuleId = "thematic-break";

    /// <summary>
    ///     The identifier of the always-applied rule that writes code blocks in the configured style.
    /// </summary>
    public const string CodeBlockStyleRuleId = "code-block-style";

    /// <summary>
    ///     The identifier of the always-applied rule that writes code fences with the configured character.
    /// </summary>
    public const string CodeFenceRuleId = "code-fence";

    /// <summary>
    ///     The identifier of the always-applied rule that decides whether a file ends with a line ending.
    /// </summary>
    public const string FinalNewlineRuleId = "final-newline";

    /// <summary>
    ///     The identifier of the always-applied rule that writes the items of a bullet list with the configured marker.
    /// </summary>
    public const string BulletRuleId = "bullet";

    /// <summary>
    ///     The identifier of the always-applied rule that numbers the items of an ordered list in the configured style.
    /// </summary>
    public const string OrderedStyleRuleId = "ordered-style";

    /// <summary>
    ///     The identifier of the always-applied rule that aligns the items of a list and indents nested bullet lists.
    /// </summary>
    public const string ListIndentRuleId = "list-indent";

    /// <summary>
    ///     The identifier of the always-applied rule that puts exactly one space after a list marker.
    /// </summary>
    public const string ListMarkerSpaceRuleId = "list-marker-space";

    /// <summary>
    ///     The identifier of the always-applied rule that puts exactly one space after a block quote marker.
    /// </summary>
    public const string BlockQuoteSpaceRuleId = "blockquote-space";

    /// <summary>
    ///     The identifier of the always-applied rule that writes emphasis with the configured delimiter.
    /// </summary>
    public const string EmphasisRuleId = "emphasis";

    /// <summary>
    ///     The identifier of the always-applied rule that writes strong emphasis with the configured delimiter.
    /// </summary>
    public const string StrongRuleId = "strong";

    /// <summary>
    ///     The identifier of the always-applied rule that wraps bare URLs in angle brackets.
    /// </summary>
    public const string BareUrlsRuleId = "bare-urls";

    /// <summary>
    ///     The identifier of the always-applied rule that removes the spaces that pad a code span on both sides.
    /// </summary>
    public const string CodeSpanSpaceRuleId = "code-span-space";

    /// <summary>
    ///     The identifier of the configurable rule that wraps paragraphs that are wider than the line length.
    /// </summary>
    public const string LineLengthRuleId = "line-length";

    /// <summary>
    ///     The identifier of the configurable rule that reports paragraphs not laid out the way the sentence or semantic wrap
    ///     mode asks for.
    /// </summary>
    public const string ParagraphWrapRuleId = "paragraph-wrap";

    /// <summary>
    ///     The identifier of the configurable rule that converts long inline links to reference links (MD054, partial). It
    ///     only acts when the <c>link-refactor</c> setting is enabled.
    /// </summary>
    public const string LinkRefactorRuleId = "link-refactor";

    /// <summary>
    ///     The identifier reported for a grid-table-looking line that is not a valid grid table.
    /// </summary>
    public const string GridTableInvalidRuleId = "grid-table-invalid";

    /// <summary>
    ///     The identifier reported for a file that cannot be read.
    /// </summary>
    public const string FileReadErrorRuleId = "file-read-error";

    /// <summary>
    ///     The identifier reported for a file that is not valid UTF-8 or has an unsupported byte order mark.
    /// </summary>
    public const string FileEncodingErrorRuleId = "file-encoding-error";

    /// <summary>
    ///     The identifier reported for a file that contains a bare carriage return.
    /// </summary>
    public const string FileLineEndingErrorRuleId = "file-line-ending-error";

    /// <summary>
    ///     The identifier reported for a file whose content makes the Markdown parser throw.
    /// </summary>
    public const string FileParseErrorRuleId = "file-parse-error";

    /// <summary>
    ///     The identifier reported when formatting a file fails.
    /// </summary>
    public const string FileFormatErrorRuleId = "file-format-error";

    /// <summary>
    ///     The identifier reported when a rewritten file cannot be written.
    /// </summary>
    public const string FileWriteErrorRuleId = "file-write-error";

    /// <summary>
    ///     The identifier reported for a problem in a configuration source.
    /// </summary>
    public const string ConfigErrorRuleId = "config-error";

    /// <summary>
    ///     Every registered rule, in declaration order before sorting.
    /// </summary>
    private static readonly RuleInfo[] Table =
    [
        // Configurable rules (formatting)
        Configurable(LineLengthRuleId, DiagnosticKind.Formatting, "A line is longer than the configured line length.", true, "MD013"),
        Configurable(ParagraphWrapRuleId, DiagnosticKind.Formatting, "A paragraph is not wrapped per the configured sentence or semantic style.", true),
        Configurable(LinkRefactorRuleId, DiagnosticKind.Formatting, "An inline link is converted to a reference link (only when the link-refactor setting is enabled).", true, "MD054"),

        // Configurable rules (lint only)
        Configurable(HeadingIncrementRuleId, DiagnosticKind.LintOnly, "A heading level skips a level.", true, "MD001"),
        Configurable(HeadingDuplicateRuleId, DiagnosticKind.LintOnly, "Two headings in the same document have the same text.", true, "MD024"),
        Configurable(HeadingTrailingPunctuationRuleId, DiagnosticKind.LintOnly, "A heading ends with punctuation.", true, "MD026"),
        Configurable(EmphasisAsHeadingRuleId, DiagnosticKind.LintOnly, "Emphasis is used where a heading is meant.", true, "MD036"),
        Configurable(HeadingMissingSpaceRuleId, DiagnosticKind.LintOnly, "A line starts with number signs but has no space after them, so it is not a heading.", true, "MD018"),
        Configurable(HeadingClosedMissingSpaceRuleId, DiagnosticKind.LintOnly, "A line looks like a closed heading but has no spaces inside the number signs, so it is not a heading.", true, "MD020"),
        Configurable(ImageAltTextRuleId, DiagnosticKind.LintOnly, "An image has no alternative text.", true, "MD045"),
        Configurable(LinkEmptyRuleId, DiagnosticKind.LintOnly, "A link has no destination.", true, "MD042"),
        Configurable(LinkFragmentRuleId, DiagnosticKind.LintOnly, "A link fragment does not match any heading.", true, "MD051"),
        Configurable(LinkReferenceUndefinedRuleId, DiagnosticKind.LintOnly, "A reference link uses a label that is not defined.", true, "MD052"),
        Configurable(LinkDefinitionUnusedRuleId, DiagnosticKind.LintOnly, "A link reference definition is never used.", true, "MD053"),
        Configurable(LinkReversedRuleId, DiagnosticKind.LintOnly, "A link is written with the brackets and parentheses reversed.", true, "MD011"),
        Configurable(LinkSpaceRuleId, DiagnosticKind.LintOnly, "The text of a link starts or ends with a space.", true, "MD039"),
        Configurable(DescriptiveLinkTextRuleId, DiagnosticKind.LintOnly, "A link text is not descriptive.", false, "MD059"),
        Configurable(CodeFenceLanguageRuleId, DiagnosticKind.LintOnly, "A fenced code block has no language.", true, "MD040"),
        Configurable(TableTooWideRuleId, DiagnosticKind.LintOnly, "A table is wider than the configured line length.", true),
        Configurable(TableColumnCountRuleId, DiagnosticKind.LintOnly, "A table row has a different number of cells than the header.", true, "MD056"),
        Configurable(TableGridUnformattedRuleId, DiagnosticKind.LintOnly, "A grid table is left as written.", false),
        Configurable(FenceUnclosedRuleId, DiagnosticKind.LintOnly, "A fenced code block is never closed.", true),
        Configurable(GridTableInvalidRuleId, DiagnosticKind.LintOnly, "A line looks like a grid table border but the table is not valid.", true),
        Configurable(FootnoteUnusedRuleId, DiagnosticKind.LintOnly, "A footnote definition is never referenced.", true),
        Configurable(BlockQuoteBlankLineRuleId, DiagnosticKind.LintOnly, "A blank line separates two block quotes.", true, "MD028"),
        Configurable(DollarSignInShellCodeRuleId, DiagnosticKind.LintOnly, "A shell command in a code block starts with a dollar sign.", false, "MD014"),
        Configurable(EmphasisSpaceRuleId, DiagnosticKind.LintOnly, "Emphasis has a space inside one of its delimiters but not the other.", true, "MD037"),

        // Always-applied formatting rules
        Fixed(LineEndingRuleId, DiagnosticKind.Formatting, "A line ending differs from the line ending chosen for the file."),
        Applied(TrailingWhitespaceRuleId, "A line ends with trailing whitespace, or a hard break is not exactly two spaces.", "MD009"),
        Applied(HardTabsRuleId, "A tab character is used in text instead of spaces.", "MD010"),
        Applied(MaxBlankLinesRuleId, "More blank lines in a row than the configuration allows.", "MD012"),
        Applied(HeadingStyleRuleId, "A heading is not written in the configured style (setext or closed headings).", "MD003", "MD021"),
        Applied(HeadingSpaceRuleId, "A heading has more or less than one space after its number signs.", "MD019"),
        Applied(HeadingStartLeftRuleId, "A heading is indented.", "MD023"),
        Applied(HeadingBlankLinesRuleId, "A heading is not surrounded by blank lines.", "MD022"),
        Applied(FenceBlankLinesRuleId, "A fenced code block is not surrounded by blank lines.", "MD031"),
        Applied(ListBlankLinesRuleId, "A list is not surrounded by blank lines.", "MD032"),
        Applied(TableBlankLinesRuleId, "A table is not surrounded by blank lines.", "MD058"),
        Applied(TablePipeStyleRuleId, "A line of a table lacks the pipe at its start or at its end.", "MD055"),
        Applied(TableColumnStyleRuleId, "A table is not written in the configured compact or aligned layout.", "MD060"),
        Applied(ThematicBreakRuleId, "A thematic break is not written in the configured style.", "MD035"),
        Applied(CodeBlockStyleRuleId, "A code block is not written in the configured style.", "MD046"),
        Applied(CodeFenceRuleId, "A code fence does not use the configured fence character.", "MD048"),
        Applied(FinalNewlineRuleId, "The file does not end with the configured final line ending.", "MD047"),
        Applied(BulletRuleId, "A bullet list item does not use the configured marker character.", "MD004"),
        Applied(OrderedStyleRuleId, "An ordered list is not numbered in the configured style.", "MD029"),
        Applied(ListIndentRuleId, "The items of a list are not aligned or a nested bullet list is not indented as configured.", "MD005", "MD007"),
        Applied(ListMarkerSpaceRuleId, "A list marker is followed by more than one space.", "MD030"),
        Applied(BlockQuoteSpaceRuleId, "A block quote marker is followed by more than one space.", "MD027"),
        Applied(EmphasisRuleId, "Emphasis is not written with the configured delimiter.", "MD049"),
        Applied(StrongRuleId, "Strong emphasis is not written with the configured delimiter.", "MD050"),
        Applied(BareUrlsRuleId, "A bare URL is not wrapped in angle brackets.", "MD034"),
        Applied(CodeSpanSpaceRuleId, "A code span has a space after its opening backtick and before its closing backtick that it does not need.", "MD038"),

        // File, formatting and configuration problems
        Fixed(FileReadErrorRuleId, DiagnosticKind.LintOnly, "A file cannot be read."),
        Fixed(FileEncodingErrorRuleId, DiagnosticKind.LintOnly, "A file is not valid UTF-8 or has an unsupported byte order mark."),
        Fixed(FileLineEndingErrorRuleId, DiagnosticKind.LintOnly, "A file contains a bare carriage return."),
        Fixed(FileParseErrorRuleId, DiagnosticKind.LintOnly, "The Markdown parser failed on a file."),
        Fixed(FileFormatErrorRuleId, DiagnosticKind.LintOnly, "Formatting a file failed."),
        Fixed(FileWriteErrorRuleId, DiagnosticKind.LintOnly, "A rewritten file could not be written."),
        Fixed(ConfigErrorRuleId, DiagnosticKind.LintOnly, "A configuration source has a problem.")
    ];

    /// <summary>
    ///     Maps every rule id and every alias of the registered rules, ignoring case, to the canonical rule id.
    /// </summary>
    private static readonly FrozenDictionary<string, string> Resolution = Table
        .SelectMany(static rule => rule.Aliases.Select(alias => KeyValuePair.Create(alias, rule.Id))
            .Prepend(KeyValuePair.Create(rule.Id, rule.Id)))
        .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Maps every registered rule id, ignoring case, to its entry.
    /// </summary>
    private static readonly FrozenDictionary<string, RuleInfo> ById =
        Table.ToFrozenDictionary(static rule => rule.Id, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     The aliases of rules that no longer exist, accepted and ignored.
    /// </summary>
    private static readonly FrozenSet<string> Dropped = new[]
    {
        "MD002", "MD006", "MD008", "MD025", "MD033", "MD041", "MD043", "MD044", "MD057"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Gets every registered rule in ordinal order of the id.
    /// </summary>
    public static ImmutableArray<RuleInfo> Rules { get; } = [.. Table.OrderBy(static rule => rule.Id, StringComparer.Ordinal)];

    /// <summary>
    ///     Gets the default state (enabled or not) of every configurable rule, keyed by canonical id with an ordinal comparer.
    /// </summary>
    public static ImmutableSortedDictionary<string, bool> Defaults { get; } = Table
        .Where(static rule => rule.IsConfigurable)
        .ToImmutableSortedDictionary(static rule => rule.Id, static rule => rule.DefaultEnabled, StringComparer.Ordinal);

    /// <summary>
    ///     Gets the legacy rule-number aliases of the configurable rules, mapped to the canonical rule id; keys compare ignoring case.
    /// </summary>
    public static ImmutableSortedDictionary<string, string> Aliases { get; } = Table
        .Where(static rule => rule.IsConfigurable)
        .SelectMany(static rule => rule.Aliases.Select(alias => KeyValuePair.Create(alias, rule.Id)))
        .ToImmutableSortedDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Resolves a rule id or alias of any registered rule to the canonical rule id.
    /// </summary>
    /// <param name="name">A kebab-case rule id or a legacy rule-number alias in any letter case. Must not be null.</param>
    /// <param name="canonicalId">Receives the canonical kebab-case id; <see langword="null"/> when the name is unknown.</param>
    /// <returns>
    ///     <see langword="true"/> when the name identifies a registered rule, configurable or not;
    ///     <see langword="false"/> for an unknown name and for the alias of a dropped rule (see <see cref="IsDroppedAlias"/>).
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
    public static bool TryResolve(string name, [NotNullWhen(true)] out string? canonicalId)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(name);

        return Resolution.TryGetValue(name, out canonicalId);
    }

    /// <summary>
    ///     Resolves a rule id or alias to the canonical id of a rule that a configuration can switch on or off.
    /// </summary>
    /// <param name="name">A kebab-case rule id or a legacy rule-number alias in any letter case. Must not be null.</param>
    /// <param name="canonicalId">Receives the canonical kebab-case id; <see langword="null"/> when the name is not a configurable rule.</param>
    /// <returns>
    ///     <see langword="true"/> when the name identifies a configurable rule; <see langword="false"/> for an unknown
    ///     name, for the alias of a dropped rule, and for a rule that is registered but not configurable.
    /// </returns>
    /// <remarks>
    ///     This is what the <c>rules</c> setting of a configuration accepts: a rule that is always applied, or an
    ///     identifier that only labels a failure, cannot be switched off, so naming it is a configuration error.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
    public static bool TryResolveConfigurable(string name, [NotNullWhen(true)] out string? canonicalId)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(name);

        // A name that resolves but names a rule that cannot be configured is treated as unknown here
        if (Resolution.TryGetValue(name, out var id) && ById[id].IsConfigurable)
        {
            canonicalId = id;
            return true;
        }

        canonicalId = null;
        return false;
    }

    /// <summary>
    ///     Looks up a registered rule by its canonical id.
    /// </summary>
    /// <param name="id">The kebab-case rule id in any letter case (an alias is not accepted). Must not be null.</param>
    /// <param name="rule">Receives the entry; <see langword="null"/> when the id is not registered.</param>
    /// <returns><see langword="true"/> when the rule is registered.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="id"/> is null.</exception>
    public static bool TryGet(string id, [NotNullWhen(true)] out RuleInfo? rule)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(id);

        return ById.TryGetValue(id, out rule);
    }

    /// <summary>
    ///     Determines whether a name is the alias of a rule that no longer exists.
    /// </summary>
    /// <param name="name">The name to test, in any letter case. Must not be null.</param>
    /// <returns><see langword="true"/> when the name is a dropped alias; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
    public static bool IsDroppedAlias(string name)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(name);

        return Dropped.Contains(name);
    }

    /// <summary>
    ///     Creates the entry of a configurable rule.
    /// </summary>
    /// <param name="id">The canonical id.</param>
    /// <param name="kind">The rule kind.</param>
    /// <param name="description">The description.</param>
    /// <param name="defaultEnabled">The default state.</param>
    /// <param name="aliases">The legacy aliases, if any.</param>
    /// <returns>The entry.</returns>
    private static RuleInfo Configurable(string id, DiagnosticKind kind, string description, bool defaultEnabled, params string[] aliases) =>
        new(id, kind, description, [.. aliases], defaultEnabled, IsConfigurable: true);

    /// <summary>
    ///     Creates the entry of an always-applied formatting rule.
    /// </summary>
    /// <param name="id">The canonical id.</param>
    /// <param name="description">The description.</param>
    /// <param name="aliases">The legacy rule-number aliases, if any.</param>
    /// <returns>The entry (formatting kind, enabled, not configurable).</returns>
    /// <remarks>
    ///     The rule is not configurable, so a configuration that names it or one of its aliases is still rejected;
    ///     the aliases resolve for suppression comments.
    /// </remarks>
    private static RuleInfo Applied(string id, string description, params string[] aliases) =>
        new(id, DiagnosticKind.Formatting, description, [.. aliases], DefaultEnabled: true, IsConfigurable: false);

    /// <summary>
    ///     Creates the entry of a rule that is always applied or only labels a failure.
    /// </summary>
    /// <param name="id">The canonical id.</param>
    /// <param name="kind">The rule kind.</param>
    /// <param name="description">The description.</param>
    /// <returns>The entry (enabled, without aliases, not configurable).</returns>
    private static RuleInfo Fixed(string id, DiagnosticKind kind, string description) =>
        new(id, kind, description, [], DefaultEnabled: true, IsConfigurable: false);
}

/// <summary>
///     The registered facts about one rule identifier.
/// </summary>
/// <param name="Id">The canonical kebab-case rule identifier.</param>
/// <param name="Kind">Whether the rule is fixable by rewriting (<see cref="DiagnosticKind.Formatting"/>) or only reported.</param>
/// <param name="Description">A one-sentence description of what the rule reports or enforces.</param>
/// <param name="Aliases">The legacy rule-number aliases (such as <c>MD013</c>) that name the rule; empty when there are none.</param>
/// <param name="DefaultEnabled">Whether the rule is enabled when the configuration does not say.</param>
/// <param name="IsConfigurable">
///     Whether the <c>rules</c> setting of a configuration can switch the rule on or off. Rules that are always
///     applied, and the identifiers under which the tool reports its own failures, are not configurable.
/// </param>
/// <remarks>
///     Immutable; the alias array is an immutable array.
/// </remarks>
internal sealed record RuleInfo(
    string Id,
    DiagnosticKind Kind,
    string Description,
    ImmutableArray<string> Aliases,
    bool DefaultEnabled,
    bool IsConfigurable);

/// <summary>
///     The rules that the command line forces on (<c>--enable</c>) or off (<c>--disable</c>), whatever the configuration says.
/// </summary>
/// <remarks>
///     <para>
///         Names are rule identifiers or legacy rule-number aliases; they are resolved through the <see cref="RuleCatalog"/>
///         when the overrides are created, so a name is either known (and stored under its canonical id) or rejected. Only
///         the rules that a configuration can switch (the lint-only rules and the two optional formatting rules
///         <c>line-length</c> and <c>paragraph-wrap</c>) can be named; the alias of a dropped rule is accepted and ignored,
///         like in the configuration. A rule named by both options is a contradiction and is rejected.
///     </para>
///     <para>
///         Thread safety: immutable; one instance can serve every worker.
///     </para>
/// </remarks>
internal sealed class RuleOverrides
{
    /// <summary>
    ///     Gets the overrides that change nothing.
    /// </summary>
    public static RuleOverrides None { get; } = new([], []);

    /// <summary>
    ///     The canonical ids forced on, in ordinal order.
    /// </summary>
    private readonly ImmutableSortedSet<string> _enabled;

    /// <summary>
    ///     The canonical ids forced off, in ordinal order.
    /// </summary>
    private readonly ImmutableSortedSet<string> _disabled;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RuleOverrides"/> class.
    /// </summary>
    /// <param name="enabled">The canonical ids forced on.</param>
    /// <param name="disabled">The canonical ids forced off.</param>
    private RuleOverrides(IEnumerable<string> enabled, IEnumerable<string> disabled)
    {
        _enabled = ImmutableSortedSet.CreateRange(StringComparer.Ordinal, enabled);
        _disabled = ImmutableSortedSet.CreateRange(StringComparer.Ordinal, disabled);
    }

    /// <summary>
    ///     Gets the canonical ids of the rules forced on, in ordinal order.
    /// </summary>
    public IReadOnlyList<string> Enabled => _enabled;

    /// <summary>
    ///     Gets the canonical ids of the rules forced off, in ordinal order.
    /// </summary>
    public IReadOnlyList<string> Disabled => _disabled;

    /// <summary>
    ///     Gets a value indicating whether the overrides change nothing.
    /// </summary>
    public bool IsEmpty => _enabled.IsEmpty && _disabled.IsEmpty;

    /// <summary>
    ///     Creates the overrides from the values of the command line options.
    /// </summary>
    /// <param name="enable">The values of every <c>--enable</c> option, each a comma-separated list of names. Must not be null or contain null.</param>
    /// <param name="disable">The values of every <c>--disable</c> option, each a comma-separated list of names. Must not be null or contain null.</param>
    /// <returns>The overrides.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument or an element is null.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when a list is blank or has an empty entry, a name is not a rule that can be switched on or off, or a rule is named by both options.
    /// </exception>
    public static RuleOverrides Create(IEnumerable<string> enable, IEnumerable<string> disable)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(enable);
        ArgumentNullException.ThrowIfNull(disable);

        var enabled = Resolve("--enable", enable);
        var disabled = Resolve("--disable", disable);
        var both = enabled.Intersect(disabled, StringComparer.Ordinal).Order(StringComparer.Ordinal).FirstOrDefault();
        if (both is not null)
        {
            throw new ArgumentException($"Rule '{both}' cannot be named by both --enable and --disable");
        }

        return enabled.Count == 0 && disabled.Count == 0 ? None : new RuleOverrides(enabled, disabled);
    }

    /// <summary>
    ///     Applies the overrides to a configuration.
    /// </summary>
    /// <param name="config">The configuration. Must not be null.</param>
    /// <returns>The configuration with the named rules forced on or off; <paramref name="config"/> itself when nothing is overridden.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is null.</exception>
    public LintMarkConfig Apply(LintMarkConfig config)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(config);

        if (IsEmpty)
        {
            return config;
        }

        var rules = config.Rules;
        foreach (var id in _enabled)
        {
            rules = rules.SetItem(id, true);
        }

        foreach (var id in _disabled)
        {
            rules = rules.SetItem(id, false);
        }

        // Naming the opt-in link refactoring on the command line is how a run switches it on: the setting is the feature's
        // own switch, the rule is only a way to switch it off
        var enableLinkRefactor = _enabled.Contains(RuleCatalog.LinkRefactorRuleId);
        return config with
        {
            Rules = rules,
            LinkRefactor = enableLinkRefactor ? config.LinkRefactor with { Enabled = true } : config.LinkRefactor
        };
    }

    /// <summary>
    ///     Resolves the values of one option to canonical rule ids.
    /// </summary>
    /// <param name="option">The option name, for the messages.</param>
    /// <param name="values">The option values.</param>
    /// <returns>The canonical ids, without duplicates; the aliases of dropped rules are left out.</returns>
    /// <exception cref="ArgumentNullException">Thrown when a value is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a value is blank, has an empty entry, or names something that is not a rule that can be switched.</exception>
    private static HashSet<string> Resolve(string option, IEnumerable<string> values)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(values), "The values must not contain null.");
            }
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"{option} requires a comma-separated list of rule ids");
            }

            foreach (var part in value.Split(','))
            {
                var name = part.Trim();
                if (name.Length == 0)
                {
                    throw new ArgumentException($"{option} has an empty rule id in '{value}'");
                }

                if (RuleCatalog.TryResolveConfigurable(name, out var id))
                {
                    ids.Add(id);
                }
                else if (!RuleCatalog.IsDroppedAlias(name))
                {
                    var known = RuleCatalog.TryResolve(name, out _);
                    throw new ArgumentException(known
                        ? $"{option} cannot switch rule '{name}'; it is always applied"
                        : $"{option} names an unknown rule '{name}'");
                }
            }
        }

        return ids;
    }
}
