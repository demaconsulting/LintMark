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

namespace DemaConsulting.LintMark.Configuration;

/// <summary>
///     The identifiers of the rules a configuration can switch on or off: kebab-case rule ids, their
///     legacy rule-number aliases (such as <c>MD013</c>), their default state, and the aliases of rules that no longer exist.
/// </summary>
/// <remarks>
///     <para>
///         Supporting type of <see cref="LintMarkConfig"/>. It exists so that the <c>rules</c> setting can be
///         validated and so that the default state of every configurable rule has one home. Rules that
///         the canonical formatter always applies are deliberately absent: they cannot be switched on or off, so
///         their aliases are unknown here. Aliases of dropped rules are recognized (so a legacy entry is
///         accepted and ignored) but never resolve to a rule.
///     </para>
///     <para>
///         Thread safety: all data is immutable and initialized once; every member is safe for concurrent use.
///     </para>
/// </remarks>
internal static class RuleIdentifiers
{
    /// <summary>
    ///     The configurable rules as (id, alias or null, default enabled), in declaration order.
    /// </summary>
    private static readonly (string Id, string? Alias, bool Default)[] Table =
    [
        ("line-length", "MD013", true),
        ("paragraph-wrap", null, true),
        ("heading-increment", "MD001", true),
        ("heading-duplicate", "MD024", true),
        ("heading-trailing-punctuation", "MD026", true),
        ("emphasis-as-heading", "MD036", true),
        ("image-alt-text", "MD045", true),
        ("link-empty", "MD042", true),
        ("link-fragment", "MD051", true),
        ("link-reference-undefined", "MD052", true),
        ("link-definition-unused", "MD053", true),
        ("link-reversed", "MD011", true),
        ("descriptive-link-text", "MD059", false),
        ("code-fence-language", "MD040", true),
        ("table-too-wide", null, true),
        ("table-column-count", "MD056", true),
        ("table-grid-unformatted", null, false),
        ("fence-unclosed", null, true),
        ("grid-table-invalid", null, true),
        ("footnote-unused", null, true),
        ("blockquote-blank-line", "MD028", true),
        ("dollar-sign-in-shell-code", "MD014", false)
    ];

    /// <summary>
    ///     Maps every rule id and every alias, ignoring case, to the canonical rule id.
    /// </summary>
    private static readonly FrozenDictionary<string, string> Resolution = Table
        .SelectMany(static entry => entry.Alias is null
            ? [KeyValuePair.Create(entry.Id, entry.Id)]
            : new[] { KeyValuePair.Create(entry.Id, entry.Id), KeyValuePair.Create(entry.Alias, entry.Id) })
        .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     The aliases of rules that no longer exist, accepted and ignored.
    /// </summary>
    private static readonly FrozenSet<string> Dropped = new[]
    {
        "MD002", "MD006", "MD008", "MD025", "MD033", "MD041", "MD043", "MD044", "MD057"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Gets the canonical ids of every configurable rule in ordinal order.
    /// </summary>
    public static ImmutableArray<string> ConfigurableRules { get; } =
        [.. Table.Select(static entry => entry.Id).Order(StringComparer.Ordinal)];

    /// <summary>
    ///     Gets the default state (enabled or not) of every configurable rule, keyed by canonical id with an ordinal comparer.
    /// </summary>
    public static ImmutableSortedDictionary<string, bool> Defaults { get; } =
        Table.ToImmutableSortedDictionary(static entry => entry.Id, static entry => entry.Default, StringComparer.Ordinal);

    /// <summary>
    ///     Gets the legacy rule-number aliases of the configurable rules, mapped to the canonical rule id; keys compare ignoring case.
    /// </summary>
    public static ImmutableSortedDictionary<string, string> Aliases { get; } = Table
        .Where(static entry => entry.Alias is not null)
        .ToImmutableSortedDictionary(static entry => entry.Alias!, static entry => entry.Id, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Gets the aliases of dropped rules in ordinal order.
    /// </summary>
    public static ImmutableArray<string> DroppedAliases { get; } = [.. Dropped.Order(StringComparer.Ordinal)];

    /// <summary>
    ///     Resolves a rule id or alias to the canonical rule id.
    /// </summary>
    /// <param name="name">A kebab-case rule id or a legacy rule-number alias in any letter case. Must not be null.</param>
    /// <param name="canonicalId">Receives the canonical kebab-case id; <see langword="null"/> when the name is unknown.</param>
    /// <returns>
    ///     <see langword="true"/> when the name identifies a configurable rule; <see langword="false"/> for an
    ///     unknown name and for the alias of a dropped rule (see <see cref="IsDroppedAlias"/>).
    /// </returns>
    /// <remarks>
    ///     Comparison is ordinal and ignores case, independent of the current culture, so the result
    ///     never depends on the machine's locale.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
    public static bool TryResolve(string name, [NotNullWhen(true)] out string? canonicalId)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(name);

        return Resolution.TryGetValue(name, out canonicalId);
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
}
