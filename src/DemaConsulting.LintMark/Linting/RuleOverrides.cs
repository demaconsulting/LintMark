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

namespace DemaConsulting.LintMark.Linting;

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
