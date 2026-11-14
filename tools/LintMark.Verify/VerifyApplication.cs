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
using DemaConsulting.LintMark.Verify.Cli;
using DemaConsulting.LintMark.Verify.Options;
using DemaConsulting.LintMark.Verify.Reporting;
using DemaConsulting.LintMark.Verify.Verification;

namespace DemaConsulting.LintMark.Verify;

/// <summary>
///     Runs the verifier: selects the files and option sets, verifies, and writes the summary and the report.
/// </summary>
/// <remarks>
///     The exit code is 0 when every check passed (a refusal by design is a pass), 1 when at least one failed
///     and 2 for a usage or setup error. The inputs are only ever read; the only file this class writes is the
///     report, and it refuses to write a report over an input. Stateless and thread-safe.
/// </remarks>
internal static class VerifyApplication
{
    /// <summary>
    ///     The exit code of a run in which every check passed.
    /// </summary>
    public const int Passed = 0;

    /// <summary>
    ///     The exit code of a run with at least one failure.
    /// </summary>
    public const int Failed = 1;

    /// <summary>
    ///     The exit code of a usage or setup error.
    /// </summary>
    public const int UsageError = 2;

    /// <summary>
    ///     Runs the verifier.
    /// </summary>
    /// <param name="options">The command line. Must not be null.</param>
    /// <param name="output">Receives the summary. Must not be null.</param>
    /// <param name="error">Receives the problems and the progress. Must not be null.</param>
    /// <param name="baseDirectory">The directory that relative paths are resolved against and shown relative to. Must not be null.</param>
    /// <param name="verifier">The verifier of one file (a seam for tests); <see langword="null"/> selects the production formatter.</param>
    /// <returns>The exit code.</returns>
    public static async Task<int> RunAsync(VerifyOptions options, TextWriter output, TextWriter error, string baseDirectory, FileVerifier? verifier = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(baseDirectory);

        if (options.Help)
        {
            await output.WriteLineAsync(VerifyOptions.Usage);
            return Passed;
        }

        try
        {
            // Select the files and the option sets
            var files = FileSelection.Select(options, baseDirectory);
            if (files.Count == 0)
            {
                throw new UsageException("No Markdown files found.");
            }

            if (options.Sample is { } sample)
            {
                files = FileSelection.Sample(files, sample);
            }

            var reportPath = options.ReportPath is null ? null : Path.GetFullPath(Path.Combine(baseDirectory, options.ReportPath));
            if (reportPath is not null && files.Any(file => string.Equals(file.Path, reportPath, StringComparison.OrdinalIgnoreCase)))
            {
                throw new UsageException("The report path is one of the files to verify; inputs are never written.");
            }

            var sets = OptionMatrix.Create(options);

            // Verify
            var runner = new VerifyRunner(
                verifier ?? new FileVerifier(maxDiffLines: options.MaxDiffLines),
                options.Jobs,
                TimeSpan.FromSeconds(options.TimeoutSeconds),
                error);
            var summary = await runner.RunAsync(files, sets, baseDirectory, options.Dema ? Path.GetFullPath(options.DemaRoot) : null);

            // Report
            ConsoleSummary.Write(output, summary, baseDirectory);
            if (reportPath is not null)
            {
                await File.WriteAllTextAsync(reportPath, MarkdownReport.Render(summary, baseDirectory), new UTF8Encoding(false));
                await output.WriteLineAsync($"Report written to {reportPath}");
            }

            return summary.FailureCount == 0 ? Passed : Failed;
        }
        catch (UsageException ex)
        {
            await error.WriteLineAsync($"error: {ex.Message}");
            return UsageError;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            await error.WriteLineAsync($"error: {ex.Message}");
            return UsageError;
        }
    }
}
