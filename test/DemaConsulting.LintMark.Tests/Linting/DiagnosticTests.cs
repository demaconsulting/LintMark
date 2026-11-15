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

using DemaConsulting.LintMark.Linting;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Unit tests for the Diagnostic record: what is fixable, and the value equality that the reporter uses to report a finding once.
/// </summary>
public class DiagnosticTests
{
    /// <summary>
    ///     Creates a diagnostic, replacing only the arguments a test wants to vary.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <param name="column">The column.</param>
    /// <returns>The diagnostic.</returns>
    private static Diagnostic Create(DiagnosticKind kind = DiagnosticKind.Formatting, int column = 7) =>
        new("line-length", kind, "docs/a.md", 3, column, "A line is too long.");

    /// <summary>
    ///     Test that only a formatting diagnostic is fixable.
    /// </summary>
    [Fact]
    public void Diagnostic_Fixable_ByKind_FormattingOnly()
    {
        // Act & Assert: rewriting removes a formatting finding and not a lint-only one
        Assert.True(Create(DiagnosticKind.Formatting).Fixable);
        Assert.False(Create(DiagnosticKind.LintOnly).Fixable);
    }

    /// <summary>
    ///     Test that equal diagnostics collapse to one entry in a hash set and different ones do not.
    /// </summary>
    [Fact]
    public void Diagnostic_Equals_SameValues_CollapseInHashSet()
    {
        // Arrange: two equal and one different diagnostic
        var set = new HashSet<Diagnostic> { Create(), Create(), Create(column: 8) };

        // Act & Assert: only the distinct values remain
        Assert.Equal(2, set.Count);
    }
}
