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
using System.Text;
using DemaConsulting.LintMark.Configuration;
using DemaConsulting.LintMark.Document;
using Markdig.Syntax.Inlines;

namespace DemaConsulting.LintMark.Linting;

/// <summary>
///     Runs the lint-only rules over a document and keeps the diagnostics that are switched on and not suppressed.
/// </summary>
/// <remarks>
///     <para>
///         The engine asks every rule whose identifiers are not all switched off for its diagnostics, drops the ones
///         whose own rule is switched off in the configuration (the configuration already carries the command line
///         overrides, see <see cref="RuleOverrides"/>), drops the ones that a suppression comment covers at their position
///         (the comments name rules by id or by legacy number; the document's suppression map resolves both), and
///         returns the rest in one deterministic order: by line, column, rule identifier and message. A rule can only
///         report: <see cref="ILintRule"/> returns diagnostics and no edits, so no lint-only rule can ever change a
///         file or make a rewrite unsafe.
///     </para>
///     <para>
///         Thread safety: holds an immutable list of rules, which must be safe for concurrent use; <see cref="Run"/> keeps
///         its state in local variables, so one instance serves every worker.
///     </para>
/// </remarks>
internal sealed class LintRuleEngine
{
    /// <summary>
    ///     The rules, in registration order.
    /// </summary>
    private readonly ImmutableArray<ILintRule> _rules;

    /// <summary>
    ///     Initializes a new instance of the <see cref="LintRuleEngine"/> class.
    /// </summary>
    /// <param name="rules">The rules to run. The list is copied.</param>
    public LintRuleEngine(IReadOnlyList<ILintRule> rules)
    {
        _rules = [.. rules];
    }

    /// <summary>
    ///     Gets the engine with the lint-only rules the tool ships with.
    /// </summary>
    /// <returns>A new engine.</returns>
    public static LintRuleEngine CreateDefault() =>
        new([new HeadingRules(), new LinkRules(), new StructureRules(), new TableRules()]);

    /// <summary>
    ///     Runs the rules over a document.
    /// </summary>
    /// <param name="document">The parsed document.</param>
    /// <param name="config">The effective configuration of the file.</param>
    /// <returns>The diagnostics of the rules that are switched on and not suppressed, ordered by line, column, rule and message.</returns>
    public ImmutableArray<Diagnostic> Run(ParsedDocument document, LintMarkConfig config)
    {
        var found = new List<Diagnostic>();
        foreach (var rule in _rules)
        {
            // A rule none of whose identifiers is switched on has nothing to report
            if (!rule.RuleIds.Any(id => IsEnabled(config, id)))
            {
                continue;
            }

            foreach (var diagnostic in rule.Check(document, config))
            {
                var offset = document.Source.OffsetOf(diagnostic.Line, diagnostic.Column);
                if (IsEnabled(config, diagnostic.RuleId) && !document.Suppressions.IsSuppressed(offset, diagnostic.RuleId))
                {
                    found.Add(diagnostic);
                }
            }
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

    /// <summary>
    ///     Determines whether a rule is switched on in a configuration.
    /// </summary>
    /// <param name="config">The configuration.</param>
    /// <param name="ruleId">The canonical rule identifier.</param>
    /// <returns><see langword="true"/> unless the configuration switches the rule off; an identifier that is not configurable is always on.</returns>
    private static bool IsEnabled(LintMarkConfig config, string ruleId) =>
        !config.Rules.TryGetValue(ruleId, out var enabled) || enabled;
}

/// <summary>
///     A rule that only reports: it finds a problem that formatting cannot fix and never proposes an edit.
/// </summary>
/// <remarks>
///     <para>
///         A lint rule reads a parsed document and returns diagnostics of kind <see cref="DiagnosticKind.LintOnly"/>.
///         It does not look at suppression comments or at whether it is switched off in the configuration: the
///         <see cref="LintRuleEngine"/> filters its diagnostics by the rule identifier they carry.
///     </para>
///     <para>
///         Thread safety: an implementation must be stateless (or immutable) so that one instance serves every worker.
///     </para>
/// </remarks>
internal interface ILintRule
{
    /// <summary>
    ///     Gets the identifiers of the rules whose diagnostics this implementation reports.
    /// </summary>
    ImmutableArray<string> RuleIds { get; }

    /// <summary>
    ///     Finds the problems of a document.
    /// </summary>
    /// <param name="document">The parsed document. Must not be null.</param>
    /// <param name="config">The effective configuration of the file. Must not be null.</param>
    /// <returns>The problems, in any order; each one carries one of <see cref="RuleIds"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    ImmutableArray<Diagnostic> Check(ParsedDocument document, LintMarkConfig config);
}

/// <summary>
///     Helpers shared by the lint-only rule units: creating a finding at an offset and reading the plain text of an inline tree.
/// </summary>
/// <remarks>
///     Thread safety: static and stateless; safe for concurrent use.
/// </remarks>
internal static class RuleSupport
{
    /// <summary>
    ///     Creates a lint-only finding at an offset of the document.
    /// </summary>
    /// <param name="source">The source document that holds the offset and the path.</param>
    /// <param name="ruleId">The canonical rule identifier.</param>
    /// <param name="offset">The offset into the LF-normalized text; a value outside the text is moved to the nearest end.</param>
    /// <param name="message">The message; line breaks are replaced by blanks so that the report keeps one line per finding.</param>
    /// <returns>The finding.</returns>
    public static Diagnostic Finding(SourceDocument source, string ruleId, int offset, string message)
    {
        var (line, column) = source.PositionOf(Math.Clamp(offset, 0, source.Text.Length));
        return Diagnostic.LintOnly(source.Path, ruleId, line, column, message);
    }

    /// <summary>
    ///     Reads the plain text of an inline tree: the text, the content of code spans and the text of links and emphasis.
    /// </summary>
    /// <param name="root">The inline content, or <see langword="null"/> when there is none.</param>
    /// <param name="includeImages">Whether the alternative text of an image is part of the text; <see langword="false"/> gives an image no text at all.</param>
    /// <returns>The text; line breaks are blanks and inline HTML contributes nothing.</returns>
    public static string PlainText(ContainerInline? root, bool includeImages = true)
    {
        var builder = new StringBuilder();
        if (root is not null)
        {
            Append(root, builder, includeImages);
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Collapses every run of whitespace to one blank and trims the ends.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The normalized text.</returns>
    public static string CollapseWhitespace(string text)
    {
        var builder = new StringBuilder(text.Length);
        var pending = false;
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                pending = builder.Length > 0;
            }
            else
            {
                if (pending)
                {
                    builder.Append(' ');
                    pending = false;
                }

                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Appends the plain text of an inline to a builder.
    /// </summary>
    /// <param name="inline">The inline.</param>
    /// <param name="builder">The builder.</param>
    /// <param name="includeImages">Whether the alternative text of an image is read.</param>
    private static void Append(Inline inline, StringBuilder builder, bool includeImages)
    {
        switch (inline)
        {
            case LiteralInline literal:
                builder.Append(literal.Content.ToString());
                break;
            case CodeInline code:
                builder.Append(code.Content);
                break;
            case LineBreakInline:
                builder.Append(' ');
                break;
            case HtmlEntityInline entity:
                builder.Append(entity.Transcoded.ToString());
                break;
            case LinkInline { IsImage: true } when !includeImages:
                break;
            case ContainerInline container:
                foreach (var child in container)
                {
                    Append(child, builder, includeImages);
                }

                break;
            default:
                break;
        }
    }
}
