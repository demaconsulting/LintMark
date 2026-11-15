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

using DemaConsulting.LintMark.Verify.Reporting;

namespace DemaConsulting.LintMark.Verify.Tests;

/// <summary>
///     Tests for the DisplayPath class: the relative form of a path below the base directory and the full form of any other.
/// </summary>
public class DisplayPathTests
{
    /// <summary>
    ///     Test that a path below the base directory is shown relative, with forward slashes, even when a segment starts with two dots.
    /// </summary>
    [Fact]
    public void DisplayPath_Of_PathBelowBase_IsRelative()
    {
        // Arrange: a base directory and files below it, one in a folder whose name starts with two dots
        var baseDirectory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "display-base"));

        // Act
        var plain = DisplayPath.Of(Path.Combine(baseDirectory, "docs", "a.md"), baseDirectory);
        var dotted = DisplayPath.Of(Path.Combine(baseDirectory, "..data", "b.md"), baseDirectory);

        // Assert
        Assert.Equal("docs/a.md", plain);
        Assert.Equal("..data/b.md", dotted);
    }

    /// <summary>
    ///     Test that a path outside the base directory is shown in full.
    /// </summary>
    [Fact]
    public void DisplayPath_Of_PathOutsideBase_IsFullPath()
    {
        // Arrange: a base directory and a file beside it
        var baseDirectory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "display-base"));
        var outside = Path.GetFullPath(Path.Combine(baseDirectory, "..", "elsewhere", "c.md"));

        // Act
        var shown = DisplayPath.Of(outside, baseDirectory);

        // Assert
        Assert.Equal(outside.Replace('\\', '/'), shown);
    }
}
