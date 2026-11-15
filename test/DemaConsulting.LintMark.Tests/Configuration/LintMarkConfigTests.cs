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
using DemaConsulting.LintMark.Document;
using DemaConsulting.LintMark.Linting;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Unit tests for the defaults of the LintMarkConfig class and the marker check of the LinkRefactorOptions type.
/// </summary>
public class LintMarkConfigTests
{
    /// <summary>
    ///     Compares two configurations by content, which the record itself does not do for its collections.
    /// </summary>
    internal sealed class ContentComparer : IEqualityComparer<LintMarkConfig>
    {
        /// <summary>
        ///     Gets the shared comparer.
        /// </summary>
        public static ContentComparer Instance { get; } = new();

        /// <inheritdoc />
        public bool Equals(LintMarkConfig? x, LintMarkConfig? y) =>
            x is not null && y is not null
            && x == y with { Rules = x.Rules, Ignore = x.Ignore }
            && x.Rules.SequenceEqual(y.Rules)
            && x.Ignore.SequenceEqual(y.Ignore, StringComparer.Ordinal);

        /// <inheritdoc />
        public int GetHashCode(LintMarkConfig obj) => obj.LineLength;
    }

    /// <summary>
    ///     Test that a marker that is not a single HTML comment on one line is rejected.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("Link References")]
    [InlineData("<!-- open")]
    [InlineData("close -->")]
    [InlineData("<!-->")]
    [InlineData("<!--->")]
    [InlineData("<!--> -->")]
    [InlineData("<!---> -->")]
    [InlineData("<!-- a --> text <!-- b -->")]
    [InlineData("<!-- a --> text")]
    [InlineData("<!-- a -->-->")]
    [InlineData("<!-- a\nb -->")]
    [InlineData("<!-- a\r\nb -->")]
    [InlineData("<!-- a\rb -->")]
    [InlineData("<!-- a\u0085b -->")]
    [InlineData("<!-- a\u2028b -->")]
    [InlineData("<!-- a\u2029b -->")]
    public void LinkRefactorOptions_IsValidMarker_NotSingleLineHtmlComment_ReturnsFalse(string marker) =>
        Assert.False(LinkRefactorOptions.IsValidMarker(marker));

    /// <summary>
    ///     Test that markers that are one HTML comment on one line are accepted, including the shortest one.
    /// </summary>
    [Theory]
    [InlineData("<!---->")]
    [InlineData("<!-- x -->")]
    [InlineData("<!--- x -->")]
    [InlineData("<!-- a - b -->")]
    [InlineData("<!-- a -- b -->")]
    [InlineData("<!-- a --->")]
    [InlineData("<!--x-->")]
    [InlineData("<!-- Link References -->")]
    public void LinkRefactorOptions_IsValidMarker_SingleLineHtmlComment_ReturnsTrue(string marker) =>
        Assert.True(LinkRefactorOptions.IsValidMarker(marker));

    /// <summary>
    ///     Test that the built-in default gives every setting its documented value.
    /// </summary>
    [Fact]
    public void LintMarkConfig_Default_EverySetting_HasDocumentedValue()
    {
        // Arrange: the built-in default configuration
        var config = LintMarkConfig.Default;

        // Act and Assert: every setting has its documented default
        Assert.Equal(120, config.LineLength);
        Assert.Equal(WrapMode.Fill, config.Wrap);
        Assert.Null(config.EndOfLine);
        Assert.True(config.FinalNewline);
        Assert.Equal(HeadingStyle.Atx, config.Headings);
        Assert.Equal(BulletMarker.Dash, config.Bullet);
        Assert.Equal(2, config.ListIndent);
        Assert.Equal(OrderedStyle.Incrementing, config.OrderedStyle);
        Assert.Equal(EmphasisMarker.Asterisk, config.Emphasis);
        Assert.Equal(EmphasisMarker.Asterisk, config.Strong);
        Assert.Equal(FenceMarker.Backtick, config.CodeFence);
        Assert.Equal(CodeBlockStyle.Fenced, config.CodeBlockStyle);
        Assert.Equal(HardBreakStyle.Spaces, config.HardBreak);
        Assert.Equal(ThematicBreakStyle.Dashes, config.ThematicBreak);
        Assert.Equal(TableStyle.Compact, config.Tables);
        Assert.Equal(BareUrlStyle.Autolink, config.BareUrls);
        Assert.Equal(1, config.MaxBlankLines);
        Assert.Equal(TrailingWhitespaceStyle.Strip, config.TrailingWhitespace);
        Assert.Equal(
            [".git/**", "node_modules/**", "**/.venv/**", "**/generated/**", "**/thirdparty/**", ".agent-logs/**"],
            config.Ignore);
    }

    /// <summary>
    ///     Test that the default configuration holds a state for every configurable rule and that only the three
    ///     opt-in rules are off.
    /// </summary>
    [Fact]
    public void LintMarkConfig_Default_Rules_HoldEveryConfigurableRuleWithOptInRulesOff()
    {
        // Arrange: the built-in default configuration
        var rules = LintMarkConfig.Default.Rules;

        // Act: collect the rules that are off by default
        var disabled = rules.Where(rule => !rule.Value).Select(rule => rule.Key).ToArray();

        // Assert: the map is the catalog's complete default table, with only the opt-in rules off
        Assert.Equal(RuleCatalog.Defaults.Count, rules.Count);
        Assert.Equal(["descriptive-link-text", "dollar-sign-in-shell-code", "table-grid-unformatted"], disabled);
    }

    /// <summary>
    ///     Test that the link-refactor setting is off by default with the documented option values.
    /// </summary>
    [Fact]
    public void LintMarkConfig_Default_LinkRefactor_IsDisabledWithDocumentedOptions()
    {
        // Arrange: the built-in default configuration
        var options = LintMarkConfig.Default.LinkRefactor;

        // Act and Assert: the conversion is off and the options have their documented defaults
        Assert.False(options.Enabled);
        Assert.Equal(60, options.Threshold);
        Assert.True(options.InTables);
        Assert.False(options.BreakLinkText);
        Assert.Equal(40, options.LabelMaxLength);
        Assert.Equal("<!-- Link References -->", options.DefinitionsMarker);
        Assert.True(LinkRefactorOptions.IsValidMarker(options.DefinitionsMarker));
    }
}
