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

using System.Globalization;
using System.Text;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Applies the unified diffs written by the diff reporter to a text, so tests can prove that a diff
///     reproduces the new text exactly.
/// </summary>
/// <remarks>
///     The applier is strict: it verifies that every context and removed line matches the original, that the
///     line counts of each hunk header agree with the hunk body, and that the new-side start of each hunk
///     is where the output stands. Any disagreement is an <see cref="InvalidOperationException"/>, so a
///     diff with a wrong header or a wrong body cannot pass for a correct one.
/// </remarks>
internal static class PatchApplier
{
    /// <summary>
    ///     The marker that says the previous line has no line terminator.
    /// </summary>
    private const string NoNewlineMarker = "\\ No newline at end of file";

    /// <summary>
    ///     Applies a diff to a text.
    /// </summary>
    /// <param name="before">The original LF-terminated text.</param>
    /// <param name="diffLines">The lines of the diff: headers, optional note lines, and hunks.</param>
    /// <returns>The text after the diff is applied.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the diff does not apply to the text or is malformed.</exception>
    public static string Apply(string before, IReadOnlyList<string> diffLines)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(diffLines);

        var session = new Session(SplitLines(before));
        var index = 0;
        while (index < diffLines.Count)
        {
            var line = diffLines[index];
            if (line.StartsWith("@@", StringComparison.Ordinal))
            {
                index = ApplyHunk(session, diffLines, index);
            }
            else if (IsHeaderOrNote(line))
            {
                index++;
            }
            else
            {
                throw new InvalidOperationException($"Unexpected line outside a hunk: {line}");
            }
        }

        session.CopyUntil(session.Original.Count);
        return session.Output.ToString();
    }

    /// <summary>
    ///     Splits a text into lines that keep their <c>\n</c> terminator.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The lines; none for empty text.</returns>
    private static List<string> SplitLines(string text)
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

        return lines;
    }

    /// <summary>
    ///     Determines whether a line outside a hunk is a file header or a comment-style note.
    /// </summary>
    /// <param name="line">The diff line.</param>
    /// <returns><see langword="true"/> for <c>---</c>, <c>+++</c> and <c>\</c> lines.</returns>
    private static bool IsHeaderOrNote(string line) =>
        line.StartsWith("--- ", StringComparison.Ordinal)
        || line.StartsWith("+++ ", StringComparison.Ordinal)
        || line.StartsWith('\\');

    /// <summary>
    ///     Applies one hunk.
    /// </summary>
    /// <param name="session">The state of the application.</param>
    /// <param name="diffLines">The lines of the diff.</param>
    /// <param name="start">Index of the hunk header.</param>
    /// <returns>The index of the first line after the hunk.</returns>
    private static int ApplyHunk(Session session, IReadOnlyList<string> diffLines, int start)
    {
        var (oldStart, oldCount, newStart, newCount) = ParseHeader(diffLines[start]);

        // An empty range starts at the line before it; any other range starts at its first line
        var oldIndex = oldCount == 0 ? oldStart : oldStart - 1;
        var newIndex = newCount == 0 ? newStart : newStart - 1;
        if (oldIndex < session.Position || oldIndex > session.Original.Count)
        {
            throw new InvalidOperationException($"The hunk starts outside the text or overlaps the previous hunk: {diffLines[start]}");
        }

        session.CopyUntil(oldIndex);
        if (newIndex != session.OutputLines)
        {
            throw new InvalidOperationException($"The new-side start does not match the output position ({session.OutputLines}): {diffLines[start]}");
        }

        var index = start + 1;
        while (oldCount > 0 || newCount > 0)
        {
            var (prefix, expected) = ReadBodyLine(diffLines, ref index);
            var removes = prefix != '+';
            var adds = prefix != '-';
            if ((removes && oldCount == 0) || (adds && newCount == 0))
            {
                throw new InvalidOperationException($"The hunk body has more lines than its header says: {diffLines[start]}");
            }

            if (removes)
            {
                session.Remove(expected);
                oldCount--;
            }

            if (adds)
            {
                session.Add(expected);
                newCount--;
            }
        }

        return index;
    }

    /// <summary>
    ///     Reads one content line of a hunk with the marker that may follow it.
    /// </summary>
    /// <param name="diffLines">The lines of the diff.</param>
    /// <param name="index">The index of the line; moved past the line and its marker.</param>
    /// <returns>The prefix and the full text of the line as it is in the file, with its terminator if it has one.</returns>
    private static (char Prefix, string Text) ReadBodyLine(IReadOnlyList<string> diffLines, ref int index)
    {
        if (index >= diffLines.Count)
        {
            throw new InvalidOperationException("The diff ends inside a hunk.");
        }

        var line = diffLines[index++];
        if (line.Length == 0 || (line[0] != ' ' && line[0] != '-' && line[0] != '+'))
        {
            throw new InvalidOperationException($"Not a hunk line: '{line}'");
        }

        // The marker belongs to the line before it and says that line has no terminator
        var terminated = true;
        if (index < diffLines.Count && diffLines[index] == NoNewlineMarker)
        {
            terminated = false;
            index++;
        }

        return (line[0], terminated ? line[1..] + "\n" : line[1..]);
    }

    /// <summary>
    ///     Parses a hunk header.
    /// </summary>
    /// <param name="header">The header line.</param>
    /// <returns>The old start and count, and the new start and count.</returns>
    private static (int OldStart, int OldCount, int NewStart, int NewCount) ParseHeader(string header)
    {
        if (!header.StartsWith("@@ -", StringComparison.Ordinal) || !header.EndsWith(" @@", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Not a hunk header: {header}");
        }

        var ranges = header[4..^3].Split(" +");
        if (ranges.Length != 2)
        {
            throw new InvalidOperationException($"Not a hunk header: {header}");
        }

        var (oldStart, oldCount) = ParseRange(ranges[0]);
        var (newStart, newCount) = ParseRange(ranges[1]);
        return (oldStart, oldCount, newStart, newCount);
    }

    /// <summary>
    ///     Parses a range of a hunk header.
    /// </summary>
    /// <param name="range">The range: <c>start</c> or <c>start,count</c>.</param>
    /// <returns>The start and the count; a missing count is one.</returns>
    private static (int Start, int Count) ParseRange(string range)
    {
        var parts = range.Split(',');
        var start = int.Parse(parts[0], CultureInfo.InvariantCulture);
        var count = parts.Length > 1 ? int.Parse(parts[1], CultureInfo.InvariantCulture) : 1;
        return (start, count);
    }

    /// <summary>
    ///     The state of one application: the original lines, the position in them and the output so far.
    /// </summary>
    /// <param name="original">The original lines.</param>
    private sealed class Session(List<string> original)
    {
        /// <summary>
        ///     Gets the original lines.
        /// </summary>
        public List<string> Original { get; } = original;

        /// <summary>
        ///     Gets the text produced so far.
        /// </summary>
        public StringBuilder Output { get; } = new();

        /// <summary>
        ///     Gets the number of original lines consumed.
        /// </summary>
        public int Position { get; private set; }

        /// <summary>
        ///     Gets the number of lines written to the output.
        /// </summary>
        public int OutputLines { get; private set; }

        /// <summary>
        ///     Copies the unchanged original lines up to an index.
        /// </summary>
        /// <param name="index">The index of the first original line not to copy.</param>
        public void CopyUntil(int index)
        {
            while (Position < index)
            {
                Add(Original[Position]);
                Position++;
            }
        }

        /// <summary>
        ///     Consumes an original line that must equal the expected text.
        /// </summary>
        /// <param name="expected">The text of the line, with its terminator if it has one.</param>
        public void Remove(string expected)
        {
            if (Position >= Original.Count || !string.Equals(Original[Position], expected, StringComparison.Ordinal))
            {
                var actual = Position < Original.Count ? Original[Position] : "(end of text)";
                throw new InvalidOperationException($"Original line {Position + 1} is '{actual}' but the diff expects '{expected}'.");
            }

            Position++;
        }

        /// <summary>
        ///     Appends a line to the output.
        /// </summary>
        /// <param name="line">The text of the line, with its terminator if it has one.</param>
        public void Add(string line)
        {
            Output.Append(line);
            OutputLines++;
        }
    }
}
