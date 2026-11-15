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
using DemaConsulting.LintMark.Document;

namespace DemaConsulting.LintMark.Engine;

/// <summary>
///     A rewrite of one file that is ready to be written.
/// </summary>
/// <param name="OriginalLf">The LF-normalized text as it is on disk.</param>
/// <param name="NewLf">The LF-normalized text after rewriting.</param>
/// <param name="OriginalEndings">The counts of the line terminators in the file as it is on disk.</param>
/// <param name="TargetEnding">The line ending the file is written with.</param>
/// <param name="OriginalBytes">The exact bytes the rewrite was computed from.</param>
/// <param name="NewBytes">The exact bytes to write (byte order mark and line endings restored).</param>
/// <param name="ResolvedTarget">The full path of the file the original bytes were read from, with symbolic links resolved at read time.</param>
/// <remarks>
///     A change exists only for a rewrite that differs from the file: for a file that must not be rewritten
///     (an invalid configuration, a format-unsafe document, a failure) the result carries no change, so
///     nothing downstream can write the file by mistake. The original bytes travel with the change so the
///     writer can refuse to replace a file that changed after it was read, and so does the resolved target
///     (the file that the path referred to through symbolic links when it was read), so the writer can also
///     refuse when a link was redirected to a file with the same bytes. Immutable, including the byte arrays.
/// </remarks>
internal sealed record FileChange(
    string OriginalLf,
    string NewLf,
    LineEndingInfo OriginalEndings,
    LineEnding TargetEnding,
    ImmutableArray<byte> OriginalBytes,
    ImmutableArray<byte> NewBytes,
    string ResolvedTarget);
