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
///     Unit tests for the paragraph wrap that the LintMarkEngine class applies by default.
/// </summary>
public partial class LintMarkEngineTests
{
    /// <summary>
    ///     Test that the fix option keeps reporting a line that could not be wrapped, as a finding that the fix cannot repair.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixParagraphThatCannotBeWrapped_ReportsItAsRemaining()
    {
        // Arrange: a list item with a lazy continuation line that is too wide, and trailing whitespace that the fix removes
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "line-length: 20\n");
        var path = directory.WriteBytes("a.md", Utf8("- ant bat cat dog \neel fox gnu hen ink jam\n"));

        // Act: run the default engine with the fix option
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(1), "--fix");

        // Assert: the whitespace is fixed, and the line that cannot be wrapped is the one remaining finding
        Assert.Equal(Utf8("- ant bat cat dog\neel fox gnu hen ink jam\n"), File.ReadAllBytes(path));
        Assert.Equal(1, exitCode);
        Assert.Equal(
            "a.md:2:21: line-length Line is 23 columns wide; the limit is 20 and the paragraph cannot be wrapped automatically",
            Assert.Single(lines));
    }
}
