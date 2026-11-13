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
using DemaConsulting.LintMark.Configuration;
using DemaConsulting.LintMark.Document;
using DemaConsulting.LintMark.Linting;

namespace DemaConsulting.LintMark.Formatting;

/// <summary>
///     Turns a parsed document into the edits and line ending that bring it into canonical form, without
///     writing anything.
/// </summary>
/// <remarks>
///     <para>
///         The formatter walks the blocks of the document in source order and gives each block to the first
///         registered <see cref="IBlockFormatter"/> that accepts it. A block is left verbatim (no edit) when no
///         formatter accepts it, or when any rule of the accepting formatter is suppressed anywhere inside the
///         block. With no formatters registered (the production setup until block formatters exist) the only
///         result is the line ending: nothing in the text changes unless a formatter says so.
///     </para>
///     <para>
///         The line ending is decided here, not by an edit, because the LF-normalized text has no line ending
///         to edit. The target is the configuration's line ending, or the file's majority ending when the
///         configuration gives none. A file with any terminator that differs from the target gets one
///         <c>line-ending</c> diagnostic at line 1, column 1. That rule is always applied: it cannot be
///         configured or suppressed, because a whole-file property has no partial form.
///     </para>
///     <para>
///         A document that is <see cref="ParsedDocument.IsFormatUnsafe"/> is never formatted: the result has no
///         edit and no diagnostic, not even for line endings, so rewriting leaves the file byte for byte as it was.
///     </para>
///     <para>
///         Thread safety: an instance holds an immutable list of formatters that must themselves be safe for
///         concurrent use (see <see cref="IBlockFormatter"/>); <see cref="Format"/> keeps its state in local
///         variables, so one instance may serve every worker.
///     </para>
/// </remarks>
internal sealed class DocumentFormatter
{
    /// <summary>
    ///     The identifier of the always-applied rule that makes line endings uniform.
    /// </summary>
    public const string LineEndingRuleId = RuleCatalog.LineEndingRuleId;

    /// <summary>
    ///     The registered formatters, in priority order.
    /// </summary>
    private readonly ImmutableArray<IBlockFormatter> _formatters;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DocumentFormatter"/> class.
    /// </summary>
    /// <param name="formatters">
    ///     The block formatters in priority order (the first that accepts a block formats it), or
    ///     <see langword="null"/> for none. The list is copied.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when an element of <paramref name="formatters"/> is null.</exception>
    public DocumentFormatter(IReadOnlyList<IBlockFormatter>? formatters = null)
    {
        // Copy to an immutable array so a caller cannot change the list afterwards, and reject null elements up front
        ImmutableArray<IBlockFormatter> copy = formatters is null ? [] : ImmutableArray.CreateRange(formatters);
        if (copy.Any(static formatter => formatter is null))
        {
            throw new ArgumentNullException(nameof(formatters), "The formatters must not contain null.");
        }

        _formatters = copy;
    }

    /// <summary>
    ///     Formats a document.
    /// </summary>
    /// <param name="document">The parsed document. Must not be null.</param>
    /// <param name="config">The effective configuration of the file. Must not be null.</param>
    /// <returns>The edits, the line ending to write and the formatting violations found.</returns>
    /// <remarks>
    ///     Pure: nothing is written and the document is not changed. The edits are not applied here, so the
    ///     caller can still verify the result before anything reaches a file.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when two formatters propose conflicting edits (a defect of a formatter).</exception>
    public FormatResult Format(ParsedDocument document, LintMarkConfig config)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(config);

        // A document that cannot be trusted is never touched
        var source = document.Source;
        if (document.IsFormatUnsafe)
        {
            return new FormatResult([], source.MajorityLineEnding, []);
        }

        // Collect the edits of the first formatter that accepts each block, skipping blocks with suppressed rules
        var edits = new EditSet();
        var context = new FormatContext(document, config);
        foreach (var block in document.Blocks)
        {
            var formatter = _formatters.FirstOrDefault(candidate => candidate.CanFormat(block));
            if (formatter is null || IsSuppressed(document, block, formatter))
            {
                continue;
            }

            formatter.Format(context, block, edits);
        }

        // The line ending is the configured one, or else the one the file uses most
        var target = config.EndOfLine ?? source.MajorityLineEnding;
        return new FormatResult(edits.ToImmutable(), target, CheckLineEndings(source, target));
    }

    /// <summary>
    ///     Determines whether any rule of a formatter is suppressed inside a block.
    /// </summary>
    /// <param name="document">The document holding the suppressions.</param>
    /// <param name="block">The block.</param>
    /// <param name="formatter">The formatter that would format the block.</param>
    /// <returns><see langword="true"/> when the block must be left verbatim.</returns>
    private static bool IsSuppressed(ParsedDocument document, BlockInfo block, IBlockFormatter formatter) =>
        formatter.RuleIds.Any(rule => document.Suppressions.IsSuppressedRange(block.Start, block.EndInclusive + 1, rule));

    /// <summary>
    ///     Reports the line-ending violation of a file, if any.
    /// </summary>
    /// <param name="source">The source document with the counts of the original terminators.</param>
    /// <param name="target">The line ending the file is written with.</param>
    /// <returns>One diagnostic at 1:1 when a terminator differs from the target; otherwise none.</returns>
    private static ImmutableArray<Diagnostic> CheckLineEndings(SourceDocument source, LineEnding target)
    {
        // A terminator that differs from the target makes the file a violation; a file without terminators never is
        var counts = source.LineEndings;
        var other = target == LineEnding.Lf ? counts.CrLfCount : counts.LfCount;
        if (other == 0)
        {
            return [];
        }

        // Name what the file has now and what it should have
        var expected = Describe(target);
        string message;
        if (counts.IsMixed)
        {
            message = $"Mixed line endings ({counts.CrLfCount} CRLF, {counts.LfCount} LF); expected {expected}";
        }
        else
        {
            var actual = Describe(target == LineEnding.Lf ? LineEnding.CrLf : LineEnding.Lf);
            message = $"Line endings are {actual}; expected {expected}";
        }

        return [new Diagnostic(LineEndingRuleId, DiagnosticKind.Formatting, source.Path, 1, 1, message)];
    }

    /// <summary>
    ///     Names a line ending for messages.
    /// </summary>
    /// <param name="ending">The line ending.</param>
    /// <returns><c>CRLF</c> or <c>LF</c>.</returns>
    private static string Describe(LineEnding ending) => ending == LineEnding.CrLf ? "CRLF" : "LF";
}
