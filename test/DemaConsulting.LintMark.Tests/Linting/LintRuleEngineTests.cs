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

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Test that the LintRuleEngine class applies the configuration and the suppression comments to what the rules report
///     and orders the result.
/// </summary>
public class LintRuleEngineTests
{
    /// <summary>
    ///     A document that triggers every default lint-only rule that works on text, with the findings of the lint-only
    ///     rules that are switched on by default.
    /// </summary>
    private const string Triggers =
        "# One\n\n### Three\n\n### Three\n\n**Not a heading**\n\n#Title\n\n![](a.png) [a]() [b](#nope) [c][nope] (t)[u] [ x ](u)\n\n"
        + "see *text * here\n\n```\nno language\n```\n\n```js\nunclosed\n\n";

    /// <summary>
    ///     Runs an engine over a text and formats the findings as <c>line:column rule-id</c>.
    /// </summary>
    /// <param name="engine">The engine.</param>
    /// <param name="text">The text.</param>
    /// <param name="config">The configuration, or <see langword="null"/> for the defaults.</param>
    /// <returns>The findings in the order of the engine.</returns>
    private static string[] Run(LintRuleEngine engine, string text, LintMarkConfig? config = null) =>
        [.. engine.Run(LintRuleTestSupport.Parse(text), config ?? LintMarkConfig.Default).Select(d => $"{d.Line}:{d.Column} {d.RuleId}")];

    /// <summary>
    ///     Test that the engine drops what the configuration switches off, and what a suppression comment covers, and orders the rest.
    /// </summary>
    [Fact]
    public void LintRuleEngine_Run_TableFindings_AreFilteredByConfigurationAndSuppression()
    {
        // Arrange: a wide table with a short row, a second table with a short row inside a suppressed region (by number), and a grid table
        const string text =
            "| aaaaaaaaaa | bbbbbbbbbb |\n| --- | --- |\n| 1 |\n\n"
            + "<!-- markdownlint-disable MD056 -->\n\n| c | d |\n| --- | --- |\n| 2 |\n\n"
            + "+---+\n| a |\n+---+\n";
        var engine = new LintRuleEngine([new TableRules()]);
        var config = LintMarkConfig.Default with { LineLength = 20 };

        // Act: run with the defaults, with a rule switched on and with a rule switched off
        var defaults = Run(engine, text, config);
        var gridOn = Run(engine, text, config with { Rules = config.Rules.SetItem("table-grid-unformatted", true) });
        var tooWideOff = Run(engine, text, config with { Rules = config.Rules.SetItem("table-too-wide", false) });

        // Assert: the findings are ordered by position, the suppressed region reports nothing and the rule that is off reports nothing
        Assert.Equal(["1:1 table-too-wide", "3:1 table-column-count"], defaults);
        Assert.Equal(["1:1 table-too-wide", "3:1 table-column-count", "11:1 table-grid-unformatted"], gridOn);
        Assert.Equal(["3:1 table-column-count"], tooWideOff);
    }

    /// <summary>
    ///     Test that the engine does not ask a rule whose identifiers are all switched off.
    /// </summary>
    [Fact]
    public void LintRuleEngine_Run_RuleSwitchedOff_IsNotAsked()
    {
        // Arrange: a rule that counts its calls, and a configuration that switches its identifier off
        var counting = new CountingRule("table-too-wide", DiagnosticKind.LintOnly);
        var engine = new LintRuleEngine([counting]);
        var off = LintMarkConfig.Default with { Rules = LintMarkConfig.Default.Rules.SetItem("table-too-wide", false) };

        // Act: run with the rule switched off and on
        var none = engine.Run(LintRuleTestSupport.Parse("x\n"), off);
        var one = engine.Run(LintRuleTestSupport.Parse("x\n"), LintMarkConfig.Default);

        // Assert: asked once, and only when switched on
        Assert.Empty(none);
        Assert.Single(one);
        Assert.Equal(1, counting.Calls);
    }

    /// <summary>
    ///     Test that the default engine reports every default lint-only rule that the document triggers, ordered by position.
    /// </summary>
    [Fact]
    public void LintRuleEngine_Run_DefaultEngine_ReportsTheTriggeredRulesInPositionOrder()
    {
        // Act: run the default engine over the document that triggers the rules
        var findings = Run(LintRuleEngine.CreateDefault(), Triggers);

        // Assert: the findings come ordered by line and column, each rule of the document is among them
        Assert.Equal(
            [
                "3:1 heading-increment",
                "5:1 heading-duplicate",
                "7:1 emphasis-as-heading",
                "9:1 heading-missing-space",
                "11:1 image-alt-text",
                "11:12 link-empty",
                "11:18 link-fragment",
                "11:29 link-reference-undefined",
                "11:39 link-reversed",
                "11:46 link-space",
                "13:5 emphasis-space",
                "15:1 code-fence-language",
                "19:1 fence-unclosed"
            ],
            findings);
    }

    /// <summary>
    ///     Test that the order of the findings does not depend on the order of the rules or on parallel use.
    /// </summary>
    [Fact]
    public void LintRuleEngine_Run_AnyRuleOrderAndParallelUse_GivesTheSameFindings()
    {
        // Arrange: the same rules in another order
        var forward = LintRuleEngine.CreateDefault();
        var backward = new LintRuleEngine([new TableRules(), new StructureRules(), new LinkRules(), new HeadingRules()]);
        var expected = Run(forward, Triggers);

        // Act: run both engines, one of them from many threads at once
        var reversed = Run(backward, Triggers);
        var parallel = new string[16][];
        Parallel.For(0, parallel.Length, index => parallel[index] = Run(forward, Triggers));

        // Assert: always the same findings in the same order
        Assert.Equal(expected, reversed);
        Assert.All(parallel, run => Assert.Equal(expected, run));
    }

    /// <summary>
    ///     Test that a comment that names a rule by its legacy number suppresses the rule, and that other rules stay on.
    /// </summary>
    [Theory]
    [InlineData("<!-- markdownlint-disable MD037 -->\n\nsee *text * here and [ x ](u)\n", "3:22 link-space")]
    [InlineData("<!-- lintmark-disable MD039 -->\n\nsee *text * here and [ x ](u)\n", "3:5 emphasis-space")]
    [InlineData("<!-- lintmark-disable-next-line MD037 -->\nsee *text * here and [ x ](u)\n", "2:22 link-space")]
    [InlineData("see *text * here <!-- markdownlint-disable-line emphasis-space -->\n[ x ](u)\n", "2:1 link-space")]
    public void LintRuleEngine_Run_SuppressionByLegacyNumber_SuppressesOnlyThatRule(string text, string remaining) =>
        Assert.Equal([remaining], Run(LintRuleEngine.CreateDefault(), text));

    /// <summary>
    ///     Test that a configuration switches a rule off by id or legacy number, and a switched-on rule that is off by default reports.
    /// </summary>
    [Fact]
    public void LintRuleEngine_Run_ConfigurationOnAndOff_DecidesWhichRulesReport()
    {
        // Arrange: text that triggers two rules, one of which is off by default
        const string text = "[click here](u) and *a * b\n";
        var engine = LintRuleEngine.CreateDefault();

        // Act & Assert: by default only the rule that is on reports
        Assert.Equal(["1:21 emphasis-space"], Run(engine, text));
        Assert.Equal(
            ["1:1 descriptive-link-text", "1:21 emphasis-space"],
            Run(engine, text, LintMarkConfig.Default with { Rules = LintMarkConfig.Default.Rules.SetItem("descriptive-link-text", true) }));
        Assert.Empty(Run(engine, text, LintMarkConfig.Default with { Rules = LintMarkConfig.Default.Rules.SetItem("emphasis-space", false) }));
    }

    /// <summary>
    ///     Test that no lint-only rule proposes an edit: the engine only returns lint-only findings, none of them fixable.
    /// </summary>
    [Fact]
    public void LintRuleEngine_Run_DefaultRules_NeverProduceFixableFindings()
    {
        // Arrange: switch on every rule and run over the document that triggers many of them
        var all = LintMarkConfig.Default with
        {
            Rules = RuleCatalog.Defaults.Keys.Where(static id => RuleCatalog.TryGet(id, out var rule) && rule.Kind == DiagnosticKind.LintOnly)
                .Aggregate(LintMarkConfig.Default.Rules, static (rules, id) => rules.SetItem(id, true))
        };

        // Act: run the default engine
        var diagnostics = LintRuleEngine.CreateDefault().Run(LintRuleTestSupport.Parse(Triggers + "$ one\n"), all);

        // Assert: findings exist, and each is lint-only and not fixable
        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, d => Assert.Equal(DiagnosticKind.LintOnly, d.Kind));
    }

    /// <summary>
    ///     Test that every rule identifier a default rule declares is a registered lint-only rule that a configuration can switch.
    /// </summary>
    [Fact]
    public void LintRuleEngine_CreateDefault_DeclaredRuleIds_AreRegisteredLintOnlyConfigurableRules()
    {
        // Act: collect the identifiers of the rules of the default engine
        ILintRule[] rules = [new HeadingRules(), new LinkRules(), new StructureRules(), new TableRules()];
        var ids = rules.SelectMany(static rule => rule.RuleIds).ToArray();

        // Assert: registered, lint-only, configurable and declared by one rule only
        Assert.All(ids, id =>
        {
            Assert.True(RuleCatalog.TryGet(id, out var info), id);
            Assert.Equal(DiagnosticKind.LintOnly, info.Kind);
            Assert.True(info.IsConfigurable, id);
        });
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    ///     A rule that reports one finding and counts how often it is asked.
    /// </summary>
    private sealed class CountingRule : ILintRule
    {
        /// <summary>
        ///     The kind of the finding.
        /// </summary>
        private readonly DiagnosticKind _kind;

        /// <summary>
        ///     Initializes a new instance of the <see cref="CountingRule"/> class.
        /// </summary>
        /// <param name="id">The identifier the rule declares and reports.</param>
        /// <param name="kind">The kind of the finding it reports.</param>
        public CountingRule(string id, DiagnosticKind kind)
        {
            RuleIds = [id];
            _kind = kind;
        }

        /// <summary>
        ///     Gets the number of calls of <see cref="Check"/>.
        /// </summary>
        public int Calls { get; private set; }

        /// <summary>
        ///     Gets the identifiers the rule declares.
        /// </summary>
        public ImmutableArray<string> RuleIds { get; }

        /// <summary>
        ///     Reports one finding at the start of the document.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="config">The configuration.</param>
        /// <returns>The finding.</returns>
        public ImmutableArray<Diagnostic> Check(ParsedDocument document, LintMarkConfig config)
        {
            Calls++;
            return [new Diagnostic(RuleIds[0], _kind, document.Source.Path, 1, 1, "Counted")];
        }
    }
}
