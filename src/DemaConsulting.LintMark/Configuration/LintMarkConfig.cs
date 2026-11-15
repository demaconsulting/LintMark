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
///     The complete, immutable set of settings that governs how one Markdown file is linted and formatted.
/// </summary>
/// <remarks>
///     <para>
///         Every option is an enumeration of deterministic canonical forms (or a bounded number), so a
///         configuration that would leave a formatting violation unfixable cannot be expressed. The
///         defaults reproduce the preferred style: 120 columns, overflow-only wrapping, dash bullets,
///         incrementing numbering, compact tables, and so on. Settings that are not given in a
///         configuration file keep these defaults. The values are validated where they come from, by the
///         configuration parser and the command line, not here.
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
    public const string LineBreakCharacters = "\r\n\u0085\u2028\u2029";

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
    ///     Gets the maximum line width; 0 means unlimited.
    /// </summary>
    public int LineLength { get; init; } = 120;

    /// <summary>
    ///     Gets how paragraphs are wrapped.
    /// </summary>
    public WrapMode Wrap { get; init; } = WrapMode.Fill;

    /// <summary>
    ///     Gets the line ending to write, or <see langword="null"/> when the configuration does not specify one
    ///     (the editor settings and then the file's own majority ending decide later).
    /// </summary>
    public LineEnding? EndOfLine { get; init; }

    /// <summary>
    ///     Gets a value indicating whether a file ends with exactly one line ending.
    /// </summary>
    public bool FinalNewline { get; init; } = true;

    /// <summary>
    ///     Gets the canonical heading syntax.
    /// </summary>
    public HeadingStyle Headings { get; init; } = HeadingStyle.Atx;

    /// <summary>
    ///     Gets the bullet list marker.
    /// </summary>
    public BulletMarker Bullet { get; init; } = BulletMarker.Dash;

    /// <summary>
    ///     Gets the indentation of nested list items in columns; 2 or 4.
    /// </summary>
    public int ListIndent { get; init; } = 2;

    /// <summary>
    ///     Gets how ordered list items are numbered.
    /// </summary>
    public OrderedStyle OrderedStyle { get; init; } = OrderedStyle.Incrementing;

    /// <summary>
    ///     Gets the emphasis delimiter.
    /// </summary>
    public EmphasisMarker Emphasis { get; init; } = EmphasisMarker.Asterisk;

    /// <summary>
    ///     Gets the strong emphasis delimiter.
    /// </summary>
    public EmphasisMarker Strong { get; init; } = EmphasisMarker.Asterisk;

    /// <summary>
    ///     Gets the fence character of fenced code blocks.
    /// </summary>
    public FenceMarker CodeFence { get; init; } = FenceMarker.Backtick;

    /// <summary>
    ///     Gets the canonical code block syntax.
    /// </summary>
    public CodeBlockStyle CodeBlockStyle { get; init; } = CodeBlockStyle.Fenced;

    /// <summary>
    ///     Gets how a hard line break is written.
    /// </summary>
    public HardBreakStyle HardBreak { get; init; } = HardBreakStyle.Spaces;

    /// <summary>
    ///     Gets how a thematic break is written.
    /// </summary>
    public ThematicBreakStyle ThematicBreak { get; init; } = ThematicBreakStyle.Dashes;

    /// <summary>
    ///     Gets how table cells are laid out.
    /// </summary>
    public TableStyle Tables { get; init; } = TableStyle.Compact;

    /// <summary>
    ///     Gets how bare URLs are treated.
    /// </summary>
    public BareUrlStyle BareUrls { get; init; } = BareUrlStyle.Autolink;

    /// <summary>
    ///     Gets the largest number of consecutive blank lines kept.
    /// </summary>
    public int MaxBlankLines { get; init; } = 1;

    /// <summary>
    ///     Gets how trailing whitespace is treated.
    /// </summary>
    public TrailingWhitespaceStyle TrailingWhitespace { get; init; } = TrailingWhitespaceStyle.Strip;

    /// <summary>
    ///     Gets the options of the conversion of inline links to reference links.
    /// </summary>
    public LinkRefactorOptions LinkRefactor { get; init; } = LinkRefactorOptions.Disabled;

    /// <summary>
    ///     Gets the enabled state of every configurable rule, keyed by canonical rule id (ordinal).
    /// </summary>
    /// <remarks>
    ///     The map is always complete: it holds exactly the ids of the configurable rules, so a consumer
    ///     never needs a second lookup of the defaults.
    /// </remarks>
    public ImmutableSortedDictionary<string, bool> Rules { get; init; } = RuleCatalog.Defaults;

    /// <summary>
    ///     Gets the glob patterns of files that are skipped.
    /// </summary>
    /// <remarks>
    ///     Patterns use forward slashes and are relative to the directory of the configuration file that
    ///     applies (the current directory when none does). The engine skips the files they match when the
    ///     files were selected by a directory, a glob or the default pattern, but not files named on the
    ///     command line.
    /// </remarks>
    public ImmutableArray<string> Ignore { get; init; } = DefaultIgnore;
}
