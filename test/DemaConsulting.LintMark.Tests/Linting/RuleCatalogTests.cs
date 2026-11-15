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

using System.Globalization;
using DemaConsulting.LintMark.Configuration;
using DemaConsulting.LintMark.Linting;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Unit tests for the RuleCatalog class: the single registry of rule identifiers.
/// </summary>
public class RuleCatalogTests
{
    /// <summary>
    ///     Test that a kebab-case rule id resolves to itself.
    /// </summary>
    [Fact]
    public void RuleCatalog_TryResolve_KebabId_ReturnsCanonical()
    {
        // Act: resolve a canonical id
        var resolved = RuleCatalog.TryResolve("heading-increment", out var id);

        // Assert: the id is returned unchanged
        Assert.True(resolved);
        Assert.Equal("heading-increment", id);
    }

    /// <summary>
    ///     Test that every alias resolves to its rule.
    /// </summary>
    [Fact]
    public void RuleCatalog_TryResolve_EveryAlias_ReturnsItsRule()
    {
        // Act & Assert: each documented alias resolves to the documented rule
        var expected = new Dictionary<string, string>
        {
            ["MD001"] = "heading-increment",
            ["MD011"] = "link-reversed",
            ["MD013"] = "line-length",
            ["MD014"] = "dollar-sign-in-shell-code",
            ["MD018"] = "heading-missing-space",
            ["MD020"] = "heading-closed-missing-space",
            ["MD024"] = "heading-duplicate",
            ["MD026"] = "heading-trailing-punctuation",
            ["MD028"] = "blockquote-blank-line",
            ["MD036"] = "emphasis-as-heading",
            ["MD037"] = "emphasis-space",
            ["MD039"] = "link-space",
            ["MD040"] = "code-fence-language",
            ["MD042"] = "link-empty",
            ["MD045"] = "image-alt-text",
            ["MD051"] = "link-fragment",
            ["MD052"] = "link-reference-undefined",
            ["MD053"] = "link-definition-unused",
            ["MD054"] = "link-refactor",
            ["MD056"] = "table-column-count",
            ["MD059"] = "descriptive-link-text"
        };
        foreach (var (alias, rule) in expected)
        {
            Assert.True(RuleCatalog.TryResolve(alias, out var id), alias);
            Assert.Equal(rule, id);
        }

        Assert.Equal(expected.Count, RuleCatalog.Aliases.Count);
    }

    /// <summary>
    ///     Test that rule ids and aliases are matched without regard to letter case.
    /// </summary>
    [Theory]
    [InlineData("LINE-LENGTH")]
    [InlineData("Line-Length")]
    [InlineData("md013")]
    [InlineData("Md013")]
    public void RuleCatalog_TryResolve_AnyLetterCase_ReturnsCanonical(string name)
    {
        // Act: resolve the spelling
        var resolved = RuleCatalog.TryResolve(name, out var id);

        // Assert: it resolves to the canonical id
        Assert.True(resolved);
        Assert.Equal("line-length", id);
    }

    /// <summary>
    ///     Test that matching does not depend on the current culture.
    /// </summary>
    [Fact]
    public void RuleCatalog_TryResolve_TurkishCulture_StillMatches()
    {
        // Arrange: a culture with a special case rule for the letter i
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");

            // Act: resolve names containing the letter i in upper case
            var id = RuleCatalog.TryResolve("LINE-LENGTH", out var lineLength);
            var link = RuleCatalog.TryResolve("LINK-EMPTY", out var linkEmpty);

            // Assert: both resolve as in any other culture
            Assert.True(id);
            Assert.Equal("line-length", lineLength);
            Assert.True(link);
            Assert.Equal("link-empty", linkEmpty);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    /// <summary>
    ///     Test that an unknown name does not resolve.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("no-such-rule")]
    [InlineData("MD999")]
    public void RuleCatalog_TryResolve_UnknownName_ReturnsFalse(string name)
    {
        // Act: resolve the name
        var resolved = RuleCatalog.TryResolve(name, out var id);

        // Assert: it is unknown and not dropped
        Assert.False(resolved);
        Assert.Null(id);
        Assert.False(RuleCatalog.IsDroppedAlias(name));
    }

    /// <summary>
    ///     Test that the alias of a dropped rule does not resolve but is recognized as dropped.
    /// </summary>
    [Theory]
    [InlineData("MD002")]
    [InlineData("MD006")]
    [InlineData("MD008")]
    [InlineData("MD025")]
    [InlineData("MD033")]
    [InlineData("MD041")]
    [InlineData("MD043")]
    [InlineData("MD044")]
    [InlineData("MD057")]
    [InlineData("md025")]
    public void RuleCatalog_TryResolve_DroppedAlias_ReturnsFalseAndIsDropped(string name)
    {
        // Act: resolve the name
        var resolved = RuleCatalog.TryResolve(name, out _);

        // Assert: it is not a rule, but it is a dropped alias
        Assert.False(resolved);
        Assert.True(RuleCatalog.IsDroppedAlias(name));
    }

    /// <summary>
    ///     Test that a registered rule that is not configurable still resolves, but is not configurable.
    /// </summary>
    [Fact]
    public void RuleCatalog_TryResolve_LineEnding_ReturnsRuleButNotConfigurable()
    {
        // Act: resolve the always-applied rule
        var resolved = RuleCatalog.TryResolve("line-ending", out var id);
        var found = RuleCatalog.TryGet("line-ending", out var rule);

        // Assert: it is registered, applied always, and not part of the configurable rules
        Assert.True(resolved);
        Assert.Equal("line-ending", id);
        Assert.True(found);
        Assert.NotNull(rule);
        Assert.False(rule.IsConfigurable);
        Assert.False(RuleCatalog.Defaults.ContainsKey("line-ending"));
    }

    /// <summary>
    ///     Test that an unknown id, an alias or a dropped alias is not found.
    /// </summary>
    [Theory]
    [InlineData("no-such-rule")]
    [InlineData("")]
    [InlineData("MD013")]
    [InlineData("MD033")]
    public void RuleCatalog_TryGet_UnknownId_ReturnsFalse(string id)
    {
        // Act: look the name up
        var found = RuleCatalog.TryGet(id, out var rule);

        // Assert: only canonical ids are found
        Assert.False(found);
        Assert.Null(rule);
    }

    /// <summary>
    ///     Test that the alias of an always-applied formatting rule resolves for suppression but is not configurable.
    /// </summary>
    [Theory]
    [InlineData("MD009", "trailing-whitespace")]
    [InlineData("md010", "hard-tabs")]
    [InlineData("MD003", "heading-style")]
    [InlineData("MD021", "heading-style")]
    [InlineData("MD047", "final-newline")]
    [InlineData("MD004", "bullet")]
    [InlineData("md029", "ordered-style")]
    [InlineData("MD005", "list-indent")]
    [InlineData("MD007", "list-indent")]
    [InlineData("MD030", "list-marker-space")]
    [InlineData("MD027", "blockquote-space")]
    [InlineData("MD049", "emphasis")]
    [InlineData("MD050", "strong")]
    [InlineData("md034", "bare-urls")]
    [InlineData("MD038", "code-span-space")]
    [InlineData("trailing-whitespace", "trailing-whitespace")]
    public void RuleCatalog_TryResolve_FormattingRuleAlias_ResolvesButIsNotConfigurable(string name, string expected)
    {
        // Act: resolve the name both ways
        var resolved = RuleCatalog.TryResolve(name, out var id);
        var configurable = RuleCatalog.TryResolveConfigurable(name, out var configurableId);

        // Assert: it names the rule for suppression comments, but a configuration may not name it
        Assert.True(resolved);
        Assert.Equal(expected, id);
        Assert.False(configurable);
        Assert.Null(configurableId);
    }

    /// <summary>
    ///     Test that the rules that report spacing problems the formatter cannot fix are registered, configurable and on by default.
    /// </summary>
    [Theory]
    [InlineData("MD018", "heading-missing-space")]
    [InlineData("MD020", "heading-closed-missing-space")]
    [InlineData("MD037", "emphasis-space")]
    [InlineData("MD039", "link-space")]
    public void RuleCatalog_TryResolve_RuleNumberOfASpacingLintOnlyRule_IsConfigurableAndEnabled(string name, string expected)
    {
        // Act: resolve the name both ways
        var resolved = RuleCatalog.TryResolve(name, out var id);
        var configurable = RuleCatalog.TryResolveConfigurable(name, out var configurableId);

        // Assert: it is a lint-only rule that is on by default
        Assert.True(resolved);
        Assert.True(configurable);
        Assert.Equal(expected, id);
        Assert.Equal(expected, configurableId);
        Assert.True(RuleCatalog.Defaults[expected]);
        Assert.True(RuleCatalog.TryGet(expected, out var rule));
        Assert.Equal(DiagnosticKind.LintOnly, rule.Kind);
    }
}

/// <summary>
///     Test that the RuleOverrides class resolves the rules named on the command line and applies them over a configuration.
/// </summary>
public class RuleOverridesTests
{
    /// <summary>
    ///     Test that names, numbers and lists in any letter case are resolved to canonical ids in ordinal order, without repeats.
    /// </summary>
    [Fact]
    public void RuleOverrides_Create_NamesAndNumbers_AreResolvedToCanonicalIds()
    {
        // Act: name rules by id, number and in a list, with a repeat
        var overrides = RuleOverrides.Create(["md037, LINK-SPACE", "emphasis-space"], ["MD013"]);

        // Assert: canonical ids, ordinal order, one entry per rule
        Assert.Equal(["emphasis-space", "link-space"], overrides.Enabled);
        Assert.Equal(["line-length"], overrides.Disabled);
        Assert.False(overrides.IsEmpty);
    }

    /// <summary>
    ///     Test that the overrides win over the configuration, in both directions, and leave every other rule alone.
    /// </summary>
    [Fact]
    public void RuleOverrides_Apply_Configuration_RulesAreForcedOnAndOff()
    {
        // Arrange: a configuration with a rule off that is on by default and the reverse
        var config = LintMarkConfig.Default with { Rules = LintMarkConfig.Default.Rules.SetItem("link-empty", false) };
        var overrides = RuleOverrides.Create(["link-empty", "descriptive-link-text"], ["emphasis-space"]);

        // Act: apply the overrides
        var applied = overrides.Apply(config);

        // Assert: forced on, forced off, and the rest unchanged
        Assert.True(applied.Rules["link-empty"]);
        Assert.True(applied.Rules["descriptive-link-text"]);
        Assert.False(applied.Rules["emphasis-space"]);
        Assert.Equal(config.Rules.Where(r => r.Key is not ("link-empty" or "descriptive-link-text" or "emphasis-space")), applied.Rules.Where(r => r.Key is not ("link-empty" or "descriptive-link-text" or "emphasis-space")));
        Assert.False(config.Rules["link-empty"]);
    }

    /// <summary>
    ///     Test that blank lists, empty entries, unknown names, rules that are always applied and contradictions are rejected with a clear message.
    /// </summary>
    [Theory]
    [InlineData("", "requires a comma-separated list")]
    [InlineData("MD037,,MD039", "empty rule id")]
    [InlineData("nonsense", "unknown rule 'nonsense'")]
    [InlineData("MD999", "unknown rule 'MD999'")]
    [InlineData("trailing-whitespace", "always applied")]
    [InlineData("MD009", "always applied")]
    public void RuleOverrides_Create_InvalidList_Throws(string value, string message)
    {
        // Act & Assert: both options reject the list with their own name in the message
        Assert.Contains("--enable", Assert.Throws<ArgumentException>(() => RuleOverrides.Create([value], [])).Message);
        var exception = Assert.Throws<ArgumentException>(() => RuleOverrides.Create([], [value]));
        Assert.Contains("--disable", exception.Message);
        Assert.Contains(message, exception.Message);
    }

    /// <summary>
    ///     Test that a rule named by both options is a contradiction.
    /// </summary>
    [Fact]
    public void RuleOverrides_Create_RuleInBothLists_Throws()
    {
        // Act & Assert: a number and an id of the same rule collide
        var exception = Assert.Throws<ArgumentException>(() => RuleOverrides.Create(["MD037"], ["emphasis-space"]));
        Assert.Contains("'emphasis-space' cannot be named by both", exception.Message);
    }
}
