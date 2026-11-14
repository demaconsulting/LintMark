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
using DemaConsulting.LintMark.Formatting;

namespace DemaConsulting.LintMark.Linting;

/// <summary>
///     Turns what the formatter would change into the findings that report it, and keeps the formatting side of the
///     report consistent with the edits.
/// </summary>
/// <remarks>
///     <para>
///         The formatter reports its own findings (a line that is too wide, a paragraph that is not wrapped, an edit of an
///         always-applied rule). The analyzer is the one place that decides what is finally reported for a rewrite: it
///         keeps those findings, and adds a fixable finding for any rule that proposes an edit but has no fixable finding
///         at all, so that "the formatter would change this" is never silent. The finding is placed at the first edit of the
///         rule and carries the description of the rule. Edits of the two optional rules (<c>line-length</c> and
///         <c>paragraph-wrap</c>), of an identifier that is not a registered formatting rule and of a lint-only rule
///         report nothing here: the optional rules have findings of their own (with their own conditions), and a lint-only rule cannot
///         propose an edit in the first place. The result is ordered by line, column, rule and message, so that it does not
///         depend on the order in which the formatter produced it.
///     </para>
///     <para>
///         Thread safety: static and stateless; safe for concurrent use.
///     </para>
/// </remarks>
internal static class FormatDiffAnalyzer
{
    /// <summary>
    ///     Gets the findings of a formatting run.
    /// </summary>
    /// <param name="source">The source document the edits apply to. Must not be null.</param>
    /// <param name="result">The result of the formatter. Must not be null.</param>
    /// <returns>The fixable and formatter-reported findings, ordered by line, column, rule and message.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public static ImmutableArray<Diagnostic> Analyze(SourceDocument source, FormatResult result)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(result);

        // A rule that already has a fixable finding needs no second one
        var reported = result.Diagnostics
            .Where(static diagnostic => diagnostic.Fixable)
            .Select(static diagnostic => diagnostic.RuleId)
            .ToHashSet(StringComparer.Ordinal);
        var found = new List<Diagnostic>(result.Diagnostics);

        // The edits are in position order, so the first edit of a rule is the one that is reported
        foreach (var edit in result.Edits)
        {
            if (!RuleCatalog.TryGet(edit.RuleId, out var rule)
                || rule.Kind != DiagnosticKind.Formatting
                || rule.IsConfigurable
                || !reported.Add(rule.Id))
            {
                continue;
            }

            var (line, column) = source.PositionOf(Math.Min(edit.Start, Math.Max(source.Text.Length - 1, 0)));
            found.Add(new Diagnostic(rule.Id, DiagnosticKind.Formatting, source.Path, line, column, rule.Description));
        }

        return
        [
            .. found
                .OrderBy(static diagnostic => diagnostic.Line)
                .ThenBy(static diagnostic => diagnostic.Column)
                .ThenBy(static diagnostic => diagnostic.RuleId, StringComparer.Ordinal)
                .ThenBy(static diagnostic => diagnostic.Message, StringComparer.Ordinal)
        ];
    }
}
