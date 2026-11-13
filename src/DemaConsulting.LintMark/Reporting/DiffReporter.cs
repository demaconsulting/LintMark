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

using DemaConsulting.LintMark.Cli;
using DemaConsulting.LintMark.Document;

namespace DemaConsulting.LintMark.Reporting;

/// <summary>
///     Writes what the fix option would change as a unified diff.
/// </summary>
/// <remarks>
///     <para>
///         The output follows the unified diff conventions that patch tools understand: for each changed file
///         a <c>--- a/path</c> and <c>+++ b/path</c> header and hunks introduced by
///         <c>@@ -start,count +start,count @@</c> with three lines of context (a count of one is written
///         without the comma, and an empty range starts at the line before it, as GNU diff does).
///         A last line without a line ending is followed by <c>\ No newline at end of file</c>. Hunks whose
///         context would touch are merged.
///     </para>
///     <para>
///         The texts are LF-normalized, so a change of line endings alone leaves them equal. It is shown by a
///         note line <c>\ Line endings change from X to Y</c> after the headers, using the comment-line
///         convention of the no-newline marker, so a person sees why a file with no visible change is
///         rewritten. A file with neither a text change nor a line ending change writes nothing.
///     </para>
///     <para>
///         The diff is for display only; the decision to write a file never depends on it. The differences are
///         found with the Myers algorithm on whole lines. When two files are so different that the search
///         would become expensive, the unchanged head and tail are kept and the rest is shown as one removal
///         and one addition, which is still a correct, if not minimal, diff.
///     </para>
///     <para>
///         Output goes through <see cref="Context.WriteLine"/> (standard output; silent mode suppresses the
///         console but not the log), files in display-path order. Thread safety: static and stateless; safe
///         for concurrent use (the context serializes its own output).
///     </para>
/// </remarks>
internal static class DiffReporter
{
    /// <summary>
    ///     The number of unchanged lines shown around each change.
    /// </summary>
    private const int ContextLines = 3;

    /// <summary>
    ///     The largest edit distance searched for before falling back to a whole-block replacement.
    /// </summary>
    private const int MaxEditDistance = 2000;

    /// <summary>
    ///     The kinds of line in a diff.
    /// </summary>
    private enum Kind
    {
        /// <summary>The line is in both texts.</summary>
        Equal,

        /// <summary>The line is only in the original text.</summary>
        Delete,

        /// <summary>The line is only in the new text.</summary>
        Insert
    }

    /// <summary>
    ///     Writes the diffs of the changed files to standard output.
    /// </summary>
    /// <param name="diffs">What would change in each file, in any order. Must not be null; elements must be initialized.</param>
    /// <param name="baseDirectory">The directory that paths are shown relative to. Must not be null.</param>
    /// <param name="context">The context that receives the lines. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when an element of <paramref name="diffs"/> is a default instance.</exception>
    public static void Report(IEnumerable<FileDiff> diffs, string baseDirectory, Context context)
    {
        // Validate input; the elements are checked before anything is written
        ArgumentNullException.ThrowIfNull(diffs);
        ArgumentNullException.ThrowIfNull(baseDirectory);
        ArgumentNullException.ThrowIfNull(context);
        var items = diffs.ToList();
        if (items.Any(static diff => diff.Path is null || diff.BeforeLf is null || diff.AfterLf is null))
        {
            throw new ArgumentException("A diff was not created with its constructor.", nameof(diffs));
        }

        // Write the files in display-path order, each as one block of lines
        foreach (var diff in items.OrderBy(diff => TextReporter.GetDisplayPath(diff.Path, baseDirectory), StringComparer.Ordinal))
        {
            foreach (var line in Format(diff, TextReporter.GetDisplayPath(diff.Path, baseDirectory)))
            {
                context.WriteLine(line);
            }
        }
    }

    /// <summary>
    ///     Formats the diff of one file.
    /// </summary>
    /// <param name="diff">What would change in the file.</param>
    /// <param name="displayPath">The path to show (relative, forward slashes).</param>
    /// <returns>The lines of the diff; empty when nothing would change.</returns>
    internal static List<string> Format(FileDiff diff, string displayPath)
    {
        var lines = new List<string>();

        // A change of line endings alone leaves the LF-normalized texts equal
        var endingChange = IsEndingChange(diff);
        var textChange = !string.Equals(diff.BeforeLf, diff.AfterLf, StringComparison.Ordinal);
        if (!endingChange && !textChange)
        {
            return lines;
        }

        lines.Add($"--- a/{displayPath}");
        lines.Add($"+++ b/{displayPath}");
        if (endingChange)
        {
            lines.Add($"\\ Line endings change from {DescribeEndings(diff.OriginalEndings)} to {DescribeEnding(diff.TargetEnding)}");
        }

        if (textChange)
        {
            AddHunks(lines, Compare(SplitLines(diff.BeforeLf), SplitLines(diff.AfterLf)));
        }

        return lines;
    }

    /// <summary>
    ///     Determines whether writing the file would change a line ending.
    /// </summary>
    /// <param name="diff">What would change in the file.</param>
    /// <returns><see langword="true"/> when a terminator differs from the target ending.</returns>
    private static bool IsEndingChange(FileDiff diff) =>
        diff.TargetEnding == LineEnding.Lf ? diff.OriginalEndings.CrLfCount > 0 : diff.OriginalEndings.LfCount > 0;

    /// <summary>
    ///     Names the line endings a file has now.
    /// </summary>
    /// <param name="endings">The counts of the terminators.</param>
    /// <returns><c>mixed (n CRLF, m LF)</c>, <c>CRLF</c> or <c>LF</c>.</returns>
    private static string DescribeEndings(LineEndingInfo endings)
    {
        if (endings.IsMixed)
        {
            return $"mixed ({endings.CrLfCount} CRLF, {endings.LfCount} LF)";
        }

        return DescribeEnding(endings.CrLfCount > 0 ? LineEnding.CrLf : LineEnding.Lf);
    }

    /// <summary>
    ///     Names a line ending.
    /// </summary>
    /// <param name="ending">The line ending.</param>
    /// <returns><c>CRLF</c> or <c>LF</c>.</returns>
    private static string DescribeEnding(LineEnding ending) => ending == LineEnding.CrLf ? "CRLF" : "LF";

    /// <summary>
    ///     Splits text into lines that keep their terminator, so a missing final line ending is a difference.
    /// </summary>
    /// <param name="text">The LF-normalized text.</param>
    /// <returns>The lines; none for empty text.</returns>
    private static string[] SplitLines(string text)
    {
        var lines = new List<string>();
        var start = 0;
        while (start < text.Length)
        {
            var end = text.IndexOf('\n', start);
            end = end < 0 ? text.Length : end + 1;
            lines.Add(text[start..end]);
            start = end;
        }

        return [.. lines];
    }

    /// <summary>
    ///     Computes the line differences between two versions.
    /// </summary>
    /// <param name="before">The original lines.</param>
    /// <param name="after">The new lines.</param>
    /// <returns>The script of equal, deleted and inserted lines, in order.</returns>
    private static List<(Kind Kind, string Line)> Compare(string[] before, string[] after)
    {
        // Keep the common head and tail out of the search
        var head = 0;
        while (head < before.Length && head < after.Length && before[head] == after[head])
        {
            head++;
        }

        var tail = 0;
        while (tail < before.Length - head && tail < after.Length - head
               && before[before.Length - 1 - tail] == after[after.Length - 1 - tail])
        {
            tail++;
        }

        var script = new List<(Kind, string)>();
        for (var i = 0; i < head; i++)
        {
            script.Add((Kind.Equal, before[i]));
        }

        // Search the middle; a search that grows too expensive is replaced by one removal and one addition
        var middleBefore = before[head..(before.Length - tail)];
        var middleAfter = after[head..(after.Length - tail)];
        script.AddRange(Myers(middleBefore, middleAfter) ?? ReplaceAll(middleBefore, middleAfter));

        for (var i = before.Length - tail; i < before.Length; i++)
        {
            script.Add((Kind.Equal, before[i]));
        }

        return script;
    }

    /// <summary>
    ///     Shows all of one block as removed and all of the other as added.
    /// </summary>
    /// <param name="before">The original lines.</param>
    /// <param name="after">The new lines.</param>
    /// <returns>The script.</returns>
    private static List<(Kind Kind, string Line)> ReplaceAll(string[] before, string[] after) =>
    [
        .. before.Select(static line => (Kind.Delete, line)),
        .. after.Select(static line => (Kind.Insert, line))
    ];

    /// <summary>
    ///     Finds a shortest script with the Myers algorithm, within the edit distance limit.
    /// </summary>
    /// <param name="a">The original lines.</param>
    /// <param name="b">The new lines.</param>
    /// <returns>The script, or <see langword="null"/> when the edit distance exceeds <see cref="MaxEditDistance"/>.</returns>
    private static List<(Kind Kind, string Line)>? Myers(string[] a, string[] b)
    {
        // Trivial cases: one side is empty
        var n = a.Length;
        var m = b.Length;
        if (n == 0 || m == 0)
        {
            return ReplaceAll(a, b);
        }

        // Forward search: the furthest x reached on each diagonal k = x - y, remembered per step for the walk back
        var limit = Math.Min(n + m, MaxEditDistance);
        var offset = limit + 1;
        var v = new int[(2 * limit) + 3];
        var trace = new List<int[]>();
        for (var d = 0; d <= limit; d++)
        {
            for (var k = -d; k <= d; k += 2)
            {
                var x = k == -d || (k != d && v[offset + k - 1] < v[offset + k + 1])
                    ? v[offset + k + 1]
                    : v[offset + k - 1] + 1;
                var y = x - k;
                while (x < n && y < m && a[x] == b[y])
                {
                    x++;
                    y++;
                }

                v[offset + k] = x;
                if (x >= n && y >= m)
                {
                    trace.Add(Slice(v, offset, d));
                    return WalkBack(a, b, trace, d);
                }
            }

            trace.Add(Slice(v, offset, d));
        }

        return null;
    }

    /// <summary>
    ///     Copies the furthest-reaching values of diagonals <c>-d</c> to <c>d</c>.
    /// </summary>
    /// <param name="v">The search array.</param>
    /// <param name="offset">The index of diagonal zero.</param>
    /// <param name="d">The number of edits of the step.</param>
    /// <returns>The values, indexed by <c>k + d</c>.</returns>
    private static int[] Slice(int[] v, int offset, int d) => v[(offset - d)..(offset + d + 1)];

    /// <summary>
    ///     Reconstructs the script from the remembered search steps.
    /// </summary>
    /// <param name="a">The original lines.</param>
    /// <param name="b">The new lines.</param>
    /// <param name="trace">The values remembered per step.</param>
    /// <param name="distance">The number of edits of the shortest script.</param>
    /// <returns>The script, in order.</returns>
    private static List<(Kind Kind, string Line)> WalkBack(string[] a, string[] b, List<int[]> trace, int distance)
    {
        var reversed = new List<(Kind, string)>();
        var x = a.Length;
        var y = b.Length;
        for (var d = distance; d > 0; d--)
        {
            // Find the diagonal the last edit came from
            var previous = trace[d - 1];
            var k = x - y;
            var previousK = k == -d || (k != d && previous[k - 1 + d - 1] < previous[k + 1 + d - 1]) ? k + 1 : k - 1;
            var previousX = previous[previousK + d - 1];
            var previousY = previousX - previousK;

            // The lines after the edit were equal
            while (x > previousX && y > previousY)
            {
                reversed.Add((Kind.Equal, a[x - 1]));
                x--;
                y--;
            }

            // The edit itself: a move down is an insertion, a move right a deletion
            if (x == previousX)
            {
                reversed.Add((Kind.Insert, b[previousY]));
            }
            else
            {
                reversed.Add((Kind.Delete, a[previousX]));
            }

            x = previousX;
            y = previousY;
        }

        // What is left at the start was equal
        while (x > 0 && y > 0)
        {
            reversed.Add((Kind.Equal, a[x - 1]));
            x--;
            y--;
        }

        reversed.Reverse();
        return reversed;
    }

    /// <summary>
    ///     Groups a script into hunks with context and appends their lines.
    /// </summary>
    /// <param name="output">Receives the lines.</param>
    /// <param name="script">The script of equal, deleted and inserted lines.</param>
    private static void AddHunks(List<string> output, List<(Kind Kind, string Line)> script)
    {
        // Number the lines: how many original and new lines precede each script position
        var before = new int[script.Count + 1];
        var after = new int[script.Count + 1];
        var changes = new List<int>();
        for (var i = 0; i < script.Count; i++)
        {
            before[i + 1] = before[i] + (script[i].Kind != Kind.Insert ? 1 : 0);
            after[i + 1] = after[i] + (script[i].Kind != Kind.Delete ? 1 : 0);
            if (script[i].Kind != Kind.Equal)
            {
                changes.Add(i);
            }
        }

        if (changes.Count == 0)
        {
            return;
        }

        // Changes closer together than twice the context share a hunk
        var first = changes[0];
        var last = first;
        foreach (var change in changes.Skip(1))
        {
            if (change - last - 1 > 2 * ContextLines)
            {
                AddHunk(output, script, before, after, first, last);
                first = change;
            }

            last = change;
        }

        AddHunk(output, script, before, after, first, last);
    }

    /// <summary>
    ///     Appends one hunk.
    /// </summary>
    /// <param name="output">Receives the lines.</param>
    /// <param name="script">The script.</param>
    /// <param name="before">Original lines preceding each script position.</param>
    /// <param name="after">New lines preceding each script position.</param>
    /// <param name="firstChange">Script index of the first change of the hunk.</param>
    /// <param name="lastChange">Script index of the last change of the hunk.</param>
    private static void AddHunk(List<string> output, List<(Kind Kind, string Line)> script, int[] before, int[] after, int firstChange, int lastChange)
    {
        var start = Math.Max(0, firstChange - ContextLines);
        var end = Math.Min(script.Count, lastChange + 1 + ContextLines);
        output.Add($"@@ -{Range(before[start], before[end])} +{Range(after[start], after[end])} @@");
        for (var i = start; i < end; i++)
        {
            var (kind, line) = script[i];
            var prefix = kind switch
            {
                Kind.Delete => '-',
                Kind.Insert => '+',
                _ => ' '
            };

            // A line without a terminator is followed by the marker that says so
            var terminated = line.EndsWith('\n');
            output.Add(prefix + (terminated ? line[..^1] : line));
            if (!terminated)
            {
                output.Add("\\ No newline at end of file");
            }
        }
    }

    /// <summary>
    ///     Formats the range of a hunk header.
    /// </summary>
    /// <param name="linesBefore">Number of lines that precede the hunk in the text.</param>
    /// <param name="linesBeforeEnd">Number of lines that precede the end of the hunk.</param>
    /// <returns>The range: <c>start,count</c>, or <c>start</c> for one line; an empty range starts at the line before it.</returns>
    private static string Range(int linesBefore, int linesBeforeEnd)
    {
        var count = linesBeforeEnd - linesBefore;
        return count switch
        {
            0 => $"{linesBefore},0",
            1 => $"{linesBefore + 1}",
            _ => $"{linesBefore + 1},{count}"
        };
    }
}
