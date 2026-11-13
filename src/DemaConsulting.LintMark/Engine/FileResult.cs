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
using DemaConsulting.LintMark.Linting;
using DemaConsulting.LintMark.Safety;

namespace DemaConsulting.LintMark.Engine;

/// <summary>
///     The outcome of processing one file.
/// </summary>
/// <remarks>
///     Workers return results and never write output; the engine emits them later in path order so
///     output does not depend on scheduling. Immutable once created, including the diagnostic and
///     configuration-error lists and the configuration itself.
/// </remarks>
internal sealed record FileResult
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="FileResult"/> class.
    /// </summary>
    /// <param name="path">The path of the file as it was given to the engine. Must not be null or empty.</param>
    /// <param name="diagnostics">The findings about the file (empty when it was read, parsed and found compliant). Must not be a default array or contain null.</param>
    /// <param name="config">
    ///     The effective configuration of the file (the configuration that applies to it, with the line ending
    ///     filled in from the editor settings when the configuration gives none), or <see langword="null"/> when
    ///     the file's configuration is invalid or has not been attached.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null or <paramref name="diagnostics"/> contains a null element.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is empty or <paramref name="diagnostics"/> is a default array.</exception>
    public FileResult(string path, ImmutableArray<Diagnostic> diagnostics, LintMarkConfig? config = null)
    {
        // Validate the arguments so that an incomplete result can never be created
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (diagnostics.IsDefault)
        {
            throw new ArgumentException("The diagnostics must not be a default array.", nameof(diagnostics));
        }

        if (diagnostics.Any(static diagnostic => diagnostic is null))
        {
            throw new ArgumentNullException(nameof(diagnostics), "The diagnostics must not contain null.");
        }

        Path = path;
        Diagnostics = diagnostics;
        Config = config;
    }

    /// <summary>
    ///     Gets the path of the file as it was given to the engine.
    /// </summary>
    public string Path { get; }

    /// <summary>
    ///     Gets the findings about the file (empty when it was read, parsed and found compliant).
    /// </summary>
    public ImmutableArray<Diagnostic> Diagnostics { get; }

    /// <summary>
    ///     Gets the effective configuration of the file, or <see langword="null"/> when the file's configuration
    ///     is invalid or has not been attached.
    /// </summary>
    public LintMarkConfig? Config { get; init; }

    /// <summary>
    ///     Gets the configuration problems that apply to the file (empty when its configuration is valid).
    /// </summary>
    /// <remarks>
    ///     The same problem is shared by every file that uses the same configuration; the engine reports each
    ///     distinct problem once.
    /// </remarks>
    public ImmutableArray<ConfigError> ConfigErrors { get; init; } = ImmutableArray<ConfigError>.Empty;

    /// <summary>
    ///     Gets the outcome of the safety checks of a proposed rewrite (<see cref="SafetyStatus.Ok"/> when none was refused).
    /// </summary>
    public SafetyStatus Safety { get; init; } = SafetyStatus.Ok;

    /// <summary>
    ///     Gets the rewrite that is ready to be written, or <see langword="null"/> when the file is not to be rewritten.
    /// </summary>
    /// <remarks>
    ///     Set only for a rewrite that passed every safety check; never set for a file with an invalid
    ///     configuration, a format-unsafe document or a failure.
    /// </remarks>
    public FileChange? Change { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the file was skipped because an <c>ignore</c> pattern of its configuration matches it.
    /// </summary>
    /// <remarks>
    ///     An ignored file is not read, produces no diagnostic and is never rewritten.
    /// </remarks>
    public bool Ignored { get; init; }
}
