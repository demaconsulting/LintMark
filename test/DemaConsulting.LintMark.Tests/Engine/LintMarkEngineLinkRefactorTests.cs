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

using DemaConsulting.LintMark.Engine;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Unit tests for the link refactoring as the LintMarkEngine class applies it to files.
/// </summary>
public partial class LintMarkEngineTests
{
    /// <summary>
    ///     A long destination (62 columns).
    /// </summary>
    private const string RefactorUrl = "https://example.com/some/very/long/path/to/a/page/that/goes/on";

    /// <summary>
    ///     Test that a file with a long link is not changed unless the feature is enabled.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixLongLinkWithoutTheSetting_LeavesTheFileAlone()
    {
        // Arrange: no configuration at all
        using var directory = new TemporaryDirectory();
        var content = Utf8($"A [long link text]({RefactorUrl}) here.\n");
        var path = directory.WriteBytes("a.md", content);

        // Act
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(1), "--fix");

        // Assert: the default is off, byte for byte
        Assert.Equal(content, File.ReadAllBytes(path));
        Assert.Equal(0, exitCode);
        Assert.Empty(lines);
    }

    /// <summary>
    ///     Test that the fix option converts the link, keeps CRLF line endings and the byte order mark, passes every safety check,
    ///     and that a second run and a check find nothing to do.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixLongLinkWithTheSetting_ConvertsKeepsLineEndingsAndIsStable()
    {
        // Arrange: a CRLF file with a byte order mark and the setting in the configuration
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "link-refactor:\n  enabled: true\n");
        var path = directory.WriteBytes("a.md", [0xEF, 0xBB, 0xBF, .. Utf8($"# T\r\n\r\nA [long link text]({RefactorUrl}) here.\r\n")]);

        // Act
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(1), "--fix");
        var written = File.ReadAllBytes(path);
        var (secondCode, secondLines) = Execute(directory, new LintMarkEngine(1), "--fix");
        var (checkCode, checkLines) = Execute(directory, new LintMarkEngine(1));

        // Assert: converted with CRLF and the mark, no safety refusal, and nothing left to do afterwards
        Assert.Equal(
            [0xEF, 0xBB, 0xBF, .. Utf8($"# T\r\n\r\nA [long link text][long-link-text] here.\r\n\r\n<!-- Link References -->\r\n[long-link-text]: {RefactorUrl}\r\n")],
            written);
        Assert.Equal(0, exitCode);
        Assert.Empty(lines);
        Assert.Equal(0, secondCode);
        Assert.Empty(secondLines);
        Assert.Equal(written, File.ReadAllBytes(path));
        Assert.Equal(0, checkCode);
        Assert.Empty(checkLines);
    }

    /// <summary>
    ///     Test that the check reports each link that would be converted and writes nothing, and that naming the rule on the
    ///     command line (here by its markdownlint number) is enough to switch the feature on.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_CheckWithEnableOption_ReportsTheLinkAndWritesNothing()
    {
        // Arrange: no configuration, and a file with an old write time
        using var directory = new TemporaryDirectory();
        var content = Utf8($"# T\n\nA [long link text]({RefactorUrl}) here.\n");
        var (path, writeTime) = WriteOld(directory, "a.md", content);

        // Act
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(1), "--enable", "MD054");

        // Assert: one finding at the link, the file is untouched
        Assert.Equal(1, exitCode);
        Assert.Equal("a.md:3:3: link-refactor Inline link is 80 columns wide (threshold 60); use a reference link", Assert.Single(lines));
        AssertUntouched(path, content, writeTime);
    }

    /// <summary>
    ///     Test that the rule can be switched off by the command line although the configuration enables the feature.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixWithDisableOption_LeavesTheLinkAlone()
    {
        // Arrange: the feature is on in the configuration
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "link-refactor:\n  enabled: true\n");
        var content = Utf8($"A [long link text]({RefactorUrl}) here.\n");
        var path = directory.WriteBytes("a.md", content);

        // Act
        var (exitCode, _) = Execute(directory, new LintMarkEngine(1), "--fix", "--disable", "link-refactor");

        // Assert
        Assert.Equal(content, File.ReadAllBytes(path));
        Assert.Equal(0, exitCode);
    }
}
