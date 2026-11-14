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

namespace DemaConsulting.LintMark.Verify.Verification;

/// <summary>
///     The totals of a verification run.
/// </summary>
/// <param name="Files">The number of files verified.</param>
/// <param name="OptionLabels">The labels of the option sets, in run order.</param>
/// <param name="KindCounts">The number of (file, option set) pairs for each primary outcome kind.</param>
/// <param name="FindingCounts">The number of findings for each failure kind (one pair can have several).</param>
/// <param name="FailuresPerSet">The number of failed pairs for each option set label.</param>
/// <param name="Failures">The failed pairs in path order; at most <see cref="VerifyRunner.MaxStoredFailures"/>.</param>
/// <param name="Elapsed">The time the run took.</param>
/// <param name="AbandonedWorkers">The number of timed-out verifications that could not be stopped and may still be running.</param>
/// <param name="Sources">The results per corpus source (see <see cref="CorpusGroup"/>), in ordinal order of the source.</param>
internal sealed record RunSummary(
    int Files,
    IReadOnlyList<string> OptionLabels,
    IReadOnlyDictionary<OutcomeKind, int> KindCounts,
    IReadOnlyDictionary<OutcomeKind, int> FindingCounts,
    IReadOnlyDictionary<string, int> FailuresPerSet,
    IReadOnlyList<Outcome> Failures,
    TimeSpan Elapsed,
    int AbandonedWorkers,
    IReadOnlyList<GroupSummary> Sources)
{
    /// <summary>
    ///     Gets the results per corpus category, in ordinal order.
    /// </summary>
    public IReadOnlyList<GroupSummary> Categories => GroupSummary.ByCategory(Sources);

    /// <summary>
    ///     Gets a value indicating whether the results are worth showing per category and source: the files come
    ///     from several groups, or from a corpus or DEMA group.
    /// </summary>
    public bool IsGrouped => Sources.Count > 1 || Sources.Any(static s => s.Group.Category != CorpusGroup.Other);

    /// <summary>
    ///     Gets the number of (file, option set) pairs verified.
    /// </summary>
    public int Total => KindCounts.Values.Sum();

    /// <summary>
    ///     Gets the number of failed pairs.
    /// </summary>
    public int FailureCount => KindCounts.Where(static pair => pair.Key.IsFailure()).Sum(static pair => pair.Value);

    /// <summary>
    ///     Gets the number of passed pairs, refusals by design included.
    /// </summary>
    public int PassCount => Total - FailureCount;

    /// <summary>
    ///     Gets the number of failed pairs that are not listed in <see cref="Failures"/>.
    /// </summary>
    public int UnlistedFailures => FailureCount - Failures.Count;
}
