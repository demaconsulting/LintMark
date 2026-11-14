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

using DemaConsulting.LintMark.Configuration;
using DemaConsulting.LintMark.Document;
using DemaConsulting.LintMark.Linting;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Corpus tests for the lint-only rules: the documents of this repository, the synthetic nesting documents and
///     seeded random documents never make a rule throw, give the same findings every time, and, for this repository, no finding at all.
/// </summary>
public class LintRulesCorpusTests
{
    /// <summary>
    ///     The configuration that switches every lint-only rule on.
    /// </summary>
    private static readonly LintMarkConfig AllRules = LintMarkConfig.Default with
    {
        Rules = RuleCatalog.ConfigurableRules
            .Where(static id => RuleCatalog.TryGet(id, out var rule) && rule.Kind == DiagnosticKind.LintOnly)
            .Aggregate(LintMarkConfig.Default.Rules, static (rules, id) => rules.SetItem(id, true))
    };

    /// <summary>
    ///     Runs the default engine over a text and renders the findings.
    /// </summary>
    /// <param name="engine">The engine.</param>
    /// <param name="text">The text.</param>
    /// <returns>One string per finding.</returns>
    private static string[] Run(LintRuleEngine engine, string text) =>
        [.. engine.Run(DocumentParser.Parse(SourceDocument.FromText("a.md", text)), AllRules).Select(d => $"{d.Line}:{d.Column} {d.RuleId} {d.Message}")];

    /// <summary>
    ///     Test that the documents of this repository, which the markdown linter accepts, have no finding of any lint-only rule that is on by default.
    /// </summary>
    [Fact]
    public void LintRulesCorpus_RepositoryDocuments_HaveNoDefaultLintOnlyFindings()
    {
        // Arrange: every tracked Markdown file of the repository, front matter and all
        var engine = LintRuleEngine.CreateDefault();
        var found = new List<string>();

        // Act: run the rules with the default configuration over every file
        foreach (var file in RepositoryMarkdownFiles.Enumerate())
        {
            var document = DocumentParser.Parse(SourceDocument.Create(file, File.ReadAllBytes(file)));
            found.AddRange(engine.Run(document, LintMarkConfig.Default).Select(d => $"{Path.GetFileName(file)}:{d.Line}:{d.Column} {d.RuleId}"));
        }

        // Assert: nothing is reported
        Assert.Empty(found);
    }

    /// <summary>
    ///     Test that the rules never throw on the documents of this repository, the synthetic documents and random documents,
    ///     with every rule switched on, and that a second run gives the same findings.
    /// </summary>
    [Fact]
    public void LintRulesCorpus_AllDocuments_RulesNeverThrowAndAreDeterministic()
    {
        // Arrange: the repository documents, the nesting documents and two seeded families of random documents
        var texts = RepositoryMarkdownFiles.Enumerate().Select(static file => File.ReadAllText(file).ReplaceLineEndings("\n"))
            .Concat(SyntheticDocuments.Generate().Concat(SyntheticDocuments.GenerateFootnoteDocuments()).Select(static fixture => fixture.Text))
            .Concat(RandomDocuments.Generate(4242u, 150).Select(static fixture => fixture.Text))
            .ToList();
        var engine = LintRuleEngine.CreateDefault();

        // Act: run every text twice
        var first = new string[texts.Count][];
        var second = new string[texts.Count][];
        Parallel.For(0, texts.Count, index =>
        {
            first[index] = Run(engine, texts[index]);
            second[index] = Run(engine, texts[index]);
        });

        // Assert: the same findings both times, and every finding is within the text
        Assert.True(texts.Count > 500);
        for (var index = 0; index < texts.Count; index++)
        {
            Assert.Equal(first[index], second[index]);
        }
    }
}
