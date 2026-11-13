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

using System.Text.RegularExpressions;

namespace DemaConsulting.LintMark.Safety;

/// <summary>
///     Decides whether a rewrite kept the Pandoc structure of a document, the second safety check: it sees
///     damage that comparing rendered HTML cannot.
/// </summary>
/// <remarks>
///     <para>
///         The Markdown parser knows nothing of Pandoc's fenced divs, raw LaTeX commands, display math
///         fences, attributes and citations: it renders them as ordinary text, so a rewrite that joins two
///         <c>:::</c> fence lines into prose renders the same HTML and would pass the HTML check. This check
///         therefore compares, before and after, the <em>sequences</em> of lines that start with <c>:::</c>, a
///         backslash and a letter, or a code fence with braces, that are exactly <c>$$</c>, or that are grid
///         table border lines (after removing leading block quote markers, indentation and trailing blanks),
///         and the <em>occurrence counts</em> of attribute groups (<c>{#id .class}</c>) and citations
///         (<c>[@key]</c>). Any difference is a failure.
///     </para>
///     <para>
///         The check is a backstop, not a substitute: formatters are expected to leave paragraphs that contain
///         Pandoc structure alone. It is deliberately strict, so it can refuse a harmless rewrite but never
///         accept a harmful one that changes these tokens. A check that cannot be completed (a pattern times
///         out) reports a difference. Only LF-normalized text is accepted.
///     </para>
///     <para>
///         Thread safety: static and stateless apart from compiled patterns; safe for concurrent use.
///     </para>
/// </remarks>
internal static partial class StructuralTokenChecker
{
    /// <summary>
    ///     The longest time a pattern may run, as a guard against pathological input.
    /// </summary>
    private const int RegexTimeoutMilliseconds = 5000;

    /// <summary>
    ///     Matches a grid table border line (after stripping): plus signs, dashes, equal signs, colons and blanks between plus signs.
    /// </summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex(
        @"^\+[-=:+ ]*\+$",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: RegexTimeoutMilliseconds)]
    private static partial Regex GridBorderRegex();

    /// <summary>
    ///     Matches an attribute group such as <c>{#id}</c> or <c>{.class key=value}</c>.
    /// </summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex(
        @"\{[#.][^{}\n]*\}",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: RegexTimeoutMilliseconds)]
    private static partial Regex AttributeGroupRegex();

    /// <summary>
    ///     Matches a citation such as <c>[@key]</c> or <c>[-@key, p. 3]</c>.
    /// </summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex(
        @"\[-?@[^\]\n]+\]",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: RegexTimeoutMilliseconds)]
    private static partial Regex CitationRegex();

    /// <summary>
    ///     Checks that two versions of a document have the same Pandoc structural tokens.
    /// </summary>
    /// <param name="beforeLf">The original LF-normalized text. Must not be null or contain a carriage return.</param>
    /// <param name="afterLf">The rewritten LF-normalized text. Must not be null or contain a carriage return.</param>
    /// <returns>
    ///     An equivalent result when the token sequences and occurrence counts are equal; otherwise a result that names
    ///     the first token that differs, or states that the check could not be completed.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when an argument contains a carriage return.</exception>
    public static EquivalenceResult Check(string beforeLf, string afterLf) =>
        Check(beforeLf, afterLf, CollectLineTokens);

    /// <summary>
    ///     Checks that two versions of a document have the same Pandoc structural tokens, using the given collector
    ///     of line tokens.
    /// </summary>
    /// <param name="beforeLf">The original LF-normalized text. Must not be null or contain a carriage return.</param>
    /// <param name="afterLf">The rewritten LF-normalized text. Must not be null or contain a carriage return.</param>
    /// <param name="collectLineTokens">The function that collects the line tokens of a text. Must not be null.</param>
    /// <returns>
    ///     An equivalent result when the token sequences and occurrence counts are equal; otherwise a result that names
    ///     the first token that differs, or states that the check could not be completed.
    /// </returns>
    /// <remarks>
    ///     This overload is a seam for tests, so that a failing collection can be simulated; the public overload
    ///     uses the real collection.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a text contains a carriage return.</exception>
    internal static EquivalenceResult Check(string beforeLf, string afterLf, Func<string, List<string>> collectLineTokens)
    {
        // Validate input: only LF-normalized text is compared
        ArgumentNullException.ThrowIfNull(collectLineTokens);
        ArgumentNullException.ThrowIfNull(beforeLf);
        ArgumentNullException.ThrowIfNull(afterLf);
        if (beforeLf.Contains('\r'))
        {
            throw new ArgumentException("The text must be LF-normalized.", nameof(beforeLf));
        }

        if (afterLf.Contains('\r'))
        {
            throw new ArgumentException("The text must be LF-normalized.", nameof(afterLf));
        }

        // Identical text has identical tokens
        if (string.Equals(beforeLf, afterLf, StringComparison.Ordinal))
        {
            return EquivalenceResult.Equivalent;
        }

        try
        {
            // The line tokens are compared as sequences, the attribute groups and citations as occurrence counts
            return CompareSequences(collectLineTokens(beforeLf), collectLineTokens(afterLf))
                ?? CompareOccurrences("Attribute group", AttributeGroupRegex(), beforeLf, afterLf)
                ?? CompareOccurrences("Citation", CitationRegex(), beforeLf, afterLf)
                ?? EquivalenceResult.Equivalent;
        }

        // A check that cannot be completed must refuse the rewrite, whatever the cause
        catch (Exception ex)
        {
            return EquivalenceResult.Different($"The Pandoc structure could not be compared ({ex.GetType().Name})");
        }
    }

    /// <summary>
    ///     Collects, in order, the lines that start a Pandoc construct.
    /// </summary>
    /// <param name="text">The LF-normalized text.</param>
    /// <returns>The stripped lines that are structural tokens.</returns>
    private static List<string> CollectLineTokens(string text)
    {
        var tokens = new List<string>();
        foreach (var rawLine in text.Split('\n'))
        {
            // Block quote markers and indentation do not change what the line is; trailing blanks never matter
            var line = rawLine.TrimStart(' ', '\t', '>').TrimEnd(' ', '\t');
            if (IsStructural(line))
            {
                tokens.Add(line);
            }
        }

        return tokens;
    }

    /// <summary>
    ///     Determines whether a stripped line starts a Pandoc construct.
    /// </summary>
    /// <param name="line">The line without leading quote markers and indentation and without trailing blanks.</param>
    /// <returns><see langword="true"/> for a fenced div line, a LaTeX command line, a display math fence, a braced code fence or a grid border.</returns>
    private static bool IsStructural(string line) =>
        line.StartsWith(":::", StringComparison.Ordinal)
        || (line.Length >= 2 && line[0] == '\\' && char.IsAsciiLetter(line[1]))
        || line == "$$"
        || line.StartsWith("```{", StringComparison.Ordinal)
        || (line.Length >= 2 && line[0] == '+' && GridBorderRegex().IsMatch(line));

    /// <summary>
    ///     Compares two token sequences.
    /// </summary>
    /// <param name="before">The tokens before.</param>
    /// <param name="after">The tokens after.</param>
    /// <returns><see langword="null"/> when equal; otherwise the result naming the first token that differs.</returns>
    private static EquivalenceResult? CompareSequences(List<string> before, List<string> after)
    {
        var common = Math.Min(before.Count, after.Count);
        for (var i = 0; i < common; i++)
        {
            if (!string.Equals(before[i], after[i], StringComparison.Ordinal))
            {
                return Describe(i, before[i], after[i]);
            }
        }

        if (before.Count == after.Count)
        {
            return null;
        }

        // One side has extra tokens at the end
        return before.Count > after.Count
            ? Describe(common, before[common], null)
            : Describe(common, null, after[common]);
    }

    /// <summary>
    ///     Describes the first structural line that differs.
    /// </summary>
    /// <param name="index">Zero-based position in the token sequence.</param>
    /// <param name="before">The token before, or <see langword="null"/> when it is missing.</param>
    /// <param name="after">The token after, or <see langword="null"/> when it is missing.</param>
    /// <returns>The result.</returns>
    private static EquivalenceResult Describe(int index, string? before, string? after) =>
        EquivalenceResult.Different(
            $"Pandoc structural line {index + 1} differs: before {Quote(before)} after {Quote(after)}");

    /// <summary>
    ///     Compares the occurrence counts of the matches of a pattern.
    /// </summary>
    /// <param name="what">Name of the construct for the description.</param>
    /// <param name="pattern">The pattern that finds the construct.</param>
    /// <param name="before">The text before.</param>
    /// <param name="after">The text after.</param>
    /// <returns><see langword="null"/> when equal; otherwise the result naming the first construct whose count differs.</returns>
    private static EquivalenceResult? CompareOccurrences(string what, Regex pattern, string before, string after)
    {
        var beforeCounts = Count(pattern, before);
        var afterCounts = Count(pattern, after);

        // Visit every construct in ordinal order so that the reported difference is deterministic
        foreach (var key in beforeCounts.Keys.Union(afterCounts.Keys).Order(StringComparer.Ordinal))
        {
            var was = beforeCounts.GetValueOrDefault(key);
            var now = afterCounts.GetValueOrDefault(key);
            if (was != now)
            {
                return EquivalenceResult.Different($"{what} {Quote(key)} occurs {was} time(s) before and {now} after");
            }
        }

        return null;
    }

    /// <summary>
    ///     Counts the matches of a pattern by matched text.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="text">The text to search.</param>
    /// <returns>Map from matched text to number of occurrences.</returns>
    private static Dictionary<string, int> Count(Regex pattern, string text)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var match in pattern.EnumerateMatches(text))
        {
            var key = text.Substring(match.Index, match.Length);
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }

        return counts;
    }

    /// <summary>
    ///     Shows a token inside a description.
    /// </summary>
    /// <param name="token">The token, or <see langword="null"/> when it is missing.</param>
    /// <returns>The quoted, shortened token, or <c>(none)</c>.</returns>
    private static string Quote(string? token) =>
        token is null ? "(none)" : $"\"{EquivalenceResult.Excerpt(token, 0)}\"";
}
