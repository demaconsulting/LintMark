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

using System.Diagnostics;
using System.Text;
using DemaConsulting.LintMark.Engine;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Performance regression tests for the LintMarkEngine class: the time for a document must grow in proportion to its size.
/// </summary>
public partial class LintMarkEngineTests
{
    /// <summary>
    ///     Builds a document with a mix of every construct that formatting and linting walk over, in unformatted form.
    /// </summary>
    /// <param name="units">The number of repeated units, each about half a kilobyte.</param>
    /// <returns>The document.</returns>
    private static string BuildLargeDocument(int units)
    {
        var text = new StringBuilder();
        for (var i = 0; i < units; i++)
        {
            text.Append($"## Section {i}\n\n");
            text.Append($"A paragraph with   extra spaces, *emphasis*, a [link][ref{i % 7}] and some `code`. It is long enough that it is wrapped, ");
            text.Append("because it keeps going for more than a hundred and twenty columns without any real need for it to do so.\n\n");
            text.Append("* first item with a continuation\n  that follows it\n* second item\n    * nested item\n\n");
            text.Append("> quoted text that is also\n> spread over two lines\n\n");
            text.Append("| Name | Value |\n|--|--|\n| a | 1 |\n| bb | 22 |\n\n");
            text.Append("```text\ncode   that stays\n```\n\n");
        }

        for (var i = 0; i < 7; i++)
        {
            text.Append($"[ref{i}]: https://example.com/{i}\n");
        }

        return text.ToString();
    }

    /// <summary>
    ///     Measures the best time of a few runs of the engine over one file.
    /// </summary>
    /// <param name="engine">The engine.</param>
    /// <param name="path">The file.</param>
    /// <returns>The shortest elapsed time.</returns>
    private static TimeSpan BestTime(LintMarkEngine engine, string path)
    {
        var best = TimeSpan.MaxValue;
        for (var run = 0; run < 3; run++)
        {
            var watch = Stopwatch.StartNew();
            var result = Assert.Single(engine.Process([path]));
            watch.Stop();
            Assert.NotNull(result.Config);
            if (watch.Elapsed < best)
            {
                best = watch.Elapsed;
            }
        }

        return best;
    }

    /// <summary>
    ///     Test that formatting and linting a large document grows in proportion to its size (a quadratic algorithm would
    ///     need about sixteen times as long for four times the size) and finishes within a generous limit.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Process_LargeDocument_TimeGrowsInProportionToSize()
    {
        // Arrange: a document and one four times as large (about 0.5 MB and 2 MB), and a warm-up so that start-up cost is not measured
        using var directory = new TemporaryDirectory();
        var small = directory.WriteFile("small.md", BuildLargeDocument(900));
        var large = directory.WriteFile("large.md", BuildLargeDocument(3600));
        var engine = new LintMarkEngine(1);
        engine.Process([directory.WriteFile("warmup.md", BuildLargeDocument(20))]);

        // Act: take the best of several runs of each, which keeps a busy machine from deciding the outcome
        var smallTime = BestTime(engine, small);
        var largeTime = BestTime(engine, large);

        // Assert: well within the limit, and far from quadratic growth (linear growth is four times; the allowance is ten)
        Assert.True(largeTime < TimeSpan.FromSeconds(30), $"The large document took {largeTime}");
        Assert.True(
            largeTime.TotalMilliseconds < 10 * Math.Max(smallTime.TotalMilliseconds, 50),
            $"The large document took {largeTime} against {smallTime} for a quarter of its size");
    }
}
