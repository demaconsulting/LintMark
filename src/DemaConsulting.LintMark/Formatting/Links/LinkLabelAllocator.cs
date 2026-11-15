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
using System.Text.RegularExpressions;
using DemaConsulting.LintMark.Document;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace DemaConsulting.LintMark.Formatting.Links;

/// <summary>
///     Chooses the labels of reference links: derives a label from the text of a link, reuses the label of a definition that
///     already has the same destination and title, and keeps every new label different from every label and bracketed text
///     that the document already has.
/// </summary>
/// <remarks>
///     <para>
///         A label is the link text in lower case with every run of characters that are not letters or digits replaced by one
///         hyphen, cut to the maximum length at a hyphen; when nothing is left, the host and first path segment of the
///         destination are used the same way, and <c>link</c> when even that is empty. A label that is taken gets <c>-2</c>,
///         <c>-3</c> and so on, the first that is free. A label counts as taken when the document has a definition with it
///         (any case, any blanks), or the text <c>[label]</c> anywhere, except as the text of a link the parser read: a definition would turn
///         such text into a link.
///     </para>
///     <para>
///         A definition that already exists (the first one that has its label, as the parser reads it) is reused for a link with
///         the same destination and title when its label is made of letters, digits, blanks and simple punctuation only; the
///         label is then written as the definition has it. Existing definitions are never changed.
///     </para>
///     <para>
///         Thread safety: an instance is a per-document builder used by one thread; <see cref="DeriveBase"/> is static and pure.
///     </para>
/// </remarks>
internal sealed partial class LinkLabelAllocator
{
    /// <summary>
    ///     A pair of brackets without brackets inside, and the parenthesis that follows it, if any. A pair that no parenthesis
    ///     follows is text that is a label by itself; a pair that one follows is the text of an inline link only when the parser
    ///     read a link there (see <see cref="LinkLabelAllocator(ParsedDocument, int)"/>).
    /// </summary>
    private const string BracketPattern = @"\[([^\[\]]*)\](\()?";

    /// <summary>
    ///     The characters other than letters and digits that a reused label may hold.
    /// </summary>
    private const string SimpleLabelPunctuation = " -_.:/#+";

    /// <summary>
    ///     The normalized labels that must not be used for a new definition.
    /// </summary>
    private readonly HashSet<string> _taken = new(StringComparer.Ordinal);

    /// <summary>
    ///     The label of every destination and title that has a definition, by destination and title.
    /// </summary>
    private readonly Dictionary<(string Url, string? Title), string> _targets = [];

    /// <summary>
    ///     The new definitions: label and the text of the definition line.
    /// </summary>
    private readonly List<(string Label, string Line)> _definitions = [];

    /// <summary>
    ///     The longest label that is generated.
    /// </summary>
    private readonly int _maxLength;

    /// <summary>
    ///     Initializes a new instance of the <see cref="LinkLabelAllocator"/> class for a document.
    /// </summary>
    /// <param name="document">The document whose labels and brackets must not be used. Must not be null.</param>
    /// <param name="maxLength">The longest label that is generated; at least 1.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="document"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxLength"/> is less than 1.</exception>
    public LinkLabelAllocator(ParsedDocument document, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLength, 1);
        _maxLength = maxLength;

        // Every definition takes its label, whether or not the parser lets it win; only the one the parser uses can be reused
        foreach (var definition in document.References.Definitions)
        {
            _taken.Add(definition.NormalizedLabel);
            if (document.References.TryGet(definition.Label, out var effective)
                && effective.Start == definition.Start
                && IsSimpleLabel(definition.Label))
            {
                _targets.TryAdd((definition.Url, definition.Title), definition.Label);
            }
        }

        // Any bracketed text is a label to the parser as soon as a definition with it exists. Brackets followed by a parenthesis
        // are exempt only where the parser read an inline link or image: the same brackets before a destination that is not
        // one (for example with a blank in it) are plain text, which a new definition would turn into a link
        var linkOpenings = LinkOpenings(document);
        foreach (Match match in BracketRegex().Matches(document.Source.Text))
        {
            if (!match.Groups[2].Success || !linkOpenings.Contains(match.Index))
            {
                _taken.Add(ReferenceIndex.NormalizeLabel(match.Groups[1].Value));
            }
        }
    }

    /// <summary>
    ///     Finds where the links and images that the parser produced open their brackets.
    /// </summary>
    /// <param name="document">The parsed document.</param>
    /// <returns>The offsets of the opening brackets of every link and image that is not an autolink.</returns>
    private static HashSet<int> LinkOpenings(ParsedDocument document)
    {
        var openings = new HashSet<int>();
        foreach (var link in document.Ast.Descendants<LinkInline>())
        {
            if (!link.IsAutoLink)
            {
                openings.Add(link.Span.Start + (link.IsImage ? 1 : 0));
            }
        }

        return openings;
    }

    /// <summary>
    ///     Gets the new definition lines, sorted by label.
    /// </summary>
    public IReadOnlyList<string> DefinitionLines =>
        [.. _definitions.OrderBy(static definition => definition.Label, StringComparer.Ordinal).Select(static definition => definition.Line)];

    /// <summary>
    ///     Gets the labels of the new definitions, sorted.
    /// </summary>
    public IReadOnlyList<string> NewLabels =>
        [.. _definitions.Select(static definition => definition.Label).Order(StringComparer.Ordinal)];

    /// <summary>
    ///     Derives the label a link would get when no other label is in the way.
    /// </summary>
    /// <param name="text">The plain text of the link. Must not be null.</param>
    /// <param name="url">The destination of the link. Must not be null.</param>
    /// <param name="maxLength">The longest label; at least 1.</param>
    /// <returns>The label: never empty, lower case, letters, digits and single hyphens.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxLength"/> is less than 1.</exception>
    public static string DeriveBase(string text, string url, int maxLength)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(url);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLength, 1);

        var label = Slugify(text, maxLength);
        if (label.Length > 0)
        {
            return label;
        }

        // The text has no letters or digits: the host and the first path segment say what the link is
        string basis;
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
        {
            var host = uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host;
            basis = host + " " + uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        }
        else
        {
            var cut = url.AsSpan().IndexOfAny('?', '#');
            basis = cut < 0 ? url : url[..cut];
        }

        label = Slugify(basis, maxLength);
        return label.Length > 0 ? label : "link";
    }

    /// <summary>
    ///     Gets the label of a link, defining a new label when the destination and title have none yet.
    /// </summary>
    /// <param name="baseLabel">The label derived from the text (see <see cref="DeriveBase"/>). Must not be null or empty.</param>
    /// <param name="tail">The destination part of the link. Must not be null.</param>
    /// <returns>The label to write: that of an existing or an earlier definition with the same destination and title, or a new one.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="baseLabel"/> is empty.</exception>
    public string Allocate(string baseLabel, InlineLinkTail tail)
    {
        // Validate input
        ArgumentException.ThrowIfNullOrEmpty(baseLabel);
        ArgumentNullException.ThrowIfNull(tail);

        var key = (tail.Url, tail.TitleText);
        if (_targets.TryGetValue(key, out var existing))
        {
            return existing;
        }

        // The first free label: the base, then the base with -2, -3 and so on (kept within the maximum length)
        var label = baseLabel;
        var number = 1;
        while (_taken.Contains(ReferenceIndex.NormalizeLabel(label)))
        {
            number++;
            var suffix = "-" + number.ToString(System.Globalization.CultureInfo.InvariantCulture);
            label = (_maxLength > suffix.Length ? Truncate(baseLabel, _maxLength - suffix.Length) : baseLabel) + suffix;
        }

        _taken.Add(ReferenceIndex.NormalizeLabel(label));
        _targets[key] = label;
        _definitions.Add((label, $"[{label}]: {tail.Destination}{(tail.Title is null ? string.Empty : " " + tail.Title)}"));
        return label;
    }

    /// <summary>
    ///     Gets the label that was given to a destination and title.
    /// </summary>
    /// <param name="tail">The destination part of a link that was given a label by <see cref="Allocate"/>. Must not be null.</param>
    /// <returns>The label.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="tail"/> is null.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when no label was given to the destination and title.</exception>
    public string LabelOf(InlineLinkTail tail)
    {
        ArgumentNullException.ThrowIfNull(tail);
        return _targets[(tail.Url, tail.TitleText)];
    }

    /// <summary>
    ///     Turns text into a label: lower case, runs of other characters replaced by one hyphen, cut to a length.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="maxLength">The longest result.</param>
    /// <returns>The label; empty when the text has no letter or digit.</returns>
    private static string Slugify(string text, int maxLength)
    {
        var builder = new StringBuilder(text.Length);
        var pending = false;
        foreach (var rune in text.EnumerateRunes())
        {
            if (!Rune.IsLetterOrDigit(rune))
            {
                pending = true;
                continue;
            }

            if (pending && builder.Length > 0)
            {
                builder.Append('-');
            }

            pending = false;
            builder.Append(rune.ToString().ToLowerInvariant());
        }

        return Truncate(builder.ToString(), maxLength);
    }

    /// <summary>
    ///     Cuts a label to a length, at a hyphen when it has one in the part that is kept.
    /// </summary>
    /// <param name="label">The label.</param>
    /// <param name="maxLength">The longest result; at least 1.</param>
    /// <returns>The label, shortened when it is longer than the maximum.</returns>
    private static string Truncate(string label, int maxLength)
    {
        if (label.Length <= maxLength)
        {
            return label;
        }

        var cut = label[..maxLength];
        if (label[maxLength] != '-')
        {
            var hyphen = cut.LastIndexOf('-');
            if (hyphen > 0)
            {
                cut = cut[..hyphen];
            }
        }

        // A cut must not split a surrogate pair
        if (cut.Length > 0 && char.IsHighSurrogate(cut[^1]))
        {
            cut = cut[..^1];
        }

        return cut.TrimEnd('-');
    }

    /// <summary>
    ///     Determines whether a label of an existing definition is plain enough to be written into a link as it is.
    /// </summary>
    /// <param name="label">The label as the parser reports it.</param>
    /// <returns><see langword="true"/> when it starts and ends with a letter or digit and holds only letters, digits and simple punctuation.</returns>
    private static bool IsSimpleLabel(string label) =>
        label.Length > 0
        && char.IsLetterOrDigit(label[0])
        && char.IsLetterOrDigit(label[^1])
        && label.All(static c => char.IsLetterOrDigit(c) || SimpleLabelPunctuation.Contains(c, StringComparison.Ordinal));

    /// <summary>
    ///     Matches every pair of brackets that holds no brackets.
    /// </summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex(BracketPattern, RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 5000)]
    private static partial Regex BracketRegex();
}
