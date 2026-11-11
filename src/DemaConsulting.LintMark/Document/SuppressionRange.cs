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

namespace DemaConsulting.LintMark.Document;

/// <summary>
///     How a <see cref="RuleSet"/> selects rules.
/// </summary>
internal enum RuleSetKind
{
    /// <summary>Every rule is selected.</summary>
    All,

    /// <summary>Only the listed rules are selected.</summary>
    Only,

    /// <summary>Every rule except the listed ones is selected (a region that disabled everything, then re-enabled some rules).</summary>
    AllExcept
}

/// <summary>
///     The set of rules a suppression range applies to.
/// </summary>
/// <remarks>
///     Rule identifiers are compared using ordinal comparison ignoring case, after any alias canonicalization
///     performed by the owner of the set. Immutable; safe for concurrent use.
/// </remarks>
internal sealed class RuleSet
{
    /// <summary>
    ///     The listed rules (empty for <see cref="RuleSetKind.All"/>).
    /// </summary>
    private readonly HashSet<string> _rules;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RuleSet"/> class.
    /// </summary>
    /// <param name="kind">How the rules are interpreted.</param>
    /// <param name="rules">The listed rules.</param>
    private RuleSet(RuleSetKind kind, IEnumerable<string> rules)
    {
        Kind = kind;
        _rules = new HashSet<string>(rules, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Gets a set that selects every rule.
    /// </summary>
    public static RuleSet All { get; } = new(RuleSetKind.All, []);

    /// <summary>
    ///     Gets how the listed rules are interpreted.
    /// </summary>
    public RuleSetKind Kind { get; }

    /// <summary>
    ///     Gets the listed rules (empty for <see cref="RuleSetKind.All"/>).
    /// </summary>
    public IReadOnlyCollection<string> Rules => _rules;

    /// <summary>
    ///     Creates a set that selects exactly the listed rules.
    /// </summary>
    /// <param name="rules">Rule identifiers. Must not be null.</param>
    /// <returns>The new set.</returns>
    public static RuleSet Only(IEnumerable<string> rules) => new(RuleSetKind.Only, rules);

    /// <summary>
    ///     Creates a set that selects every rule except the listed ones.
    /// </summary>
    /// <param name="rules">Rule identifiers to exclude. Must not be null.</param>
    /// <returns>The new set.</returns>
    public static RuleSet AllExcept(IEnumerable<string> rules) => new(RuleSetKind.AllExcept, rules);

    /// <summary>
    ///     Determines whether a rule is selected.
    /// </summary>
    /// <param name="ruleId">Rule identifier (already canonicalized by the caller).</param>
    /// <returns><see langword="true"/> when the rule is selected.</returns>
    public bool Contains(string ruleId) => Kind switch
    {
        RuleSetKind.All => true,
        RuleSetKind.Only => _rules.Contains(ruleId),
        _ => !_rules.Contains(ruleId)
    };
}

/// <summary>
///     A half-open offset range in which a set of rules is suppressed.
/// </summary>
/// <param name="Start">Offset of the first suppressed character.</param>
/// <param name="End">Exclusive end offset; <see cref="int.MaxValue"/> for ranges that run to the end of the file.</param>
/// <param name="Rules">The rules suppressed in the range.</param>
internal sealed record SuppressionRange(int Start, int End, RuleSet Rules);
