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
