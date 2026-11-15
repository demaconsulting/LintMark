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
///     Shared helpers for the tests of the lint-only rule units.
/// </summary>
internal static class LintRuleTestSupport
{
    /// <summary>
    ///     Parses LF-normalized text as a document named <c>a.md</c>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The parsed document.</returns>
    public static ParsedDocument Parse(string text) => DocumentParser.Parse(SourceDocument.FromText("a.md", text));

    /// <summary>
    ///     Runs a rule over a text and formats the findings as <c>line:column rule-id</c>, in the order the rule reported them.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <param name="text">The text.</param>
    /// <param name="config">The configuration, or <see langword="null"/> for the defaults.</param>
    /// <returns>The findings.</returns>
    public static string[] Findings(ILintRule rule, string text, LintMarkConfig? config = null) =>
        [.. rule.Check(Parse(text), config ?? LintMarkConfig.Default).Select(d => $"{d.Line}:{d.Column} {d.RuleId}")];

    /// <summary>
    ///     Runs a rule over a text and returns the messages of its findings.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <param name="text">The text.</param>
    /// <returns>The messages.</returns>
    public static string[] Messages(ILintRule rule, string text) =>
        [.. rule.Check(Parse(text), LintMarkConfig.Default).Select(d => d.Message)];
}
