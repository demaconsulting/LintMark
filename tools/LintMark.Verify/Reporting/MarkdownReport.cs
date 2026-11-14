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
using DemaConsulting.LintMark.Verify.Verification;

namespace DemaConsulting.LintMark.Verify.Reporting;

/// <summary>
///     Renders the Markdown report of a run.
/// </summary>
/// <remarks>
///     The report holds the totals, the failures per option set and the evidence of every stored failure. Every
///     diff is fenced with a fence longer than any run of backticks inside it, so the report is itself valid
///     Markdown whatever the corpus contained. The text uses LF line endings. Stateless and thread-safe.
/// </remarks>
internal static class MarkdownReport
{
    /// <summary>
    ///     Renders the report.
    /// </summary>
    /// <param name="summary">The totals of the run. Must not be null.</param>
    /// <param name="baseDirectory">The directory that paths are shown relative to. Must not be null.</param>
    /// <returns>The Markdown text, ending with a line feed.</returns>
    public static string Render(RunSummary summary, string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(baseDirectory);
        var text = new StringBuilder();

        // Totals
        text.Append("# LintMark verification report\n\n");
        text.Append(summary.FailureCount == 0 ? "**Result**: PASSED\n\n" : "**Result**: FAILED\n\n");
        text.Append($"- Files verified: {summary.Files}\n");
        text.Append($"- Option sets: {summary.OptionLabels.Count}\n");
        text.Append($"- Checks (file x option set): {summary.Total}\n");
        text.Append($"- Passed: {summary.PassCount} (refusals by design included)\n");
        text.Append($"- Failed: {summary.FailureCount}\n");
        text.Append($"- Elapsed: {summary.Elapsed:hh\\:mm\\:ss}\n");
        if (summary.AbandonedWorkers > 0)
        {
            text.Append($"- Abandoned workers after a timeout: {summary.AbandonedWorkers}\n");
        }

        text.Append("\n## Outcomes\n\n| Outcome | Count |\n| --- | ---: |\n");
        foreach (var (kind, count) in summary.KindCounts.OrderBy(static pair => pair.Key))
        {
            text.Append($"| {kind.Name()} | {count} |\n");
        }

        // The failures per option set, when there are several
        if (summary.OptionLabels.Count > 1)
        {
            text.Append("\n## Failures per option set\n\n| Option set | Failed |\n| --- | ---: |\n");
            foreach (var label in summary.OptionLabels)
            {
                text.Append($"| {Escape(label)} | {summary.FailuresPerSet.GetValueOrDefault(label)} |\n");
            }
        }

        // The evidence of each failure
        if (summary.Failures.Count > 0)
        {
            text.Append("\n## Failures\n");
            foreach (var failure in summary.Failures)
            {
                AppendFailure(text, failure, baseDirectory);
            }

            if (summary.UnlistedFailures > 0)
            {
                text.Append($"\n{summary.UnlistedFailures} more failure(s) are not listed.\n");
            }
        }

        return text.ToString();
    }

    /// <summary>
    ///     Appends the section of one failure.
    /// </summary>
    /// <param name="text">The report.</param>
    /// <param name="failure">The failed pair.</param>
    /// <param name="baseDirectory">The directory that paths are shown relative to.</param>
    private static void AppendFailure(StringBuilder text, Outcome failure, string baseDirectory)
    {
        text.Append($"\n### {Escape(DisplayPath.Of(failure.Path, baseDirectory))}: {failure.Kind.Name()}\n\n");
        text.Append($"Option set: {Escape(failure.OptionLabel)}\n");
        foreach (var finding in failure.Findings)
        {
            text.Append($"\n**{finding.Kind.Name()}**: {Escape(finding.Description)}\n");
            AppendFence(text, "Formatted text diff", "diff", finding.TextDiff);
            AppendFence(text, "HTML difference", "diff", finding.HtmlDiff);
        }
    }

    /// <summary>
    ///     Appends a titled fenced block.
    /// </summary>
    /// <param name="text">The report.</param>
    /// <param name="title">The title.</param>
    /// <param name="language">The info string of the fence.</param>
    /// <param name="lines">The lines; nothing is appended when there are none.</param>
    private static void AppendFence(StringBuilder text, string title, string language, IReadOnlyList<string> lines)
    {
        if (lines.Count == 0)
        {
            return;
        }

        // A fence longer than any run of backticks in the content cannot be closed by the content
        var fence = new string('`', Math.Max(3, lines.Max(LongestBacktickRun) + 1));
        text.Append($"\n{title}:\n\n{fence}{language}\n");
        foreach (var line in lines)
        {
            text.Append(line).Append('\n');
        }

        text.Append(fence).Append('\n');
    }

    /// <summary>
    ///     Finds the longest run of backticks in a line.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <returns>The length of the longest run.</returns>
    private static int LongestBacktickRun(string line)
    {
        int longest = 0, current = 0;
        foreach (var c in line)
        {
            current = c == '`' ? current + 1 : 0;
            longest = Math.Max(longest, current);
        }

        return longest;
    }

    /// <summary>
    ///     Makes text safe to put in a line of the report: no line breaks, and no characters that start Markdown constructs.
    /// </summary>
    /// <param name="value">The text.</param>
    /// <returns>The escaped text.</returns>
    private static string Escape(string value)
    {
        var flat = value.ReplaceLineEndings(" ");
        var escaped = new StringBuilder(flat.Length);
        foreach (var c in flat)
        {
            if (c is '\\' or '`' or '*' or '_' or '[' or ']' or '<' or '>' or '|' or '#')
            {
                escaped.Append('\\');
            }

            escaped.Append(c);
        }

        return escaped.ToString();
    }
}
