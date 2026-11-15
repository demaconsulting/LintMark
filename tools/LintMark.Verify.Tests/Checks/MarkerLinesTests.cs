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

using DemaConsulting.LintMark.Verify.Checks;

namespace DemaConsulting.LintMark.Verify.Tests;

/// <summary>
///     Test that the MarkerLines class takes the marker that the link refactoring added, and only that, out of a rewrite.
/// </summary>
public class MarkerLinesTests
{
    /// <summary>
    ///     Test that the marker the refactoring adds is the only thing taken out of the comparison of the rendered pages.
    /// </summary>
    [Fact]
    public void MarkerLines_WithoutAddedMarker_OneMarkerAdded_RemovesThatLineOnly()
    {
        // Arrange / Act
        var added = MarkerLines.WithoutAddedMarker("A\n", "A\n\n<!-- M -->\n[a]: b\n", "<!-- M -->");
        var existing = MarkerLines.WithoutAddedMarker("A\n<!-- M -->\n", "A\n<!-- M -->\n[a]: b\n", "<!-- M -->");
        var twoAdded = MarkerLines.WithoutAddedMarker("A\n", "<!-- M -->\nA\n<!-- M -->\n", "<!-- M -->");
        var crlf = MarkerLines.WithoutAddedMarker("A\r\n", "A\r\n\r\n<!-- M -->\r\n[a]: b\r\n", "<!-- M -->");

        // Assert: removed once when one is added, never when it was there or when two were added
        Assert.Equal("A\n\n[a]: b\n", added);
        Assert.Equal("A\n<!-- M -->\n[a]: b\n", existing);
        Assert.Equal("<!-- M -->\nA\n<!-- M -->\n", twoAdded);
        Assert.Equal("A\r\n\r\n[a]: b\r\n", crlf);
    }

    /// <summary>
    ///     Test that a missing argument is rejected and named.
    /// </summary>
    [Fact]
    public void MarkerLines_WithoutAddedMarker_NullArguments_Throw()
    {
        Assert.Equal("original", Assert.Throws<ArgumentNullException>(() => MarkerLines.WithoutAddedMarker(null!, "a", "m")).ParamName);
        Assert.Equal("rewritten", Assert.Throws<ArgumentNullException>(() => MarkerLines.WithoutAddedMarker("a", null!, "m")).ParamName);
        Assert.Equal("marker", Assert.Throws<ArgumentNullException>(() => MarkerLines.WithoutAddedMarker("a", "a", null!)).ParamName);
    }
}
