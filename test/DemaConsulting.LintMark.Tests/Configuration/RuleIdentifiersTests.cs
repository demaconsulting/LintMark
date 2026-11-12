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

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Unit tests for the RuleIdentifiers class.
/// </summary>
public class RuleIdentifiersTests
{
    /// <summary>
    ///     Test that a kebab-case rule id resolves to itself.
    /// </summary>
    [Fact]
    public void RuleIdentifiers_TryResolve_KebabId_ReturnsCanonical()
    {
        // Act: resolve a canonical id
        var resolved = RuleIdentifiers.TryResolve("heading-increment", out var id);

        // Assert: the id is returned unchanged
        Assert.True(resolved);
        Assert.Equal("heading-increment", id);
    }

    /// <summary>
    ///     Test that every alias resolves to its rule.
    /// </summary>
    [Fact]
    public void RuleIdentifiers_TryResolve_EveryAlias_ReturnsItsRule()
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
            Assert.True(RuleIdentifiers.TryResolve(alias, out var id), alias);
            Assert.Equal(rule, id);
        }

        Assert.Equal(expected.Count, RuleIdentifiers.Aliases.Count);
    }

    /// <summary>
    ///     Test that rule ids and aliases are matched without regard to letter case.
    /// </summary>
    [Theory]
    [InlineData("LINE-LENGTH")]
    [InlineData("Line-Length")]
    [InlineData("md013")]
    [InlineData("Md013")]
    public void RuleIdentifiers_TryResolve_AnyLetterCase_ReturnsCanonical(string name)
    {
        // Act: resolve the spelling
        var resolved = RuleIdentifiers.TryResolve(name, out var id);

        // Assert: it resolves to the canonical id
        Assert.True(resolved);
        Assert.Equal("line-length", id);
    }

    /// <summary>
    ///     Test that matching does not depend on the current culture.
    /// </summary>
    [Fact]
    public void RuleIdentifiers_TryResolve_TurkishCulture_StillMatches()
    {
        // Arrange: a culture with a special case rule for the letter i
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");

            // Act: resolve names containing the letter i in upper case
            var id = RuleIdentifiers.TryResolve("LINE-LENGTH", out var lineLength);
            var link = RuleIdentifiers.TryResolve("LINK-EMPTY", out var linkEmpty);

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
    [InlineData("MD009")]
    [InlineData("MD999")]
    public void RuleIdentifiers_TryResolve_UnknownName_ReturnsFalse(string name)
    {
        // Act: resolve the name
        var resolved = RuleIdentifiers.TryResolve(name, out var id);

        // Assert: it is unknown and not dropped
        Assert.False(resolved);
        Assert.Null(id);
        Assert.False(RuleIdentifiers.IsDroppedAlias(name));
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
    public void RuleIdentifiers_TryResolve_DroppedAlias_ReturnsFalseAndIsDropped(string name)
    {
        // Act: resolve the name
        var resolved = RuleIdentifiers.TryResolve(name, out _);

        // Assert: it is not a rule, but it is a dropped alias
        Assert.False(resolved);
        Assert.True(RuleIdentifiers.IsDroppedAlias(name));
    }

    /// <summary>
    ///     Test that a null name is rejected.
    /// </summary>
    [Fact]
    public void RuleIdentifiers_TryResolve_Null_Throws()
    {
        // Act & Assert: null is rejected by both lookups
        Assert.Throws<ArgumentNullException>(() => RuleIdentifiers.TryResolve(null!, out _));
        Assert.Throws<ArgumentNullException>(() => RuleIdentifiers.IsDroppedAlias(null!));
    }

    /// <summary>
    ///     Test that the configurable rules are sorted in ordinal order and covered by the defaults.
    /// </summary>
    [Fact]
    public void RuleIdentifiers_ConfigurableRules_AreSortedOrdinalAndCoveredByDefaults()
    {
        // Act: take the rule list
        var rules = RuleIdentifiers.ConfigurableRules;

        // Assert: 22 unique ids in ordinal order, each with a default
        Assert.Equal(22, rules.Length);
        Assert.Equal(rules.Order(StringComparer.Ordinal), rules);
        Assert.Equal(rules.Length, rules.Distinct(StringComparer.Ordinal).Count());
        Assert.All(rules, rule => Assert.True(RuleIdentifiers.Defaults.ContainsKey(rule)));
        Assert.Equal(rules.Length, RuleIdentifiers.Defaults.Count);
    }

    /// <summary>
    ///     Test that aliases are unique and map to configurable rules.
    /// </summary>
    [Fact]
    public void RuleIdentifiers_Aliases_AreUniqueAndMapToConfigurableRules()
    {
        // Act & Assert: every alias target is a configurable rule, and no alias is also dropped
        Assert.All(RuleIdentifiers.Aliases, pair => Assert.Contains(pair.Value, RuleIdentifiers.ConfigurableRules));
        Assert.Equal(RuleIdentifiers.Aliases.Count, RuleIdentifiers.Aliases.Values.Distinct(StringComparer.Ordinal).Count());
        Assert.All(RuleIdentifiers.Aliases.Keys, alias => Assert.False(RuleIdentifiers.IsDroppedAlias(alias)));
        Assert.Equal(9, RuleIdentifiers.DroppedAliases.Length);
    }

    /// <summary>
    ///     Test that exactly the three documented rules are off by default.
    /// </summary>
    [Fact]
    public void RuleIdentifiers_Defaults_DisabledRules_AreTheDocumentedThree()
    {
        // Act: select the disabled rules
        var disabled = RuleIdentifiers.Defaults.Where(pair => !pair.Value).Select(pair => pair.Key);

        // Assert: the three documented rules
        Assert.Equal(["descriptive-link-text", "dollar-sign-in-shell-code", "table-grid-unformatted"], disabled);
    }
}
