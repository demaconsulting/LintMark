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
            ["MD024"] = "heading-duplicate",
            ["MD026"] = "heading-trailing-punctuation",
            ["MD028"] = "blockquote-blank-line",
            ["MD036"] = "emphasis-as-heading",
            ["MD040"] = "code-fence-language",
            ["MD042"] = "link-empty",
            ["MD045"] = "image-alt-text",
            ["MD051"] = "link-fragment",
            ["MD052"] = "link-reference-undefined",
            ["MD053"] = "link-definition-unused",
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
    ///     Test that a null name is rejected by the resolving method.
    /// </summary>
    [Fact]
    public void RuleCatalog_TryResolve_Null_Throws()
    {
        // Act & Assert: null is rejected and the parameter is named
        var exception = Assert.Throws<ArgumentNullException>(() => RuleCatalog.TryResolve(null!, out _));
        Assert.Equal("name", exception.ParamName);
    }

    /// <summary>
    ///     Test that a null name is rejected by the dropped-alias check.
    /// </summary>
    [Fact]
    public void RuleCatalog_IsDroppedAlias_Null_Throws()
    {
        // Act & Assert: null is rejected and the parameter is named
        var exception = Assert.Throws<ArgumentNullException>(() => RuleCatalog.IsDroppedAlias(null!));
        Assert.Equal("name", exception.ParamName);
    }

    /// <summary>
    ///     Test that the configurable rules are sorted in ordinal order and covered by the defaults.
    /// </summary>
    [Fact]
    public void RuleCatalog_ConfigurableRules_AllRules_SortedOrdinalAndCoveredByDefaults()
    {
        // Act: take the rule list
        var rules = RuleCatalog.ConfigurableRules;

        // Assert: 22 unique ids in ordinal order, each with a default
        Assert.Equal(22, rules.Length);
        Assert.Equal(rules.Order(StringComparer.Ordinal), rules);
        Assert.Equal(rules.Length, rules.Distinct(StringComparer.Ordinal).Count());
        Assert.All(rules, rule => Assert.True(RuleCatalog.Defaults.ContainsKey(rule)));
        Assert.Equal(rules.Length, RuleCatalog.Defaults.Count);
    }

    /// <summary>
    ///     Test that aliases are unique and map to configurable rules.
    /// </summary>
    [Fact]
    public void RuleCatalog_Aliases_AllAliases_UniqueAndMapToConfigurableRules()
    {
        // Act & Assert: every alias target is a configurable rule, and no alias is also dropped
        Assert.All(RuleCatalog.Aliases, pair => Assert.Contains(pair.Value, RuleCatalog.ConfigurableRules));
        Assert.Equal(RuleCatalog.Aliases.Count, RuleCatalog.Aliases.Values.Distinct(StringComparer.Ordinal).Count());
        Assert.All(RuleCatalog.Aliases.Keys, alias => Assert.False(RuleCatalog.IsDroppedAlias(alias)));
        Assert.Equal(9, RuleCatalog.DroppedAliases.Length);
    }

    /// <summary>
    ///     Test that exactly the three documented rules are off by default.
    /// </summary>
    [Fact]
    public void RuleCatalog_Defaults_DisabledRules_AreTheDocumentedThree()
    {
        // Act: select the disabled rules
        var disabled = RuleCatalog.Defaults.Where(pair => !pair.Value).Select(pair => pair.Key);

        // Assert: the three documented rules
        Assert.Equal(["descriptive-link-text", "dollar-sign-in-shell-code", "table-grid-unformatted"], disabled);
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
        Assert.DoesNotContain("line-ending", RuleCatalog.ConfigurableRules);
        Assert.False(RuleCatalog.Defaults.ContainsKey("line-ending"));
    }

    /// <summary>
    ///     Test that a rule that is registered but not configurable is not resolved as configurable.
    /// </summary>
    [Theory]
    [InlineData("line-ending")]
    [InlineData("LINE-ENDING")]
    [InlineData("config-error")]
    [InlineData("safety-html-mismatch")]
    public void RuleCatalog_TryResolveConfigurable_LineEnding_ReturnsFalse(string name)
    {
        // Act: resolve the name as a configurable rule
        var resolved = RuleCatalog.TryResolveConfigurable(name, out var id);

        // Assert: it is rejected, as an unknown rule is
        Assert.False(resolved);
        Assert.Null(id);
    }

    /// <summary>
    ///     Test that a kebab-case id and an alias resolve to the canonical id of a configurable rule.
    /// </summary>
    [Theory]
    [InlineData("line-length", "line-length")]
    [InlineData("LINE-LENGTH", "line-length")]
    [InlineData("MD013", "line-length")]
    [InlineData("md013", "line-length")]
    [InlineData("link-reversed", "link-reversed")]
    [InlineData("MD011", "link-reversed")]
    [InlineData("paragraph-wrap", "paragraph-wrap")]
    public void RuleCatalog_TryResolveConfigurable_KebabIdAndAlias_ReturnsCanonical(string name, string expected)
    {
        // Act: resolve the name as a configurable rule
        var resolved = RuleCatalog.TryResolveConfigurable(name, out var id);

        // Assert: the canonical id is returned
        Assert.True(resolved);
        Assert.Equal(expected, id);
    }

    /// <summary>
    ///     Test that a dropped alias or an unknown name is not a configurable rule.
    /// </summary>
    [Theory]
    [InlineData("MD033")]
    [InlineData("md025")]
    [InlineData("no-such-rule")]
    [InlineData("MD009")]
    [InlineData("")]
    public void RuleCatalog_TryResolveConfigurable_DroppedAlias_ReturnsFalse(string name)
    {
        // Act: resolve the name as a configurable rule
        var resolved = RuleCatalog.TryResolveConfigurable(name, out var id);

        // Assert: nothing is resolved, and the name is not a registered id either
        Assert.False(resolved);
        Assert.Null(id);
        Assert.False(RuleCatalog.TryGet(name, out _));
    }

    /// <summary>
    ///     Test that a null name is rejected when resolving a configurable rule.
    /// </summary>
    [Fact]
    public void RuleCatalog_TryResolveConfigurable_Null_Throws()
    {
        // Act & Assert: null is rejected and the parameter is named
        var exception = Assert.Throws<ArgumentNullException>(() => RuleCatalog.TryResolveConfigurable(null!, out _));
        Assert.Equal("name", exception.ParamName);
    }

    /// <summary>
    ///     Test that a registered id returns its entry, in any letter case.
    /// </summary>
    [Theory]
    [InlineData("line-length")]
    [InlineData("LINE-LENGTH")]
    public void RuleCatalog_TryGet_RegisteredId_ReturnsEntry(string id)
    {
        // Act: look the rule up
        var found = RuleCatalog.TryGet(id, out var rule);

        // Assert: the entry carries the registered facts
        Assert.True(found);
        Assert.NotNull(rule);
        Assert.Equal("line-length", rule.Id);
        Assert.Equal(DiagnosticKind.Formatting, rule.Kind);
        Assert.Equal(["MD013"], rule.Aliases);
        Assert.True(rule.DefaultEnabled);
        Assert.True(rule.IsConfigurable);
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
    ///     Test that a null id is rejected by the lookup.
    /// </summary>
    [Fact]
    public void RuleCatalog_TryGet_Null_Throws()
    {
        // Act & Assert: null is rejected and the parameter is named
        var exception = Assert.Throws<ArgumentNullException>(() => RuleCatalog.TryGet(null!, out _));
        Assert.Equal("id", exception.ParamName);
    }

    /// <summary>
    ///     Test that every entry has a description and that ids and aliases are unique.
    /// </summary>
    [Fact]
    public void RuleCatalog_Rules_EveryEntry_HasDescriptionAndUniqueId()
    {
        // Act: take the entries
        var rules = RuleCatalog.Rules;
        var names = rules.Select(rule => rule.Id).Concat(rules.SelectMany(rule => rule.Aliases)).ToList();

        // Assert: each entry is described, and no id or alias is used twice (ignoring case)
        Assert.All(rules, rule => Assert.False(string.IsNullOrWhiteSpace(rule.Description), rule.Id));
        Assert.All(rules, rule => Assert.EndsWith(".", rule.Description, StringComparison.Ordinal));
        Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    /// <summary>
    ///     Test that the entries are sorted in ordinal order of the id.
    /// </summary>
    [Fact]
    public void RuleCatalog_Rules_AllEntries_SortedOrdinalById()
    {
        // Act: take the ids in registry order
        var ids = RuleCatalog.Rules.Select(rule => rule.Id).ToList();

        // Assert: they are already in ordinal order
        Assert.Equal(ids.Order(StringComparer.Ordinal), ids);
    }

    /// <summary>
    ///     Test that the formatting rules are the configurable line length and paragraph wrap rules, the line
    ///     ending rule and the fourteen always-applied whitespace and block rules.
    /// </summary>
    [Fact]
    public void RuleCatalog_Rules_FormattingRules_AreTheConfigurableOnesLineEndingAndTheAlwaysApplied()
    {
        // Act: select the rules whose findings rewriting removes
        var formatting = RuleCatalog.Rules
            .Where(rule => rule.Kind == DiagnosticKind.Formatting)
            .Select(rule => rule.Id);

        // Assert: those rules, in ordinal order
        Assert.Equal(
            [
                "code-block-style",
                "code-fence",
                "fence-blank-lines",
                "final-newline",
                "hard-tabs",
                "heading-blank-lines",
                "heading-space",
                "heading-start-left",
                "heading-style",
                "line-ending",
                "line-length",
                "list-blank-lines",
                "max-blank-lines",
                "paragraph-wrap",
                "table-blank-lines",
                "thematic-break",
                "trailing-whitespace"
            ],
            formatting);
    }

    /// <summary>
    ///     Test that the non-configurable entries are the always-applied rules and the failure identifiers.
    /// </summary>
    [Fact]
    public void RuleCatalog_Rules_NonConfigurableEntries_ListTheAlwaysOnAndFailureIds()
    {
        // Arrange: the identifiers that are known but cannot be switched on or off
        var expected = new[]
        {
            "line-ending",
            "trailing-whitespace",
            "hard-tabs",
            "max-blank-lines",
            "heading-style",
            "heading-space",
            "heading-start-left",
            "heading-blank-lines",
            "fence-blank-lines",
            "list-blank-lines",
            "table-blank-lines",
            "thematic-break",
            "code-block-style",
            "code-fence",
            "final-newline",
            "safety-html-mismatch",
            "safety-structure-mismatch",
            "safety-not-idempotent",
            "file-read-error",
            "file-encoding-error",
            "file-line-ending-error",
            "file-parse-error",
            "file-format-error",
            "file-write-error",
            "config-error"
        };

        // Act: select the non-configurable entries
        var actual = RuleCatalog.Rules.Where(rule => !rule.IsConfigurable).ToList();

        // Assert: exactly those identifiers, all enabled
        Assert.Equal(expected.Order(StringComparer.Ordinal), actual.Select(rule => rule.Id));
        Assert.All(actual, rule => Assert.True(rule.DefaultEnabled));
    }

    /// <summary>
    ///     Test that the always-applied formatting rules carry their legacy rule-number aliases and nothing else does.
    /// </summary>
    [Fact]
    public void RuleCatalog_Rules_NonConfigurableEntries_OnlyFormattingRulesHaveAliases()
    {
        // Arrange: the aliases of the always-applied formatting rules
        var expected = new Dictionary<string, string[]>
        {
            ["trailing-whitespace"] = ["MD009"],
            ["hard-tabs"] = ["MD010"],
            ["max-blank-lines"] = ["MD012"],
            ["heading-style"] = ["MD003", "MD021"],
            ["heading-space"] = ["MD019"],
            ["heading-start-left"] = ["MD023"],
            ["heading-blank-lines"] = ["MD022"],
            ["fence-blank-lines"] = ["MD031"],
            ["list-blank-lines"] = ["MD032"],
            ["table-blank-lines"] = ["MD058"],
            ["thematic-break"] = ["MD035"],
            ["code-block-style"] = ["MD046"],
            ["code-fence"] = ["MD048"],
            ["final-newline"] = ["MD047"]
        };

        // Act: collect the aliases of the non-configurable entries
        var actual = RuleCatalog.Rules
            .Where(rule => !rule.IsConfigurable && rule.Aliases.Length > 0)
            .ToDictionary(rule => rule.Id, rule => rule.Aliases.ToArray());

        // Assert: exactly those rules have exactly those aliases
        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), actual.Keys.Order(StringComparer.Ordinal));
        Assert.All(expected, pair => Assert.Equal(pair.Value, actual[pair.Key]));
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
    ///     Test that every registered id is accepted as the rule id of a diagnostic.
    /// </summary>
    [Fact]
    public void RuleCatalog_Rules_EveryRuleId_IsValidDiagnosticRuleId()
    {
        // Act & Assert: a diagnostic can be created with each id, which proves it is kebab-case
        Assert.All(RuleCatalog.Rules, rule =>
        {
            var diagnostic = new Diagnostic(rule.Id, rule.Kind, "a.md", 1, 1, "Message.");
            Assert.Equal(rule.Id, diagnostic.RuleId);
        });
    }

    /// <summary>
    ///     Test that the reversed link rule can be reached by its legacy alias.
    /// </summary>
    [Fact]
    public void RuleCatalog_Aliases_LinkReversed_ResolvesMd011()
    {
        // Act: look the alias up in the alias map and resolve it
        var mapped = RuleCatalog.Aliases["MD011"];
        var resolved = RuleCatalog.TryResolveConfigurable("MD011", out var id);

        // Assert: both give the reversed link rule, which is on by default
        Assert.Equal("link-reversed", mapped);
        Assert.True(resolved);
        Assert.Equal("link-reversed", id);
        Assert.True(RuleCatalog.Defaults["link-reversed"]);
    }

    /// <summary>
    ///     Test that the exposed collections cannot be modified through a cast to a mutable interface.
    /// </summary>
    [Fact]
    public void RuleCatalog_Rules_CastToMutableTypes_CannotBeModified()
    {
        // Act & Assert: the immutable arrays refuse changes through the list views
        ReadOnlyAssertions.AssertCannotBeModified((IReadOnlyList<string>)RuleCatalog.ConfigurableRules);
        ReadOnlyAssertions.AssertCannotBeModified((IReadOnlyList<string>)RuleCatalog.DroppedAliases);
        ReadOnlyAssertions.AssertCannotBeModified((IReadOnlyList<RuleInfo>)RuleCatalog.Rules);
        Assert.True(((IList<string>)RuleCatalog.ConfigurableRules).IsReadOnly);
        Assert.True(((IList<string>)RuleCatalog.DroppedAliases).IsReadOnly);
        Assert.True(((IList<RuleInfo>)RuleCatalog.Rules).IsReadOnly);

        // Act & Assert: the immutable dictionaries refuse changes through the dictionary views
        ReadOnlyAssertions.AssertCannotBeModified((IReadOnlyDictionary<string, bool>)RuleCatalog.Defaults);
        ReadOnlyAssertions.AssertCannotBeModified((IReadOnlyDictionary<string, string>)RuleCatalog.Aliases);
        Assert.True(((IDictionary<string, bool>)RuleCatalog.Defaults).IsReadOnly);
        Assert.True(((IDictionary<string, string>)RuleCatalog.Aliases).IsReadOnly);
    }

    /// <summary>
    ///     Test that the registry holds 22 configurable rules, 25 non-configurable rules, 9 dropped aliases and 16 configurable aliases.
    /// </summary>
    [Fact]
    public void RuleCatalog_Rules_Configurable22NonConfigurable25AndDropped9_CountsMatch()
    {
        // Act & Assert: the registered counts agree with each other
        Assert.Equal(22, RuleCatalog.Rules.Count(rule => rule.IsConfigurable));
        Assert.Equal(25, RuleCatalog.Rules.Count(rule => !rule.IsConfigurable));
        Assert.Equal(47, RuleCatalog.Rules.Length);
        Assert.Equal(22, RuleCatalog.ConfigurableRules.Length);
        Assert.Equal(22, RuleCatalog.Defaults.Count);
        Assert.Equal(16, RuleCatalog.Aliases.Count);
        Assert.Equal(9, RuleCatalog.DroppedAliases.Length);
        Assert.Equal(RuleCatalog.Rules.Where(rule => rule.IsConfigurable).Select(rule => rule.Id), RuleCatalog.ConfigurableRules);
    }
}
