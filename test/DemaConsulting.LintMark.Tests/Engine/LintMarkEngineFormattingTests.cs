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
///     Unit tests for the whitespace and block formatting that the LintMarkEngine class applies by default.
/// </summary>
public partial class LintMarkEngineTests
{
    /// <summary>
    ///     A small document that breaks several rules, with its line endings as LF.
    /// </summary>
    private const string DirtyText = "Title\n=====\ntext  \nmore \n- a\n- b\n\n\n\n~~~text\ncode\n~~~\n";

    /// <summary>
    ///     The canonical form of <see cref="DirtyText"/>.
    /// </summary>
    private const string CleanText = "# Title\n\ntext  \nmore\n\n- a\n- b\n\n```text\ncode\n```\n";

    /// <summary>
    ///     Test that the default engine rewrites whitespace and block structure with the fix option and reports nothing afterwards.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixDefaultFormatters_RewritesFileAndReportsNothing()
    {
        // Arrange: a file that breaks several rules
        using var directory = new TemporaryDirectory();
        var path = directory.WriteBytes("a.md", Utf8(DirtyText));

        // Act: run the default engine with the fix option
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(2), "--fix");

        // Assert: the file is canonical, every finding was fixed, nothing is reported
        Assert.Equal(Utf8(CleanText), File.ReadAllBytes(path));
        Assert.Equal(0, exitCode);
        Assert.Empty(lines);
    }

    /// <summary>
    ///     Test that the default engine reports every violation at its place without writing the file.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_CheckDefaultFormatters_ReportsEachViolationAndWritesNothing()
    {
        // Arrange: a file that breaks several rules, with an old write time
        using var directory = new TemporaryDirectory();
        var (path, writeTime) = WriteOld(directory, "a.md", Utf8(DirtyText));

        // Act: run the default engine without options
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(1));

        // Assert: one finding per rule and line, exit code one, the file is untouched
        Assert.Equal(1, exitCode);
        Assert.Equal(
            [
                "a.md:1:1: heading-style",
                "a.md:3:1: heading-blank-lines",
                "a.md:4:5: trailing-whitespace",
                "a.md:5:1: list-blank-lines",
                "a.md:8:1: max-blank-lines",
                "a.md:10:1: code-fence",
                "a.md:12:1: code-fence"
            ],
            lines.Select(line => string.Join(' ', line.Split(' ').Take(2))));
        AssertUntouched(path, Utf8(DirtyText), writeTime);
    }

    /// <summary>
    ///     Test that the diff option shows the changes of the default engine without writing the file.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_DiffDefaultFormatters_PrintsTheChangeAndWritesNothing()
    {
        // Arrange: a file with trailing whitespace and a closed heading
        using var directory = new TemporaryDirectory();
        var (path, writeTime) = WriteOld(directory, "a.md", Utf8("# Title #\n\ntext \n"));

        // Act: run with the diff option
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(1), "--diff");

        // Assert: the findings, then a unified diff of both changes; the file is untouched
        Assert.Equal(1, exitCode);
        Assert.StartsWith("a.md:1:8: heading-style ", lines[0], StringComparison.Ordinal);
        Assert.StartsWith("a.md:3:5: trailing-whitespace ", lines[1], StringComparison.Ordinal);
        Assert.Equal(["--- a/a.md", "+++ b/a.md", "@@ -1,3 +1,3 @@", "-# Title #", "+# Title", " ", "-text ", "+text"], lines[2..]);
        AssertUntouched(path, Utf8("# Title #\n\ntext \n"), writeTime);
    }

}
