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
using DemaConsulting.LintMark.Formatting;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Subsystem tests of the Formatting subsystem: the document formatter is driven with the production block
///     formatters, the inline normalizer, the paragraph formatter and the document passes, with no substitutes.
/// </summary>
public class FormattingSubsystemTests
{
    /// <summary>
    ///     Test that the formatter gives every block to one formatter, composes the edits of all rule families into one
    ///     ordered list, decides the line ending, and reports a violation for every rule that changed something.
    /// </summary>
    [Fact]
    public void FormattingSubsystem_Orchestration_DefaultFormatter_ComposesEditsOfAllRuleFamilies()
    {
        // Arrange: a CRLF document with a closed heading, trailing whitespace and a star bullet list
        var source = SourceDocument.Create("a.md", Encoding.UTF8.GetBytes("# Title #\r\n\r\ntext \r\nmore\r\n\r\n* a\r\n* b\r\n"));
        var document = DocumentParser.Parse(source);
        var formatter = BlockFormatterAssertions.ProductionFormatter();

        // Act: format with the defaults and with a configuration that asks for LF
        var result = formatter.Format(document, LintMarkConfig.Default);
        var asked = formatter.Format(document, LintMarkConfig.Default with { EndOfLine = LineEnding.Lf });

        // Assert: one ordered edit list produces the canonical text, the majority ending is kept unless the configuration asks,
        // and the violations name the rules that changed something
        Assert.Equal("# Title\n\ntext\nmore\n\n- a\n- b\n", SourcePatcher.Apply(source, result.Edits));
        Assert.Equal(result.Edits.OrderBy(edit => edit.Start).ThenBy(edit => edit.Length), result.Edits);
        Assert.Equal(LineEnding.CrLf, result.LineEnding);
        Assert.Equal(LineEnding.Lf, asked.LineEnding);
        Assert.Equal(
            ["bullet", "heading-style", "trailing-whitespace"],
            result.Diagnostics.Where(d => d.Kind == Linting.DiagnosticKind.Formatting).Select(d => d.RuleId).Distinct().Order(StringComparer.Ordinal));
    }

    /// <summary>
    ///     Test that the document passes remove trailing whitespace and tabs, limit blank lines, put blank lines around
    ///     blocks and end the file with one line ending, and that a second run has nothing to do.
    /// </summary>
    [Fact]
    public void FormattingSubsystem_Passes_WhitespaceBlankLinesBlockSpacingAndFileEnd_AreApplied()
    {
        // Arrange and Act and Assert: a text with trailing spaces, a tab, a run of blank lines, a fence that touches its
        // paragraph and no final line ending is formatted to the expected text, and that text is stable
        BlockFormatterAssertions.AssertFormats(
            "text  \n\n\n\nwith\ttab\n```text\ncode\n```\nlast",
            "text\n\nwith    tab\n\n```text\ncode\n```\n\nlast\n");
    }

    /// <summary>
    ///     Test that edits are checked for conflicts as they are added, applied in one pass that leaves other text
    ///     as it is, and rebased so that edits computed on edited text apply to the original text with the same result.
    /// </summary>
    [Fact]
    public void FormattingSubsystem_Edits_ConflictsPatchAndRebase_ComposeWithoutLosingText()
    {
        // Arrange: a text, two edits that do not conflict, an edit that conflicts with the first, and a follow-up edit of the edited text
        const string text = "abcdef";
        var source = SourceDocument.FromText("a.md", text);
        var replace = new TextEdit(1, 1, "XX", "test");
        var insert = new TextEdit(4, 0, "Y", "test");
        var conflicting = new TextEdit(1, 2, "Z", "test");
        var edited = SourcePatcher.Apply(source, [replace]);
        var followUp = new TextEdit(3, 1, "Z", "test");

        // Act: add the edits in the opposite order, add the conflicting one, and rebase the follow-up onto the original text
        var set = new EditSet();
        set.Add(insert);
        set.Add(replace);
        var conflict = Assert.Throws<InvalidOperationException>(() => set.Add(conflicting));
        var rebased = EditRebaser.Rebase([replace], [followUp]);

        // Assert: the set is ordered whatever the order of adding, the patch changes only what is edited, the conflict names both
        // edits, and the rebased edits turn the original text into what the two steps produce
        Assert.Equal([replace, insert], set.ToImmutable());
        Assert.Equal("aXXcdYef", SourcePatcher.Apply(source, set.ToImmutable()));
        Assert.Contains("conflicts with edit", conflict.Message);
        Assert.Equal("aXXZdef", SourcePatcher.Apply(SourceDocument.FromText("a.md", edited), [followUp]));
        Assert.Equal("aXXZdef", SourcePatcher.Apply(source, rebased));
    }

    /// <summary>
    ///     Test that code, HTML, math and front matter are never changed, and that a document that has to be read without
    ///     grid tables is not rewritten at all.
    /// </summary>
    [Fact]
    public void FormattingSubsystem_Protection_CodeHtmlMathFrontMatterAndUnsafeDocuments_AreLeftAlone()
    {
        // Arrange: protected regions that hold trailing whitespace and blank-line runs, and a document with an invalid grid table
        const string protectedText =
            "---\ntitle:  x  \n---\n\n```text\ncode  \n\n\n\nmore\n```\n\n<div>\n  html  \n</div>\n\n$$\nx  \n$$\n";
        const string unsafeText = "para\n+---+---+\n\n\n\ntext  \n";
        var formatter = BlockFormatterAssertions.ProductionFormatter();

        // Act: format both
        var (protectedResult, _) = BlockFormatterAssertions.Apply(formatter, protectedText);
        var (unsafeResult, unsafeFormatted) = BlockFormatterAssertions.Apply(formatter, unsafeText);

        // Assert: no edit for the protected regions, and none for the unsafe document, which is recognized as such
        Assert.Empty(protectedResult.Edits);
        Assert.True(BlockFormatterAssertions.Parse(unsafeText).IsFormatUnsafe);
        Assert.Empty(unsafeResult.Edits);
        Assert.Equal(unsafeText, unsafeFormatted);
    }
}
