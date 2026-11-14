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
using DemaConsulting.LintMark.Configuration;
using DemaConsulting.LintMark.Document;
using DemaConsulting.LintMark.Engine;
using DemaConsulting.LintMark.Formatting;
using DemaConsulting.LintMark.Formatting.Links;
using DemaConsulting.LintMark.Formatting.Proof;
using DemaConsulting.LintMark.Linting;
using DemaConsulting.LintMark.Verify.Checks;
using DemaConsulting.LintMark.Verify.Reporting;

namespace DemaConsulting.LintMark.Verify.Verification;

/// <summary>
///     Formats one file in memory under one option set and verifies the result.
/// </summary>
/// <remarks>
///     <para>
///         The file goes through the product's real fix pipeline (<see cref="LintMarkEngine.Process(IReadOnlyList{DiscoveredFile}, ConfigLoader, EditorConfigResolver?, string?, Linting.RuleOverrides?)"/>),
///         which reads the file and never writes it; this class never writes anything either, so verifying a
///         corpus cannot change it. The refusals the formatter makes by design (an ignored file, a file that is
///         not valid UTF-8 or has a bare carriage return, a document with an invalid grid table) count as passes.
///     </para>
///     <para>
///         A formatted file must render to the same HTML (as LF text and as the bytes it is written as, which
///         catches the line-ending sensitive rendering of a multi-line code span), have the same Pandoc structure
///         tokens, be read as before (no invalid grid table border) and be stable: formatting it again, as text
///         and as bytes, changes nothing. The one thing the link refactoring writes that the page contains, the
///         comment that introduces the new definitions, is ignored in the HTML comparison only when the option
///         set turns the link refactoring on. Every check that throws is reported as a verifier error, a
///         formatter that fails as a crash.
///     </para>
///     <para>
///         Thread safety: the engine and the formatter are stateless, so one instance serves every worker.
///     </para>
/// </remarks>
internal sealed class FileVerifier
{
    /// <summary>
    ///     The byte order mark of UTF-8.
    /// </summary>
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    /// <summary>
    ///     The engine that formats a file.
    /// </summary>
    private readonly LintMarkEngine _engine;

    /// <summary>
    ///     The formatter of the second pass; the same one the engine uses.
    /// </summary>
    private readonly DocumentFormatter _formatter;

    /// <summary>
    ///     The longest diff kept for a finding, in lines.
    /// </summary>
    private readonly int _maxDiffLines;

    /// <summary>
    ///     Initializes a new instance of the <see cref="FileVerifier"/> class.
    /// </summary>
    /// <param name="formatter">The formatter to verify (a seam for tests that install broken ones); <see langword="null"/> selects the production formatter.</param>
    /// <param name="maxDiffLines">The longest diff kept for a finding, in lines.</param>
    public FileVerifier(DocumentFormatter? formatter = null, int maxDiffLines = 60)
    {
        _formatter = formatter ?? LintMarkEngine.CreateDefaultFormatter();
        _engine = new LintMarkEngine(1, formatter: _formatter);
        _maxDiffLines = maxDiffLines;
    }

    /// <summary>
    ///     Verifies one file under one option set.
    /// </summary>
    /// <param name="file">The file; an explicit file is never ignored by a configuration.</param>
    /// <param name="optionLabel">The label of the option set, for the outcome.</param>
    /// <param name="loader">Supplies the configuration of the file.</param>
    /// <param name="editorConfig">Supplies the editor settings, or <see langword="null"/> to ignore them.</param>
    /// <param name="baseDirectory">The directory that relative ignore patterns apply to.</param>
    /// <returns>The outcome; never throws.</returns>
    public Outcome Verify(DiscoveredFile file, string optionLabel, ConfigLoader loader, EditorConfigResolver? editorConfig, string baseDirectory)
    {
        var path = file.Path;

        // Run the real pipeline on the file; an exception is a crash of the product
        FileResult result;
        try
        {
            result = _engine.Process([file], loader, editorConfig, baseDirectory)[0];
        }
        catch (Exception ex)
        {
            return Outcome.Failure(path, optionLabel, OutcomeKind.Crash, $"The formatting threw {ex.GetType().Name}: {FirstLine(ex.Message)}");
        }

        // The refusals by design, then the failures of the file or of the configuration
        if (Classify(path, optionLabel, result) is { } early)
        {
            return early;
        }

        if (result.Change is not { } change || result.Config is not { } config)
        {
            return new Outcome(path, optionLabel, OutcomeKind.Unchanged);
        }

        // Verify the proposed rewrite
        var findings = new List<Finding>();
        try
        {
            CheckHtml(path, change, config, findings);
            CheckStructure(path, change, findings);
            CheckReparse(path, change, findings);
            CheckStability(path, change, config, findings);
        }
        catch (Exception ex)
        {
            findings.Add(new Finding(OutcomeKind.VerifierError, $"A check threw {ex.GetType().Name}: {FirstLine(ex.Message)}", [], []));
        }

        return findings.Count == 0 ? new Outcome(path, optionLabel, OutcomeKind.Ok) : new Outcome(path, optionLabel, findings[0].Kind, findings);
    }

    /// <summary>
    ///     Decides the outcomes that need no verification of a rewrite.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="optionLabel">The label of the option set.</param>
    /// <param name="result">The result of the pipeline.</param>
    /// <returns>The outcome, or <see langword="null"/> when a rewrite is to be verified or the file is unchanged.</returns>
    private static Outcome? Classify(string path, string optionLabel, FileResult result)
    {
        // An ignored file was never read
        if (result.Ignored)
        {
            return new Outcome(path, optionLabel, OutcomeKind.ExemptIgnored);
        }

        // A configuration problem is a problem of the option set, not of the file
        if (!result.ConfigErrors.IsDefaultOrEmpty)
        {
            return Outcome.Failure(path, optionLabel, OutcomeKind.VerifierError, "Configuration problem: " + string.Join("; ", result.ConfigErrors.Select(static e => e.Message)));
        }

        // Failures of the formatter and parser win over the exemptions
        var problems = result.Diagnostics.Where(static d => d.Kind == DiagnosticKind.LintOnly).ToList();
        if (problems.FirstOrDefault(static d => d.RuleId is RuleCatalog.FileFormatErrorRuleId or RuleCatalog.FileParseErrorRuleId) is { } crash)
        {
            return Outcome.Failure(path, optionLabel, OutcomeKind.Crash, $"{crash.RuleId}: {crash.Message}");
        }

        if (problems.FirstOrDefault(static d => d.RuleId == RuleCatalog.FileReadErrorRuleId) is { } unreadable)
        {
            return Outcome.Failure(path, optionLabel, OutcomeKind.VerifierError, $"{unreadable.RuleId}: {unreadable.Message}");
        }

        if (problems.Any(static d => d.RuleId == RuleCatalog.FileEncodingErrorRuleId))
        {
            return new Outcome(path, optionLabel, OutcomeKind.ExemptEncodingError);
        }

        if (problems.Any(static d => d.RuleId == RuleCatalog.FileLineEndingErrorRuleId))
        {
            return new Outcome(path, optionLabel, OutcomeKind.ExemptLineEndingError);
        }

        return problems.Any(static d => d.RuleId == RuleCatalog.GridTableInvalidRuleId) ? new Outcome(path, optionLabel, OutcomeKind.ExemptGridTableInvalid) : null;
    }

    /// <summary>
    ///     Checks that the formatted text and the formatted file render to the same HTML.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="change">The rewrite.</param>
    /// <param name="config">The configuration the rewrite was computed under.</param>
    /// <param name="findings">Receives the findings.</param>
    private void CheckHtml(string path, FileChange change, LintMarkConfig config, List<Finding> findings)
    {
        // The comment that introduces new link definitions is the only difference allowed, and only when the option is on
        var marker = config.LinkRefactor.Enabled ? config.LinkRefactor.DefinitionsMarker : null;
        string Visible(string original, string rewritten) =>
            marker is null ? rewritten : LinkRefactorer.WithoutAddedMarker(original, rewritten, marker);

        // The LF text, when it changed
        var textChanged = !string.Equals(change.OriginalLf, change.NewLf, StringComparison.Ordinal);
        if (textChanged)
        {
            var visible = Visible(change.OriginalLf, change.NewLf);
            var verdict = Guard(() => HtmlEquivalenceChecker.Check(change.OriginalLf, visible));
            if (!verdict.IsEquivalent)
            {
                findings.Add(new Finding(
                    OutcomeKind.HtmlMismatch,
                    verdict.Description ?? "The rendered HTML differs",
                    DiffText.Text(path, change.OriginalLf, change.NewLf, _maxDiffLines),
                    DiffText.Html(change.OriginalLf, visible, _maxDiffLines)));
                return;
            }
        }

        // The text as the bytes written to disk, with its real line endings
        var before = Decode(change.OriginalBytes.AsSpan());
        var onDisk = Visible(before, Decode(change.NewBytes.AsSpan()));
        var onDiskVerdict = Guard(() => HtmlEquivalenceChecker.CheckOnDisk(before, onDisk));
        if (!onDiskVerdict.IsEquivalent)
        {
            findings.Add(new Finding(
                OutcomeKind.HtmlMismatchOnDisk,
                onDiskVerdict.Description ?? "The rendered HTML differs",
                DiffText.Text(path, change.OriginalLf, change.NewLf, _maxDiffLines),
                DiffText.Html(before, onDisk, _maxDiffLines)));
        }
    }

    /// <summary>
    ///     Checks that the formatted text has the Pandoc structure of the original.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="change">The rewrite.</param>
    /// <param name="findings">Receives the findings.</param>
    private void CheckStructure(string path, FileChange change, List<Finding> findings)
    {
        if (string.Equals(change.OriginalLf, change.NewLf, StringComparison.Ordinal))
        {
            return;
        }

        var verdict = Guard(() => StructuralTokenChecker.Check(change.OriginalLf, change.NewLf));
        if (!verdict.IsEquivalent)
        {
            findings.Add(new Finding(
                OutcomeKind.StructureMismatch,
                verdict.Description ?? "The Pandoc structure differs",
                DiffText.Text(path, change.OriginalLf, change.NewLf, _maxDiffLines),
                []));
        }
    }

    /// <summary>
    ///     Checks that the formatted text is still read with grid tables, as a document the formatter may touch.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="change">The rewrite.</param>
    /// <param name="findings">Receives the findings.</param>
    private void CheckReparse(string path, FileChange change, List<Finding> findings)
    {
        if (string.Equals(change.OriginalLf, change.NewLf, StringComparison.Ordinal))
        {
            return;
        }

        if (DocumentParser.Parse(SourceDocument.FromText(path, change.NewLf)).IsFormatUnsafe)
        {
            findings.Add(new Finding(
                OutcomeKind.ReparseGridTable,
                "The formatted text contains a grid table border line that is not part of a valid grid table",
                DiffText.Text(path, change.OriginalLf, change.NewLf, _maxDiffLines),
                []));
        }
    }

    /// <summary>
    ///     Checks that formatting the formatted file again changes nothing, as text and as bytes.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="change">The rewrite.</param>
    /// <param name="config">The configuration the rewrite was computed under.</param>
    /// <param name="findings">Receives the findings.</param>
    private void CheckStability(string path, FileChange change, LintMarkConfig config, List<Finding> findings)
    {
        // The LF text first: this names the first line that moves
        var textChanged = !string.Equals(change.OriginalLf, change.NewLf, StringComparison.Ordinal);
        if (textChanged)
        {
            var verdict = IdempotencyChecker.Check(change.NewLf, text => ReformatText(path, text, config));
            if (!verdict.IsEquivalent)
            {
                var reformatted = string.Empty;
                try
                {
                    reformatted = ReformatText(path, change.NewLf, config);
                }
                catch (Exception)
                {
                    // The checker already described the failure; there is no second text to compare
                }

                findings.Add(new Finding(
                    OutcomeKind.NotIdempotent,
                    verdict.Description ?? "Formatting is not stable",
                    reformatted.Length == 0 ? [] : DiffText.Text(path, change.NewLf, reformatted, _maxDiffLines),
                    []));
                return;
            }
        }

        // Then the bytes: the line endings and the byte order mark have to be stable too
        var newBytes = change.NewBytes.AsSpan().ToArray();
        byte[] again;
        try
        {
            again = ReformatBytes(path, newBytes, config);
        }
        catch (Exception ex)
        {
            findings.Add(new Finding(OutcomeKind.NotIdempotent, $"Formatting the formatted file again failed ({ex.GetType().Name})", [], []));
            return;
        }

        if (!again.AsSpan().SequenceEqual(newBytes))
        {
            findings.Add(new Finding(
                OutcomeKind.NotIdempotent,
                $"Formatting the formatted file again changes its bytes (first difference at byte {FirstByteDifference(newBytes, again)})",
                [],
                []));
        }
    }

    /// <summary>
    ///     Formats LF text the way the engine formats a file.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="text">The LF-normalized text.</param>
    /// <param name="config">The configuration.</param>
    /// <returns>The formatted LF text.</returns>
    private string ReformatText(string path, string text, LintMarkConfig config)
    {
        var document = DocumentParser.Parse(SourceDocument.FromText(path, text));
        return SourcePatcher.Apply(document.Source, _formatter.Format(document, config).Edits);
    }

    /// <summary>
    ///     Formats the bytes of a file the way the engine formats it.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="bytes">The bytes.</param>
    /// <param name="config">The configuration.</param>
    /// <returns>The formatted bytes.</returns>
    private byte[] ReformatBytes(string path, byte[] bytes, LintMarkConfig config)
    {
        var document = DocumentParser.Parse(SourceDocument.Create(path, bytes));
        var format = _formatter.Format(document, config);
        return document.Source.ToBytes(SourcePatcher.Apply(document.Source, format.Edits), format.LineEnding);
    }

    /// <summary>
    ///     Runs a check and turns an exception into a result that is not equivalent.
    /// </summary>
    /// <param name="check">The check.</param>
    /// <returns>The verdict.</returns>
    private static EquivalenceResult Guard(Func<EquivalenceResult> check)
    {
        try
        {
            return check();
        }
        catch (Exception ex)
        {
            return EquivalenceResult.Different($"the check failed: {ex.GetType().Name}");
        }
    }

    /// <summary>
    ///     Decodes the bytes of a file to text, leaving out a byte order mark and keeping the line terminators.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The text.</returns>
    private static string Decode(ReadOnlySpan<byte> bytes) =>
        Encoding.UTF8.GetString(bytes.StartsWith(Utf8Bom) ? bytes[Utf8Bom.Length..] : bytes);

    /// <summary>
    ///     Finds the first position where two byte sequences differ.
    /// </summary>
    /// <param name="first">The first sequence.</param>
    /// <param name="second">The second sequence.</param>
    /// <returns>The zero-based position.</returns>
    private static int FirstByteDifference(byte[] first, byte[] second) => first.AsSpan().CommonPrefixLength(second);

    /// <summary>
    ///     Takes the first line of a message so a description stays on one line.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns>Its first line.</returns>
    private static string FirstLine(string message) => message.ReplaceLineEndings("\n").Split('\n')[0];
}
