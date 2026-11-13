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

namespace DemaConsulting.LintMark.Configuration;

/// <summary>
///     The options of the opt-in conversion of inline links to reference links (the <c>link-refactor</c> setting).
/// </summary>
/// <remarks>
///     <para>
///         Supporting type of <see cref="LintMarkConfig"/>. The record is immutable and every accessor
///         validates, including the ones used by a <c>with</c> expression, so an invalid instance cannot
///         be created.
///     </para>
///     <para>
///         Thread safety: instances are immutable and may be shared between threads.
///     </para>
/// </remarks>
internal sealed record LinkRefactorOptions
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
    ///     Backing field of <see cref="Threshold"/>.
    /// </summary>
    private readonly int _threshold;

    /// <summary>
    ///     Backing field of <see cref="LabelMaxLength"/>.
    /// </summary>
    private readonly int _labelMaxLength;

    /// <summary>
    ///     Backing field of <see cref="DefinitionsMarker"/>.
    /// </summary>
    private readonly string _definitionsMarker = DefaultMarker;

    /// <summary>
    ///     Initializes a new instance of the <see cref="LinkRefactorOptions"/> class.
    /// </summary>
    /// <param name="enabled">Whether inline links are converted.</param>
    /// <param name="threshold">Inline-link source width that triggers conversion; 1 to <see cref="LintMarkConfig.MaxNumber"/>.</param>
    /// <param name="inTables">Whether links inside tables are converted.</param>
    /// <param name="breakLinkText">Whether the link text may be broken across lines.</param>
    /// <param name="labelMaxLength">Maximum length of a generated label; 1 to <see cref="LintMarkConfig.MaxNumber"/>.</param>
    /// <param name="definitionsMarker">A single-line HTML comment that introduces the definitions. Must not be null.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a number is outside its range.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="definitionsMarker"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="definitionsMarker"/> is not a single-line HTML comment.</exception>
    public LinkRefactorOptions(bool enabled, int threshold, bool inTables, bool breakLinkText, int labelMaxLength, string definitionsMarker)
    {
        // Assign through the validating accessors so a constructor call and a with expression share one set of rules
        Enabled = enabled;
        Threshold = threshold;
        InTables = inTables;
        BreakLinkText = breakLinkText;
        LabelMaxLength = labelMaxLength;
        DefinitionsMarker = definitionsMarker;
    }

    /// <summary>
    ///     Gets the disabled default: conversion off, threshold 60, tables included, link text not broken,
    ///     labels up to 40 characters, and the default marker.
    /// </summary>
    public static LinkRefactorOptions Disabled { get; } = new(false, 60, true, false, 40, DefaultMarker);

    /// <summary>
    ///     Gets a value indicating whether inline links are converted to reference links.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    ///     Gets the inline-link source width that triggers conversion.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown by the initializer when the value is not in 1 to <see cref="LintMarkConfig.MaxNumber"/>.</exception>
    public int Threshold
    {
        get => _threshold;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, LintMarkConfig.MaxNumber);
            _threshold = value;
        }
    }

    /// <summary>
    ///     Gets a value indicating whether links inside tables are converted.
    /// </summary>
    public bool InTables { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the link text may be broken across lines.
    /// </summary>
    public bool BreakLinkText { get; init; }

    /// <summary>
    ///     Gets the maximum length of a generated label.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown by the initializer when the value is not in 1 to <see cref="LintMarkConfig.MaxNumber"/>.</exception>
    public int LabelMaxLength
    {
        get => _labelMaxLength;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, LintMarkConfig.MaxNumber);
            _labelMaxLength = value;
        }
    }

    /// <summary>
    ///     Gets the single-line HTML comment that introduces the generated link reference definitions.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown by the initializer when the value is null.</exception>
    /// <exception cref="ArgumentException">Thrown by the initializer when the value is not a single-line HTML comment.</exception>
    public string DefinitionsMarker
    {
        get => _definitionsMarker;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            if (!IsValidMarker(value))
            {
                throw new ArgumentException("The definitions marker must be a single-line HTML comment.", nameof(value));
            }

            _definitionsMarker = value;
        }
    }

    /// <summary>
    ///     Determines whether text is acceptable as a definitions marker.
    /// </summary>
    /// <param name="value">The text to test. Must not be null.</param>
    /// <returns>
    ///     <see langword="true"/> when the text is exactly one single-line HTML comment (it starts with
    ///     <c>&lt;!--</c>, the text after that does not start with <c>&gt;</c> or <c>-&gt;</c>, the first
    ///     <c>--&gt;</c> after the opening is the end of the text, and there is no line break); otherwise
    ///     <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///     Shared with the configuration loader so that a file is rejected with a message
    ///     exactly when the accessor would have thrown.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    public static bool IsValidMarker(string value)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(value);

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
