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

using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace DemaConsulting.LintMark.Document;

/// <summary>
///     Resolves suppression directives written as HTML comments into offset ranges and answers
///     "is this rule suppressed here?" queries.
/// </summary>
/// <remarks>
///     <para>
///         Three directive families are understood, and their (different) scoping semantics are kept
///         distinct on purpose: <c>lintmark-*</c> and <c>markdownlint-*</c> (disable, enable,
///         disable-line, disable-next-line, disable-file, capture, restore) and
///         <c>prettier-ignore</c>, <c>prettier-ignore-start</c>, <c>prettier-ignore-end</c>. Prettier's
///         <c>prettier-ignore</c> covers the next <em>block</em>, not the next line, and only block-level
///         comments carry prettier directives.
///     </para>
///     <para>
///         Directives are found by walking the parsed syntax tree (HTML blocks and inline HTML),
///         never by scanning the text, so comments inside code spans, code blocks and front matter are
///         not directives. Rule identifiers pass through an optional alias-resolving function so that rule
///         aliases can later be resolved without changing this unit.
///     </para>
///     <para>
///         Ranges are half-open <c>[Start, End)</c> offsets into the LF-normalized text. A region
///         <c>disable</c> starts at the start of its comment and ends at the start of the matching
///         <c>enable</c> comment (or the end of the file when unterminated). Ranges may overlap when
///         they carry different rule sets. Instances are immutable after <see cref="Build"/> and
///         safe for concurrent reads; they are created per file.
///     </para>
/// </remarks>
internal sealed partial class SuppressionMap
{
    /// <summary>
    ///     Matches the body of a lintmark/markdownlint directive comment.
    /// </summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex(
        @"^(?<family>lintmark|markdownlint)-(?<verb>disable-next-line|disable-line|disable-file|disable|enable|capture|restore)(?:\s+(?<args>.*))?$",
        RegexOptions.Singleline | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex DirectiveRegex();

    /// <summary>
    ///     Matches the body of a prettier directive comment.
    /// </summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex(
        @"^prettier-ignore(?<suffix>-start|-end)?$",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex PrettierRegex();

    /// <summary>
    ///     Matches one HTML comment inside the text of an HTML block.
    /// </summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex(
        @"<!--(?<body>.*?)-->",
        RegexOptions.Singleline | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex CommentRegex();

    /// <summary>
    ///     The resolved ranges, sorted by start offset.
    /// </summary>
    private readonly ImmutableArray<SuppressionRange> _ranges;

    /// <summary>
    ///     Alias-resolving function applied to every rule identifier before comparison.
    /// </summary>
    private readonly Func<string, string> _canonicalize;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SuppressionMap"/> class.
    /// </summary>
    /// <param name="ranges">Resolved ranges (copied into an immutable array).</param>
    /// <param name="canonicalize">Rule identifier alias-resolving function.</param>
    private SuppressionMap(List<SuppressionRange> ranges, Func<string, string> canonicalize)
    {
        _ranges = ranges.ToImmutableArray();
        _canonicalize = canonicalize;
    }

    /// <summary>
    ///     Gets the resolved ranges sorted by start offset.
    /// </summary>
    /// <remarks>
    ///     The list is backed by an immutable array, so a caller cannot change the map through it,
    ///     not even by casting.
    /// </remarks>
    public IReadOnlyList<SuppressionRange> Ranges => _ranges;

    /// <summary>
    ///     Builds the suppression map of a parsed document.
    /// </summary>
    /// <param name="source">The LF-normalized source. Must not be null.</param>
    /// <param name="blocks">The flattened, source-ordered blocks (used to find the block after a <c>prettier-ignore</c>). Must not be null.</param>
    /// <param name="ast">The syntax tree used to find inline HTML comments. Must not be null.</param>
    /// <param name="canonicalizeRuleId">
    ///     Optional rule identifier alias-resolving function; <see langword="null"/> means identity.
    /// </param>
    /// <returns>The suppression map.</returns>
    /// <remarks>Pure with respect to its inputs; thread-safe.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when a required argument is null.</exception>
    public static SuppressionMap Build(
        SourceDocument source,
        IReadOnlyList<BlockInfo> blocks,
        MarkdownDocument ast,
        Func<string, string>? canonicalizeRuleId = null)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(ast);

        var canonicalize = canonicalizeRuleId ?? (static id => id);
        var ranges = new List<SuppressionRange>();

        // Directives are processed in source order so region state evolves as a reader would see it
        var regions = new RegionTracker(ranges);
        var prettierStart = -1;
        foreach (var comment in FindComments(source, blocks, ast))
        {
            var prettier = comment.IsBlock ? PrettierRegex().Match(comment.Body) : Match.Empty;
            if (prettier.Success)
            {
                prettierStart = ApplyPrettier(prettier.Groups["suffix"].Value, comment, blocks, ranges, prettierStart);
                continue;
            }

            var directive = DirectiveRegex().Match(comment.Body);
            if (directive.Success)
            {
                ApplyDirective(source, directive, comment, ranges, regions, canonicalize);
            }
        }

        // Close anything left open: an unterminated region or prettier-ignore-start runs to the end of the file
        regions.Close(int.MaxValue);
        if (prettierStart >= 0)
        {
            ranges.Add(new SuppressionRange(prettierStart, int.MaxValue, RuleSet.All));
        }

        ranges.Sort(static (a, b) => a.Start != b.Start ? a.Start.CompareTo(b.Start) : a.End.CompareTo(b.End));
        return new SuppressionMap(ranges, canonicalize);
    }

    /// <summary>
    ///     Determines whether a rule is suppressed at an offset.
    /// </summary>
    /// <param name="offset">Offset into the LF-normalized text.</param>
    /// <param name="ruleId">Rule identifier; aliases are resolved through the alias-resolving function. Must not be null.</param>
    /// <returns><see langword="true"/> when any range covering the offset suppresses the rule.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="ruleId"/> is null.</exception>
    public bool IsSuppressed(int offset, string ruleId)
    {
        ArgumentNullException.ThrowIfNull(ruleId);
        return IsSuppressedRange(offset, offset + 1, ruleId);
    }

    /// <summary>
    ///     Determines whether a rule is suppressed anywhere inside an offset range.
    /// </summary>
    /// <param name="start">Offset of the first character of the range.</param>
    /// <param name="end">Exclusive end offset; values not greater than <paramref name="start"/> are treated as a single character.</param>
    /// <param name="ruleId">Rule identifier; aliases are resolved through the alias-resolving function. Must not be null.</param>
    /// <returns><see langword="true"/> when any suppression range intersecting the range suppresses the rule.</returns>
    /// <remarks>
    ///     Block-granularity consumers use this to route any block that intersects a suppressed
    ///     region to verbatim handling.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="ruleId"/> is null.</exception>
    public bool IsSuppressedRange(int start, int end, string ruleId)
    {
        ArgumentNullException.ThrowIfNull(ruleId);

        // Ranges are sorted by start, so stop as soon as one begins after the queried range
        var id = _canonicalize(ruleId);
        var queryEnd = end > start ? end : start + 1;
        foreach (var range in _ranges)
        {
            if (range.Start >= queryEnd)
            {
                break;
            }

            if (range.End > start && range.Rules.Contains(id))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     An HTML comment that may carry a directive.
    /// </summary>
    /// <param name="Start">Offset of the <c>&lt;!--</c>.</param>
    /// <param name="End">Exclusive offset after the <c>--&gt;</c>.</param>
    /// <param name="Body">Trimmed text between the comment delimiters.</param>
    /// <param name="IsBlock"><see langword="true"/> for a block-level comment.</param>
    private sealed record Comment(int Start, int End, string Body, bool IsBlock);

    /// <summary>
    ///     Finds every HTML comment (block-level and inline) in source order.
    /// </summary>
    /// <param name="source">The source document.</param>
    /// <param name="blocks">The flattened blocks.</param>
    /// <param name="ast">The syntax tree.</param>
    /// <returns>The comments sorted by start offset.</returns>
    private static List<Comment> FindComments(SourceDocument source, IReadOnlyList<BlockInfo> blocks, MarkdownDocument ast)
    {
        var comments = new List<Comment>();

        // Block-level comments: HTML blocks of the comment type (may hold several comments on one line)
        foreach (var block in blocks.Where(static b => b is { Kind: BlockKind.HtmlBlock, Node: HtmlBlock { Type: HtmlBlockType.Comment } }))
        {
            var text = source.Text.AsSpan(block.Start, block.EndInclusive - block.Start + 1).ToString();
            foreach (Match match in CommentRegex().Matches(text))
            {
                comments.Add(new Comment(block.Start + match.Index, block.Start + match.Index + match.Length, match.Groups["body"].Value.Trim(), true));
            }
        }

        // Inline comments: inline HTML whose source text is a complete comment (never inside code spans)
        foreach (var span in ast.Descendants<HtmlInline>().Select(static inline => inline.Span))
        {
            if (span.End < span.Start || span.Start < 0 || span.End >= source.Text.Length)
            {
                continue;
            }

            var text = source.Text.AsSpan(span.Start, span.Length).ToString();
            if (text.Length >= 7 && text.StartsWith("<!--", StringComparison.Ordinal) && text.EndsWith("-->", StringComparison.Ordinal))
            {
                comments.Add(new Comment(span.Start, span.End + 1, text[4..^3].Trim(), false));
            }
        }

        comments.Sort(static (a, b) => a.Start.CompareTo(b.Start));
        return comments;
    }

    /// <summary>
    ///     Applies a prettier directive.
    /// </summary>
    /// <param name="suffix">Empty for <c>prettier-ignore</c>, otherwise <c>-start</c> or <c>-end</c>.</param>
    /// <param name="comment">The directive comment.</param>
    /// <param name="blocks">The flattened blocks.</param>
    /// <param name="ranges">Receives the produced ranges.</param>
    /// <param name="prettierStart">Offset of the open <c>prettier-ignore-start</c>, or -1 when none is open.</param>
    /// <returns>The updated open-start offset.</returns>
    private static int ApplyPrettier(string suffix, Comment comment, IReadOnlyList<BlockInfo> blocks, List<SuppressionRange> ranges, int prettierStart)
    {
        switch (suffix)
        {
            case "-start":
                // A second start inside an open region is redundant; keep the earliest
                return prettierStart >= 0 ? prettierStart : comment.Start;

            case "-end":
                // An end without a start is ignored
                if (prettierStart >= 0)
                {
                    ranges.Add(new SuppressionRange(prettierStart, comment.End, RuleSet.All));
                }

                return -1;

            default:
                // prettier-ignore covers the next block (the first block that starts after the comment)
                foreach (var block in blocks)
                {
                    if (block.Start >= comment.End)
                    {
                        ranges.Add(new SuppressionRange(block.Start, block.EndInclusive + 1, RuleSet.All));
                        break;
                    }
                }

                return prettierStart;
        }
    }

    /// <summary>
    ///     Applies a lintmark/markdownlint directive.
    /// </summary>
    /// <param name="source">The source document.</param>
    /// <param name="directive">The successful directive match.</param>
    /// <param name="comment">The directive comment.</param>
    /// <param name="ranges">Receives the produced one-shot ranges.</param>
    /// <param name="regions">Tracks the disable/enable region state.</param>
    /// <param name="canonicalize">Rule identifier alias-resolving function.</param>
    private static void ApplyDirective(
        SourceDocument source,
        Match directive,
        Comment comment,
        List<SuppressionRange> ranges,
        RegionTracker regions,
        Func<string, string> canonicalize)
    {
        // Parse the optional rule list; an empty list means "all rules"
        var rules = directive.Groups["args"].Value
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(canonicalize)
            .ToList();
        var ruleSet = rules.Count == 0 ? RuleSet.All : RuleSet.Only(rules);

        switch (directive.Groups["verb"].Value)
        {
            case "disable":
                regions.Disable(comment.Start, rules);
                break;

            case "enable":
                regions.Enable(comment.Start, rules);
                break;

            case "capture":
                regions.Capture();
                break;

            case "restore":
                regions.Restore(comment.Start);
                break;

            case "disable-file":
                ranges.Add(new SuppressionRange(0, int.MaxValue, ruleSet));
                break;

            case "disable-line":
                ranges.Add(LineRange(source, comment, 0, ruleSet));
                break;

            default:
                // disable-next-line: the line after the last line of the comment
                if (NextLineRange(source, comment, ruleSet) is { } next)
                {
                    ranges.Add(next);
                }

                break;
        }
    }

    /// <summary>
    ///     Creates the range covering the lines occupied by a comment.
    /// </summary>
    /// <param name="source">The source document.</param>
    /// <param name="comment">The directive comment.</param>
    /// <param name="lineOffset">Number of lines to skip after the comment's last line.</param>
    /// <param name="ruleSet">Rules to suppress.</param>
    /// <returns>The range, from the start of the comment's first line to the start of the line after its last line.</returns>
    private static SuppressionRange LineRange(SourceDocument source, Comment comment, int lineOffset, RuleSet ruleSet)
    {
        var firstLine = source.PositionOf(comment.Start).Line - 1;
        var lastLine = source.PositionOf(comment.End - 1).Line - 1 + lineOffset;
        var end = lastLine + 1 < source.LineStarts.Count ? source.LineStarts[lastLine + 1] : source.Text.Length;
        return new SuppressionRange(source.LineStarts[firstLine], end, ruleSet);
    }

    /// <summary>
    ///     Creates the range covering the line after a comment.
    /// </summary>
    /// <param name="source">The source document.</param>
    /// <param name="comment">The directive comment.</param>
    /// <param name="ruleSet">Rules to suppress.</param>
    /// <returns>The range, or <see langword="null"/> when the comment is on the last line.</returns>
    private static SuppressionRange? NextLineRange(SourceDocument source, Comment comment, RuleSet ruleSet)
    {
        var nextLine = source.PositionOf(comment.End - 1).Line;
        if (nextLine >= source.LineStarts.Count || source.LineStarts[nextLine] >= source.Text.Length)
        {
            return null;
        }

        var start = source.LineStarts[nextLine];
        var end = nextLine + 1 < source.LineStarts.Count ? source.LineStarts[nextLine + 1] : source.Text.Length;
        return new SuppressionRange(start, end, ruleSet);
    }

    /// <summary>
    ///     Tracks the disable/enable/capture/restore state and emits a range each time the state changes.
    /// </summary>
    private sealed class RegionTracker
    {
        /// <summary>
        ///     Receives the emitted ranges.
        /// </summary>
        private readonly List<SuppressionRange> _ranges;

        /// <summary>
        ///     Snapshots pushed by <c>capture</c>.
        /// </summary>
        private readonly Stack<(bool AllDisabled, HashSet<string> Rules)> _captures = new();

        /// <summary>
        ///     Whether every rule is currently disabled.
        /// </summary>
        private bool _allDisabled;

        /// <summary>
        ///     Disabled rules (when not all are disabled) or re-enabled exceptions (when all are disabled).
        /// </summary>
        private HashSet<string> _rules = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        ///     Offset where the current state began.
        /// </summary>
        private int _stateStart;

        /// <summary>
        ///     Initializes a new instance of the <see cref="RegionTracker"/> class.
        /// </summary>
        /// <param name="ranges">Receives the emitted ranges.</param>
        public RegionTracker(List<SuppressionRange> ranges)
        {
            _ranges = ranges;
        }

        /// <summary>
        ///     Handles a region <c>disable</c>.
        /// </summary>
        /// <param name="offset">Offset of the directive.</param>
        /// <param name="rules">Rules to disable; empty disables everything.</param>
        public void Disable(int offset, IReadOnlyCollection<string> rules)
        {
            Close(offset);
            if (rules.Count == 0)
            {
                _allDisabled = true;
                _rules.Clear();
            }
            else if (_allDisabled)
            {
                _rules.ExceptWith(rules);
            }
            else
            {
                _rules.UnionWith(rules);
            }
        }

        /// <summary>
        ///     Handles a region <c>enable</c>.
        /// </summary>
        /// <param name="offset">Offset of the directive.</param>
        /// <param name="rules">Rules to enable; empty enables everything.</param>
        public void Enable(int offset, IReadOnlyCollection<string> rules)
        {
            Close(offset);
            if (rules.Count == 0)
            {
                _allDisabled = false;
                _rules.Clear();
            }
            else if (_allDisabled)
            {
                _rules.UnionWith(rules);
            }
            else
            {
                _rules.ExceptWith(rules);
            }
        }

        /// <summary>
        ///     Handles <c>capture</c>: snapshot the current state.
        /// </summary>
        public void Capture() => _captures.Push((_allDisabled, new HashSet<string>(_rules, StringComparer.OrdinalIgnoreCase)));

        /// <summary>
        ///     Handles <c>restore</c>: return to the most recent snapshot (a no-op without one).
        /// </summary>
        /// <param name="offset">Offset of the directive.</param>
        public void Restore(int offset)
        {
            if (_captures.Count == 0)
            {
                return;
            }

            Close(offset);
            var (allDisabled, rules) = _captures.Pop();
            _allDisabled = allDisabled;
            _rules = rules;
        }

        /// <summary>
        ///     Describes the currently disabled rules.
        /// </summary>
        /// <returns>The rule set of the current state.</returns>
        private RuleSet CurrentRuleSet()
        {
            if (!_allDisabled)
            {
                return RuleSet.Only(_rules);
            }

            return _rules.Count == 0 ? RuleSet.All : RuleSet.AllExcept(_rules);
        }

        /// <summary>
        ///     Emits the range of the current state (if anything is disabled) and starts a new state at the offset.
        /// </summary>
        /// <param name="offset">Offset where the current state ends.</param>
        public void Close(int offset)
        {
            var hasEffect = _allDisabled || _rules.Count > 0;
            if (hasEffect && offset > _stateStart)
            {
                _ranges.Add(new SuppressionRange(_stateStart, offset, CurrentRuleSet()));
            }

            _stateStart = offset;
        }
    }
}
