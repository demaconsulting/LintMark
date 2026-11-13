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
///         setting of a configuration can switch them on or off), the always-applied formatting rule
///         <c>line-ending</c>, and the identifiers under which the tool reports its own failures (file, safety
///         and configuration problems). The last two groups are known but not configurable, so a configuration
///         that names one is still rejected as an unknown rule. Aliases of dropped rules are recognized (so a
///         legacy entry is accepted and ignored) but never resolve to a rule.
///     </para>
///     <para>
///         Names are compared with an ordinal comparison that ignores case, independent of the current culture, so the result never
///         depends on the machine's locale.
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
    ///     The identifier reported when a rewrite would change how the document renders.
    /// </summary>
    public const string HtmlMismatchRuleId = "safety-html-mismatch";

    /// <summary>
    ///     The identifier reported when a rewrite would change Pandoc structure that Markdown rendering does not show.
    /// </summary>
    public const string StructureMismatchRuleId = "safety-structure-mismatch";

    /// <summary>
    ///     The identifier reported when formatting the formatted text again would change it again.
    /// </summary>
    public const string NotIdempotentRuleId = "safety-not-idempotent";

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
        Configurable("line-length", DiagnosticKind.Formatting, "A line is longer than the configured line length.", true, "MD013"),
        Configurable("paragraph-wrap", DiagnosticKind.Formatting, "A paragraph is not wrapped the way the configuration asks for.", true),

        // Configurable rules (lint only)
        Configurable("heading-increment", DiagnosticKind.LintOnly, "A heading level skips a level.", true, "MD001"),
        Configurable("heading-duplicate", DiagnosticKind.LintOnly, "Two headings in the same document have the same text.", true, "MD024"),
        Configurable("heading-trailing-punctuation", DiagnosticKind.LintOnly, "A heading ends with punctuation.", true, "MD026"),
        Configurable("emphasis-as-heading", DiagnosticKind.LintOnly, "Emphasis is used where a heading is meant.", true, "MD036"),
        Configurable("image-alt-text", DiagnosticKind.LintOnly, "An image has no alternative text.", true, "MD045"),
        Configurable("link-empty", DiagnosticKind.LintOnly, "A link has no destination.", true, "MD042"),
        Configurable("link-fragment", DiagnosticKind.LintOnly, "A link fragment does not match any heading.", true, "MD051"),
        Configurable("link-reference-undefined", DiagnosticKind.LintOnly, "A reference link uses a label that is not defined.", true, "MD052"),
        Configurable("link-definition-unused", DiagnosticKind.LintOnly, "A link reference definition is never used.", true, "MD053"),
        Configurable("link-reversed", DiagnosticKind.LintOnly, "A link is written with the brackets and parentheses reversed.", true, "MD011"),
        Configurable("descriptive-link-text", DiagnosticKind.LintOnly, "A link text is not descriptive.", false, "MD059"),
        Configurable("code-fence-language", DiagnosticKind.LintOnly, "A fenced code block has no language.", true, "MD040"),
        Configurable("table-too-wide", DiagnosticKind.LintOnly, "A table is wider than the configured line length.", true),
        Configurable("table-column-count", DiagnosticKind.LintOnly, "A table row has a different number of cells than the header.", true, "MD056"),
        Configurable("table-grid-unformatted", DiagnosticKind.LintOnly, "A grid table is left as written.", false),
        Configurable("fence-unclosed", DiagnosticKind.LintOnly, "A fenced code block is never closed.", true),
        Configurable(GridTableInvalidRuleId, DiagnosticKind.LintOnly, "A line looks like a grid table border but the table is not valid.", true),
        Configurable("footnote-unused", DiagnosticKind.LintOnly, "A footnote definition is never referenced.", true),
        Configurable("blockquote-blank-line", DiagnosticKind.LintOnly, "A blank line separates two block quotes.", true, "MD028"),
        Configurable("dollar-sign-in-shell-code", DiagnosticKind.LintOnly, "A shell command in a code block starts with a dollar sign.", false, "MD014"),

        // Always-applied formatting rule
        Fixed(LineEndingRuleId, DiagnosticKind.Formatting, "A line ending differs from the line ending chosen for the file."),

        // Safety refusals
        Fixed(HtmlMismatchRuleId, DiagnosticKind.LintOnly, "A rewrite would change how the document renders, so the file is not rewritten."),
        Fixed(StructureMismatchRuleId, DiagnosticKind.LintOnly, "A rewrite would change Pandoc structure, so the file is not rewritten."),
        Fixed(NotIdempotentRuleId, DiagnosticKind.LintOnly, "Formatting the rewritten text would change it again, so the file is not rewritten."),

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
    ///     Gets the canonical ids of every configurable rule in ordinal order.
    /// </summary>
    public static ImmutableArray<string> ConfigurableRules { get; } =
        [.. Table.Where(static rule => rule.IsConfigurable).Select(static rule => rule.Id).Order(StringComparer.Ordinal)];

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
    ///     Gets the aliases of dropped rules in ordinal order.
    /// </summary>
    public static ImmutableArray<string> DroppedAliases { get; } = [.. Dropped.Order(StringComparer.Ordinal)];

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
    ///     Creates the entry of a rule that is always applied or only labels a failure.
    /// </summary>
    /// <param name="id">The canonical id.</param>
    /// <param name="kind">The rule kind.</param>
    /// <param name="description">The description.</param>
    /// <returns>The entry (enabled, without aliases, not configurable).</returns>
    private static RuleInfo Fixed(string id, DiagnosticKind kind, string description) =>
        new(id, kind, description, [], DefaultEnabled: true, IsConfigurable: false);
}
