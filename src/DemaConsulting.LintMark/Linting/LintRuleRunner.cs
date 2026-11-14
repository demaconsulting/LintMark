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

namespace DemaConsulting.LintMark.Linting;

/// <summary>
///     Runs the lint-only rules over a document and keeps the diagnostics that are switched on and not suppressed.
/// </summary>
/// <remarks>
///     <para>
///         A minimal runner for the lint-only rules that exist so far: it asks every rule for its diagnostics, drops the ones
///         whose rule is switched off in the configuration (a rule identifier that is not configurable is always on) or
///         suppressed by a comment at the position of the diagnostic, and returns the rest ordered by position. The
///         later lint rule engine can take over this contract: <see cref="ILintRule"/> stays the unit of a rule.
///     </para>
///     <para>
///         Thread safety: holds an immutable list of rules, which must be safe for concurrent use; <see cref="Run"/> keeps
///         its state in local variables, so one instance serves every worker.
///     </para>
/// </remarks>
internal sealed class LintRuleRunner
{
    /// <summary>
    ///     The rules, in registration order.
    /// </summary>
    private readonly ImmutableArray<ILintRule> _rules;

    /// <summary>
    ///     Initializes a new instance of the <see cref="LintRuleRunner"/> class.
    /// </summary>
    /// <param name="rules">The rules to run, or <see langword="null"/> for none. The list is copied.</param>
    /// <exception cref="ArgumentNullException">Thrown when an element of <paramref name="rules"/> is null.</exception>
    public LintRuleRunner(IReadOnlyList<ILintRule>? rules = null)
    {
        // Copy to an immutable array so a caller cannot change the list afterwards, and reject null elements up front
        ImmutableArray<ILintRule> copy = rules is null ? [] : ImmutableArray.CreateRange(rules);
        if (copy.Any(static rule => rule is null))
        {
            throw new ArgumentNullException(nameof(rules), "The rules must not contain null.");
        }

        _rules = copy;
    }

    /// <summary>
    ///     Gets the runner with the lint-only rules the tool ships with.
    /// </summary>
    /// <returns>A new runner.</returns>
    public static LintRuleRunner CreateDefault() => new([new Rules.TableRules()]);

    /// <summary>
    ///     Runs the rules over a document.
    /// </summary>
    /// <param name="document">The parsed document. Must not be null.</param>
    /// <param name="config">The effective configuration of the file. Must not be null.</param>
    /// <returns>The diagnostics of the rules that are switched on and not suppressed, ordered by line, column and rule.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public ImmutableArray<Diagnostic> Run(ParsedDocument document, LintMarkConfig config)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(config);

        var found = new List<Diagnostic>();
        foreach (var rule in _rules)
        {
            // A rule none of whose identifiers is switched on has nothing to report
            if (!rule.RuleIds.Any(id => IsEnabled(config, id)))
            {
                continue;
            }

            var reported = rule.Check(document, config).Where(diagnostic =>
                IsEnabled(config, diagnostic.RuleId)
                && !document.Suppressions.IsSuppressed(document.Source.OffsetOf(diagnostic.Line, diagnostic.Column), diagnostic.RuleId));
            found.AddRange(reported);
        }

        return
        [
            .. found
                .OrderBy(static diagnostic => diagnostic.Line)
                .ThenBy(static diagnostic => diagnostic.Column)
                .ThenBy(static diagnostic => diagnostic.RuleId, StringComparer.Ordinal)
        ];
    }

    /// <summary>
    ///     Determines whether a rule is switched on.
    /// </summary>
    /// <param name="config">The configuration.</param>
    /// <param name="ruleId">The canonical rule identifier.</param>
    /// <returns><see langword="true"/> unless the configuration switches the rule off.</returns>
    private static bool IsEnabled(LintMarkConfig config, string ruleId) =>
        !config.Rules.TryGetValue(ruleId, out var enabled) || enabled;
}
