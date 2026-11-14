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

namespace DemaConsulting.LintMark.Linting;

/// <summary>
///     A rule that only reports: it finds a problem that formatting cannot fix and never proposes an edit.
/// </summary>
/// <remarks>
///     <para>
///         A lint rule reads a parsed document and returns diagnostics of kind <see cref="DiagnosticKind.LintOnly"/>.
///         It does not look at suppression comments or at whether it is switched off in the configuration: the
///         <see cref="LintRuleRunner"/> filters its diagnostics by the rule identifier they carry.
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
