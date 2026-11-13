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

namespace DemaConsulting.LintMark.Engine;

/// <summary>
///     The outcome of processing one file.
/// </summary>
/// <param name="Path">The path of the file as it was given to the engine.</param>
/// <param name="Failures">The failures found (empty when the file was read and parsed successfully).</param>
/// <param name="Config">
///     The effective configuration of the file (the configuration that applies to it, with the line ending
///     filled in from the editor settings when the configuration gives none), or <see langword="null"/> when
///     the file's configuration is invalid or has not been attached.
/// </param>
/// <remarks>
///     Workers return results and never write output; the engine emits them later in path order so
///     output does not depend on scheduling. Immutable once created, including the failure and
///     configuration-error lists and the configuration itself.
/// </remarks>
internal sealed record FileResult(string Path, ImmutableArray<FileFailure> Failures, LintMarkConfig? Config = null)
{
    /// <summary>
    ///     Gets the configuration problems that apply to the file (empty when its configuration is valid).
    /// </summary>
    /// <remarks>
    ///     The same problem is shared by every file that uses the same configuration; the engine reports each
    ///     distinct problem once.
    /// </remarks>
    public ImmutableArray<ConfigError> ConfigErrors { get; init; } = ImmutableArray<ConfigError>.Empty;
}
