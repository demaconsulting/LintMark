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

namespace DemaConsulting.LintMark.Verify.Verification;

/// <summary>
///     What the verification of one file under one option set found.
/// </summary>
/// <remarks>
///     The first group are passes (the file was formatted and verified, or there was nothing to verify); the
///     exempt kinds are refusals by design of the formatter and count as passes too. The rest are failures.
/// </remarks>
internal enum OutcomeKind
{
    /// <summary>The formatted file passed every check.</summary>
    Ok,

    /// <summary>Formatting does not change the file.</summary>
    Unchanged,

    /// <summary>The file has a grid table border that is not part of a valid grid table; it is never formatted (by design).</summary>
    ExemptGridTableInvalid,

    /// <summary>The file is not valid UTF-8 (by design it is not formatted).</summary>
    ExemptEncodingError,

    /// <summary>The file has a bare carriage return (by design it is not formatted).</summary>
    ExemptLineEndingError,

    /// <summary>The configuration says to ignore the file.</summary>
    ExemptIgnored,

    /// <summary>The formatted text renders to different HTML.</summary>
    HtmlMismatch,

    /// <summary>The formatted file renders to different HTML when read with its real line endings.</summary>
    HtmlMismatchOnDisk,

    /// <summary>The formatted text has different Pandoc structure tokens.</summary>
    StructureMismatch,

    /// <summary>The formatted text contains a grid table border that is not part of a valid grid table.</summary>
    ReparseGridTable,

    /// <summary>Formatting the formatted text again changes it.</summary>
    NotIdempotent,

    /// <summary>The formatter or the parser threw, or reported a format or parse error.</summary>
    Crash,

    /// <summary>The verification of the file exceeded the time limit.</summary>
    Timeout,

    /// <summary>The verifier itself failed (a check threw, the file could not be read, the configuration is invalid).</summary>
    VerifierError
}

/// <summary>
///     Classification helpers for <see cref="OutcomeKind"/>.
/// </summary>
internal static class OutcomeKinds
{
    /// <summary>
    ///     Determines whether a kind is a failure.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns><see langword="true"/> for every kind from <see cref="OutcomeKind.HtmlMismatch"/> on.</returns>
    public static bool IsFailure(this OutcomeKind kind) => kind >= OutcomeKind.HtmlMismatch;

    /// <summary>
    ///     Determines whether a kind is a refusal by design.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns><see langword="true"/> for the exempt kinds.</returns>
    public static bool IsExempt(this OutcomeKind kind) => kind is >= OutcomeKind.ExemptGridTableInvalid and <= OutcomeKind.ExemptIgnored;

    /// <summary>
    ///     Gets the name of a kind as shown in the reports.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>A kebab-case name.</returns>
    public static string Name(this OutcomeKind kind) => kind switch
    {
        OutcomeKind.Ok => "ok",
        OutcomeKind.Unchanged => "unchanged",
        OutcomeKind.ExemptGridTableInvalid => "exempt-grid-table-invalid",
        OutcomeKind.ExemptEncodingError => "exempt-encoding-error",
        OutcomeKind.ExemptLineEndingError => "exempt-line-ending-error",
        OutcomeKind.ExemptIgnored => "exempt-ignored",
        OutcomeKind.HtmlMismatch => "html-mismatch",
        OutcomeKind.HtmlMismatchOnDisk => "html-mismatch-on-disk",
        OutcomeKind.StructureMismatch => "structure-mismatch",
        OutcomeKind.ReparseGridTable => "reparse-grid-table",
        OutcomeKind.NotIdempotent => "not-idempotent",
        OutcomeKind.Crash => "crash",
        OutcomeKind.Timeout => "timeout",
        _ => "verifier-error"
    };
}
