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

using System.Text;
using DemaConsulting.LintMark.Configuration;
using DemaConsulting.LintMark.Document;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Unit tests for the ConfigLoader class: parsing and validating configuration content.
/// </summary>
public class ConfigLoaderParseTests
{
    /// <summary>
    ///     The path reported in the errors of every test.
    /// </summary>
    private const string ConfigPath = "conf/.lintmark.yaml";

    /// <summary>
    ///     The documented sample configuration that sets every setting to a non-default value.
    /// </summary>
    internal const string FullSample =
        "line-length: 100\n"
        + "wrap: sentence\n"
        + "end-of-line: crlf\n"
        + "final-newline: false\n"
        + "heading-style: atx\n"
        + "bullet: asterisk\n"
        + "list-indent: 4\n"
        + "ordered-style: one\n"
        + "emphasis: underscore\n"
        + "strong: underscore\n"
        + "code-fence: tilde\n"
        + "code-block-style: fenced\n"
        + "hard-break: backslash\n"
        + "thematic-break: asterisks\n"
        + "bare-urls: leave\n"
        + "tables: aligned\n"
        + "max-blank-lines: 2\n"
        + "trailing-whitespace: strip\n"
        + "link-refactor:\n"
        + "  enabled: true\n"
        + "  threshold: 80\n"
        + "  in-tables: false\n"
        + "  break-link-text: true\n"
        + "  label-max-length: 25\n"
        + "  definitions-marker: \"<!-- Links -->\"\n"
        + "rules:\n"
        + "  MD013: false\n"
        + "  heading-increment: false\n"
        + "  descriptive-link-text: true\n"
        + "  MD025: false\n"
        + "ignore:\n"
        + "  - \"docs/generated/**\"\n"
        + "  - \"**/vendor/**\"\n";

    /// <summary>
    ///     Parses text as UTF-8 content of a configuration file.
    /// </summary>
    /// <param name="yaml">The text.</param>
    /// <returns>The result.</returns>
    private static ConfigLoadResult Parse(string yaml) => ConfigLoader.Parse(ConfigPath, Encoding.UTF8.GetBytes(yaml));

    /// <summary>
    ///     Parses text that must be valid and returns its configuration.
    /// </summary>
    /// <param name="yaml">The text.</param>
    /// <returns>The configuration.</returns>
    private static LintMarkConfig Valid(string yaml)
    {
        var result = Parse(yaml);
        Assert.True(result.Errors.IsEmpty, string.Join("; ", result.Errors.Select(e => $"{e.Line}:{e.Column} {e.Message}")));
        Assert.NotNull(result.Config);
        return result.Config;
    }

    /// <summary>
    ///     Parses text that must have exactly one error and returns it.
    /// </summary>
    /// <param name="yaml">The text.</param>
    /// <returns>The error.</returns>
    private static ConfigError OneError(string yaml)
    {
        var result = Parse(yaml);
        Assert.Null(result.Config);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ConfigPath, error.Path);
        return error;
    }

    /// <summary>
    ///     Asserts the position and message fragment of an error.
    /// </summary>
    /// <param name="error">The error.</param>
    /// <param name="line">Expected line.</param>
    /// <param name="column">Expected column.</param>
    /// <param name="message">Text the message must contain.</param>
    private static void AssertError(ConfigError error, int line, int column, string message)
    {
        Assert.Equal((line, column), (error.Line, error.Column));
        Assert.Contains(message, error.Message);
    }

    /// <summary>
    ///     Test that an empty file gives the defaults.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_EmptyContent_ReturnsDefaults()
    {
        // Act: parse empty content
        var result = ConfigLoader.Parse(ConfigPath, []);

        // Assert: the defaults, attributed to the file
        Assert.Equal(LintMarkConfig.Default, result.Config);
        Assert.Equal(ConfigPath, result.SourcePath);
        Assert.Empty(result.Errors);
    }

    /// <summary>
    ///     Test that content without settings gives the defaults.
    /// </summary>
    [Theory]
    [InlineData("\n\n  \n")]
    [InlineData("# only a comment\n# another\n")]
    [InlineData("---\n")]
    [InlineData("---\n...\n")]
    [InlineData("~\n")]
    [InlineData("null\n")]
    public void ConfigLoader_Parse_ContentWithoutSettings_ReturnsDefaults(string yaml)
    {
        // Act: parse content that holds no settings
        var config = Valid(yaml);

        // Assert: the defaults
        Assert.Equal(LintMarkConfig.Default, config);
    }

    /// <summary>
    ///     Test that omitted settings keep their defaults.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_PartialFile_OmittedSettingsKeepDefaults()
    {
        // Act: parse a file that sets two settings
        var config = Valid("line-length: 80\nbullet: plus\n");

        // Assert: the two settings are applied and everything else is the default
        Assert.Equal(LintMarkConfig.Default with { LineLength = 80, Bullet = BulletMarker.Plus }, config);
    }

    /// <summary>
    ///     Test that the documented sample file applies every setting.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_FullSampleFile_AppliesEverySetting()
    {
        // Act: parse the documented sample
        var config = Valid(FullSample);

        // Assert: every setting has the sample's value
        Assert.Equal(100, config.LineLength);
        Assert.Equal(WrapMode.Sentence, config.Wrap);
        Assert.Equal(LineEnding.CrLf, config.EndOfLine);
        Assert.False(config.FinalNewline);
        Assert.Equal(HeadingStyle.Atx, config.Headings);
        Assert.Equal(BulletMarker.Asterisk, config.Bullet);
        Assert.Equal(4, config.ListIndent);
        Assert.Equal(OrderedStyle.One, config.OrderedStyle);
        Assert.Equal(EmphasisMarker.Underscore, config.Emphasis);
        Assert.Equal(EmphasisMarker.Underscore, config.Strong);
        Assert.Equal(FenceMarker.Tilde, config.CodeFence);
        Assert.Equal(CodeBlockStyle.Fenced, config.CodeBlockStyle);
        Assert.Equal(HardBreakStyle.Backslash, config.HardBreak);
        Assert.Equal(ThematicBreakStyle.Asterisks, config.ThematicBreak);
        Assert.Equal(BareUrlStyle.Leave, config.BareUrls);
        Assert.Equal(TableStyle.Aligned, config.Tables);
        Assert.Equal(2, config.MaxBlankLines);
        Assert.Equal(TrailingWhitespaceStyle.Strip, config.TrailingWhitespace);
        Assert.Equal(new LinkRefactorOptions(true, 80, false, true, 25, "<!-- Links -->"), config.LinkRefactor);
        Assert.False(config.Rules["line-length"]);
        Assert.False(config.Rules["heading-increment"]);
        Assert.True(config.Rules["descriptive-link-text"]);
        Assert.Equal(["docs/generated/**", "**/vendor/**"], config.Ignore);
    }

    /// <summary>
    ///     Test that a byte order mark at the start of the file is ignored.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_Utf8Bom_IsIgnored()
    {
        // Arrange: content that starts with a UTF-8 byte order mark
        byte[] content = [0xEF, 0xBB, 0xBF, .. "line-length: 90\n"u8];

        // Act: parse it
        var result = ConfigLoader.Parse(ConfigPath, content);

        // Assert: the first key is recognized
        Assert.Equal(90, result.Config!.LineLength);
    }

    /// <summary>
    ///     Test that CRLF line endings parse and give the same line numbers as LF.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_CrlfLineEndings_ParsesAndNumbersLinesCorrectly()
    {
        // Arrange: the same two-error content with both line endings
        const string lf = "line-length: 90\nbogus: 1\nwrap: nope\n";
        var crlf = lf.Replace("\n", "\r\n", StringComparison.Ordinal);

        // Act: parse both
        var lfResult = Parse(lf);
        var crlfResult = Parse(crlf);

        // Assert: identical positions and messages
        Assert.Equal(2, crlfResult.Errors.Length);
        Assert.Equal(
            lfResult.Errors.Select(e => (e.Line, e.Column, e.Message)),
            crlfResult.Errors.Select(e => (e.Line, e.Column, e.Message)));
        Assert.Equal(2, crlfResult.Errors[0].Line);
        Assert.Equal(3, crlfResult.Errors[1].Line);
    }

    /// <summary>
    ///     Test that the order of settings does not matter.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_KeyOrder_DoesNotMatter()
    {
        // Act: parse the same settings in two orders
        var first = Valid("wrap: semantic\nline-length: 70\nbullet: plus\n");
        var second = Valid("bullet: plus\nline-length: 70\nwrap: semantic\n");

        // Assert: the configurations are equal
        Assert.Equal(first, second);
    }

    /// <summary>
    ///     Test that flow-style mappings are accepted.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_FlowStyleMapping_Accepted()
    {
        // Act: parse flow-style collections
        var config = Valid("link-refactor: {enabled: true, threshold: 50}\nrules: {MD013: false}\nignore: [\"a/**\", b/**]\n");

        // Assert: they are applied
        Assert.True(config.LinkRefactor.Enabled);
        Assert.Equal(50, config.LinkRefactor.Threshold);
        Assert.False(config.Rules["line-length"]);
        Assert.Equal(["a/**", "b/**"], config.Ignore);
    }

    /// <summary>
    ///     Test that a quoted value of an enumeration setting is accepted.
    /// </summary>
    [Theory]
    [InlineData("wrap: \"semantic\"\n")]
    [InlineData("wrap: 'semantic'\n")]
    [InlineData("\"wrap\": semantic\n")]
    public void ConfigLoader_Parse_QuotedEnumValue_Accepted(string yaml)
    {
        // Act: parse the quoted spelling
        var config = Valid(yaml);

        // Assert: the value is applied
        Assert.Equal(WrapMode.Semantic, config.Wrap);
    }

    /// <summary>
    ///     Test that every token of every enumeration setting is accepted.
    /// </summary>
    [Theory]
    [InlineData("wrap", "fill")]
    [InlineData("wrap", "sentence")]
    [InlineData("wrap", "semantic")]
    [InlineData("heading-style", "atx")]
    [InlineData("bullet", "dash")]
    [InlineData("bullet", "asterisk")]
    [InlineData("bullet", "plus")]
    [InlineData("ordered-style", "incrementing")]
    [InlineData("ordered-style", "one")]
    [InlineData("emphasis", "asterisk")]
    [InlineData("emphasis", "underscore")]
    [InlineData("strong", "asterisk")]
    [InlineData("strong", "underscore")]
    [InlineData("code-fence", "backtick")]
    [InlineData("code-fence", "tilde")]
    [InlineData("code-block-style", "fenced")]
    [InlineData("hard-break", "spaces")]
    [InlineData("hard-break", "backslash")]
    [InlineData("thematic-break", "dashes")]
    [InlineData("thematic-break", "asterisks")]
    [InlineData("thematic-break", "underscores")]
    [InlineData("bare-urls", "autolink")]
    [InlineData("bare-urls", "leave")]
    [InlineData("tables", "compact")]
    [InlineData("tables", "aligned")]
    [InlineData("trailing-whitespace", "strip")]
    [InlineData("end-of-line", "lf")]
    [InlineData("end-of-line", "crlf")]
    public void ConfigLoader_Parse_EveryEnumValue_Accepted(string setting, string token)
    {
        // Act: parse the setting with the token
        var result = Parse($"{setting}: {token}\n");

        // Assert: it is accepted
        Assert.Empty(result.Errors);
        Assert.NotNull(result.Config);
    }

    /// <summary>
    ///     Test that a token of an enumeration setting that is not allowed lists the allowed tokens.
    /// </summary>
    [Theory]
    [InlineData("wrap", "reflow", "fill, sentence, semantic")]
    [InlineData("heading-style", "setext", "atx")]
    [InlineData("bullet", "star", "dash, asterisk, plus")]
    [InlineData("ordered-style", "consistent", "incrementing, one")]
    [InlineData("emphasis", "star", "asterisk, underscore")]
    [InlineData("strong", "star", "asterisk, underscore")]
    [InlineData("code-fence", "dash", "backtick, tilde")]
    [InlineData("code-block-style", "indented", "fenced")]
    [InlineData("hard-break", "html", "spaces, backslash")]
    [InlineData("thematic-break", "stars", "dashes, asterisks, underscores")]
    [InlineData("bare-urls", "link", "autolink, leave")]
    [InlineData("tables", "grid", "compact, aligned")]
    [InlineData("trailing-whitespace", "preserve", "strip")]
    [InlineData("end-of-line", "cr", "lf, crlf")]
    [InlineData("end-of-line", "auto", "lf, crlf")]
    [InlineData("end-of-line", "consistent", "lf, crlf")]
    public void ConfigLoader_Parse_InvalidEnumValue_ListsAllowedValues(string setting, string token, string allowed)
    {
        // Act: parse the setting with an invalid token
        var error = OneError($"{setting}: {token}\n");

        // Assert: positioned at the value, naming the setting and listing the allowed tokens in order
        AssertError(error, 1, setting.Length + 3, $"Invalid value '{token}' for setting '{setting}'; expected one of: {allowed}");
    }

    /// <summary>
    ///     Test that setting names and tokens are case-sensitive.
    /// </summary>
    [Theory]
    [InlineData("Line-Length: 100\n", "Unknown setting 'Line-Length'")]
    [InlineData("wrap: Fill\n", "Invalid value 'Fill'")]
    [InlineData("end-of-line: LF\n", "Invalid value 'LF'")]
    [InlineData("end-of-line: Crlf\n", "Invalid value 'Crlf'")]
    public void ConfigLoader_Parse_WrongCase_Reported(string yaml, string message)
    {
        // Act: parse a wrong-case spelling
        var error = OneError(yaml);

        // Assert: the spelling is rejected
        Assert.Contains(message, error.Message);
    }

    /// <summary>
    ///     Test that the whole range of line length values and the list indent values are accepted.
    /// </summary>
    [Theory]
    [InlineData("line-length: 0\n", 0)]
    [InlineData("line-length: 1\n", 1)]
    [InlineData("line-length: 100000\n", 100000)]
    public void ConfigLoader_Parse_LineLengthInRange_Accepted(string yaml, int expected)
    {
        // Act: parse the value
        var config = Valid(yaml);

        // Assert: it is stored, zero meaning unlimited
        Assert.Equal(expected, config.LineLength);
    }

    /// <summary>
    ///     Test that a list indent of two or four is accepted.
    /// </summary>
    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public void ConfigLoader_Parse_ListIndentTwoAndFour_Accepted(int indent)
    {
        // Act: parse the value
        var config = Valid($"list-indent: {indent}\n");

        // Assert: it is stored
        Assert.Equal(indent, config.ListIndent);
    }

    /// <summary>
    ///     Test that a list indent other than two or four is reported.
    /// </summary>
    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("3")]
    [InlineData("8")]
    [InlineData("\"2\"")]
    [InlineData("two")]
    public void ConfigLoader_Parse_ListIndentNotTwoOrFour_Reported(string value)
    {
        // Act: parse the invalid value
        var error = OneError($"list-indent: {value}\n");

        // Assert: the value is rejected at its position
        AssertError(error, 1, 14, "list-indent");
    }

    /// <summary>
    ///     Test that numbers that are not plain decimal whole numbers are reported.
    /// </summary>
    [Theory]
    [InlineData("abc")]
    [InlineData("\"120\"")]
    [InlineData("'120'")]
    [InlineData("1.5")]
    [InlineData("0x10")]
    [InlineData("+5")]
    [InlineData("1_000")]
    [InlineData("007")]
    [InlineData("1e3")]
    [InlineData("true")]
    public void ConfigLoader_Parse_IntegerNotPlainDecimal_Reported(string value)
    {
        // Act: parse the invalid number
        var error = OneError($"line-length: {value}\n");

        // Assert: reported at the value with the expected form
        AssertError(error, 1, 14, "expected a whole number without quotes from 0 to 100000");
    }

    /// <summary>
    ///     Test that numbers outside the allowed range, including beyond the integer range, are reported.
    /// </summary>
    [Theory]
    [InlineData("-1")]
    [InlineData("100001")]
    [InlineData("2147483648")]
    [InlineData("99999999999999999999")]
    public void ConfigLoader_Parse_LineLengthOutOfRange_Reported(string value)
    {
        // Act: parse the out-of-range number
        var error = OneError($"line-length: {value}\n");

        // Assert: reported with the range
        AssertError(error, 1, 14, "from 0 to 100000");
    }

    /// <summary>
    ///     Test that the maximum blank lines setting rejects zero.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_MaxBlankLinesZero_Reported()
    {
        // Act: parse zero
        var error = OneError("max-blank-lines: 0\n");

        // Assert: the range starts at one
        AssertError(error, 1, 18, "from 1 to 100000");
    }

    /// <summary>
    ///     Test that only plain true and false are accepted as booleans.
    /// </summary>
    [Theory]
    [InlineData("yes")]
    [InlineData("no")]
    [InlineData("on")]
    [InlineData("off")]
    [InlineData("True")]
    [InlineData("FALSE")]
    [InlineData("\"true\"")]
    [InlineData("'false'")]
    [InlineData("1")]
    public void ConfigLoader_Parse_BooleanYesNoOnQuoted_Reported(string value)
    {
        // Act: parse the invalid boolean
        var error = OneError($"final-newline: {value}\n");

        // Assert: reported at the value
        AssertError(error, 1, 16, "expected true or false without quotes");
    }

    /// <summary>
    ///     Test that true and false are accepted for boolean settings and for rule toggles.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_BooleanSettings_TrueAndFalse_Accepted()
    {
        // Act: parse both spellings in different places
        var config = Valid("final-newline: false\nlink-refactor:\n  in-tables: false\n  break-link-text: true\nrules:\n  link-empty: false\n  descriptive-link-text: true\n");

        // Assert: each value is applied
        Assert.False(config.FinalNewline);
        Assert.False(config.LinkRefactor.InTables);
        Assert.True(config.LinkRefactor.BreakLinkText);
        Assert.False(config.Rules["link-empty"]);
        Assert.True(config.Rules["descriptive-link-text"]);
    }

    /// <summary>
    ///     Test that a null spelled in any YAML way is an error.
    /// </summary>
    [Theory]
    [InlineData("wrap:\n")]
    [InlineData("wrap: ~\n")]
    [InlineData("wrap: null\n")]
    [InlineData("wrap: Null\n")]
    [InlineData("wrap: NULL\n")]
    public void ConfigLoader_Parse_NullValue_EmptyTildeAndNull_Reported(string yaml)
    {
        // Act: parse the null value
        var error = OneError(yaml);

        // Assert: reported at the key, not silently reverted to the default
        AssertError(error, 1, 1, "Setting 'wrap' must not be empty");
    }

    /// <summary>
    ///     Test that an empty rules, link-refactor or ignore setting is an error.
    /// </summary>
    [Theory]
    [InlineData("rules:\n", "rules")]
    [InlineData("link-refactor:\n", "link-refactor")]
    [InlineData("ignore:\n", "ignore")]
    [InlineData("rules: ~\n", "rules")]
    [InlineData("ignore: null\n", "ignore")]
    [InlineData("line-length:\n", "line-length")]
    public void ConfigLoader_Parse_EmptyRulesLinkRefactorIgnore_Reported(string yaml, string setting)
    {
        // Act: parse the empty setting
        var error = OneError(yaml);

        // Assert: reported at the key
        AssertError(error, 1, 1, $"Setting '{setting}' must not be empty");
    }

    /// <summary>
    ///     Test that explicit empty collections are valid.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_ExplicitEmptyCollections_Accepted()
    {
        // Act: parse an empty list and empty mappings
        var config = Valid("ignore: []\nrules: {}\nlink-refactor: {}\n");

        // Assert: nothing is ignored, and rules and link-refactor keep their defaults
        Assert.Empty(config.Ignore);
        Assert.Equal(LintMarkConfig.Default.Rules, config.Rules);
        Assert.Equal(LinkRefactorOptions.Disabled, config.LinkRefactor);
    }

    /// <summary>
    ///     Test that a mapping or list where a single value is expected is reported.
    /// </summary>
    [Theory]
    [InlineData("wrap: {a: 1}\n", "Setting 'wrap' must be a single value")]
    [InlineData("wrap: [fill]\n", "Setting 'wrap' must be a single value")]
    [InlineData("line-length:\n  - 1\n", "Setting 'line-length' must be a single value")]
    [InlineData("final-newline:\n  a: b\n", "Setting 'final-newline' must be a single value")]
    public void ConfigLoader_Parse_MappingOrSequenceForScalar_Reported(string yaml, string message)
    {
        // Act: parse the collection value
        var error = OneError(yaml);

        // Assert: reported at the collection, not at the key
        Assert.Contains(message, error.Message);
        Assert.True(error.Column > 1);
    }

    /// <summary>
    ///     Test that a scalar where a collection is expected is reported.
    /// </summary>
    [Theory]
    [InlineData("rules: true\n", "Setting 'rules' must be a mapping of rule names to true or false")]
    [InlineData("rules: [a]\n", "Setting 'rules' must be a mapping")]
    [InlineData("link-refactor: yes\n", "Setting 'link-refactor' must be a mapping of options")]
    [InlineData("ignore: docs/**\n", "Setting 'ignore' must be a list of patterns")]
    [InlineData("ignore: {a: b}\n", "Setting 'ignore' must be a list of patterns")]
    public void ConfigLoader_Parse_ScalarForMappingOrSequence_Reported(string yaml, string message)
    {
        // Act: parse the wrong shape
        var error = OneError(yaml);

        // Assert: reported at the first character of the value, after the colon and the space
        Assert.Contains(message, error.Message);
        Assert.Equal((1, yaml.IndexOf(": ", StringComparison.Ordinal) + 3), (error.Line, error.Column));
    }

    /// <summary>
    ///     Test that an unknown setting is reported at its key.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_UnknownKey_ReportsAtKey()
    {
        // Act: parse a file with an unknown setting on the second line
        var error = OneError("line-length: 80\n  \nunknown-setting: 1\n");

        // Assert: reported at the key position
        AssertError(error, 3, 1, "Unknown setting 'unknown-setting'");
    }

    /// <summary>
    ///     Test that several unknown settings are all reported in position order.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_SeveralUnknownKeys_AllReportedInPositionOrder()
    {
        // Act: parse a file with three unknown settings, one with a wrong value too
        var result = Parse("zeta: 1\nline-length: 80\nalpha: 2\nwrap: nope\nbeta: 3\n");

        // Assert: every problem is reported, ordered by position and not by name
        Assert.Equal([1, 3, 4, 5], result.Errors.Select(e => e.Line));
        Assert.Contains("'zeta'", result.Errors[0].Message);
        Assert.Contains("'alpha'", result.Errors[1].Message);
        Assert.Contains("Invalid value 'nope'", result.Errors[2].Message);
        Assert.Contains("'beta'", result.Errors[3].Message);
    }

    /// <summary>
    ///     Test that a duplicate setting is reported at its second occurrence and names the first line.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_DuplicateKey_ReportsAtSecondOccurrence()
    {
        // Act: parse a file that sets one setting twice
        var error = OneError("line-length: 80\nwrap: fill\nline-length: 90\n");

        // Assert: reported at the later key, naming the first line
        AssertError(error, 3, 1, "Duplicate setting 'line-length' (first defined on line 1)");
    }

    /// <summary>
    ///     Test that a duplicate nested setting is reported with its dotted name.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_DuplicateSubKey_Reported()
    {
        // Act: parse a nested setting twice
        var error = OneError("link-refactor:\n  threshold: 50\n  threshold: 60\n");

        // Assert: reported at the second occurrence
        AssertError(error, 3, 3, "Duplicate setting 'link-refactor.threshold' (first defined on line 2)");
    }

    /// <summary>
    ///     Test that a duplicate key is reported even when the first occurrence is invalid.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_DuplicateKeyWithInvalidFirstValue_ReportsBoth()
    {
        // Act: parse a duplicate whose first value is invalid
        var result = Parse("wrap: nope\nwrap: fill\n");

        // Assert: the invalid value and the duplicate are both reported
        Assert.Equal(2, result.Errors.Length);
        Assert.Contains("Invalid value 'nope'", result.Errors[0].Message);
        Assert.Contains("Duplicate setting 'wrap'", result.Errors[1].Message);
    }

    /// <summary>
    ///     Test that the settings of the link-refactor mapping are applied and that omitted ones keep their defaults.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_LinkRefactorPartial_KeepsDefaults()
    {
        // Act: parse a mapping that sets only one option
        var config = Valid("link-refactor:\n  enabled: true\n");

        // Assert: only that option changed
        Assert.Equal(LinkRefactorOptions.Disabled with { Enabled = true }, config.LinkRefactor);
    }

    /// <summary>
    ///     Test that an unknown link-refactor option is reported with its dotted name.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_LinkRefactorUnknownSubKey_ReportedWithDottedName()
    {
        // Act: parse an unknown option
        var error = OneError("link-refactor:\n  color: red\n");

        // Assert: reported at the nested key
        AssertError(error, 2, 3, "Unknown setting 'link-refactor.color'");
    }

    /// <summary>
    ///     Test that link-refactor numbers outside their range are reported with their dotted names.
    /// </summary>
    [Theory]
    [InlineData("threshold: 0", "link-refactor.threshold")]
    [InlineData("threshold: -4", "link-refactor.threshold")]
    [InlineData("label-max-length: 0", "link-refactor.label-max-length")]
    [InlineData("label-max-length: -1", "link-refactor.label-max-length")]
    [InlineData("threshold: 100001", "link-refactor.threshold")]
    public void ConfigLoader_Parse_LinkRefactorNumberOutOfRange_Reported(string line, string setting)
    {
        // Act: parse the out-of-range option
        var error = OneError($"link-refactor:\n  {line}\n");

        // Assert: reported at the value on the nested line
        Assert.Equal(2, error.Line);
        Assert.Contains($"for setting '{setting}'", error.Message);
        Assert.Contains("from 1 to 100000", error.Message);
    }

    /// <summary>
    ///     Test that a non-boolean link-refactor flag is reported.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_LinkRefactorEnabledNotBoolean_Reported()
    {
        // Act: parse a non-boolean flag
        var error = OneError("link-refactor:\n  enabled: yes\n");

        // Assert: reported at the value
        AssertError(error, 2, 12, "Invalid value 'yes' for setting 'link-refactor.enabled'; expected true or false");
    }

    /// <summary>
    ///     Test that a definitions marker that is not an HTML comment is reported.
    /// </summary>
    [Theory]
    [InlineData("\"Links\"")]
    [InlineData("\"<!-- open\"")]
    [InlineData("\"<!--->\"")]
    [InlineData("\"close -->\"")]
    public void ConfigLoader_Parse_LinkRefactorMarkerNotComment_Reported(string value)
    {
        // Act: parse the invalid marker
        var error = OneError($"link-refactor:\n  definitions-marker: {value}\n");

        // Assert: reported at the value
        Assert.Equal(2, error.Line);
        Assert.Contains("expected a single-line HTML comment", error.Message);
    }

    /// <summary>
    ///     Test that a definitions marker that spans lines is reported.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_LinkRefactorMarkerMultiline_Reported()
    {
        // Act: parse a marker with an escaped line break
        var error = OneError("link-refactor:\n  definitions-marker: \"<!-- a\\nb -->\"\n");

        // Assert: reported
        Assert.Contains("expected a single-line HTML comment", error.Message);
    }

    /// <summary>
    ///     Test that a rule given by its kebab-case id is accepted.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_RulesKebabId_Accepted()
    {
        // Act: switch a rule off by id
        var config = Valid("rules:\n  heading-duplicate: false\n");

        // Assert: only that rule changed
        Assert.False(config.Rules["heading-duplicate"]);
        Assert.Equal(LintMarkConfig.Default.Rules.SetItem("heading-duplicate", false), config.Rules);
    }

    /// <summary>
    ///     Test that a rule given by an alias is stored under its canonical id.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_RulesAlias_StoredAsCanonicalId()
    {
        // Act: switch a rule off by alias
        var config = Valid("rules:\n  MD013: false\n");

        // Assert: stored under the canonical id, and no alias key appears
        Assert.False(config.Rules["line-length"]);
        Assert.False(config.Rules.ContainsKey("MD013"));
        Assert.Equal(22, config.Rules.Count);
    }

    /// <summary>
    ///     Test that rule names are matched without regard to letter case.
    /// </summary>
    [Theory]
    [InlineData("md013")]
    [InlineData("Md013")]
    [InlineData("Line-Length")]
    [InlineData("LINE-LENGTH")]
    public void ConfigLoader_Parse_RulesAnyLetterCase_Accepted(string name)
    {
        // Act: switch the rule off using the spelling
        var config = Valid($"rules:\n  {name}: false\n");

        // Assert: the canonical rule changed
        Assert.False(config.Rules["line-length"]);
    }

    /// <summary>
    ///     Test that an unknown rule is reported at its key.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_RulesUnknownRule_ReportedAtKey()
    {
        // Act: parse an unknown rule name
        var error = OneError("rules:\n  no-such-rule: true\n");

        // Assert: reported at the key
        AssertError(error, 2, 3, "Unknown rule 'no-such-rule'");
    }

    /// <summary>
    ///     Test that the alias of a rule the formatter always applies is unknown.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_RulesFormatterAlias_Md009_ReportedUnknown()
    {
        // Act: try to toggle a formatter-owned rule
        var error = OneError("rules:\n  MD009: false\n");

        // Assert: it is not a rule that can be switched on or off
        AssertError(error, 2, 3, "Unknown rule 'MD009'");
    }

    /// <summary>
    ///     Test that a rule value that is not a boolean is reported.
    /// </summary>
    [Theory]
    [InlineData("yes")]
    [InlineData("\"true\"")]
    [InlineData("1")]
    [InlineData("")]
    public void ConfigLoader_Parse_RulesValueNotBoolean_Reported(string value)
    {
        // Act: parse an invalid rule value
        var error = OneError($"rules:\n  line-length: {value}\n");

        // Assert: reported against the dotted rule name
        Assert.Equal(2, error.Line);
        Assert.Contains("rules.line-length", error.Message);
    }

    /// <summary>
    ///     Test that the alias of a dropped rule is accepted and ignored.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_RulesDroppedAlias_AcceptedAndNotStored()
    {
        // Act: configure dropped rules
        var config = Valid("rules:\n  MD025: false\n  md033: true\n  MD041: false\n");

        // Assert: nothing changed
        Assert.Equal(LintMarkConfig.Default.Rules, config.Rules);
    }

    /// <summary>
    ///     Test that a dropped alias still takes part in duplicate detection and its value is still validated.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_RulesDroppedAliasRepeatedOrInvalid_Reported()
    {
        // Act: repeat a dropped alias in another case, and give another one an invalid value
        var result = Parse("rules:\n  MD025: false\n  md025: true\n  MD033: maybe\n");

        // Assert: both problems are reported
        Assert.Equal([3, 4], result.Errors.Select(e => e.Line));
        Assert.Contains("already configured as 'MD025' on line 2", result.Errors[0].Message);
        Assert.Contains("rules.MD033", result.Errors[1].Message);
    }

    /// <summary>
    ///     Test that an alias and the id of the same rule conflict at the later occurrence.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_RulesAliasAndKebabConflict_ReportedAtLaterOccurrence()
    {
        // Act: configure one rule by alias and by id
        var error = OneError("rules:\n  MD013: false\n  line-length: true\n");

        // Assert: reported at the second entry, naming the first
        AssertError(error, 3, 3, "Rule 'line-length' is already configured as 'MD013' on line 2");
    }

    /// <summary>
    ///     Test that the conflict is reported at the alias when the id comes first.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_RulesKebabThenAlias_ConflictReportedAtAlias()
    {
        // Act: configure one rule by id, then by alias
        var error = OneError("rules:\n  line-length: true\n  MD013: false\n");

        // Assert: reported at the later entry
        AssertError(error, 3, 3, "Rule 'MD013' is already configured as 'line-length' on line 2");
    }

    /// <summary>
    ///     Test that the conflict is reported at the id when the alias comes first.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_RulesAliasThenKebab_ConflictReportedAtKebab()
    {
        // Act: configure one rule by alias, then by id
        var error = OneError("rules:\n  MD013: true\n  line-length: false\n");

        // Assert: reported at the later entry, so the outcome follows the file and not any processing order
        AssertError(error, 3, 3, "Rule 'line-length' is already configured as 'MD013' on line 2");
    }

    /// <summary>
    ///     Test that two spellings that differ only in letter case conflict.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_RulesDifferentCaseConflict_Reported()
    {
        // Act: configure one alias in two cases
        var error = OneError("rules:\n  MD013: false\n  md013: false\n");

        // Assert: reported at the second spelling
        AssertError(error, 3, 3, "Rule 'md013' is already configured as 'MD013' on line 2");
    }

    /// <summary>
    ///     Test that reordering the entries of the rules mapping does not change the result.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_RulesReorderedKeys_ProduceEqualRules()
    {
        // Act: parse the same toggles in opposite orders
        var first = Valid("rules:\n  link-empty: false\n  MD001: false\n  code-fence-language: false\n");
        var second = Valid("rules:\n  code-fence-language: false\n  MD001: false\n  link-empty: false\n");

        // Assert: equal rules
        Assert.Equal(first.Rules, second.Rules);
    }

    /// <summary>
    ///     Test that rules that are not mentioned keep their defaults.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_RulesPartial_OthersKeepDefaults()
    {
        // Act: enable one rule that is off by default and disable one that is on
        var config = Valid("rules:\n  descriptive-link-text: true\n  link-empty: false\n");

        // Assert: exactly those two differ from the defaults
        var differing = config.Rules.Where(r => LintMarkConfig.Default.Rules[r.Key] != r.Value).Select(r => r.Key);
        Assert.Equal(["descriptive-link-text", "link-empty"], differing);
    }

    /// <summary>
    ///     Test that an ignore list is accepted and replaces the defaults.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_IgnoreReplacesDefaults()
    {
        // Act: parse an ignore list
        var config = Valid("ignore:\n  - build/**\n");

        // Assert: only the given pattern remains
        Assert.Equal(["build/**"], config.Ignore);
    }

    /// <summary>
    ///     Test that an empty ignore list clears the default patterns.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_IgnoreEmptyList_ClearsDefaults()
    {
        // Act: parse an empty list
        var config = Valid("ignore: []\n");

        // Assert: nothing is ignored
        Assert.Empty(config.Ignore);
    }

    /// <summary>
    ///     Test that backslashes in ignore patterns are normalized to forward slashes.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_IgnoreBackslashesNormalized()
    {
        // Act: parse patterns written with Windows separators
        var config = Valid("ignore:\n  - 'docs\\generated\\**'\n");

        // Assert: forward slashes are stored
        Assert.Equal(["docs/generated/**"], config.Ignore);
    }

    /// <summary>
    ///     Test that exact duplicate patterns are removed keeping the first, and the order is preserved.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_IgnoreExactDuplicates_RemovedKeepingFirstAndOrder()
    {
        // Act: parse patterns with a duplicate, including one that is a duplicate only after normalization
        var config = Valid("ignore:\n  - b/**\n  - a/**\n  - b/**\n  - 'a\\**'\n  - c/**\n");

        // Assert: first occurrences remain in their original order
        Assert.Equal(["b/**", "a/**", "c/**"], config.Ignore);
    }

    /// <summary>
    ///     Test that patterns that differ only in letter case are different patterns.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_IgnoreCaseVariants_AreDistinct()
    {
        // Act: parse two spellings that differ in case
        var config = Valid("ignore:\n  - Docs/**\n  - docs/**\n");

        // Assert: both are kept
        Assert.Equal(["Docs/**", "docs/**"], config.Ignore);
    }

    /// <summary>
    ///     Test that empty, blank, null, nested and multi-line patterns are reported.
    /// </summary>
    [Theory]
    [InlineData("  - ''\n", "must not be empty or blank")]
    [InlineData("  - '   '\n", "must not be empty or blank")]
    [InlineData("  - ~\n", "must not be empty or blank")]
    [InlineData("  -\n", "must not be empty or blank")]
    [InlineData("  - [a, b]\n", "Ignore patterns must be text")]
    [InlineData("  - {a: b}\n", "Ignore patterns must be text")]
    [InlineData("  - \"a\\nb\"\n", "must be on a single line")]
    public void ConfigLoader_Parse_IgnoreInvalidElement_Reported(string item, string message)
    {
        // Act: parse a list with one valid and one invalid element
        var result = Parse($"ignore:\n  - ok/**\n{item}");

        // Assert: only the invalid element is reported, at its line
        var error = Assert.Single(result.Errors);
        Assert.Equal(3, error.Line);
        Assert.Contains(message, error.Message);
        Assert.Null(result.Config);
    }

    /// <summary>
    ///     Test that an unclosed flow sequence is reported as invalid YAML even though the parser fails with an unusual exception.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_MalformedYamlUnclosedFlowSequence_ReportsInvalidYaml()
    {
        // Act: parse an unclosed flow sequence
        var result = Parse("line-length: 80\nignore: [a, b\nwrap: fill\n");

        // Assert: one positioned syntax error and no configuration
        Assert.Null(result.Config);
        var error = Assert.Single(result.Errors);
        Assert.StartsWith("Invalid YAML", error.Message);
        Assert.True(error.Line >= 1 && error.Column >= 1);
    }

    /// <summary>
    ///     Test that tab indentation is reported as invalid YAML.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_MalformedYamlTabIndentation_ReportsInvalidYaml()
    {
        // Act: parse a nested mapping indented with a tab
        var result = Parse("rules:\n\tline-length: false\n");

        // Assert: one syntax error
        var error = Assert.Single(result.Errors);
        Assert.StartsWith("Invalid YAML", error.Message);
        Assert.Null(result.Config);
    }

    /// <summary>
    ///     Test that an unterminated quoted string is reported as invalid YAML at or after its start.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_MalformedYamlUnterminatedQuote_ReportsInvalidYaml()
    {
        // Act: parse a quote that is never closed
        var error = OneError("line-length: 80\nwrap: \"fill\n");

        // Assert: reported as invalid YAML on or after the line of the quote
        Assert.StartsWith("Invalid YAML", error.Message);
        Assert.True(error.Line >= 2);
    }

    /// <summary>
    ///     Test that syntax errors never carry a position before line one, column one.
    /// </summary>
    [Theory]
    [InlineData("{")]
    [InlineData("[")]
    [InlineData("a: [")]
    [InlineData("a: {b")]
    [InlineData("a: 'x")]
    [InlineData(": :")]
    [InlineData("a: b: c")]
    [InlineData("\t")]
    [InlineData("%YAML 1.2\n")]
    [InlineData("? [a\n")]
    public void ConfigLoader_Parse_MalformedYaml_PositionIsAtLeastLineOneColumnOne(string yaml)
    {
        // Act: parse malformed content
        var result = Parse(yaml);

        // Assert: it never throws, and any error has a valid position
        Assert.All(result.Errors, e => Assert.True(e.Line >= 1 && e.Column >= 1));
        Assert.True((result.Config is null) == !result.Errors.IsEmpty);
    }

    /// <summary>
    ///     Test that a root that is a list or a scalar is reported.
    /// </summary>
    [Theory]
    [InlineData("- a\n- b\n")]
    [InlineData("just text\n")]
    [InlineData("42\n")]
    [InlineData("[a, b]\n")]
    public void ConfigLoader_Parse_RootSequenceOrScalar_Reported(string yaml)
    {
        // Act: parse a root that is not a mapping
        var error = OneError(yaml);

        // Assert: reported at the start of the root
        AssertError(error, 1, 1, "The configuration must be a mapping of settings");
    }

    /// <summary>
    ///     Test that a second document is reported at its marker.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_MultipleDocuments_ReportedAtSecondDocument()
    {
        // Act: parse two documents
        var error = OneError("line-length: 80\n---\nwrap: fill\n");

        // Assert: reported at the second document marker
        AssertError(error, 2, 1, "Only one YAML document is allowed");
    }

    /// <summary>
    ///     Test that anchors, aliases and tags are rejected.
    /// </summary>
    [Theory]
    [InlineData("line-length: &x 80\n", 1, 14)]
    [InlineData("a: &x 1\nline-length: *x\n", 1, 4)]
    [InlineData("wrap: !!str fill\n", 1, 7)]
    [InlineData("rules: !!map {}\n", 1, 8)]
    [InlineData("ignore: &list [a]\n", 1, 9)]
    public void ConfigLoader_Parse_AnchorAliasOrTag_Reported(string yaml, int line, int column)
    {
        // Act: parse content using the unsupported construct
        var result = Parse(yaml);

        // Assert: the construct is reported at its position
        Assert.Null(result.Config);
        var error = result.Errors.First(e => e.Message.Contains("not supported", StringComparison.Ordinal));
        AssertError(error, line, column, "Anchors, aliases and tags are not supported");
    }

    /// <summary>
    ///     Test that a complex key is reported.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_ComplexKey_Reported()
    {
        // Act: parse a key that is a list
        var result = Parse("? [a, b]\n: 1\n");

        // Assert: reported at the key
        var error = Assert.Single(result.Errors);
        AssertError(error, 1, 3, "Complex keys are not supported");
    }

    /// <summary>
    ///     Test that deeply nested content inside an unknown setting is reported once, without a crash.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_DeeplyNestedUnknownValue_ReportsOneErrorWithoutCrash()
    {
        // Arrange: 50 levels of nesting (below the limit) inside an unknown setting, using block style
        var yaml = "deep:\n" + string.Concat(Enumerable.Range(1, 50).Select(i => new string(' ', i * 2) + "k:\n")) + new string(' ', 102) + "v: 1\n";

        // Act: parse it
        var result = Parse(yaml);

        // Assert: only the unknown top-level setting is reported
        var error = Assert.Single(result.Errors);
        AssertError(error, 1, 1, "Unknown setting 'deep'");
    }

    /// <summary>
    ///     Test that flow nesting beyond the limit is reported at the offending bracket instead of being parsed.
    /// </summary>
    [Theory]
    [InlineData('[', ']')]
    [InlineData('{', '}')]
    public void ConfigLoader_Parse_NestingBeyondLimit_ReportsPositionOfOffendingBracket(char open, char close)
    {
        // Arrange: 65 nested brackets after a prefix
        var depth = ConfigParser.MaxNestingDepth + 1;
        var yaml = "a: " + new string(open, depth) + new string(close, depth) + "\n";

        // Act: parse it
        var error = OneError(yaml);

        // Assert: reported at the first bracket beyond the limit (column 3 + 64 + 1)
        AssertError(error, 1, 3 + ConfigParser.MaxNestingDepth + 1, "Nesting is too deep (more than 64 levels)");
    }

    /// <summary>
    ///     Test that flow nesting exactly at the limit is still parsed.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_NestingAtLimit_IsParsed()
    {
        // Arrange: exactly 64 nested sequences inside an unknown setting
        var depth = ConfigParser.MaxNestingDepth;
        var yaml = "a: " + new string('[', depth) + new string(']', depth) + "\n";

        // Act: parse it
        var error = OneError(yaml);

        // Assert: the nesting is accepted, so only the unknown setting is reported
        AssertError(error, 1, 1, "Unknown setting 'a'");
    }

    /// <summary>
    ///     Test that invalid UTF-8 is reported at the offending position.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_InvalidUtf8_ReportedAtOffendingPosition()
    {
        // Arrange: an invalid byte on the second line, after two characters
        byte[] content = [.. "wrap: fill\nab"u8, 0xFF, .. "\n"u8];

        // Act: parse it
        var result = ConfigLoader.Parse(ConfigPath, content);

        // Assert: reported at 2:3
        var error = Assert.Single(result.Errors);
        AssertError(error, 2, 3, "Invalid UTF-8 byte sequence");
        Assert.Null(result.Config);
    }

    /// <summary>
    ///     Test that a UTF-16 or UTF-32 byte order mark is rejected.
    /// </summary>
    [Theory]
    [InlineData(new byte[] { 0xFF, 0xFE, 0x61, 0x00 })]
    [InlineData(new byte[] { 0xFE, 0xFF, 0x00, 0x61 })]
    [InlineData(new byte[] { 0x00, 0x00, 0xFE, 0xFF })]
    public void ConfigLoader_Parse_Utf16Bom_Rejected(byte[] content)
    {
        // Act: parse content that starts with a non-UTF-8 byte order mark
        var result = ConfigLoader.Parse(ConfigPath, content);

        // Assert: reported at the start of the file
        var error = Assert.Single(result.Errors);
        AssertError(error, 1, 1, "Unsupported byte order mark (only UTF-8 is supported)");
    }

    /// <summary>
    ///     Test that the position of an invalid byte does not count the byte order mark.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_Utf8BomThenInvalidUtf8_PositionAccountsForBomStrip()
    {
        // Arrange: a UTF-8 byte order mark followed by an invalid byte at the very start
        byte[] content = [0xEF, 0xBB, 0xBF, 0xFF];

        // Act: parse it
        var result = ConfigLoader.Parse(ConfigPath, content);

        // Assert: the invalid byte is at column 1, as an editor would show it
        AssertError(Assert.Single(result.Errors), 1, 1, "Invalid UTF-8 byte sequence");
    }

    /// <summary>
    ///     Test that content larger than the limit is rejected.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_TooLarge_ReportsSizeError()
    {
        // Arrange: one byte more than the limit
        var content = new byte[ConfigLoader.MaxFileBytes + 1];
        Array.Fill(content, (byte)'#');

        // Act: parse it
        var result = ConfigLoader.Parse(ConfigPath, content);

        // Assert: reported at the start
        AssertError(Assert.Single(result.Errors), 1, 1, "larger than 1 MiB");
    }

    /// <summary>
    ///     Test that content of exactly the limit is accepted.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_AtSizeLimit_Accepted()
    {
        // Arrange: a comment of exactly the limit
        var content = new byte[ConfigLoader.MaxFileBytes];
        Array.Fill(content, (byte)'#');

        // Act: parse it
        var result = ConfigLoader.Parse(ConfigPath, content);

        // Assert: it is a valid, empty configuration
        Assert.Empty(result.Errors);
    }

    /// <summary>
    ///     Test that errors are sorted by position and then by message.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_SeveralErrors_SortedByPositionThenMessage()
    {
        // Act: parse a file whose errors arise in a different order than their positions
        var result = Parse("rules:\n  MD013: false\n  line-length: true\nwrap: nope\nignore:\n  - ''\n  - 'ok'\nzz: 1\n");

        // Assert: the errors are in position order
        var positions = result.Errors.Select(e => (e.Line, e.Column)).ToList();
        Assert.Equal(positions.OrderBy(p => p.Line).ThenBy(p => p.Column), positions);
        Assert.Equal(4, positions.Count);
    }

    /// <summary>
    ///     Test that parsing the same content repeatedly gives the same errors.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_Errors_AreDeterministicAcrossRepeatedCalls()
    {
        // Arrange: content with several errors
        const string yaml = "b: 1\na: 2\nwrap: x\nrules: {MD013: false, line-length: true}\n";

        // Act: parse it repeatedly
        var first = Parse(yaml).Errors.Select(e => $"{e.Line}:{e.Column}:{e.Message}").ToList();
        var results = Enumerable.Range(0, 20).Select(_ => Parse(yaml).Errors.Select(e => $"{e.Line}:{e.Column}:{e.Message}").ToList());

        // Assert: every run is identical
        Assert.All(results, r => Assert.Equal(first, r));
    }

    /// <summary>
    ///     Test that every error carries the path it was given.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_Errors_CarryTheGivenPath()
    {
        // Act: parse invalid content under another path
        var result = ConfigLoader.Parse("other/place.yaml", "x: 1\ny: 2\n"u8);

        // Assert: every error and the result name that path
        Assert.All(result.Errors, e => Assert.Equal("other/place.yaml", e.Path));
        Assert.Equal("other/place.yaml", result.SourcePath);
    }

    /// <summary>
    ///     Test that the errors of a result cannot be modified through a cast.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_ErrorsCastToMutableTypes_CannotBeModified()
    {
        // Arrange: a result with errors
        var result = Parse("x: 1\n");

        // Act & Assert: the list refuses changes made through the mutable interfaces
        ReadOnlyAssertions.AssertCannotBeModified((IReadOnlyList<ConfigError>)result.Errors);
    }

    /// <summary>
    ///     Test that a null or empty path is rejected.
    /// </summary>
    [Fact]
    public void ConfigLoader_Parse_NullOrEmptyPath_Throws()
    {
        // Act & Assert: the path is validated
        Assert.Throws<ArgumentNullException>(() => ConfigLoader.Parse(null!, []));
        Assert.Throws<ArgumentException>(() => ConfigLoader.Parse(string.Empty, []));
    }

    /// <summary>
    ///     Test that a loader result with errors and a configuration, or neither, is rejected.
    /// </summary>
    [Fact]
    public void ConfigLoadResult_InconsistentState_Throws()
    {
        // Arrange: one error and an empty list
        var error = new ConfigError("a.yaml", 1, 1, "problem");

        // Act & Assert: both outcomes at once, or neither, are rejected; so are bad inputs
        Assert.Throws<ArgumentException>(() => new ConfigLoadResult(LintMarkConfig.Default, null, [error]));
        Assert.Throws<ArgumentException>(() => new ConfigLoadResult(null, null, []));
        Assert.Throws<ArgumentException>(() => new ConfigLoadResult(LintMarkConfig.Default, null, default));
        Assert.Throws<ArgumentException>(() => new ConfigLoadResult(LintMarkConfig.Default, string.Empty, []));
    }

    /// <summary>
    ///     Test that an error rejects an invalid path, position or message, and flattens line breaks.
    /// </summary>
    [Fact]
    public void ConfigError_InvalidArguments_Throw()
    {
        // Act & Assert: every argument is validated
        Assert.Throws<ArgumentNullException>(() => new ConfigError(null!, 1, 1, "m"));
        Assert.Throws<ArgumentException>(() => new ConfigError(string.Empty, 1, 1, "m"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ConfigError("a", 0, 1, "m"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ConfigError("a", 1, 0, "m"));
        Assert.Throws<ArgumentNullException>(() => new ConfigError("a", 1, 1, null!));
        Assert.Throws<ArgumentException>(() => new ConfigError("a", 1, 1, "  "));
        Assert.Equal("one two", new ConfigError("a", 1, 1, "one\ntwo").Message);
    }
}
