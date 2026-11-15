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

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     System-level tests of the lint-only rule families, which run the LintMark application via dotnet with the
///     check option on real files and compare the reported rule, line and column.
/// </summary>
/// <remarks>
///     Only relative paths are compared (never the resolved temporary directory), because the temporary directory
///     may be reached through a link (<c>/var</c> and <c>/private/var</c> on macOS).
/// </remarks>
[Collection("Sequential")]
public class IntegrationLintTests
{
    /// <summary>
    ///     The path of the application DLL.
    /// </summary>
    private readonly string _dllPath;

    /// <summary>
    ///     Initializes a new instance of the <see cref="IntegrationLintTests"/> class by locating the LintMark DLL.
    /// </summary>
    public IntegrationLintTests()
    {
        _dllPath = Path.Combine(AppContext.BaseDirectory, "DemaConsulting.LintMark.dll");
        Assert.True(File.Exists(_dllPath), $"Could not find LintMark DLL at {_dllPath}");
    }

    /// <summary>
    ///     Checks one file without changing it and returns the exit code and the output.
    /// </summary>
    /// <param name="content">The content of the file.</param>
    /// <param name="config">The content of the configuration file, or <see langword="null"/> for none.</param>
    /// <param name="options">Additional command-line options.</param>
    /// <returns>The exit code and the output; the file is checked to be unchanged.</returns>
    private (int ExitCode, string Output) Check(string content, string? config = null, params string[] options)
    {
        using var directory = new TemporaryDirectory();
        if (config is not null)
        {
            directory.WriteFile(".lintmark.yaml", config);
        }

        var file = directory.WriteFile("a.md", content);
        var before = File.ReadAllBytes(file);
        var exitCode = Runner.Run(out var output, "dotnet", [_dllPath, "--check", .. options, file]);
        Assert.Equal(before, File.ReadAllBytes(file));
        return (exitCode, output);
    }

    /// <summary>
    ///     Test that the heading rules report a skipped level, a repeated text, trailing punctuation, emphasis used as a
    ///     heading and the two shapes of a heading without a space.
    /// </summary>
    [Fact]
    public void LintMark_LintHeadings_HeadingProblems_AreReportedWithRuleAndPosition()
    {
        // Arrange and Act: check a document with one problem for each heading rule
        var (exitCode, output) = Check("# One\n\n### Three\n\n## Dup\n\n## Dup\n\n## Punct.\n\n**Bold para**\n\n#NoSpace\n\n#Closed#\n");

        // Assert: one finding per rule at the start of its line
        Assert.Equal(1, exitCode);
        Assert.Contains("a.md:3:1: heading-increment", output);
        Assert.Contains("a.md:7:1: heading-duplicate", output);
        Assert.Contains("a.md:9:1: heading-trailing-punctuation", output);
        Assert.Contains("a.md:11:1: emphasis-as-heading", output);
        Assert.Contains("a.md:13:1: heading-missing-space", output);
        Assert.Contains("a.md:15:1: heading-closed-missing-space", output);
    }

    /// <summary>
    ///     Test that the link rules report a missing alternative text, an empty link, an unknown fragment, an undefined
    ///     and an unused reference, a reversed link, a padded link text and a link text that says nothing.
    /// </summary>
    [Fact]
    public void LintMark_LintLinks_LinkProblems_AreReportedWithRuleAndPosition()
    {
        // Arrange and Act: check a document with one problem for each link rule, with the optional rule switched on
        var (exitCode, output) = Check(
            "![](img.png)\n\n[empty]()\n\n[frag](#nowhere)\n\n[ref][undefined]\n\n[unused]: https://example.com\n\n"
            + "(reversed)[https://example.com]\n\n[ spaced ](https://example.com)\n\n[click here](https://example.com)\n",
            "rules:\n  descriptive-link-text: true\n");

        // Assert: one finding per rule at the start of its line
        Assert.Equal(1, exitCode);
        Assert.Contains("a.md:1:1: image-alt-text", output);
        Assert.Contains("a.md:3:1: link-empty", output);
        Assert.Contains("a.md:5:1: link-fragment", output);
        Assert.Contains("a.md:7:1: link-reference-undefined", output);
        Assert.Contains("a.md:9:1: link-definition-unused", output);
        Assert.Contains("a.md:11:1: link-reversed", output);
        Assert.Contains("a.md:13:1: link-space", output);
        Assert.Contains("a.md:15:1: descriptive-link-text", output);
    }

    /// <summary>
    ///     Test that the code rules report a fence that is never closed, a fence without a language and, when switched on,
    ///     a shell block in which every command starts with a dollar sign.
    /// </summary>
    [Fact]
    public void LintMark_LintCode_FenceAndShellProblems_AreReportedWithRuleAndPosition()
    {
        // Arrange and Act: check an unclosed fence, and a document with a shell block and a fence without a language
        var (unclosedExit, unclosed) = Check("```text\nunclosed fence\n");
        var (shellExit, shell) = Check("```sh\n$ ls\n$ pwd\n```\n\n```\nplain\n```\n", null, "--enable", "dollar-sign-in-shell-code");

        // Assert: the unclosed fence, then the dollar signs at the first command and the missing language at its fence
        Assert.Equal(1, unclosedExit);
        Assert.Contains("a.md:1:1: fence-unclosed", unclosed);
        Assert.Equal(1, shellExit);
        Assert.Contains("a.md:2:1: dollar-sign-in-shell-code", shell);
        Assert.Contains("a.md:6:1: code-fence-language", shell);
    }

    /// <summary>
    ///     Test that the table rules report a row with the wrong number of cells, a table that is too wide even when
    ///     compact, an invalid grid table and, when switched on, a grid table that is left as written.
    /// </summary>
    [Fact]
    public void LintMark_LintTables_TableProblems_AreReportedWithRuleAndPosition()
    {
        // Arrange and Act: check a short row and a wide table, an invalid grid table and a valid grid table
        var (pipeExit, pipe) = Check(
            "| a | b |\n| --- | --- |\n| 1 |\n\n| a | b | c |\n| --- | --- | --- |\n| averyveryverylongcellaverylong | averyveryverylongcellaverylong | averyveryverylongcellaverylong |\n",
            "line-length: 40\n");
        var (invalidExit, invalid) = Check("| a | b |\n+---+---+\n");
        var (gridExit, grid) = Check("+---+---+\n| a | b |\n+---+---+\n| 1 | 2 |\n+---+---+\n", null, "--enable", "table-grid-unformatted");

        // Assert: each rule reports at the first line of the table or row it names
        Assert.Equal(1, pipeExit);
        Assert.Contains("a.md:3:1: table-column-count Table row has 1 cells; the header has 2", pipe);
        Assert.Contains("a.md:7:1: table-too-wide", pipe);
        Assert.Equal(1, invalidExit);
        Assert.Contains("a.md:2:1: grid-table-invalid", invalid);
        Assert.Equal(1, gridExit);
        Assert.Contains("a.md:1:1: table-grid-unformatted", grid);
    }

    /// <summary>
    ///     Test that the remaining lint rules report a footnote that is never referenced, a blank line between block
    ///     quotes and emphasis with a space on one side.
    /// </summary>
    [Fact]
    public void LintMark_LintMisc_FootnoteQuoteAndEmphasisProblems_AreReportedWithRuleAndPosition()
    {
        // Arrange and Act: check a document with one problem for each rule
        var (exitCode, output) = Check("Text[^note] and.\n\n[^note]: Used.\n\n[^unused]: Footnote.\n\n> one\n\n> two\n\nsee *text * here\n");

        // Assert: one finding per rule, and the footnote that is referenced is not reported
        Assert.Equal(1, exitCode);
        Assert.Contains("a.md:5:1: footnote-unused", output);
        Assert.DoesNotContain("a.md:3:1", output);
        Assert.Contains("a.md:8:1: blockquote-blank-line", output);
        Assert.Contains("a.md:11:5: emphasis-space", output);
    }
}
