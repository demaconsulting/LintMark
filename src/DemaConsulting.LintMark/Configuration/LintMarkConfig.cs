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

/// <summary>
///     The options of the opt-in conversion of inline links to reference links (the <c>link-refactor</c> setting).
/// </summary>
/// <param name="Enabled">Whether inline links are converted.</param>
/// <param name="Threshold">Inline-link source width that triggers conversion; 1 to <see cref="LintMarkConfig.MaxNumber"/>.</param>
/// <param name="InTables">Whether links inside tables are converted.</param>
/// <param name="BreakLinkText">Whether the link text may be broken across lines.</param>
/// <param name="LabelMaxLength">Maximum length of a generated label; 1 to <see cref="LintMarkConfig.MaxNumber"/>.</param>
/// <param name="DefinitionsMarker">A single-line HTML comment that introduces the definitions (see <see cref="IsValidMarker"/>).</param>
/// <remarks>
///     <para>
///         Supporting type of <see cref="LintMarkConfig"/>. The record is immutable; the configuration parser
///         validates the values that come from a file.
///     </para>
///     <para>
///         Thread safety: instances are immutable and may be shared between threads.
///     </para>
/// </remarks>
internal sealed record LinkRefactorOptions(bool Enabled, int Threshold, bool InTables, bool BreakLinkText, int LabelMaxLength, string DefinitionsMarker)
{
    /// <summary>
    ///     The default comment that introduces the generated link reference definitions.
    /// </summary>
    public const string DefaultMarker = "<!-- Link References -->";

    /// <summary>
    ///     The shortest possible marker: an empty HTML comment.
    /// </summary>
    private const int MinimumMarkerLength = 7;

    /// <summary>
    ///     The text that opens an HTML comment.
    /// </summary>
    private const string CommentOpen = "<!--";

    /// <summary>
    ///     The text that closes an HTML comment.
    /// </summary>
    private const string CommentClose = "-->";

    /// <summary>
    ///     Gets the disabled default: conversion off, threshold 60, tables included, link text not broken,
    ///     labels up to 40 characters, and the default marker.
    /// </summary>
    public static LinkRefactorOptions Disabled { get; } = new(false, 60, true, false, 40, DefaultMarker);

    /// <summary>
    ///     Determines whether text is acceptable as a definitions marker.
    /// </summary>
    /// <param name="value">The text to test.</param>
    /// <returns>
    ///     <see langword="true"/> when the text is exactly one single-line HTML comment (it starts with
    ///     <c>&lt;!--</c>, the text after that does not start with <c>&gt;</c> or <c>-&gt;</c>, the first
    ///     <c>--&gt;</c> after the opening is the end of the text, and there is no line break); otherwise
    ///     <see langword="false"/>.
    /// </returns>
    public static bool IsValidMarker(string value)
    {
        // A marker is written on a line of its own, so it must be one comment on one line
        if (value.Length < MinimumMarkerLength
            || !value.StartsWith(CommentOpen, StringComparison.Ordinal)
            || value.AsSpan().IndexOfAny(LintMarkConfig.LineBreakCharacters) >= 0)
        {
            return false;
        }

        // Text right after the opening that starts with ">" or "->" would end the comment immediately (an HTML parser
        // treats those as empty comments), and the first end delimiter must be the final three characters so that
        // nothing follows the comment
        var afterOpen = value.AsSpan(CommentOpen.Length);
        return !afterOpen.StartsWith(">") && !afterOpen.StartsWith("->")
               && value.IndexOf(CommentClose, CommentOpen.Length, StringComparison.Ordinal) == value.Length - CommentClose.Length;
    }
}

/// <summary>
///     How bare URLs in text are treated (the <c>bare-urls</c> setting).
/// </summary>
/// <remarks>
///     The YAML token of each member is its lower-case name. Supporting type of <see cref="LintMarkConfig"/>.
/// </remarks>
internal enum BareUrlStyle
{
    /// <summary>A bare URL is wrapped in angle brackets (<c>&lt;url&gt;</c>).</summary>
    Autolink,

    /// <summary>A bare URL is left as written.</summary>
    Leave
}

/// <summary>
///     The marker character of bullet list items (the <c>bullet</c> setting).
/// </summary>
/// <remarks>
///     The YAML token of each member is its lower-case name. Supporting type of <see cref="LintMarkConfig"/>.
/// </remarks>
internal enum BulletMarker
{
    /// <summary>A hyphen-minus (<c>-</c>).</summary>
    Dash,

    /// <summary>An asterisk (<c>*</c>).</summary>
    Asterisk,

    /// <summary>A plus sign (<c>+</c>).</summary>
    Plus
}

/// <summary>
///     The canonical code block syntax (the <c>code-block-style</c> setting).
/// </summary>
/// <remarks>
///     A single value today; the setting exists so the canonical form is explicit and a later
///     style can be added without changing the file format. The YAML token of each member is its
///     lower-case name. Supporting type of <see cref="LintMarkConfig"/>.
/// </remarks>
internal enum CodeBlockStyle
{
    /// <summary>Code blocks are fenced.</summary>
    Fenced
}

/// <summary>
///     The delimiter character of emphasis and strong emphasis (the <c>emphasis</c> and <c>strong</c> settings).
/// </summary>
/// <remarks>
///     The YAML token of each member is its lower-case name. Supporting type of <see cref="LintMarkConfig"/>.
/// </remarks>
internal enum EmphasisMarker
{
    /// <summary>An asterisk (<c>*</c> or <c>**</c>).</summary>
    Asterisk,

    /// <summary>An underscore (<c>_</c> or <c>__</c>).</summary>
    Underscore
}

/// <summary>
///     The fence character of fenced code blocks (the <c>code-fence</c> setting).
/// </summary>
/// <remarks>
///     The YAML token of each member is its lower-case name. Supporting type of <see cref="LintMarkConfig"/>.
/// </remarks>
internal enum FenceMarker
{
    /// <summary>Three or more backticks.</summary>
    Backtick,

    /// <summary>Three or more tildes.</summary>
    Tilde
}

/// <summary>
///     How a hard line break is written (the <c>hard-break</c> setting).
/// </summary>
/// <remarks>
///     The YAML token of each member is its lower-case name. Supporting type of <see cref="LintMarkConfig"/>.
/// </remarks>
internal enum HardBreakStyle
{
    /// <summary>Two trailing spaces.</summary>
    Spaces,

    /// <summary>A trailing backslash.</summary>
    Backslash
}

/// <summary>
///     The canonical heading syntax (the <c>heading-style</c> setting).
/// </summary>
/// <remarks>
///     A single value today; the setting exists so the canonical form is explicit and a later
///     style can be added without changing the file format. The YAML token of each member is its
///     lower-case name. Supporting type of <see cref="LintMarkConfig"/>.
/// </remarks>
internal enum HeadingStyle
{
    /// <summary>Headings start with number signs (<c># Title</c>).</summary>
    Atx
}

/// <summary>
///     How ordered list items are numbered (the <c>ordered-style</c> setting).
/// </summary>
/// <remarks>
///     The YAML token of each member is its lower-case name. Supporting type of <see cref="LintMarkConfig"/>.
/// </remarks>
internal enum OrderedStyle
{
    /// <summary>Items count up (<c>1.</c>, <c>2.</c>, <c>3.</c>).</summary>
    Incrementing,

    /// <summary>Every item repeats the number of the first item (<c>1.</c>, <c>1.</c>, <c>1.</c>).</summary>
    One
}

/// <summary>
///     How table cells are laid out (the <c>tables</c> setting).
/// </summary>
/// <remarks>
///     The YAML token of each member is its lower-case name. Supporting type of <see cref="LintMarkConfig"/>.
/// </remarks>
internal enum TableStyle
{
    /// <summary>Cells are separated by single spaces without padding.</summary>
    Compact,

    /// <summary>Columns are padded to align when the aligned table fits the line length, otherwise compact.</summary>
    Aligned
}

/// <summary>
///     How a thematic break (horizontal rule) is written (the <c>thematic-break</c> setting).
/// </summary>
/// <remarks>
///     The YAML token of each member is its lower-case name. Supporting type of <see cref="LintMarkConfig"/>.
/// </remarks>
internal enum ThematicBreakStyle
{
    /// <summary>Three hyphen-minus characters (<c>---</c>).</summary>
    Dashes,

    /// <summary>Three asterisks (<c>***</c>).</summary>
    Asterisks,

    /// <summary>Three underscores (<c>___</c>).</summary>
    Underscores
}

/// <summary>
///     How trailing whitespace is treated (the <c>trailing-whitespace</c> setting).
/// </summary>
/// <remarks>
///     A single value today; the setting exists so the canonical form is explicit and a later
///     style can be added without changing the file format. The YAML token of each member is its
///     lower-case name. Supporting type of <see cref="LintMarkConfig"/>.
/// </remarks>
internal enum TrailingWhitespaceStyle
{
    /// <summary>Trailing whitespace is removed (hard breaks are preserved per the hard-break style).</summary>
    Strip
}

/// <summary>
///     How paragraphs are wrapped (the <c>wrap</c> setting).
/// </summary>
/// <remarks>
///     Every option is one deterministic canonical form, so a configuration that would leave a
///     violation unfixable cannot be expressed. The YAML token of each member is its lower-case name.
///     Supporting type of <see cref="LintMarkConfig"/>.
/// </remarks>
internal enum WrapMode
{
    /// <summary>Re-wrap a paragraph only when a line exceeds the line length.</summary>
    Fill,

    /// <summary>Start every sentence on a new line.</summary>
    Sentence,

    /// <summary>Start every sentence on a new line, and break a sentence that is too wide at the latest clause break that fits.</summary>
    Semantic
}
