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
///     Unit tests for the table rules that the LintMarkEngine class applies by default: the table layout, and the
///     lint-only findings about tables with the configuration and the suppression comments.
/// </summary>
public partial class LintMarkEngineTests
{
    /// <summary>
    ///     Test that rows with the wrong number of cells are reported and kept, with a rule that can be switched off by id or by number.
    /// </summary>
    [Theory]
    [InlineData("rules:\n  table-column-count: false\n", false)]
    [InlineData("rules:\n  MD056: false\n", false)]
    [InlineData("", true)]
    public void LintMarkEngine_Run_FixTableWithShortRow_ReportsItUnlessSwitchedOff(string config, bool reported)
    {
        // Arrange: a table with a row that lacks a cell
        using var directory = new TemporaryDirectory();
        if (config.Length > 0)
        {
            directory.WriteFile(".lintmark.yaml", config);
        }

        var path = directory.WriteBytes("a.md", Utf8("|a|b|\n|-|-|\n|1|\n"));

        // Act: run the default engine with the fix option
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(1), "--fix");

        // Assert: the row is never repaired; the finding is there exactly when the rule is on
        Assert.Equal(Utf8("| a | b |\n| --- | --- |\n| 1 |\n"), File.ReadAllBytes(path));
        Assert.Equal(reported ? 1 : 0, exitCode);
        Assert.Equal(reported ? ["a.md:3:1: table-column-count Table row has 1 cells; the header has 2"] : [], lines);
    }

    /// <summary>
    ///     Test that a grid table is kept as written and reported only when the rule is switched on.
    /// </summary>
    [Theory]
    [InlineData("", 0)]
    [InlineData("rules:\n  table-grid-unformatted: true\n", 1)]
    public void LintMarkEngine_Run_FixGridTable_KeepsTheTableAndReportsItWhenAsked(string config, int expectedExitCode)
    {
        // Arrange: a grid table
        using var directory = new TemporaryDirectory();
        if (config.Length > 0)
        {
            directory.WriteFile(".lintmark.yaml", config);
        }

        var text = "+-----+-----+\n| a   | b   |\n+=====+=====+\n| 1   | 2   |\n+-----+-----+\n";
        var path = directory.WriteBytes("a.md", Utf8(text));

        // Act: run the default engine with the fix option
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(1), "--fix");

        // Assert: the table is as written, and the finding is there exactly when asked for
        Assert.Equal(Utf8(text), File.ReadAllBytes(path));
        Assert.Equal(expectedExitCode, exitCode);
        Assert.Equal(expectedExitCode == 1 ? ["a.md:1:1: table-grid-unformatted Grid table is left as written"] : [], lines);
    }
}
