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
///     Test that the RuleOverrides class resolves the rules named on the command line and applies them over a configuration.
/// </summary>
public class RuleOverridesTests
{
    /// <summary>
    ///     Test that null arguments are rejected.
    /// </summary>
    [Fact]
    public void RuleOverrides_CreateAndApply_NullArguments_Throw()
    {
        // Act & Assert: both lists, a null element and the configuration are checked
        Assert.Equal("enable", Assert.Throws<ArgumentNullException>(() => RuleOverrides.Create(null!, [])).ParamName);
        Assert.Equal("disable", Assert.Throws<ArgumentNullException>(() => RuleOverrides.Create([], null!)).ParamName);
        Assert.Equal("values", Assert.Throws<ArgumentNullException>(() => RuleOverrides.Create([null!], [])).ParamName);
        Assert.Equal("config", Assert.Throws<ArgumentNullException>(() => RuleOverrides.None.Apply(null!)).ParamName);
    }

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
    ///     Test that empty overrides return the same configuration.
    /// </summary>
    [Fact]
    public void RuleOverrides_Apply_NoOverrides_ReturnsTheSameConfiguration()
    {
        // Act & Assert: nothing is forced, so the configuration is not copied
        Assert.Same(RuleOverrides.None, RuleOverrides.Create([], []));
        Assert.True(RuleOverrides.None.IsEmpty);
        Assert.Same(LintMarkConfig.Default, RuleOverrides.None.Apply(LintMarkConfig.Default));
    }

    /// <summary>
    ///     Test that an alias of a dropped rule is accepted and ignored.
    /// </summary>
    [Fact]
    public void RuleOverrides_Create_DroppedAlias_IsIgnored() =>
        Assert.True(RuleOverrides.Create(["MD033"], ["md041"]).IsEmpty);

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
