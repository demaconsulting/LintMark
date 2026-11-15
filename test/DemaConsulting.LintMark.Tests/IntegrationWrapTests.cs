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

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     System-level tests of the line length rule and the paragraph wrap, which run the LintMark
///     application via dotnet on real files.
/// </summary>
/// <remarks>
///     Only relative paths are compared (never the resolved temporary directory), because the temporary directory
///     may be reached through a link (<c>/var</c> and <c>/private/var</c> on macOS).
/// </remarks>
[Collection("Sequential")]
public class IntegrationWrapTests
{
    /// <summary>
    ///     The path of the application DLL.
    /// </summary>
    private readonly string _dllPath;

    /// <summary>
    ///     Initializes a new instance of the <see cref="IntegrationWrapTests"/> class by locating the LintMark DLL.
    /// </summary>
    public IntegrationWrapTests()
    {
        _dllPath = Path.Combine(AppContext.BaseDirectory, "DemaConsulting.LintMark.dll");
        Assert.True(File.Exists(_dllPath), $"Could not find LintMark DLL at {_dllPath}");
    }

    /// <summary>
    ///     Encodes text as UTF-8 without a byte order mark.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The bytes.</returns>
    private static byte[] Bytes(string text) => new UTF8Encoding(false).GetBytes(text);

    /// <summary>
    ///     Runs the tool with the fix option over one file and returns the file's bytes as text.
    /// </summary>
    /// <param name="directory">The temporary directory that holds the file and, when given, the configuration.</param>
    /// <param name="content">The content of the file.</param>
    /// <param name="config">The content of the configuration file, or <see langword="null"/> for none.</param>
    /// <returns>The exit code, the output and the text of the file after the run.</returns>
    private (int ExitCode, string Output, string Text) FixFile(TemporaryDirectory directory, string content, string? config = "line-length: 20\n")
    {
        if (config is not null)
        {
            directory.WriteFile(".lintmark.yaml", config);
        }

        var file = directory.WriteBytes("a.md", Bytes(content));
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--fix", file);
        return (exitCode, output, Encoding.UTF8.GetString(File.ReadAllBytes(file)));
    }

    /// <summary>
    ///     Runs the tool with the check option over one file.
    /// </summary>
    /// <param name="directory">The temporary directory that holds the file and, when given, the configuration.</param>
    /// <param name="content">The content of the file.</param>
    /// <param name="config">The content of the configuration file, or <see langword="null"/> for none.</param>
    /// <returns>The exit code and the output; the file is checked to be unchanged.</returns>
    private (int ExitCode, string Output) CheckFile(TemporaryDirectory directory, string content, string? config = "line-length: 20\n")
    {
        if (config is not null)
        {
            directory.WriteFile(".lintmark.yaml", config);
        }

        var file = directory.WriteBytes("a.md", Bytes(content));
        var before = File.ReadAllBytes(file);
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--check", file);
        Assert.Equal(before, File.ReadAllBytes(file));
        return (exitCode, output);
    }

    /// <summary>
    ///     Test that the check option reports a paragraph line that is wider than the line length at the first column beyond the width.
    /// </summary>
    [Fact]
    public void LintMark_CheckFlag_OverlongParagraph_ReportsLineLengthAtTheFirstColumnBeyondTheWidth()
    {
        // Arrange: a paragraph of 27 columns and a limit of 20
        using var directory = new TemporaryDirectory();

        // Act: check the file
        var (exitCode, output) = CheckFile(directory, "ant bat cat dog eel fox gnu\n");

        // Assert: the rule, the position and the width are reported, and the exit code is one
        Assert.Equal(1, exitCode);
        Assert.Contains("a.md:1:21: line-length Line is 27 columns wide; the limit is 20", output);
    }

    /// <summary>
    ///     Test that the fix option wraps such a paragraph so that no line is wider than the line length.
    /// </summary>
    [Fact]
    public void LintMark_FixFlag_OverlongParagraph_IsWrappedWithinTheWidth()
    {
        // Arrange and Act: fix a paragraph of 27 columns with a limit of 20
        using var directory = new TemporaryDirectory();
        var (exitCode, output, text) = FixFile(directory, "ant bat cat dog eel fox gnu\n");

        // Assert: wrapped, nothing remains to report
        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("line-length", output);
        Assert.Equal("ant bat cat dog eel\nfox gnu\n", text);
    }

    /// <summary>
    ///     Test that the fix option leaves every paragraph that has no line that is too wide byte for byte as written.
    /// </summary>
    [Fact]
    public void LintMark_FixFlag_ParagraphsWithinTheWidth_StayByteIdentical()
    {
        // Arrange: ragged paragraphs and one whose line is exactly as wide as the limit
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "line-length: 20\n");
        var file = directory.WriteBytes("a.md", Bytes("a\nbat cat\nd e f\n\nant bat cat dog else\n"));
        var before = File.ReadAllBytes(file);

        // Act: fix the file
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--fix", file);

        // Assert: success, no report, the same bytes
        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("line-length", output);
        Assert.Equal(before, File.ReadAllBytes(file));
    }

}
