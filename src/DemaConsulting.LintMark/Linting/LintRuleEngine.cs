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
using DemaConsulting.LintMark.Linting.Rules;

namespace DemaConsulting.LintMark.Linting;

/// <summary>
///     Runs the lint-only rules over a document and keeps the diagnostics that are switched on and not suppressed.
/// </summary>
/// <remarks>
///     <para>
///         The engine asks every rule whose identifiers are not all switched off for its diagnostics, drops the ones
///         whose own rule is switched off in the configuration (the configuration already carries the command line
///         overrides, see <see cref="RuleOverrides"/>), drops the ones that a suppression comment covers at their position
///         (the comments name rules by id or by legacy number; the document's suppression map resolves both), and
///         returns the rest in one deterministic order: by line, column, rule identifier and message. A rule can only
///         report: <see cref="ILintRule"/> returns diagnostics and no edits, and the engine refuses a diagnostic that is
///         not lint-only or that carries an identifier the rule did not declare, so no lint-only rule can ever change a
///         file or make a rewrite unsafe.
///     </para>
///     <para>
///         Thread safety: holds an immutable list of rules, which must be safe for concurrent use; <see cref="Run"/> keeps
///         its state in local variables, so one instance serves every worker.
///     </para>
/// </remarks>
internal sealed class LintRuleEngine
{
    /// <summary>
    ///     The rules, in registration order.
    /// </summary>
    private readonly ImmutableArray<ILintRule> _rules;

    /// <summary>
    ///     Initializes a new instance of the <see cref="LintRuleEngine"/> class.
    /// </summary>
    /// <param name="rules">The rules to run, or <see langword="null"/> for none. The list is copied.</param>
    /// <exception cref="ArgumentNullException">Thrown when an element of <paramref name="rules"/> is null.</exception>
    public LintRuleEngine(IReadOnlyList<ILintRule>? rules = null)
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
    ///     Gets the engine with the lint-only rules the tool ships with.
    /// </summary>
    /// <returns>A new engine.</returns>
    public static LintRuleEngine CreateDefault() =>
        new([new HeadingRules(), new LinkRules(), new StructureRules(), new TableRules()]);

    /// <summary>
    ///     Runs the rules over a document.
    /// </summary>
    /// <param name="document">The parsed document. Must not be null.</param>
    /// <param name="config">The effective configuration of the file. Must not be null.</param>
    /// <returns>The diagnostics of the rules that are switched on and not suppressed, ordered by line, column, rule and message.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when a rule reports a diagnostic that is not lint-only or that carries an identifier the rule does not declare.
    /// </exception>
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

            foreach (var diagnostic in rule.Check(document, config))
            {
                if (diagnostic.Kind != DiagnosticKind.LintOnly || !rule.RuleIds.Contains(diagnostic.RuleId))
                {
                    throw new InvalidOperationException(
                        $"The rule that declares {string.Join(", ", rule.RuleIds)} reported a diagnostic that is not a lint-only finding of its own: {diagnostic.RuleId}.");
                }

                var offset = document.Source.OffsetOf(diagnostic.Line, diagnostic.Column);
                if (IsEnabled(config, diagnostic.RuleId) && !document.Suppressions.IsSuppressed(offset, diagnostic.RuleId))
                {
                    found.Add(diagnostic);
                }
            }
        }

        return
        [
            .. found
                .OrderBy(static diagnostic => diagnostic.Line)
                .ThenBy(static diagnostic => diagnostic.Column)
                .ThenBy(static diagnostic => diagnostic.RuleId, StringComparer.Ordinal)
                .ThenBy(static diagnostic => diagnostic.Message, StringComparer.Ordinal)
        ];
    }

    /// <summary>
    ///     Determines whether a rule is switched on in a configuration.
    /// </summary>
    /// <param name="config">The configuration.</param>
    /// <param name="ruleId">The canonical rule identifier.</param>
    /// <returns><see langword="true"/> unless the configuration switches the rule off; an identifier that is not configurable is always on.</returns>
    private static bool IsEnabled(LintMarkConfig config, string ruleId) =>
        !config.Rules.TryGetValue(ruleId, out var enabled) || enabled;
}
