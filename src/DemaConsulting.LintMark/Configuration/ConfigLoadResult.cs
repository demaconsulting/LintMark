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

namespace DemaConsulting.LintMark.Configuration;

/// <summary>
///     The outcome of loading a configuration: either a valid configuration or the list of problems found.
/// </summary>
/// <remarks>
///     <para>
///         Supporting type of <see cref="ConfigLoader"/>. A load never yields a half-valid configuration:
///         <see cref="Config"/> is set exactly when <see cref="Errors"/> is empty, which makes it
///         impossible to use settings from a file that was reported as invalid. The errors are stored
///         sorted by line, column and message so the report does not depend on discovery order.
///     </para>
///     <para>
///         Thread safety: instances are immutable and may be shared between threads.
///     </para>
/// </remarks>
internal sealed record ConfigLoadResult
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ConfigLoadResult"/> class.
    /// </summary>
    /// <param name="config">The valid configuration, or <see langword="null"/> when there are errors.</param>
    /// <param name="sourcePath">Full path of the file the configuration came from; <see langword="null"/> for built-in defaults.</param>
    /// <param name="errors">The problems found; must be empty exactly when <paramref name="config"/> is set. Must be initialized.</param>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="errors"/> is uninitialized, when a configuration and errors are both present
    ///     or both absent, or when <paramref name="sourcePath"/> is empty.
    /// </exception>
    public ConfigLoadResult(LintMarkConfig? config, string? sourcePath, ImmutableArray<ConfigError> errors)
    {
        // Validate input: the two outcomes are mutually exclusive and one of them must hold
        if (errors.IsDefault)
        {
            throw new ArgumentException("The errors must be initialized.", nameof(errors));
        }

        if ((config is null) == errors.IsEmpty)
        {
            throw new ArgumentException("A configuration must be present exactly when there are no errors.", nameof(errors));
        }

        if (sourcePath is { Length: 0 })
        {
            throw new ArgumentException("The source path must not be empty.", nameof(sourcePath));
        }

        // Store the errors in report order
        Config = config;
        SourcePath = sourcePath;
        Errors =
        [
            .. errors
                .OrderBy(static e => e.Line)
                .ThenBy(static e => e.Column)
                .ThenBy(static e => e.Message, StringComparer.Ordinal)
        ];
    }

    /// <summary>
    ///     Gets the valid configuration, or <see langword="null"/> when the source had errors.
    /// </summary>
    public LintMarkConfig? Config { get; }

    /// <summary>
    ///     Gets the full path of the configuration file, or <see langword="null"/> when built-in defaults apply.
    /// </summary>
    public string? SourcePath { get; }

    /// <summary>
    ///     Gets the problems found, sorted by line, column and message (empty when <see cref="Config"/> is set).
    /// </summary>
    public ImmutableArray<ConfigError> Errors { get; }
}
