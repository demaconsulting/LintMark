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

using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace DemaConsulting.LintMark.Formatting.Inline;

/// <summary>
///     Finds the places between atoms where a sentence ends and where a clause ends.
/// </summary>
/// <remarks>
///     <para>
///         The splitter works on whole atoms (see <see cref="AtomTokenizer"/>), so a sentence end inside a link, a code
///         span, an inline HTML tag or any other atom is never seen: only the end of an atom can end a sentence. An
///         atom ends a sentence when, after the closing marks that may follow the terminator (a closing quote or
///         parenthesis, the end of emphasis, a footnote reference), it ends with a period, an exclamation mark, a question mark
///         or their East Asian forms, and the next atom starts like a sentence (an upper-case letter, a digit or a
///         letter without case, or a code span). The splitter is conservative on purpose, because a missed split only keeps two sentences on a
///         line while a wrong split reads badly: it does not split after an ellipsis, a known abbreviation (<c>e.g.</c>,
///         <c>Dr.</c>, <c>etc.</c>), an initial (<c>J.</c>), a dotted abbreviation (<c>U.S.</c>), a number of one or
///         two digits (<c>3.</c>), or before a word that starts in lower case, an HTML tag or a symbol.
///     </para>
///     <para>
///         An atom ends a clause when it ends with a comma, semicolon, colon or dash (not an emoji code such as
///         <c>:smile:</c> or a scope such as <c>std::</c>).
///     </para>
///     <para>
///         The answers depend on the atoms alone, never on how they were laid out, which is what makes the sentence and the
///         semantic wrap modes idempotent. The list of abbreviations is a constant of the tool and cannot be configured.
///         Thread safety: static and stateless apart from compiled patterns with a match timeout; safe for concurrent use.
///     </para>
/// </remarks>
internal static partial class SentenceSplitter
{
    /// <summary>
    ///     The longest time a pattern may run, as a guard against pathological input.
    /// </summary>
    private const int RegexTimeoutMilliseconds = 1000;

    /// <summary>
    ///     The characters that may open a word without being its first letter: quotes, parentheses, brackets and the start of emphasis.
    /// </summary>
    private static readonly char[] Openers = ['(', '[', '{', '"', '\'', '“', '‘', '«', '‹', '*', '_', '~'];

    /// <summary>
    ///     The characters that always close a sentence when they follow its terminator: quotes and the end of emphasis.
    /// </summary>
    private static readonly char[] Closers = ['"', '\'', '”', '’', '»', '›', '*', '_', '~'];

    /// <summary>
    ///     The abbreviations, in lower case and without their final period, after which a period does not end a sentence.
    /// </summary>
    private static readonly FrozenSet<string> Abbreviations = new[]
    {
        "e.g", "i.e", "cf", "vs", "viz", "etc", "et", "al", "ca", "approx", "resp", "dr", "mr", "mrs", "ms", "mx", "prof",
        "sr", "jr", "st", "mt", "ft", "gen", "col", "lt", "sgt", "capt", "rev", "hon", "inc", "ltd", "co", "corp", "dept",
        "univ", "est", "fig", "figs", "no", "nos", "vol", "vols", "ch", "sec", "eq", "eqs", "ed", "eds", "p", "pp", "ref",
        "refs", "misc", "ie", "eg"
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    ///     Matches a footnote reference at the end of an atom.
    /// </summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex(@"\[\^[^\]\s]+\]$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: RegexTimeoutMilliseconds)]
    private static partial Regex FootnoteReferenceRegex();

    /// <summary>
    ///     Matches a dotted abbreviation without its final period (<c>e.g</c>, <c>U.S</c>, <c>Ph.D</c>): groups of one or two
    ///     letters separated by periods.
    /// </summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex(@"^\p{L}{1,2}(?:\.\p{L}{1,2})+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: RegexTimeoutMilliseconds)]
    private static partial Regex DottedAbbreviationRegex();

    /// <summary>
    ///     Finds the atoms that start a sentence other than the first.
    /// </summary>
    /// <param name="atoms">The atoms of one run (a paragraph segment). Must not be a default array or contain null or an empty atom.</param>
    /// <returns>
    ///     The indexes, in increasing order and greater than 0, of the atoms that begin a new sentence: a sentence ends after the
    ///     atom before each.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="atoms"/> is a default array or holds an empty atom.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="atoms"/> holds a null element.</exception>
    public static ImmutableArray<int> FindSentenceStarts(ImmutableArray<string> atoms) => Find(atoms, EndsSentence);

    /// <summary>
    ///     Finds the atoms that start a clause other than the first.
    /// </summary>
    /// <param name="atoms">The atoms of one run (a sentence). Must not be a default array or contain null or an empty atom.</param>
    /// <returns>The indexes, in increasing order and greater than 0, of the atoms that begin a new clause.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="atoms"/> is a default array or holds an empty atom.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="atoms"/> holds a null element.</exception>
    public static ImmutableArray<int> FindClauseStarts(ImmutableArray<string> atoms) => Find(atoms, (atom, _) => EndsClause(atom));

    /// <summary>
    ///     Determines whether a sentence ends after an atom, given the atom that follows.
    /// </summary>
    /// <param name="atom">The atom. Must not be null or empty.</param>
    /// <param name="next">The next atom. Must not be null or empty.</param>
    /// <returns><see langword="true"/> when a sentence ends after <paramref name="atom"/> and <paramref name="next"/> starts the next one.</returns>
    /// <remarks>A pattern that times out gives <see langword="false"/>: when the splitter cannot decide it does not split.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when an argument is empty.</exception>
    public static bool EndsSentence(string atom, string next)
    {
        // Validate input
        ArgumentException.ThrowIfNullOrEmpty(atom);
        ArgumentException.ThrowIfNullOrEmpty(next);

        try
        {
            return HasTerminator(atom) && StartsSentence(next);
        }
        catch (RegexMatchTimeoutException)
        {
            // An undecided test never splits
            return false;
        }
    }

    /// <summary>
    ///     Determines whether a clause ends after an atom.
    /// </summary>
    /// <param name="atom">The atom. Must not be null or empty.</param>
    /// <returns>
    ///     <see langword="true"/> when the atom, without the closing marks after it, ends with a comma, semicolon, colon or dash.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="atom"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="atom"/> is empty.</exception>
    public static bool EndsClause(string atom)
    {
        // Validate input
        ArgumentException.ThrowIfNullOrEmpty(atom);

        try
        {
            var core = StripClosers(atom);
            if (core.Length == 0)
            {
                return false;
            }

            return core[^1] switch
            {
                ',' or ';' or '，' or '；' or '、' => core.Length > 1,
                '—' or '–' => true,
                ':' or '：' => core.Length > 1 && !core.EndsWith("::", StringComparison.Ordinal) && !(core[0] == ':' && core[^1] == ':'),
                _ => false
            };
        }
        catch (RegexMatchTimeoutException)
        {
            // An undecided test never splits
            return false;
        }
    }

    /// <summary>
    ///     Finds the atoms that follow a break, for a test on a pair of neighbours.
    /// </summary>
    /// <param name="atoms">The atoms.</param>
    /// <param name="breaksAfter">Tells whether a break lies between an atom (first argument) and the next (second argument).</param>
    /// <returns>The indexes of the atoms that start after a break.</returns>
    private static ImmutableArray<int> Find(ImmutableArray<string> atoms, Func<string, string, bool> breaksAfter)
    {
        // Validate input
        if (atoms.IsDefault)
        {
            throw new ArgumentException("The atoms must not be a default array.", nameof(atoms));
        }

        if (atoms.Any(static atom => atom is null))
        {
            throw new ArgumentNullException(nameof(atoms), "The atoms must not contain null.");
        }

        if (atoms.Any(static atom => atom.Length == 0))
        {
            throw new ArgumentException("The atoms must not contain an empty atom.", nameof(atoms));
        }

        var starts = ImmutableArray.CreateBuilder<int>();
        for (var i = 1; i < atoms.Length; i++)
        {
            if (breaksAfter(atoms[i - 1], atoms[i]))
            {
                starts.Add(i);
            }
        }

        return starts.ToImmutable();
    }

    /// <summary>
    ///     Determines whether an atom ends with a sentence terminator that is not part of an abbreviation, an initial, a number or an ellipsis.
    /// </summary>
    /// <param name="atom">The atom.</param>
    /// <returns><see langword="true"/> when the atom may end a sentence.</returns>
    private static bool HasTerminator(string atom)
    {
        var core = StripClosers(atom);
        if (core.Length == 0)
        {
            return false;
        }

        var last = core[^1];
        if (last is '!' or '?' or '。' or '！' or '？' or '｡')
        {
            return core.TrimEnd('!', '?', '。', '！', '？', '｡').Length > 0;
        }

        if (last != '.' || core.EndsWith("..", StringComparison.Ordinal))
        {
            return false;
        }

        // A period: the word before it decides
        var word = core[..^1].TrimStart(Openers);
        if (word.Length == 0 || Abbreviations.Contains(word.ToLowerInvariant()))
        {
            return false;
        }

        return !(word.Length == 1 && char.IsLetter(word[0]))
            && !DottedAbbreviationRegex().IsMatch(word)
            && !(word.Length <= 2 && word.All(char.IsAsciiDigit));
    }

    /// <summary>
    ///     Determines whether an atom starts like the first word of a sentence.
    /// </summary>
    /// <param name="atom">The atom.</param>
    /// <returns>
    ///     <see langword="true"/> when the atom, after its opening marks, starts with an upper-case letter, a digit or a letter
    ///     that has no case; <see langword="false"/> for a lower-case letter, a tag, a symbol or a footnote reference.
    /// </returns>
    private static bool StartsSentence(string atom)
    {
        var word = atom.TrimStart(Openers);
        if (word.Length == 0)
        {
            return false;
        }

        // A code span after a sentence end starts a sentence far more often than it continues one (abbreviations are excluded earlier)
        if (word[0] == '`')
        {
            return true;
        }

        var category = char.GetUnicodeCategory(word, 0);
        return char.IsLetterOrDigit(word, 0) && category != System.Globalization.UnicodeCategory.LowercaseLetter;
    }

    /// <summary>
    ///     Removes the marks that may follow the terminator of a sentence from the end of an atom: closing quotes, the end of emphasis,
    ///     a closing parenthesis or bracket that has no opening one in the atom, and a footnote reference.
    /// </summary>
    /// <param name="atom">The atom.</param>
    /// <returns>The atom without its closing marks.</returns>
    private static string StripClosers(string atom)
    {
        var core = atom;
        while (core.Length > 0)
        {
            var last = core[^1];
            if (Array.IndexOf(Closers, last) >= 0)
            {
                core = core[..^1];
            }
            else if (last == ')' && core.Count(static c => c == ')') > core.Count(static c => c == '('))
            {
                core = core[..^1];
            }
            else if (last == ']' && FootnoteReferenceRegex().Match(core) is { Success: true } footnote)
            {
                core = core[..footnote.Index];
            }
            else if (last == ']' && core.Count(static c => c == ']') > core.Count(static c => c == '['))
            {
                core = core[..^1];
            }
            else
            {
                break;
            }
        }

        return core;
    }
}
