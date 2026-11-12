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

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Unit tests for the LintMarkConfig class and its supporting LinkRefactorOptions type.
/// </summary>
public class LintMarkConfigTests
{
    /// <summary>
    ///     Test that the default configuration has every documented default value.
    /// </summary>
    [Fact]
    public void LintMarkConfig_Default_Values_MatchDocumentedDefaults()
    {
        // Act: take the default configuration
        var config = LintMarkConfig.Default;

        // Assert: every setting has its documented default
        Assert.Equal(120, config.LineLength);
        Assert.Equal(WrapMode.Fill, config.Wrap);
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
    }

    /// <summary>
    ///     Test that the default configuration enables every rule except the three documented as off.
    /// </summary>
    [Fact]
    public void LintMarkConfig_Default_Rules_MatchDocumentedRuleDefaults()
    {
        // Act: take the default rules
        var rules = LintMarkConfig.Default.Rules;

        // Assert: all configurable rules are present and only the documented three are disabled
        Assert.Equal(22, rules.Count);
        Assert.Equal(
            ["descriptive-link-text", "dollar-sign-in-shell-code", "table-grid-unformatted"],
            rules.Where(r => !r.Value).Select(r => r.Key));
        Assert.True(rules["line-length"]);
        Assert.True(rules["paragraph-wrap"]);
    }

    /// <summary>
    ///     Test that the default ignore patterns are the documented ones, in order.
    /// </summary>
    [Fact]
    public void LintMarkConfig_Default_Ignore_ListsDocumentedPatterns()
    {
        // Act: take the default patterns
        var ignore = LintMarkConfig.Default.Ignore;

        // Assert: the six documented patterns in the documented order
        Assert.Equal(
            [".git/**", "node_modules/**", "**/.venv/**", "**/generated/**", "**/thirdparty/**", ".agent-logs/**"],
            ignore);
    }

    /// <summary>
    ///     Test that the default configuration does not specify a line ending.
    /// </summary>
    [Fact]
    public void LintMarkConfig_Default_EndOfLine_IsNotSpecified()
    {
        // Act & Assert: no line ending is chosen by default
        Assert.Null(LintMarkConfig.Default.EndOfLine);
    }

    /// <summary>
    ///     Test that link refactoring is disabled by default with the documented option values.
    /// </summary>
    [Fact]
    public void LintMarkConfig_Default_LinkRefactor_IsDisabledWithDocumentedValues()
    {
        // Act: take the default link-refactor options
        var options = LintMarkConfig.Default.LinkRefactor;

        // Assert: disabled, with the documented option values
        Assert.False(options.Enabled);
        Assert.Equal(60, options.Threshold);
        Assert.True(options.InTables);
        Assert.False(options.BreakLinkText);
        Assert.Equal(40, options.LabelMaxLength);
        Assert.Equal("<!-- Link References -->", options.DefinitionsMarker);
    }

    /// <summary>
    ///     Test that the rules cannot be modified through a cast.
    /// </summary>
    [Fact]
    public void LintMarkConfig_Rules_CastToMutableTypes_CannotBeModified()
    {
        // Arrange: the default configuration
        var config = LintMarkConfig.Default;

        // Act & Assert: the dictionary refuses changes made through the mutable interface
        ReadOnlyAssertions.AssertCannotBeModified(config.Rules);
    }

    /// <summary>
    ///     Test that the ignore patterns cannot be modified through a cast.
    /// </summary>
    [Fact]
    public void LintMarkConfig_Ignore_CastToMutableTypes_CannotBeModified()
    {
        // Arrange: the default configuration
        var config = LintMarkConfig.Default;

        // Act & Assert: the list refuses changes made through the mutable interfaces
        ReadOnlyAssertions.AssertCannotBeModified((IReadOnlyList<string>)config.Ignore);
    }

    /// <summary>
    ///     Test that changing the array an ignore list was created from does not change the configuration.
    /// </summary>
    [Fact]
    public void LintMarkConfig_Ignore_SourceArrayChangedAfterAssignment_ConfigUnchanged()
    {
        // Arrange: a mutable array that is copied into the configuration
        var source = new[] { "a/**", "b/**" };
        var config = LintMarkConfig.Default with { Ignore = [.. source] };

        // Act: change the source array
        source[0] = "changed/**";

        // Assert: the configuration still holds the original patterns
        Assert.Equal(["a/**", "b/**"], config.Ignore);
    }

    /// <summary>
    ///     Test that changing the rule table a configuration was created from does not change the configuration.
    /// </summary>
    [Fact]
    public void LintMarkConfig_Rules_SourceDictionaryChangedAfterAssignment_ConfigUnchanged()
    {
        // Arrange: a builder whose snapshot is assigned to the configuration
        var builder = LintMarkConfig.Default.Rules.ToBuilder();
        builder.Remove("line-length");
        builder.Add("line-length", false);
        var config = LintMarkConfig.Default with { Rules = builder.ToImmutable() };

        // Act: change the builder after the assignment
        builder.Remove("line-length");
        builder.Add("line-length", true);
        builder.Remove("link-empty");

        // Assert: the configuration still holds the assigned states, and every rule
        Assert.False(config.Rules["line-length"]);
        Assert.True(config.Rules["link-empty"]);
        Assert.Equal(LintMarkConfig.Default.Rules.Count, config.Rules.Count);
    }

    /// <summary>
    ///     Test that every setting reads back the value it was given.
    /// </summary>
    [Fact]
    public void LintMarkConfig_Init_EverySetting_ReadsBackGivenValue()
    {
        // Arrange: a non-default value for every setting
        var rules = LintMarkConfig.Default.Rules.SetItem("line-length", false);
        var linkRefactor = new LinkRefactorOptions(true, 30, false, true, 20, "<!-- Links -->");

        // Act: set every setting
        var config = new LintMarkConfig
        {
            LineLength = 100,
            Wrap = WrapMode.Semantic,
            EndOfLine = LineEnding.CrLf,
            FinalNewline = false,
            Headings = HeadingStyle.Atx,
            Bullet = BulletMarker.Plus,
            ListIndent = 4,
            OrderedStyle = OrderedStyle.One,
            Emphasis = EmphasisMarker.Underscore,
            Strong = EmphasisMarker.Underscore,
            CodeFence = FenceMarker.Tilde,
            CodeBlockStyle = CodeBlockStyle.Fenced,
            HardBreak = HardBreakStyle.Backslash,
            ThematicBreak = ThematicBreakStyle.Asterisks,
            Tables = TableStyle.Aligned,
            BareUrls = BareUrlStyle.Leave,
            MaxBlankLines = 2,
            TrailingWhitespace = TrailingWhitespaceStyle.Strip,
            LinkRefactor = linkRefactor,
            Rules = rules,
            Ignore = ["a/**"]
        };

        // Assert: every setting holds the given value
        Assert.Equal(100, config.LineLength);
        Assert.Equal(WrapMode.Semantic, config.Wrap);
        Assert.Equal(LineEnding.CrLf, config.EndOfLine);
        Assert.False(config.FinalNewline);
        Assert.Equal(HeadingStyle.Atx, config.Headings);
        Assert.Equal(BulletMarker.Plus, config.Bullet);
        Assert.Equal(4, config.ListIndent);
        Assert.Equal(OrderedStyle.One, config.OrderedStyle);
        Assert.Equal(EmphasisMarker.Underscore, config.Emphasis);
        Assert.Equal(EmphasisMarker.Underscore, config.Strong);
        Assert.Equal(FenceMarker.Tilde, config.CodeFence);
        Assert.Equal(CodeBlockStyle.Fenced, config.CodeBlockStyle);
        Assert.Equal(HardBreakStyle.Backslash, config.HardBreak);
        Assert.Equal(ThematicBreakStyle.Asterisks, config.ThematicBreak);
        Assert.Equal(TableStyle.Aligned, config.Tables);
        Assert.Equal(BareUrlStyle.Leave, config.BareUrls);
        Assert.Equal(2, config.MaxBlankLines);
        Assert.Equal(TrailingWhitespaceStyle.Strip, config.TrailingWhitespace);
        Assert.Equal(linkRefactor, config.LinkRefactor);
        Assert.Equal(rules, config.Rules);
        Assert.Equal(["a/**"], config.Ignore);
    }

    /// <summary>
    ///     Test that a modified copy leaves the original untouched.
    /// </summary>
    [Fact]
    public void LintMarkConfig_With_EndOfLine_LeavesOriginalUnchanged()
    {
        // Arrange: the default configuration
        var original = LintMarkConfig.Default;

        // Act: derive a copy with a line ending
        var copy = original with { EndOfLine = LineEnding.CrLf };

        // Assert: only the copy has the line ending
        Assert.Null(original.EndOfLine);
        Assert.Equal(LineEnding.CrLf, copy.EndOfLine);
        Assert.NotEqual(original, copy);
    }

    /// <summary>
    ///     Test that two configurations with the same content are equal, including their collections.
    /// </summary>
    [Fact]
    public void LintMarkConfig_Equals_SameContentDifferentCollections_AreEqual()
    {
        // Arrange: two configurations built separately with equal content
        var first = LintMarkConfig.Default with { Ignore = ["a/**"], Rules = LintMarkConfig.Default.Rules.SetItem("line-length", false) };
        var second = LintMarkConfig.Default with { Ignore = ["a/**"], Rules = LintMarkConfig.Default.Rules.SetItem("line-length", false) };
        var different = second with { Ignore = ["b/**"] };

        // Act & Assert: equality and hash codes follow the content
        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.NotEqual(first, different);
        Assert.NotEqual(LintMarkConfig.Default, first);
    }

    /// <summary>
    ///     Test that a line length outside its range is rejected.
    /// </summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(100_001)]
    public void LintMarkConfig_LineLength_OutOfRange_ThrowsArgumentOutOfRangeException(int value)
    {
        // Act & Assert: the value is rejected
        Assert.Throws<ArgumentOutOfRangeException>(() => LintMarkConfig.Default with { LineLength = value });
    }

    /// <summary>
    ///     Test that the smallest and largest line length are accepted.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(100_000)]
    public void LintMarkConfig_LineLength_ZeroAndMax_Accepted(int value)
    {
        // Act: set the boundary value
        var config = LintMarkConfig.Default with { LineLength = value };

        // Assert: it is stored
        Assert.Equal(value, config.LineLength);
    }

    /// <summary>
    ///     Test that the smallest and largest maximum blank line count are accepted.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(100_000)]
    public void LintMarkConfig_MaxBlankLines_OneAndMax_Accepted(int value)
    {
        // Act: set the boundary value
        var config = LintMarkConfig.Default with { MaxBlankLines = value };

        // Assert: it is stored
        Assert.Equal(value, config.MaxBlankLines);
    }

    /// <summary>
    ///     Test that both list indent values are accepted.
    /// </summary>
    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public void LintMarkConfig_ListIndent_TwoAndFour_Accepted(int value)
    {
        // Act: set the value
        var config = LintMarkConfig.Default with { ListIndent = value };

        // Assert: it is stored
        Assert.Equal(value, config.ListIndent);
    }

    /// <summary>
    ///     Test that the smallest and largest threshold and label length are accepted.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(100_000)]
    public void LinkRefactorOptions_ThresholdAndLabelLength_OneAndMax_Accepted(int value)
    {
        // Act: set the boundary value through a modified copy and through the constructor
        var copy = LinkRefactorOptions.Disabled with { Threshold = value, LabelMaxLength = value };
        var constructed = new LinkRefactorOptions(true, value, true, true, value, "<!-- x -->");

        // Assert: both numbers are stored by both routes
        Assert.Equal((value, value), (copy.Threshold, copy.LabelMaxLength));
        Assert.Equal((value, value), (constructed.Threshold, constructed.LabelMaxLength));
    }

    /// <summary>
    ///     Test that a list indent other than two or four is rejected.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(8)]
    public void LintMarkConfig_ListIndent_NotTwoOrFour_Throws(int value)
    {
        // Act & Assert: the value is rejected
        Assert.Throws<ArgumentOutOfRangeException>(() => LintMarkConfig.Default with { ListIndent = value });
    }

    /// <summary>
    ///     Test that a maximum blank line count below one or above the cap is rejected.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(100_001)]
    public void LintMarkConfig_MaxBlankLines_OutOfRange_Throws(int value)
    {
        // Act & Assert: the value is rejected
        Assert.Throws<ArgumentOutOfRangeException>(() => LintMarkConfig.Default with { MaxBlankLines = value });
    }

    /// <summary>
    ///     Test that an undefined enumeration value is rejected by every enumeration setting.
    /// </summary>
    [Fact]
    public void LintMarkConfig_Init_EnumSettings_UndefinedValue_ThrowsArgumentOutOfRange()
    {
        // Arrange: the default configuration
        var config = LintMarkConfig.Default;

        // Act & Assert: every enumeration setting rejects a value no member names
        Assert.Throws<ArgumentOutOfRangeException>(() => config with { Wrap = (WrapMode)99 });
        Assert.Throws<ArgumentOutOfRangeException>(() => config with { EndOfLine = (LineEnding)99 });
        Assert.Throws<ArgumentOutOfRangeException>(() => config with { Headings = (HeadingStyle)99 });
        Assert.Throws<ArgumentOutOfRangeException>(() => config with { Bullet = (BulletMarker)99 });
        Assert.Throws<ArgumentOutOfRangeException>(() => config with { OrderedStyle = (OrderedStyle)99 });
        Assert.Throws<ArgumentOutOfRangeException>(() => config with { Emphasis = (EmphasisMarker)99 });
        Assert.Throws<ArgumentOutOfRangeException>(() => config with { Strong = (EmphasisMarker)99 });
        Assert.Throws<ArgumentOutOfRangeException>(() => config with { CodeFence = (FenceMarker)99 });
        Assert.Throws<ArgumentOutOfRangeException>(() => config with { CodeBlockStyle = (CodeBlockStyle)99 });
        Assert.Throws<ArgumentOutOfRangeException>(() => config with { HardBreak = (HardBreakStyle)99 });
        Assert.Throws<ArgumentOutOfRangeException>(() => config with { ThematicBreak = (ThematicBreakStyle)99 });
        Assert.Throws<ArgumentOutOfRangeException>(() => config with { Tables = (TableStyle)99 });
        Assert.Throws<ArgumentOutOfRangeException>(() => config with { BareUrls = (BareUrlStyle)99 });
        Assert.Throws<ArgumentOutOfRangeException>(() => config with { TrailingWhitespace = (TrailingWhitespaceStyle)99 });
    }

    /// <summary>
    ///     Test that an uninitialized ignore array is rejected.
    /// </summary>
    [Fact]
    public void LintMarkConfig_Ignore_DefaultArray_Throws()
    {
        // Act & Assert: the uninitialized array is rejected
        Assert.Throws<ArgumentException>(() => LintMarkConfig.Default with { Ignore = default });
    }

    /// <summary>
    ///     Test that null, empty, blank, multi-line and backslash patterns are rejected.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a\nb")]
    [InlineData("a\r\nb")]
    [InlineData("a\rb")]
    [InlineData("a\u0085b")]
    [InlineData("a\u2028b")]
    [InlineData("a\u2029b")]
    [InlineData("a\\b")]
    public void LintMarkConfig_Ignore_InvalidElement_Throws(string? pattern)
    {
        // Act & Assert: an invalid element is rejected
        Assert.Throws<ArgumentException>(() => LintMarkConfig.Default with { Ignore = ["ok/**", pattern!] });
    }

    /// <summary>
    ///     Test that an empty ignore list is accepted.
    /// </summary>
    [Fact]
    public void LintMarkConfig_Ignore_EmptyList_Accepted()
    {
        // Act: clear the patterns
        var config = LintMarkConfig.Default with { Ignore = [] };

        // Assert: no pattern remains
        Assert.Empty(config.Ignore);
    }

    /// <summary>
    ///     Test that rules with an empty or unknown key, or with a missing rule, are rejected.
    /// </summary>
    [Fact]
    public void LintMarkConfig_Rules_InvalidKeyOrMissingRule_Throws()
    {
        // Arrange: rules that are almost complete
        var rules = LintMarkConfig.Default.Rules;

        // Act & Assert: an empty key, an unknown key, an alias and a missing rule are all rejected
        Assert.Throws<ArgumentException>(() => LintMarkConfig.Default with { Rules = rules.Remove("line-length").Add(string.Empty, true) });
        Assert.Throws<ArgumentException>(() => LintMarkConfig.Default with { Rules = rules.Add("not-a-rule", true) });
        Assert.Throws<ArgumentException>(() => LintMarkConfig.Default with { Rules = rules.Remove("line-length").Add("MD013", true) });
        Assert.Throws<ArgumentException>(() => LintMarkConfig.Default with { Rules = rules.Remove("line-length") });
        Assert.Throws<ArgumentNullException>(() => LintMarkConfig.Default with { Rules = null! });
    }

    /// <summary>
    ///     Test that rules given with a case-insensitive comparer are stored with an ordinal one.
    /// </summary>
    [Fact]
    public void LintMarkConfig_Rules_CaseInsensitiveComparer_StoredOrdinal()
    {
        // Arrange: the default rules with a case-insensitive comparer
        var rules = LintMarkConfig.Default.Rules.WithComparers(StringComparer.OrdinalIgnoreCase);

        // Act: assign them
        var config = LintMarkConfig.Default with { Rules = rules };

        // Assert: lookups are case-sensitive again
        Assert.True(config.Rules.ContainsKey("line-length"));
        Assert.False(config.Rules.ContainsKey("LINE-LENGTH"));
    }

    /// <summary>
    ///     Test that a null link-refactor value is rejected.
    /// </summary>
    [Fact]
    public void LintMarkConfig_LinkRefactor_Null_Throws()
    {
        // Act & Assert: null is rejected
        Assert.Throws<ArgumentNullException>(() => LintMarkConfig.Default with { LinkRefactor = null! });
    }

    /// <summary>
    ///     Test that a threshold or label length outside its range is rejected.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100_001)]
    public void LinkRefactorOptions_Constructor_ThresholdOrLabelLengthOutOfRange_ThrowsArgumentOutOfRange(int value)
    {
        // Arrange: the default options
        var options = LinkRefactorOptions.Disabled;

        // Act & Assert: both numbers reject the value
        Assert.Throws<ArgumentOutOfRangeException>(() => options with { Threshold = value });
        Assert.Throws<ArgumentOutOfRangeException>(() => options with { LabelMaxLength = value });
        Assert.Throws<ArgumentOutOfRangeException>(() => new LinkRefactorOptions(true, value, true, true, 10, "<!-- x -->"));
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
    public void LinkRefactorOptions_DefinitionsMarker_NotSingleLineHtmlComment_ThrowsException(string marker)
    {
        // Act & Assert: the marker is rejected by the property, and the shared check agrees
        Assert.Throws<ArgumentException>(() => LinkRefactorOptions.Disabled with { DefinitionsMarker = marker });
        Assert.False(LinkRefactorOptions.IsValidMarker(marker));
        Assert.Throws<ArgumentNullException>(() => LinkRefactorOptions.Disabled with { DefinitionsMarker = null! });
    }

    /// <summary>
    ///     Test that a null marker is rejected by the shared marker check.
    /// </summary>
    [Fact]
    public void LinkRefactorOptions_IsValidMarker_Null_ThrowsArgumentNullException()
    {
        // Act & Assert: null is not a marker
        Assert.Throws<ArgumentNullException>(() => LinkRefactorOptions.IsValidMarker(null!));
    }

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
    public void LinkRefactorOptions_DefinitionsMarker_SingleLineHtmlComment_IsAccepted(string marker)
    {
        // Act: set the marker
        var options = LinkRefactorOptions.Disabled with { DefinitionsMarker = marker };

        // Assert: it is stored and the shared check agrees
        Assert.Equal(marker, options.DefinitionsMarker);
        Assert.True(LinkRefactorOptions.IsValidMarker(marker));
    }

    /// <summary>
    ///     Test that the default ignore patterns are an initialized immutable array value.
    /// </summary>
    [Fact]
    public void LintMarkConfig_DefaultIgnore_Value_IsInitializedImmutableArray()
    {
        // Act & Assert: the shared default list is an initialized immutable array
        Assert.False(LintMarkConfig.DefaultIgnore.IsDefault);
        Assert.IsType<ImmutableArray<string>>(LintMarkConfig.DefaultIgnore);
    }
}
