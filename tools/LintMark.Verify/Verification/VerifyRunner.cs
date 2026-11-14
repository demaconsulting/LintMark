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

using System.Collections.Concurrent;
using System.Diagnostics;
using DemaConsulting.LintMark.Configuration;
using DemaConsulting.LintMark.Engine;
using DemaConsulting.LintMark.Verify.Options;

namespace DemaConsulting.LintMark.Verify.Verification;

/// <summary>
///     Verifies every file under every option set in parallel.
/// </summary>
/// <remarks>
///     <para>
///         The work items are the pairs of a file and an option set. Each runs on a pool thread under a time
///         limit: a verification that exceeds it is recorded as a <see cref="OutcomeKind.Timeout"/> and the run
///         goes on. A runaway computation cannot be stopped from inside the process, so it keeps its thread
///         until it ends; the summary counts such abandoned workers.
///     </para>
///     <para>
///         Progress is written to the progress writer at most every 15 seconds. Memory stays small: a pass is
///         only counted, and at most <see cref="MaxStoredFailures"/> failures keep their evidence. Nothing here
///         writes to a file.
///     </para>
/// </remarks>
internal sealed class VerifyRunner
{
    /// <summary>
    ///     The largest number of failures that keep their evidence for the reports.
    /// </summary>
    public const int MaxStoredFailures = 5000;

    /// <summary>
    ///     The time between two progress lines.
    /// </summary>
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromSeconds(15);

    /// <summary>
    ///     The verifier that handles one pair.
    /// </summary>
    private readonly FileVerifier _verifier;

    /// <summary>
    ///     The degree of parallelism.
    /// </summary>
    private readonly int _jobs;

    /// <summary>
    ///     The time limit for one pair.
    /// </summary>
    private readonly TimeSpan _timeout;

    /// <summary>
    ///     Receives the progress lines, or <see langword="null"/> for none.
    /// </summary>
    private readonly TextWriter? _progress;

    /// <summary>
    ///     Serializes the progress lines.
    /// </summary>
    private readonly object _progressLock = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="VerifyRunner"/> class.
    /// </summary>
    /// <param name="verifier">The verifier of one pair. Must not be null.</param>
    /// <param name="jobs">The degree of parallelism, at least 1.</param>
    /// <param name="timeout">The time limit for one pair; must be positive.</param>
    /// <param name="progress">Receives the progress lines, or <see langword="null"/> for none.</param>
    public VerifyRunner(FileVerifier verifier, int jobs, TimeSpan timeout, TextWriter? progress = null)
    {
        ArgumentNullException.ThrowIfNull(verifier);
        ArgumentOutOfRangeException.ThrowIfLessThan(jobs, 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        _verifier = verifier;
        _jobs = jobs;
        _timeout = timeout;
        _progress = progress;
    }

    /// <summary>
    ///     Runs the verification.
    /// </summary>
    /// <param name="files">The files. Must not be null.</param>
    /// <param name="sets">The option sets, at least one. Must not be null.</param>
    /// <param name="baseDirectory">The directory that relative ignore patterns apply to.</param>
    /// <returns>The totals and the failures.</returns>
    /// <exception cref="InvalidOperationException">Thrown when an option set is invalid.</exception>
    public async Task<RunSummary> RunAsync(IReadOnlyList<DiscoveredFile> files, IReadOnlyList<OptionSet> sets, string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(sets);
        ArgumentOutOfRangeException.ThrowIfZero(sets.Count);
        var clock = Stopwatch.StartNew();

        // Create the configuration of every set up front: an invalid one stops the run before any work
        var loaders = sets.Select(static set => set.CreateLoader()).ToArray();
        var editorConfigs = sets.Select(static set => set.DiscoverPerFile ? new EditorConfigResolver() : null).ToArray();

        // The shared tallies; every worker only adds to them
        var kindCounts = new ConcurrentDictionary<OutcomeKind, int>();
        var findingCounts = new ConcurrentDictionary<OutcomeKind, int>();
        var perSet = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        var failures = new ConcurrentBag<Outcome>();
        var stored = 0;
        var done = 0;
        var abandoned = 0;
        var total = (long)files.Count * sets.Count;
        var nextProgress = ProgressInterval;

        var items = files.SelectMany(file => Enumerable.Range(0, sets.Count).Select(index => (file, index)));
        var options = new ParallelOptions { MaxDegreeOfParallelism = _jobs };
        await Parallel.ForEachAsync(items, options, async (item, cancellation) =>
        {
            var (file, index) = item;
            var set = sets[index];
            var target = set.DiscoverPerFile ? file : new DiscoveredFile(file.Path, isExplicit: true);

            // Run the pair on a pool thread under the time limit
            var work = Task.Run(() => _verifier.Verify(target, set.Label, loaders[index], editorConfigs[index], baseDirectory), CancellationToken.None);
            Outcome outcome;
            try
            {
                outcome = await work.WaitAsync(_timeout, cancellation);
            }
            catch (TimeoutException)
            {
                Interlocked.Increment(ref abandoned);
                outcome = Outcome.Failure(file.Path, set.Label, OutcomeKind.Timeout, $"The verification took longer than {_timeout.TotalSeconds:0} seconds");
            }

            // Tally the outcome and keep the evidence of a failure
            kindCounts.AddOrUpdate(outcome.Kind, 1, static (_, count) => count + 1);
            if (outcome.IsFailure)
            {
                perSet.AddOrUpdate(set.Label, 1, static (_, count) => count + 1);
                foreach (var finding in outcome.Findings)
                {
                    findingCounts.AddOrUpdate(finding.Kind, 1, static (_, count) => count + 1);
                }

                if (Interlocked.Increment(ref stored) <= MaxStoredFailures)
                {
                    failures.Add(outcome);
                }
            }

            ReportProgress(Interlocked.Increment(ref done), total, kindCounts, clock, ref nextProgress);
        });

        // Order the failures so that the report does not depend on scheduling
        var ordered = failures
            .OrderBy(static f => f.Path, StringComparer.Ordinal)
            .ThenBy(static f => f.OptionLabel, StringComparer.Ordinal)
            .ToList();
        return new RunSummary(
            files.Count,
            [.. sets.Select(static s => s.Label)],
            kindCounts.ToDictionary(),
            findingCounts.ToDictionary(),
            perSet.ToDictionary(),
            ordered,
            clock.Elapsed,
            abandoned);
    }

    /// <summary>
    ///     Writes a progress line when the interval has passed.
    /// </summary>
    /// <param name="done">The number of pairs finished.</param>
    /// <param name="total">The number of pairs.</param>
    /// <param name="kindCounts">The tallies.</param>
    /// <param name="clock">The clock of the run.</param>
    /// <param name="nextProgress">The elapsed time at which the next line is due; moved forward when a line is written.</param>
    private void ReportProgress(int done, long total, ConcurrentDictionary<OutcomeKind, int> kindCounts, Stopwatch clock, ref TimeSpan nextProgress)
    {
        if (_progress is null || clock.Elapsed < nextProgress)
        {
            return;
        }

        lock (_progressLock)
        {
            if (clock.Elapsed < nextProgress)
            {
                return;
            }

            nextProgress = clock.Elapsed + ProgressInterval;
            var failed = kindCounts.Where(static pair => pair.Key.IsFailure()).Sum(static pair => pair.Value);
            _progress.WriteLine($"[{clock.Elapsed:hh\\:mm\\:ss}] {done}/{total} verified, {failed} failed");
        }
    }
}
