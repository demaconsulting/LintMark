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
    ///     A problem that is only reported (file problems, configuration problems, safety refusals and lint rules).
    /// </summary>
    LintOnly,

    /// <summary>
    ///     A violation of canonical form that rewriting the file removes.
    /// </summary>
    Formatting
}
