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
using DemaConsulting.LintMark.Document;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Unit tests for the defaults of the LintMarkConfig class and the marker check of the LinkRefactorOptions type.
/// </summary>
public class LintMarkConfigTests
{
    /// <summary>
    ///     Compares two configurations by content, which the record itself does not do for its collections.
    /// </summary>
    internal sealed class ContentComparer : IEqualityComparer<LintMarkConfig>
    {
        /// <summary>
        ///     Gets the shared comparer.
        /// </summary>
        public static ContentComparer Instance { get; } = new();

        /// <inheritdoc />
        public bool Equals(LintMarkConfig? x, LintMarkConfig? y) =>
            x is not null && y is not null
            && x == y with { Rules = x.Rules, Ignore = x.Ignore }
            && x.Rules.SequenceEqual(y.Rules)
            && x.Ignore.SequenceEqual(y.Ignore, StringComparer.Ordinal);

        /// <inheritdoc />
        public int GetHashCode(LintMarkConfig obj) => obj.LineLength;
    }

    /// <summary>
    ///     Test that a marker that is not a single HTML comment on one line is rejected.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("Link References")]
    [InlineData("<!-- open")]
    [InlineData("close -->")]
    [InlineData("<!-->")]
    [InlineData("<!--->")]
    [InlineData("<!--> -->")]
    [InlineData("<!---> -->")]
    [InlineData("<!-- a --> text <!-- b -->")]
    [InlineData("<!-- a --> text")]
    [InlineData("<!-- a -->-->")]
    [InlineData("<!-- a\nb -->")]
    [InlineData("<!-- a\r\nb -->")]
    [InlineData("<!-- a\rb -->")]
    [InlineData("<!-- a\u0085b -->")]
    [InlineData("<!-- a\u2028b -->")]
    [InlineData("<!-- a\u2029b -->")]
    public void LinkRefactorOptions_IsValidMarker_NotSingleLineHtmlComment_ReturnsFalse(string marker) =>
        Assert.False(LinkRefactorOptions.IsValidMarker(marker));

    /// <summary>
    ///     Test that markers that are one HTML comment on one line are accepted, including the shortest one.
    /// </summary>
    [Theory]
    [InlineData("<!---->")]
    [InlineData("<!-- x -->")]
    [InlineData("<!--- x -->")]
    [InlineData("<!-- a - b -->")]
    [InlineData("<!-- a -- b -->")]
    [InlineData("<!-- a --->")]
    [InlineData("<!--x-->")]
    [InlineData("<!-- Link References -->")]
    public void LinkRefactorOptions_IsValidMarker_SingleLineHtmlComment_ReturnsTrue(string marker) =>
        Assert.True(LinkRefactorOptions.IsValidMarker(marker));
}
