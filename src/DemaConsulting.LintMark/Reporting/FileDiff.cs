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

using DemaConsulting.LintMark.Document;

namespace DemaConsulting.LintMark.Reporting;

/// <summary>
///     What the fix option would change in one file, for the diff option to display.
/// </summary>
/// <param name="Path">Full path of the file.</param>
/// <param name="BeforeLf">The LF-normalized text as it is on disk.</param>
/// <param name="AfterLf">The LF-normalized text after rewriting.</param>
/// <param name="OriginalEndings">The counts of the line terminators in the file as it is on disk.</param>
/// <param name="TargetEnding">The line ending the file would be written with.</param>
/// <remarks>
///     Both texts are LF-normalized, so a change of line endings alone leaves them equal; the original counts
///     and the target let the reporter say so. Immutable value type; safe for concurrent use.
/// </remarks>
internal readonly record struct FileDiff(string Path, string BeforeLf, string AfterLf, LineEndingInfo OriginalEndings, LineEnding TargetEnding);
