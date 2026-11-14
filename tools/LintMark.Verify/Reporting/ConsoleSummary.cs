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

using DemaConsulting.LintMark.Verify.Verification;

namespace DemaConsulting.LintMark.Verify.Reporting;

/// <summary>
///     Writes the summary and the failure details of a run to the console.
/// </summary>
/// <remarks>
///     The summary lists the totals, the number of failures per kind, the exempt (by design) refusals and, when
///     several option sets ran, the failures per set. The details of the first few failures follow; the report
///     file holds all of them. Stateless and thread-safe.
/// </remarks>
internal static class ConsoleSummary
{
    /// <summary>
    ///     The largest number of failures whose details are written to the console.
    /// </summary>
    internal const int MaxDetailedFailures = 20;

    /// <summary>
    ///     Writes the summary and the details.
    /// </summary>
    /// <param name="writer">The writer. Must not be null.</param>
    /// <param name="summary">The totals of the run. Must not be null.</param>
    /// <param name="baseDirectory">The directory that paths are shown relative to. Must not be null.</param>
    public static void Write(TextWriter writer, RunSummary summary, string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(baseDirectory);

        // Totals
        writer.WriteLine();
        writer.WriteLine($"Verified {summary.Files} files under {summary.OptionLabels.Count} option set(s): {summary.Total} checks in {summary.Elapsed:hh\\:mm\\:ss}.");
        writer.WriteLine($"Passed: {summary.PassCount}  Failed: {summary.FailureCount}");
        foreach (var line in KindLines(summary))
        {
            writer.WriteLine("  " + line);
        }

        // The results per corpus category and source, when the files come from a corpus or several places
        if (summary.IsGrouped)
        {
            writer.WriteLine("By category (files, checks passed, checks failed; failures by type):");
            foreach (var line in GroupLines(summary.Categories))
            {
                writer.WriteLine("  " + line);
            }

            writer.WriteLine("By source:");
            foreach (var line in GroupLines(summary.Sources))
            {
                writer.WriteLine("  " + line);
            }
        }

        // The failures per option set matter when there are several
        if (summary.OptionLabels.Count > 1)
        {
            writer.WriteLine("Failures per option set:");
            foreach (var label in summary.OptionLabels)
            {
                writer.WriteLine($"  {summary.FailuresPerSet.GetValueOrDefault(label)}  {label}");
            }
        }

        if (summary.AbandonedWorkers > 0)
        {
            writer.WriteLine($"Warning: {summary.AbandonedWorkers} timed-out verification(s) could not be stopped and may still be running.");
        }

        // The details of the first failures
        foreach (var failure in summary.Failures.Take(MaxDetailedFailures))
        {
            writer.WriteLine();
            writer.WriteLine($"FAIL {failure.Kind.Name()}  {DisplayPath.Of(failure.Path, baseDirectory)}  [{failure.OptionLabel}]");
            foreach (var finding in failure.Findings)
            {
                writer.WriteLine($"  {finding.Kind.Name()}: {finding.Description}");
                WriteBlock(writer, "formatted text diff", finding.TextDiff);
                WriteBlock(writer, "HTML difference", finding.HtmlDiff);
            }
        }

        var unlisted = summary.FailureCount - Math.Min(summary.Failures.Count, MaxDetailedFailures);
        if (unlisted > 0)
        {
            writer.WriteLine();
            writer.WriteLine($"... and {unlisted} more failure(s); use --report <file> for the details.");
        }

        writer.WriteLine();
        writer.WriteLine(summary.FailureCount == 0 ? "RESULT: PASSED" : "RESULT: FAILED");
    }

    /// <summary>
    ///     Creates the lines that name the number of pairs for each kind and the findings for each failure kind.
    /// </summary>
    /// <param name="summary">The totals.</param>
    /// <returns>The lines, passes first.</returns>
    internal static IEnumerable<string> KindLines(RunSummary summary)
    {
        foreach (var (kind, count) in summary.KindCounts.OrderBy(static pair => pair.Key))
        {
            var note = kind.IsExempt() ? " (refused by design, counts as a pass)" : string.Empty;
            yield return $"{count,8}  {kind.Name()}{note}";
        }

        // A pair can fail several checks; the per-kind counts of findings are shown when they add information
        if (summary.FindingCounts.Count > 0)
        {
            yield return "Findings by check: " + string.Join(", ", summary.FindingCounts.OrderBy(static pair => pair.Key).Select(static pair => $"{pair.Key.Name()} {pair.Value}"));
        }
    }

    /// <summary>
    ///     Creates the line of each group: the name, the number of files, the passed and failed checks and the failures by type.
    /// </summary>
    /// <param name="groups">The groups.</param>
    /// <returns>The lines.</returns>
    internal static IEnumerable<string> GroupLines(IReadOnlyList<GroupSummary> groups)
    {
        var width = groups.Count == 0 ? 0 : groups.Max(static g => g.Group.Source.Length);
        foreach (var group in groups)
        {
            var types = string.Join(", ", group.FailuresByKind.Select(static pair => $"{pair.Key.Name()} {pair.Value}"));
            var line = $"{group.Group.Source.PadRight(width)}  {group.Files,6} files  {group.PassCount,7} passed  {group.FailureCount,6} failed";
            yield return types.Length == 0 ? line : $"{line}  ({types})";
        }
    }

    /// <summary>
    ///     Writes a titled block of lines.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="title">The title.</param>
    /// <param name="lines">The lines; nothing is written when there are none.</param>
    private static void WriteBlock(TextWriter writer, string title, IReadOnlyList<string> lines)
    {
        if (lines.Count == 0)
        {
            return;
        }

        writer.WriteLine($"    {title}:");
        foreach (var line in lines)
        {
            writer.WriteLine("      " + line);
        }
    }
}
