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

using DemaConsulting.LintMark.Configuration;
using DemaConsulting.LintMark.Linting;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Subsystem tests of the Linting subsystem: the rule catalog and the lint rule engine are driven together on
///     parsed documents, with the real rules and no substitutes.
/// </summary>
public class LintingSubsystemTests
{
    /// <summary>
    ///     Runs the default lint rule engine over a text and formats the findings as <c>line:column rule-id</c>.
    /// </summary>
    /// <param name="text">The LF-normalized text.</param>
    /// <param name="config">The configuration, or <see langword="null"/> for the defaults.</param>
    /// <returns>The findings in the order the engine returns them.</returns>
    private static string[] Run(string text, LintMarkConfig? config = null) =>
        [.. LintRuleEngine.CreateDefault().Run(LintRuleTestSupport.Parse(text), config ?? LintMarkConfig.Default).Select(d => $"{d.Line}:{d.Column} {d.RuleId}")];

    /// <summary>
    ///     Test that the catalog holds every rule once with its aliases and default state, resolves names in any letter
    ///     case, rejects what a configuration cannot switch, and that the command line overrides change the states.
    /// </summary>
    [Fact]
    public void LintingSubsystem_Catalog_RulesAliasesDefaultsAndOverrides_ResolveConsistently()
    {
        // Arrange: the command-line overrides that switch one rule on and another off, the second by its number
        var overrides = RuleOverrides.Create(["descriptive-link-text"], ["MD037"]);

        // Act: resolve names and apply the overrides to the default configuration
        var resolved = RuleCatalog.TryResolve("md037", out var byNumber);
        var fixedRule = RuleCatalog.TryResolveConfigurable("line-ending", out _);
        var effective = overrides.Apply(LintMarkConfig.Default);

        // Assert: ids are unique and in order, a number resolves to its id, an always-applied rule and a dropped number
        // cannot be configured, the defaults are in the catalog, and the overrides win over the configuration
        var ids = RuleCatalog.Rules.Select(rule => rule.Id).ToList();
        Assert.Equal(ids.Order(StringComparer.Ordinal), ids);
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.True(resolved);
        Assert.Equal("emphasis-space", byNumber);
        Assert.False(fixedRule);
        Assert.True(RuleCatalog.IsDroppedAlias("MD033"));
        Assert.False(RuleCatalog.TryResolve("MD033", out _));
        Assert.True(RuleCatalog.Defaults["emphasis-space"]);
        Assert.False(RuleCatalog.Defaults["descriptive-link-text"]);
        Assert.Equal(RuleCatalog.Defaults, LintMarkConfig.Default.Rules);
        Assert.True(effective.Rules["descriptive-link-text"]);
        Assert.False(effective.Rules["emphasis-space"]);
    }

    /// <summary>
    ///     Test that findings are lint-only, carry the rule, the file and a position, and are returned in source order.
    /// </summary>
    [Fact]
    public void LintingSubsystem_Findings_Document_AreLintOnlyWithPositionAndInSourceOrder()
    {
        // Arrange: three problems of three rule families, the last two on the same line
        const string text = "# One\n\n### Three\n\nsee *text * here and [x](#nowhere)\n";

        // Act: run the lint rules and keep the diagnostics themselves
        var diagnostics = LintRuleEngine.CreateDefault().Run(LintRuleTestSupport.Parse(text), LintMarkConfig.Default);

        // Assert: every finding is lint-only, belongs to the file and has a one-line message; the order is by position
        Assert.Equal(["3:1 heading-increment", "5:5 emphasis-space", "5:22 link-fragment"], Run(text));
        Assert.All(diagnostics, d =>
        {
            Assert.Equal(DiagnosticKind.LintOnly, d.Kind);
            Assert.Equal("a.md", d.Path);
            Assert.DoesNotContain('\n', d.Message);
            Assert.True(RuleCatalog.TryGet(d.RuleId, out _));
        });
    }

    /// <summary>
    ///     Test that a rule that is disabled, or suppressed by a comment, reports nothing, and that an optional rule
    ///     reports only when it is enabled.
    /// </summary>
    [Fact]
    public void LintingSubsystem_LintChecks_DisabledSuppressedAndOptionalRules_HonorEnablement()
    {
        // Arrange: the same finding outside and inside a suppressed region, and a link text that is only reported by an optional rule
        const string text =
            "see *text * here\n\n<!-- lintmark-disable emphasis-space -->\nsee *text * here\n<!-- lintmark-enable emphasis-space -->\n\n[click here](https://example.com)\n";
        var enabled = LintMarkConfig.Default with { Rules = LintMarkConfig.Default.Rules.SetItem("descriptive-link-text", true) };
        var disabled = LintMarkConfig.Default with { Rules = LintMarkConfig.Default.Rules.SetItem("emphasis-space", false) };

        // Act: run with the defaults, with the optional rule enabled and with the finding's rule disabled
        var byDefault = Run(text);
        var withOptional = Run(text, enabled);
        var withoutRule = Run(text, disabled);

        // Assert: the suppressed line is never reported, the optional rule only when enabled, a disabled rule never
        Assert.Equal(["1:5 emphasis-space"], byDefault);
        Assert.Equal(["1:5 emphasis-space", "7:1 descriptive-link-text"], withOptional);
        Assert.Empty(withoutRule);
    }
}
