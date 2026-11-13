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

using System.Reflection;
using System.Text;
using DemaConsulting.LintMark.Configuration;
using DemaConsulting.LintMark.Document;
using DemaConsulting.LintMark.Engine;
using DemaConsulting.LintMark.Formatting;
using DemaConsulting.LintMark.Linting;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Subsystem tests for the Linting subsystem: the diagnostic model and the rule registry working with the units that report.
/// </summary>
public class LintingSubsystemTests
{
    /// <summary>
    ///     Parses text as UTF-8 content of a configuration file.
    /// </summary>
    /// <param name="yaml">The text.</param>
    /// <returns>The result.</returns>
    private static ConfigLoadResult Parse(string yaml) =>
        ConfigLoader.Parse("conf/.lintmark.yaml", Encoding.UTF8.GetBytes(yaml));

    /// <summary>
    ///     Test that a diagnostic of a registered formatting rule is fixable and one of a lint-only rule is not.
    /// </summary>
    [Fact]
    public void LintingSubsystem_Diagnostics_FormattingKind_IsFixable()
    {
        // Arrange: the registered kind of two rules
        Assert.True(RuleCatalog.TryGet("line-length", out var formatting));
        Assert.True(RuleCatalog.TryGet("link-empty", out var lintOnly));

        // Act: create a diagnostic of each kind from the registry
        var fixable = new Diagnostic(formatting.Id, formatting.Kind, "a.md", 1, 1, "A line is too long.");
        var reportOnly = new Diagnostic(lintOnly.Id, lintOnly.Kind, "a.md", 1, 1, "A link has no destination.");

        // Assert: the formatting diagnostic is fixable, the lint-only one is not
        Assert.True(fixable.Fixable);
        Assert.False(reportOnly.Fixable);
    }

    /// <summary>
    ///     Test that a diagnostic of a registered rule carries the rule identifier, path, position, and message it was made from.
    /// </summary>
    [Fact]
    public void LintingSubsystem_Diagnostics_RegisteredRule_CarriesAllParts()
    {
        // Arrange: the registered identifier and kind of a rule
        Assert.True(RuleCatalog.TryGet("link-empty", out var rule));

        // Act: create a diagnostic from the registered rule
        var diagnostic = new Diagnostic(rule.Id, rule.Kind, "docs/a.md", 4, 9, "A link has no destination.");

        // Assert: every part is carried
        Assert.Equal("link-empty", diagnostic.RuleId);
        Assert.Equal("docs/a.md", diagnostic.Path);
        Assert.Equal(4, diagnostic.Line);
        Assert.Equal(9, diagnostic.Column);
        Assert.Equal("A link has no destination.", diagnostic.Message);
    }

    /// <summary>
    ///     Test that a diagnostic cannot be created from parts that a report line cannot carry.
    /// </summary>
    [Fact]
    public void LintingSubsystem_Diagnostics_InvalidParts_AreRefused()
    {
        // Arrange: a registered rule to build valid parts from
        Assert.True(RuleCatalog.TryGet("line-length", out var rule));

        // Act & Assert: a malformed identifier, a position of zero, and a message on two lines are refused
        Assert.ThrowsAny<ArgumentException>(() => new Diagnostic("Line Length", rule.Kind, "a.md", 1, 1, "A line is too long."));
        Assert.ThrowsAny<ArgumentException>(() => new Diagnostic(rule.Id, rule.Kind, "a.md", 0, 1, "A line is too long."));
        Assert.ThrowsAny<ArgumentException>(() => new Diagnostic(rule.Id, rule.Kind, "a.md", 1, 0, "A line is too long."));
        Assert.ThrowsAny<ArgumentException>(() => new Diagnostic(rule.Id, rule.Kind, "a.md", 1, 1, "A line is\ntoo long."));
    }

    /// <summary>
    ///     Test that two diagnostics made from the same parts are the same problem and collapse into one.
    /// </summary>
    [Fact]
    public void LintingSubsystem_Diagnostics_SameParts_AreTheSameProblem()
    {
        // Arrange: a registered rule
        Assert.True(RuleCatalog.TryGet("line-length", out var rule));

        // Act: create the same problem twice and one that differs in the column
        var first = new Diagnostic(rule.Id, rule.Kind, "a.md", 1, 1, "A line is too long.");
        var second = new Diagnostic(rule.Id, rule.Kind, "a.md", 1, 1, "A line is too long.");
        var other = new Diagnostic(rule.Id, rule.Kind, "a.md", 1, 2, "A line is too long.");

        // Assert: the equal problems collapse in a set and the different one stays
        Assert.Equal(first, second);
        Assert.NotEqual(first, other);
        Assert.Equal(2, new HashSet<Diagnostic> { first, second, other }.Count);
    }

    /// <summary>
    ///     Test that the registry records which rules are formatting rules that rewriting repairs and which only report.
    /// </summary>
    [Fact]
    public void LintingSubsystem_RuleRegistry_FormattingAndLintOnlyRules_AreRegistered()
    {
        // Act: look up a formatting rule and a rule that only reports
        Assert.True(RuleCatalog.TryGet("line-length", out var formatting));
        Assert.True(RuleCatalog.TryGet("link-empty", out var lintOnly));

        // Assert: the registered kinds differ as documented
        Assert.Equal(DiagnosticKind.Formatting, formatting.Kind);
        Assert.Equal(DiagnosticKind.LintOnly, lintOnly.Kind);
    }

    /// <summary>
    ///     Test that a rule that a configuration does not mention keeps the enabled state that the registry gives it.
    /// </summary>
    [Fact]
    public void LintingSubsystem_RuleRegistry_NotConfiguredRule_KeepsRegisteredState()
    {
        // Act: parse a configuration that switches one rule off
        var result = Parse("rules:\n  MD013: false\n");

        // Assert: every other rule has the state that the registry registered
        Assert.Empty(result.Errors);
        Assert.All(
            RuleCatalog.Defaults.Where(pair => pair.Key != "line-length"),
            pair => Assert.Equal(pair.Value, result.Config!.Rules[pair.Key]));
        Assert.False(result.Config!.Rules["line-length"]);
    }

    /// <summary>
    ///     Test that every rule identifier the tool emits is registered in the catalog.
    /// </summary>
    [Fact]
    public void LintingSubsystem_RuleRegistry_EveryEmittedRuleId_IsRegistered()
    {
        // Arrange: the identifiers named by the units that report, and every identifier constant of the catalog
        var emitted = new List<string>
        {
            LintMarkEngine.ReadErrorRuleId,
            LintMarkEngine.ParseErrorRuleId,
            LintMarkEngine.FormatErrorRuleId,
            LintMarkEngine.WriteErrorRuleId,
            SourceDocumentException.EncodingRuleId,
            SourceDocumentException.LineEndingRuleId,
            ConfigLoader.ErrorRuleId,
            DocumentFormatter.LineEndingRuleId
        };
        var catalogConstants = typeof(RuleCatalog)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string) && field.Name.EndsWith("RuleId", StringComparison.Ordinal))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToList();
        emitted.AddRange(catalogConstants);

        // Act & Assert: the reflection found the constants, and each identifier is registered under its own canonical id
        Assert.NotEmpty(catalogConstants);
        Assert.All(emitted, id =>
        {
            Assert.True(RuleCatalog.TryGet(id, out var rule), id);
            Assert.Equal(id, rule.Id);
        });
    }

    /// <summary>
    ///     Test that the rules setting accepts aliases and dropped aliases and rejects rules that are not configurable.
    /// </summary>
    [Fact]
    public void LintingSubsystem_RuleRegistry_ConfigRules_AcceptAliasesAndRejectNonConfigurable()
    {
        // Act: parse an alias, a dropped alias, and two rules that cannot be configured
        var alias = Parse("rules:\n  MD013: false\n");
        var dropped = Parse("rules:\n  MD033: false\n");
        var lineEnding = Parse("rules:\n  line-ending: false\n");
        var failure = Parse("rules:\n  config-error: false\n");

        // Assert: the alias switches the canonical rule off
        Assert.Empty(alias.Errors);
        Assert.False(alias.Config!.Rules["line-length"]);

        // Assert: the dropped alias is accepted and changes nothing
        Assert.Empty(dropped.Errors);
        Assert.Equal(LintMarkConfig.Default.Rules, dropped.Config!.Rules);

        // Assert: a rule that is always applied or only labels a failure is an unknown rule
        Assert.Null(lineEnding.Config);
        var lineEndingError = Assert.Single(lineEnding.Errors);
        Assert.Equal((2, 3, "Unknown rule 'line-ending'"), (lineEndingError.Line, lineEndingError.Column, lineEndingError.Message));
        Assert.Null(failure.Config);
        var failureError = Assert.Single(failure.Errors);
        Assert.Equal((2, 3, "Unknown rule 'config-error'"), (failureError.Line, failureError.Column, failureError.Message));
    }
}
