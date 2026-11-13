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
using DemaConsulting.LintMark.Document;
using DemaConsulting.LintMark.Linting;

namespace DemaConsulting.LintMark.Configuration;

/// <summary>
///     The complete, validated, immutable set of settings that governs how one Markdown file is linted
///     and formatted.
/// </summary>
/// <remarks>
///     <para>
///         Every option is an enumeration of deterministic canonical forms (or a bounded number), so a
///         configuration that would leave a formatting violation unfixable cannot be expressed. The
///         defaults reproduce the preferred style: 120 columns, overflow-only wrapping, dash bullets,
///         incrementing numbering, compact tables, and so on. Settings that are not given in a
///         configuration file keep these defaults.
///     </para>
///     <para>
///         The record is deeply immutable: collections are immutable types, so a <c>with</c> expression
///         can never share a mutable collection between configurations, and every accessor validates its
///         value (also when used by <c>with</c>), so an invalid instance cannot be created. Value
///         equality includes the collections. Supporting types: <see cref="RuleCatalog"/>,
///         <see cref="LinkRefactorOptions"/> and the setting enumerations in this namespace.
///     </para>
///     <para>
///         Thread safety: instances are immutable and may be shared between threads without synchronization.
///     </para>
/// </remarks>
internal sealed record LintMarkConfig
{
    /// <summary>
    ///     The largest number accepted for any numeric setting; a guard against arithmetic overflow in width calculations.
    /// </summary>
    public const int MaxNumber = 100_000;

    /// <summary>
    ///     The characters that end a line: CR, LF, NEL (U+0085), line separator (U+2028) and paragraph separator (U+2029).
    /// </summary>
    /// <remarks>
    ///     Shared by every single-line check of the configuration so they cannot drift apart.
    /// </remarks>
    public const string LineBreakCharacters = "\r\n\u0085\u2028\u2029";

    /// <summary>
    ///     Backing field of <see cref="LineLength"/>.
    /// </summary>
    private readonly int _lineLength = 120;

    /// <summary>
    ///     Backing field of <see cref="Wrap"/>.
    /// </summary>
    private readonly WrapMode _wrap = WrapMode.Fill;

    /// <summary>
    ///     Backing field of <see cref="EndOfLine"/>.
    /// </summary>
    private readonly LineEnding? _endOfLine;

    /// <summary>
    ///     Backing field of <see cref="Headings"/>.
    /// </summary>
    private readonly HeadingStyle _headings = HeadingStyle.Atx;

    /// <summary>
    ///     Backing field of <see cref="Bullet"/>.
    /// </summary>
    private readonly BulletMarker _bullet = BulletMarker.Dash;

    /// <summary>
    ///     Backing field of <see cref="ListIndent"/>.
    /// </summary>
    private readonly int _listIndent = 2;

    /// <summary>
    ///     Backing field of <see cref="OrderedStyle"/>.
    /// </summary>
    private readonly OrderedStyle _orderedStyle = OrderedStyle.Incrementing;

    /// <summary>
    ///     Backing field of <see cref="Emphasis"/>.
    /// </summary>
    private readonly EmphasisMarker _emphasis = EmphasisMarker.Asterisk;

    /// <summary>
    ///     Backing field of <see cref="Strong"/>.
    /// </summary>
    private readonly EmphasisMarker _strong = EmphasisMarker.Asterisk;

    /// <summary>
    ///     Backing field of <see cref="CodeFence"/>.
    /// </summary>
    private readonly FenceMarker _codeFence = FenceMarker.Backtick;

    /// <summary>
    ///     Backing field of <see cref="CodeBlockStyle"/>.
    /// </summary>
    private readonly CodeBlockStyle _codeBlockStyle = CodeBlockStyle.Fenced;

    /// <summary>
    ///     Backing field of <see cref="HardBreak"/>.
    /// </summary>
    private readonly HardBreakStyle _hardBreak = HardBreakStyle.Spaces;

    /// <summary>
    ///     Backing field of <see cref="ThematicBreak"/>.
    /// </summary>
    private readonly ThematicBreakStyle _thematicBreak = ThematicBreakStyle.Dashes;

    /// <summary>
    ///     Backing field of <see cref="Tables"/>.
    /// </summary>
    private readonly TableStyle _tables = TableStyle.Compact;

    /// <summary>
    ///     Backing field of <see cref="BareUrls"/>.
    /// </summary>
    private readonly BareUrlStyle _bareUrls = BareUrlStyle.Autolink;

    /// <summary>
    ///     Backing field of <see cref="MaxBlankLines"/>.
    /// </summary>
    private readonly int _maxBlankLines = 1;

    /// <summary>
    ///     Backing field of <see cref="TrailingWhitespace"/>.
    /// </summary>
    private readonly TrailingWhitespaceStyle _trailingWhitespace = TrailingWhitespaceStyle.Strip;

    /// <summary>
    ///     Backing field of <see cref="LinkRefactor"/>.
    /// </summary>
    private readonly LinkRefactorOptions _linkRefactor = LinkRefactorOptions.Disabled;

    /// <summary>
    ///     Backing field of <see cref="Rules"/>.
    /// </summary>
    private readonly ImmutableSortedDictionary<string, bool> _rules = RuleCatalog.Defaults;

    /// <summary>
    ///     Backing field of <see cref="Ignore"/>.
    /// </summary>
    private readonly ImmutableArray<string> _ignore = DefaultIgnore;

    /// <summary>
    ///     Gets the glob patterns excluded by default.
    /// </summary>
    public static ImmutableArray<string> DefaultIgnore { get; } =
        [".git/**", "node_modules/**", "**/.venv/**", "**/generated/**", "**/thirdparty/**", ".agent-logs/**"];

    /// <summary>
    ///     Gets the configuration that has every documented default.
    /// </summary>
    public static LintMarkConfig Default { get; } = new();

    /// <summary>
    ///     Gets the maximum line width; 0 means unlimited (default 120).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown by the initializer when the value is not in 0 to <see cref="MaxNumber"/>.</exception>
    public int LineLength
    {
        get => _lineLength;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, MaxNumber);
            _lineLength = value;
        }
    }

    /// <summary>
    ///     Gets how paragraphs are wrapped (default <see cref="WrapMode.Fill"/>).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown by the initializer when the value is not a defined member.</exception>
    public WrapMode Wrap
    {
        get => _wrap;
        init => _wrap = CheckDefined(value);
    }

    /// <summary>
    ///     Gets the line ending to write, or <see langword="null"/> when the configuration does not specify one
    ///     (the editor settings and then the file's own majority ending decide later).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown by the initializer when the value is not a defined member.</exception>
    public LineEnding? EndOfLine
    {
        get => _endOfLine;
        init => _endOfLine = value.HasValue ? CheckDefined(value.Value) : null;
    }

    /// <summary>
    ///     Gets a value indicating whether a file ends with exactly one line ending (default <see langword="true"/>).
    /// </summary>
    public bool FinalNewline { get; init; } = true;

    /// <summary>
    ///     Gets the canonical heading syntax (default <see cref="HeadingStyle.Atx"/>).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown by the initializer when the value is not a defined member.</exception>
    public HeadingStyle Headings
    {
        get => _headings;
        init => _headings = CheckDefined(value);
    }

    /// <summary>
    ///     Gets the bullet list marker (default <see cref="BulletMarker.Dash"/>).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown by the initializer when the value is not a defined member.</exception>
    public BulletMarker Bullet
    {
        get => _bullet;
        init => _bullet = CheckDefined(value);
    }

    /// <summary>
    ///     Gets the indentation of nested list items in columns; 2 or 4 (default 2).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown by the initializer when the value is neither 2 nor 4.</exception>
    public int ListIndent
    {
        get => _listIndent;
        init
        {
            if (value is not (2 or 4))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The list indent must be 2 or 4.");
            }

            _listIndent = value;
        }
    }

    /// <summary>
    ///     Gets how ordered list items are numbered (default <see cref="Configuration.OrderedStyle.Incrementing"/>).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown by the initializer when the value is not a defined member.</exception>
    public OrderedStyle OrderedStyle
    {
        get => _orderedStyle;
        init => _orderedStyle = CheckDefined(value);
    }

    /// <summary>
    ///     Gets the emphasis delimiter (default <see cref="EmphasisMarker.Asterisk"/>).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown by the initializer when the value is not a defined member.</exception>
    public EmphasisMarker Emphasis
    {
        get => _emphasis;
        init => _emphasis = CheckDefined(value);
    }

    /// <summary>
    ///     Gets the strong emphasis delimiter (default <see cref="EmphasisMarker.Asterisk"/>).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown by the initializer when the value is not a defined member.</exception>
    public EmphasisMarker Strong
    {
        get => _strong;
        init => _strong = CheckDefined(value);
    }

    /// <summary>
    ///     Gets the fence character of fenced code blocks (default <see cref="FenceMarker.Backtick"/>).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown by the initializer when the value is not a defined member.</exception>
    public FenceMarker CodeFence
    {
        get => _codeFence;
        init => _codeFence = CheckDefined(value);
    }

    /// <summary>
    ///     Gets the canonical code block syntax (default <see cref="Configuration.CodeBlockStyle.Fenced"/>).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown by the initializer when the value is not a defined member.</exception>
    public CodeBlockStyle CodeBlockStyle
    {
        get => _codeBlockStyle;
        init => _codeBlockStyle = CheckDefined(value);
    }

    /// <summary>
    ///     Gets how a hard line break is written (default <see cref="HardBreakStyle.Spaces"/>).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown by the initializer when the value is not a defined member.</exception>
    public HardBreakStyle HardBreak
    {
        get => _hardBreak;
        init => _hardBreak = CheckDefined(value);
    }

    /// <summary>
    ///     Gets how a thematic break is written (default <see cref="ThematicBreakStyle.Dashes"/>).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown by the initializer when the value is not a defined member.</exception>
    public ThematicBreakStyle ThematicBreak
    {
        get => _thematicBreak;
        init => _thematicBreak = CheckDefined(value);
    }

    /// <summary>
    ///     Gets how table cells are laid out (default <see cref="TableStyle.Compact"/>).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown by the initializer when the value is not a defined member.</exception>
    public TableStyle Tables
    {
        get => _tables;
        init => _tables = CheckDefined(value);
    }

    /// <summary>
    ///     Gets how bare URLs are treated (default <see cref="BareUrlStyle.Autolink"/>).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown by the initializer when the value is not a defined member.</exception>
    public BareUrlStyle BareUrls
    {
        get => _bareUrls;
        init => _bareUrls = CheckDefined(value);
    }

    /// <summary>
    ///     Gets the largest number of consecutive blank lines kept (default 1).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown by the initializer when the value is not in 1 to <see cref="MaxNumber"/>.</exception>
    public int MaxBlankLines
    {
        get => _maxBlankLines;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, MaxNumber);
            _maxBlankLines = value;
        }
    }

    /// <summary>
    ///     Gets how trailing whitespace is treated (default <see cref="TrailingWhitespaceStyle.Strip"/>).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown by the initializer when the value is not a defined member.</exception>
    public TrailingWhitespaceStyle TrailingWhitespace
    {
        get => _trailingWhitespace;
        init => _trailingWhitespace = CheckDefined(value);
    }

    /// <summary>
    ///     Gets the options of the conversion of inline links to reference links (default <see cref="LinkRefactorOptions.Disabled"/>).
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown by the initializer when the value is null.</exception>
    public LinkRefactorOptions LinkRefactor
    {
        get => _linkRefactor;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            _linkRefactor = value;
        }
    }

    /// <summary>
    ///     Gets the enabled state of every configurable rule, keyed by canonical rule id (default
    ///     <see cref="RuleCatalog.Defaults"/>).
    /// </summary>
    /// <remarks>
    ///     The map is always complete: it holds exactly the ids of <see cref="RuleCatalog.ConfigurableRules"/>
    ///     (ordinal comparer), so a consumer never needs a second lookup of the defaults.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown by the initializer when the value is null.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown by the initializer when a key is empty or not a canonical rule id, or when a rule is missing.
    /// </exception>
    public ImmutableSortedDictionary<string, bool> Rules
    {
        get => _rules;
        init
        {
            ArgumentNullException.ThrowIfNull(value);

            // Always store an ordinal-keyed map so lookups cannot depend on the caller's comparer
            var normalized = value.WithComparers(StringComparer.Ordinal);
            if (normalized.Count != RuleCatalog.Defaults.Count
                || normalized.Keys.Any(static key => !RuleCatalog.Defaults.ContainsKey(key)))
            {
                throw new ArgumentException("The rules must contain exactly the configurable rule ids.", nameof(value));
            }

            _rules = normalized;
        }
    }

    /// <summary>
    ///     Gets the glob patterns of files that are skipped (default <see cref="DefaultIgnore"/>).
    /// </summary>
    /// <remarks>
    ///     Patterns use forward slashes and are relative to the directory of the configuration file that
    ///     applies (the current directory when none does). The engine skips the files they match when the
    ///     files were selected by a directory, a glob or the default pattern, but not files named on the
    ///     command line.
    /// </remarks>
    /// <exception cref="ArgumentException">
    ///     Thrown by the initializer when the array is the uninitialized default, or an element is null, empty,
    ///     blank, spans several lines or contains a backslash.
    /// </exception>
    public ImmutableArray<string> Ignore
    {
        get => _ignore;
        init
        {
            if (value.IsDefault)
            {
                throw new ArgumentException("The ignore patterns must be initialized.", nameof(value));
            }

            if (value.Any(static pattern => string.IsNullOrWhiteSpace(pattern)
                                            || pattern.AsSpan().IndexOfAny(LineBreakCharacters + "\\") >= 0))
            {
                throw new ArgumentException(
                    "Ignore patterns must be non-blank, single-line and use forward slashes.",
                    nameof(value));
            }

            _ignore = value;
        }
    }

    /// <summary>
    ///     Determines whether this configuration has the same settings as another, comparing the
    ///     collections by content.
    /// </summary>
    /// <param name="other">The configuration to compare with; may be null.</param>
    /// <returns><see langword="true"/> when every setting is equal.</returns>
    public bool Equals(LintMarkConfig? other)
    {
        if (other is null)
        {
            return false;
        }

        // Compare the scalar settings first, then the collections by content
        return _lineLength == other._lineLength
               && _wrap == other._wrap
               && _endOfLine == other._endOfLine
               && FinalNewline == other.FinalNewline
               && _headings == other._headings
               && _bullet == other._bullet
               && _listIndent == other._listIndent
               && _orderedStyle == other._orderedStyle
               && _emphasis == other._emphasis
               && _strong == other._strong
               && _codeFence == other._codeFence
               && _codeBlockStyle == other._codeBlockStyle
               && _hardBreak == other._hardBreak
               && _thematicBreak == other._thematicBreak
               && _tables == other._tables
               && _bareUrls == other._bareUrls
               && _maxBlankLines == other._maxBlankLines
               && _trailingWhitespace == other._trailingWhitespace
               && _linkRefactor.Equals(other._linkRefactor)
               && _rules.SequenceEqual(other._rules)
               && _ignore.SequenceEqual(other._ignore, StringComparer.Ordinal);
    }

    /// <summary>
    ///     Gets a hash code consistent with <see cref="Equals(LintMarkConfig)"/>.
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode()
    {
        // Combine every setting, including the content of the collections
        var hash = default(HashCode);
        hash.Add(_lineLength);
        hash.Add(_wrap);
        hash.Add(_endOfLine);
        hash.Add(FinalNewline);
        hash.Add(_headings);
        hash.Add(_bullet);
        hash.Add(_listIndent);
        hash.Add(_orderedStyle);
        hash.Add(_emphasis);
        hash.Add(_strong);
        hash.Add(_codeFence);
        hash.Add(_codeBlockStyle);
        hash.Add(_hardBreak);
        hash.Add(_thematicBreak);
        hash.Add(_tables);
        hash.Add(_bareUrls);
        hash.Add(_maxBlankLines);
        hash.Add(_trailingWhitespace);
        hash.Add(_linkRefactor);
        foreach (var rule in _rules)
        {
            hash.Add(rule.Key, StringComparer.Ordinal);
            hash.Add(rule.Value);
        }

        foreach (var pattern in _ignore)
        {
            hash.Add(pattern, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }

    /// <summary>
    ///     Verifies that an enumeration value is a defined member.
    /// </summary>
    /// <typeparam name="T">The enumeration type.</typeparam>
    /// <param name="value">The value to check.</param>
    /// <returns>The same value.</returns>
    /// <remarks>
    ///     A cast from an integer can produce a value no member names; accepting it would let an
    ///     unrepresentable canonical form reach the formatters.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is not a defined member.</exception>
    private static T CheckDefined<T>(T value)
        where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, $"Not a defined {typeof(T).Name} value.");
        }

        return value;
    }
}
