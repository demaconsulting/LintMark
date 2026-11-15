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
///     Test that the StructureRules class reports fences without a language, unclosed fences, blank lines between block quotes,
///     shell prompts, headings without a space and emphasis with a stray space.
/// </summary>
public class StructureRulesTests
{
    /// <summary>
    ///     The rules under test.
    /// </summary>
    private static readonly StructureRules Rules = new();

    /// <summary>
    ///     Test that a fenced code block without an info string is reported, backtick and tilde fences alike, and one with a language is not.
    /// </summary>
    [Fact]
    public void StructureRules_Check_FenceWithoutLanguage_IsReportedAtTheFence()
    {
        // Act & Assert: backtick and tilde fences without language are reported; with language, or indented, they are not
        Assert.Equal(
            ["1:1 code-fence-language", "9:1 code-fence-language"],
            LintRuleTestSupport.Findings(Rules, "```\nno lang\n```\n\n```js\nok\n```\n\n~~~\ntilde\n~~~\n"));
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "```js\nok\n```\n\n~~~python\nok\n~~~\n\n```{.sh}\nok\n```\n"));
        Assert.Equal(["3:3 code-fence-language"], LintRuleTestSupport.Findings(Rules, "- item\n\n  ```\n  x\n  ```\n"));
    }

    /// <summary>
    ///     Test that a fence that is never closed is reported once and as a fence without language is not.
    /// </summary>
    [Fact]
    public void StructureRules_Check_UnclosedFence_IsReportedAtTheFence()
    {
        // Act & Assert: the code fence and the math block are reported at their opening line
        Assert.Equal(["3:1 fence-unclosed"], LintRuleTestSupport.Findings(Rules, "text\n\n```js\nnever closed\n"));
        Assert.Equal(["Fenced code block is never closed"], LintRuleTestSupport.Messages(Rules, "```js\nx\n"));
        Assert.Equal(["Math block is never closed"], LintRuleTestSupport.Messages(Rules, "$$\nx\n"));
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "```js\nclosed\n```\n"));
    }

    /// <summary>
    ///     Test that a blank line between two block quotes is reported, in a list item as well, and one quote with a blank quoted line is not.
    /// </summary>
    [Fact]
    public void StructureRules_Check_BlankLineBetweenQuotes_IsReported()
    {
        // Act & Assert: two quotes apart, a third that continues, and two inside a list item
        Assert.Equal(["2:1 blockquote-blank-line", "4:1 blockquote-blank-line"], LintRuleTestSupport.Findings(Rules, "> a\n\n> b\n\n> c\n> d\n"));
        Assert.Equal(["4:1 blockquote-blank-line"], LintRuleTestSupport.Findings(Rules, "- item\n\n  > x\n\n  > y\n"));
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "> a\n>\n> b\n"));
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "> a\n\ntext\n\n> b\n"));
    }

    /// <summary>
    ///     Test that a code block whose every command starts with a dollar sign is reported, fenced and indented, and a block with output is not.
    /// </summary>
    [Fact]
    public void StructureRules_Check_ShellPrompts_AreReportedWhenEveryLineHasOne()
    {
        // Act & Assert: the first prompt is the position of the finding
        Assert.Equal(["2:1 dollar-sign-in-shell-code"], LintRuleTestSupport.Findings(Rules, "```sh\n$ ls\n$ pwd\n```\n"));
        Assert.Equal(["1:5 dollar-sign-in-shell-code"], LintRuleTestSupport.Findings(Rules, "    $ indented\n    $ again\n"));

        // Output without a prompt, no prompt and an empty block are not reported
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "```sh\n$ ls\noutput\n```\n"));
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "```sh\nls\n```\n"));
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "```sh\n```\n"));
    }

    /// <summary>
    ///     Test that a paragraph that starts like a heading without the space is reported, closed or not, and numbers, escapes and later lines are not.
    /// </summary>
    [Fact]
    public void StructureRules_Check_HeadingWithoutSpace_IsReportedAsAParagraph()
    {
        // Act & Assert: a missing space and a closed form
        Assert.Equal(["1:1 heading-missing-space"], LintRuleTestSupport.Findings(Rules, "#Title\n"));
        Assert.Equal(["1:1 heading-missing-space"], LintRuleTestSupport.Findings(Rules, "##Title with words\n"));
        Assert.Equal(["1:1 heading-closed-missing-space"], LintRuleTestSupport.Findings(Rules, "##Title##\n"));
        Assert.Equal(["1:1 heading-missing-space"], LintRuleTestSupport.Findings(Rules, "#Title #\n"));
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "> #Title\n"));
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "- #X\n- #Y\n"));

        // Issue numbers, shebangs, escapes, seven number signs, real headings and a later line are not reported
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "#123 is an issue\n"));
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "#!shebang\n"));
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "\\#escaped\n"));
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "#######Seven\n"));
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "# Title\n\n## Title ##\n"));
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "text\n#midline\n"));
        Assert.Equal(["1:1 code-fence-language"], LintRuleTestSupport.Findings(Rules, "```\n#code\n```\n"));
    }

    /// <summary>
    ///     Test that emphasis with a space inside one delimiter only is reported for every marker form.
    /// </summary>
    [Fact]
    public void StructureRules_Check_AsymmetricSpacedEmphasis_IsReported()
    {
        // Act & Assert: a trailing space, a leading space, strong and underscore forms
        Assert.Equal(["1:5 emphasis-space"], LintRuleTestSupport.Findings(Rules, "see *text * here\n"));
        Assert.Equal(["1:5 emphasis-space"], LintRuleTestSupport.Findings(Rules, "and **bold ** too\n"));
        Assert.Equal(["1:5 emphasis-space"], LintRuleTestSupport.Findings(Rules, "see _text _ here\n"));
        Assert.Equal(["1:5 emphasis-space"], LintRuleTestSupport.Findings(Rules, "see __text __ here\n"));
        Assert.Equal(["1:2 emphasis-space"], LintRuleTestSupport.Findings(Rules, "(* text*) here\n"));
        Assert.Equal(["1:2 emphasis-space"], LintRuleTestSupport.Findings(Rules, "(** bold**) here\n"));
        Assert.Equal(["1:2 emphasis-space"], LintRuleTestSupport.Findings(Rules, "(_ text_) here\n"));

        // At the start of a line, in a quote and after inline HTML, and the message shows the corrected form
        Assert.Equal(["1:1 emphasis-space"], LintRuleTestSupport.Findings(Rules, "*text *\n"));
        Assert.Equal(["1:3 emphasis-space"], LintRuleTestSupport.Findings(Rules, "> *text * here\n"));
        Assert.Equal(["1:4 emphasis-space"], LintRuleTestSupport.Findings(Rules, "<b>*x *</b>\n"));
        Assert.Equal(["Emphasis has a space inside one of its delimiters; use *text* instead of *text *"], LintRuleTestSupport.Messages(Rules, "see *text * here\n"));
        Assert.Equal(["Emphasis has a space inside one of its delimiters; use **bold** instead of ** bold**"], LintRuleTestSupport.Messages(Rules, "(** bold**) here\n"));

        // Each line is read alone: a second line with its own problem is reported at its own position
        Assert.Equal(["1:5 emphasis-space", "2:5 emphasis-space"], LintRuleTestSupport.Findings(Rules, "see *a * here\nand *b * too\n"));
    }

    /// <summary>
    ///     Test that spaces on both sides, literal asterisks, escaped markers, code, valid emphasis and multi-line emphasis are not reported.
    /// </summary>
    [Fact]
    public void StructureRules_Check_LiteralAsterisksEscapesAndValidEmphasis_AreNotReported()
    {
        // Symmetric spaced markers and literal asterisks between words are not emphasis typos
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "a * b * c\n"));
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "see * text * here\n"));
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "2 * 3 * 4 and 2 *3 * 4\n"));
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "x * y* z\n"));
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "__ init __ and _ _\n"));

        // Wildcards and globs
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "*.md and * wildcard\n"));
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "match *.txt files, not * those\n"));

        // Escaped markers
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "\\*a * b\n"));
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "\\*text \\*\n"));
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "a \\* b* c\n"));

        // Code spans, autolinks, link destinations, code blocks and inline HTML attributes
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "`*x *` and ``* y*``\n"));
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "<https://example.com/a*b*c>\n"));
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "[l](<u/*a *>)\n"));
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "<span title=\"*a *\">x</span>\n"));
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "```text\n*a *\n```\n"));

        // Valid emphasis, intraword markers, snake case and markers that continue on the next line
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "*ok* and **fine** and x*y*z and snake_case_name\n"));
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "see *text\n* here\n"));
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "see *text\nmore * here\n"));
    }
}
