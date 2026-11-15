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

namespace DemaConsulting.LintMark.Linting;

/// <summary>
///     One finding about a file: a problem in the file, in its configuration, or in an attempt to rewrite it.
/// </summary>
/// <param name="RuleId">Kebab-case rule identifier the finding is reported under.</param>
/// <param name="Kind">Whether rewriting the file removes the finding.</param>
/// <param name="Path">Full path of the file the finding belongs to (the reporter makes it relative).</param>
/// <param name="Line">1-based line.</param>
/// <param name="Column">1-based column.</param>
/// <param name="Message">Single-line description without the path.</param>
/// <remarks>
///     <para>
///         The single diagnostic model exists so that every finding of the tool (read and parse failures,
///         configuration problems, formatting violations and format refusals) is created, ordered,
///         de-duplicated and reported the same way. Whether a diagnostic is
///         <see cref="Fixable"/> is derived from its <see cref="Kind"/> so the two can never disagree.
///     </para>
///     <para>
///         The record is immutable and has value equality over all members, which the reporter uses to report
///         the same finding once. Thread safety: instances are immutable and may be shared between threads.
///     </para>
/// </remarks>
internal sealed record Diagnostic(string RuleId, DiagnosticKind Kind, string Path, int Line, int Column, string Message)
{
    /// <summary>
    ///     Gets a value indicating whether rewriting the file removes the finding.
    /// </summary>
    public bool Fixable => Kind == DiagnosticKind.Formatting;

    /// <summary>
    ///     Creates a finding that is only reported.
    /// </summary>
    /// <param name="path">Path of the file.</param>
    /// <param name="ruleId">Rule identifier.</param>
    /// <param name="line">1-based line.</param>
    /// <param name="column">1-based column.</param>
    /// <param name="message">Description; line breaks are replaced so the report stays one line per finding.</param>
    /// <returns>The finding.</returns>
    public static Diagnostic LintOnly(string path, string ruleId, int line, int column, string message) =>
        new(ruleId, DiagnosticKind.LintOnly, path, line, column, message.ReplaceLineEndings(" "));
}

/// <summary>
///     Whether a diagnostic can be removed by rewriting the file or can only be reported.
/// </summary>
/// <remarks>
///     The kind is what makes the fix option's contract checkable: a <see cref="Formatting"/> diagnostic is
///     one that the formatter removes when it rewrites the file, a <see cref="LintOnly"/> diagnostic is
///     reported and stays until a person changes the file.
/// </remarks>
internal enum DiagnosticKind
{
    /// <summary>
    ///     A problem that is only reported (file problems, configuration problems, format refusals and lint rules).
    /// </summary>
    LintOnly,

    /// <summary>
    ///     A violation of canonical form that rewriting the file removes.
    /// </summary>
    Formatting
}
