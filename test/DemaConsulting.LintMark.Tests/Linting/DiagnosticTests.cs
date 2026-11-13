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

using DemaConsulting.LintMark.Linting;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Unit tests for the Diagnostic record and its DiagnosticKind.
/// </summary>
public class DiagnosticTests
{
    /// <summary>
    ///     Creates a valid diagnostic, replacing only the arguments a test wants to vary.
    /// </summary>
    /// <param name="ruleId">The rule identifier.</param>
    /// <param name="kind">The kind.</param>
    /// <param name="path">The path.</param>
    /// <param name="line">The line.</param>
    /// <param name="column">The column.</param>
    /// <param name="message">The message.</param>
    /// <returns>The diagnostic.</returns>
    private static Diagnostic Create(
        string ruleId = "line-length",
        DiagnosticKind kind = DiagnosticKind.Formatting,
        string path = "docs/a.md",
        int line = 3,
        int column = 7,
        string message = "A line is too long.") =>
        new(ruleId, kind, path, line, column, message);

    /// <summary>
    ///     Test that the constructor stores every argument.
    /// </summary>
    [Fact]
    public void Diagnostic_Constructor_AllArguments_ExposesValues()
    {
        // Act: create a diagnostic
        var diagnostic = new Diagnostic("link-empty", DiagnosticKind.LintOnly, "docs/a.md", 4, 9, "A link has no destination.");

        // Assert: every value is exposed unchanged
        Assert.Equal("link-empty", diagnostic.RuleId);
        Assert.Equal(DiagnosticKind.LintOnly, diagnostic.Kind);
        Assert.Equal("docs/a.md", diagnostic.Path);
        Assert.Equal(4, diagnostic.Line);
        Assert.Equal(9, diagnostic.Column);
        Assert.Equal("A link has no destination.", diagnostic.Message);
    }

    /// <summary>
    ///     Test that kebab-case identifiers with digits and single hyphens are accepted.
    /// </summary>
    [Theory]
    [InlineData("a")]
    [InlineData("line-length")]
    [InlineData("a1")]
    [InlineData("rule-2a-b3")]
    public void Diagnostic_Constructor_ValidRuleId_Accepted(string ruleId)
    {
        // Act: create a diagnostic with the identifier
        var diagnostic = Create(ruleId: ruleId);

        // Assert: the identifier is kept
        Assert.Equal(ruleId, diagnostic.RuleId);
    }

    /// <summary>
    ///     Test that the first line and column are accepted as the smallest valid position.
    /// </summary>
    [Fact]
    public void Diagnostic_Constructor_LineAndColumnOne_Accepted()
    {
        // Act: create a diagnostic at the first position
        var diagnostic = Create(line: 1, column: 1);

        // Assert: the position is kept
        Assert.Equal((1, 1), (diagnostic.Line, diagnostic.Column));
    }

    /// <summary>
    ///     Test that a null rule identifier is rejected.
    /// </summary>
    [Fact]
    public void Diagnostic_Constructor_NullRuleId_Throws()
    {
        // Act & Assert: null is rejected and the parameter is named
        var exception = Assert.Throws<ArgumentNullException>(() => Create(ruleId: null!));
        Assert.Equal("ruleId", exception.ParamName);
    }

    /// <summary>
    ///     Test that a null path is rejected.
    /// </summary>
    [Fact]
    public void Diagnostic_Constructor_NullPath_Throws()
    {
        // Act & Assert: null is rejected and the parameter is named
        var exception = Assert.Throws<ArgumentNullException>(() => Create(path: null!));
        Assert.Equal("path", exception.ParamName);
    }

    /// <summary>
    ///     Test that a null message is rejected.
    /// </summary>
    [Fact]
    public void Diagnostic_Constructor_NullMessage_Throws()
    {
        // Act & Assert: null is rejected and the parameter is named
        var exception = Assert.Throws<ArgumentNullException>(() => Create(message: null!));
        Assert.Equal("message", exception.ParamName);
    }

    /// <summary>
    ///     Test that a rule identifier with an uppercase letter is rejected.
    /// </summary>
    [Fact]
    public void Diagnostic_Constructor_UppercaseRuleId_Throws()
    {
        // Act & Assert: only lowercase letters are allowed
        var exception = Assert.Throws<ArgumentException>(() => Create(ruleId: "Line-length"));
        Assert.Equal("ruleId", exception.ParamName);
    }

    /// <summary>
    ///     Test that a rule identifier with a trailing hyphen is rejected.
    /// </summary>
    [Fact]
    public void Diagnostic_Constructor_RuleIdWithTrailingHyphen_Throws()
    {
        // Act & Assert: a trailing hyphen is not kebab-case
        var exception = Assert.Throws<ArgumentException>(() => Create(ruleId: "line-"));
        Assert.Equal("ruleId", exception.ParamName);
    }

    /// <summary>
    ///     Test that a rule identifier with a doubled hyphen is rejected.
    /// </summary>
    [Fact]
    public void Diagnostic_Constructor_RuleIdWithDoubleHyphen_Throws()
    {
        // Act & Assert: hyphens must be single
        var exception = Assert.Throws<ArgumentException>(() => Create(ruleId: "line--length"));
        Assert.Equal("ruleId", exception.ParamName);
    }

    /// <summary>
    ///     Test that an empty rule identifier is rejected.
    /// </summary>
    [Fact]
    public void Diagnostic_Constructor_EmptyRuleId_Throws()
    {
        // Act & Assert: an empty identifier is not kebab-case
        var exception = Assert.Throws<ArgumentException>(() => Create(ruleId: string.Empty));
        Assert.Equal("ruleId", exception.ParamName);
    }

    /// <summary>
    ///     Test that a rule identifier that starts with a digit or a hyphen is rejected.
    /// </summary>
    [Theory]
    [InlineData("1rule")]
    [InlineData("-rule")]
    public void Diagnostic_Constructor_RuleIdStartingWithDigit_Throws(string ruleId)
    {
        // Act & Assert: the first character must be a lowercase letter
        var exception = Assert.Throws<ArgumentException>(() => Create(ruleId: ruleId));
        Assert.Equal("ruleId", exception.ParamName);
    }

    /// <summary>
    ///     Test that a rule identifier with an underscore, a space or a non-ASCII letter is rejected.
    /// </summary>
    [Theory]
    [InlineData("line_length")]
    [InlineData("line length")]
    [InlineData("rule-\u00E9")]
    public void Diagnostic_Constructor_RuleIdWithInvalidCharacter_Throws(string ruleId)
    {
        // Act & Assert: only ASCII lowercase letters, digits and hyphens are allowed
        var exception = Assert.Throws<ArgumentException>(() => Create(ruleId: ruleId));
        Assert.Equal("ruleId", exception.ParamName);
        Assert.Contains("kebab-case", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a blank path is rejected.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Diagnostic_Constructor_BlankPath_Throws(string path)
    {
        // Act & Assert: an empty or whitespace path is rejected
        var exception = Assert.Throws<ArgumentException>(() => Create(path: path));
        Assert.Equal("path", exception.ParamName);
    }

    /// <summary>
    ///     Test that a line below one is rejected.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Diagnostic_Constructor_LineZero_Throws(int line)
    {
        // Act & Assert: positions are 1-based
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(line: line));
    }

    /// <summary>
    ///     Test that a column below one is rejected.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Diagnostic_Constructor_ColumnZero_Throws(int column)
    {
        // Act & Assert: positions are 1-based
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(column: column));
    }

    /// <summary>
    ///     Test that a blank message is rejected.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("  \t ")]
    public void Diagnostic_Constructor_BlankMessage_Throws(string message)
    {
        // Act & Assert: an empty or whitespace message is rejected
        var exception = Assert.Throws<ArgumentException>(() => Create(message: message));
        Assert.Equal("message", exception.ParamName);
    }

    /// <summary>
    ///     Test that a message that contains a line break of any kind is rejected.
    /// </summary>
    [Theory]
    [InlineData("first\nsecond")]
    [InlineData("first\rsecond")]
    [InlineData("first\r\nsecond")]
    [InlineData("first\u0085second")]
    [InlineData("first\u2028second")]
    [InlineData("first\u2029second")]
    public void Diagnostic_Constructor_MultilineMessage_Throws(string message)
    {
        // Act & Assert: the message must be a single line
        var exception = Assert.Throws<ArgumentException>(() => Create(message: message));
        Assert.Equal("message", exception.ParamName);
        Assert.Contains("single line", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that an undefined kind is rejected.
    /// </summary>
    [Fact]
    public void Diagnostic_Constructor_UndefinedKind_Throws()
    {
        // Act & Assert: only defined members are accepted
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => Create(kind: (DiagnosticKind)99));
        Assert.Equal("kind", exception.ParamName);
    }

    /// <summary>
    ///     Test that a formatting diagnostic is fixable.
    /// </summary>
    [Fact]
    public void Diagnostic_Fixable_FormattingKind_IsTrue()
    {
        // Act: create a formatting diagnostic
        var diagnostic = Create(kind: DiagnosticKind.Formatting);

        // Assert: rewriting removes it
        Assert.True(diagnostic.Fixable);
    }

    /// <summary>
    ///     Test that a lint-only diagnostic is not fixable.
    /// </summary>
    [Fact]
    public void Diagnostic_Fixable_LintOnlyKind_IsFalse()
    {
        // Act: create a lint-only diagnostic
        var diagnostic = Create(kind: DiagnosticKind.LintOnly);

        // Assert: rewriting does not remove it
        Assert.False(diagnostic.Fixable);
    }

    /// <summary>
    ///     Test that two diagnostics with the same values are equal and have the same hash code.
    /// </summary>
    [Fact]
    public void Diagnostic_Equals_SameValues_AreEqual()
    {
        // Arrange: two separately created diagnostics with the same values
        var first = Create();
        var second = Create();

        // Act & Assert: they are equal in every way equality can be asked
        Assert.Equal(first, second);
        Assert.True(first.Equals(second));
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    /// <summary>
    ///     Test that diagnostics that differ in the column are not equal.
    /// </summary>
    [Fact]
    public void Diagnostic_Equals_DifferentColumn_AreNotEqual()
    {
        // Arrange: two diagnostics that differ only in the column
        var first = Create(column: 7);
        var second = Create(column: 8);

        // Act & Assert: they are not equal
        Assert.NotEqual(first, second);
    }

    /// <summary>
    ///     Test that a difference in any single member makes two diagnostics unequal.
    /// </summary>
    [Fact]
    public void Diagnostic_Equals_AnyMemberDifferent_AreNotEqual()
    {
        // Arrange: the base diagnostic
        var baseline = Create();

        // Act & Assert: changing any one member breaks equality
        Assert.NotEqual(baseline, Create(ruleId: "paragraph-wrap"));
        Assert.NotEqual(baseline, Create(kind: DiagnosticKind.LintOnly));
        Assert.NotEqual(baseline, Create(path: "docs/b.md"));
        Assert.NotEqual(baseline, Create(line: 4));
        Assert.NotEqual(baseline, Create(message: "Another message."));
    }

    /// <summary>
    ///     Test that a diagnostic is never equal to null.
    /// </summary>
    [Fact]
    public void Diagnostic_Equals_Null_AreNotEqual()
    {
        // Act & Assert: a diagnostic differs from null
        Assert.False(Create().Equals((Diagnostic?)null));
    }

    /// <summary>
    ///     Test that equal diagnostics collapse to one entry in a hash set.
    /// </summary>
    [Fact]
    public void Diagnostic_GetHashCode_EqualDiagnostics_CollapseInHashSet()
    {
        // Arrange: two equal and one different diagnostic
        var set = new HashSet<Diagnostic> { Create(), Create(), Create(column: 8) };

        // Act & Assert: only the distinct values remain
        Assert.Equal(2, set.Count);
    }
}
