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

using DemaConsulting.LintMark.Verify.Cli;

namespace DemaConsulting.LintMark.Verify.Options;

/// <summary>
///     Builds the option sets a run verifies under.
/// </summary>
/// <remarks>
///     Without <c>--matrix</c> there is one set made of the base configuration and the <c>--option</c> entries.
///     With it there is one set for every combination of wrap mode, line length, table style and link refactoring:
///     3 x 3 x 2 x 2 = 36 sets, each made of the <c>--option</c> entries followed by the matrix values (so a matrix
///     value replaces the same key given on the command line). Stateless and thread-safe.
/// </remarks>
internal static class OptionMatrix
{
    /// <summary>
    ///     The wrap modes of the matrix.
    /// </summary>
    internal static readonly string[] WrapModes = ["fill", "sentence", "semantic"];

    /// <summary>
    ///     The line lengths of the matrix.
    /// </summary>
    internal static readonly int[] LineLengths = [120, 80, 40];

    /// <summary>
    ///     The table styles of the matrix.
    /// </summary>
    internal static readonly string[] TableStyles = ["compact", "aligned"];

    /// <summary>
    ///     Creates the option sets of a run.
    /// </summary>
    /// <param name="options">The command line. Must not be null.</param>
    /// <returns>At least one set.</returns>
    public static IReadOnlyList<OptionSet> Create(VerifyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // The configuration found for each file is a set of its own
        if (options.RepoConfig)
        {
            return [OptionSet.RepoConfiguration()];
        }

        // A single set from the base file and the overrides
        if (!options.Matrix)
        {
            return [new OptionSet(Describe(options), [.. options.Overrides], options.ConfigFile)];
        }

        // The matrix: every combination, in a stable order
        var sets = new List<OptionSet>();
        foreach (var wrap in WrapModes)
        {
            foreach (var width in LineLengths)
            {
                foreach (var tables in TableStyles)
                {
                    foreach (var links in new[] { false, true })
                    {
                        List<KeyValuePair<string, string>> values = [.. options.Overrides];
                        values.Add(new("wrap", wrap));
                        values.Add(new("line-length", width.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                        values.Add(new("tables", tables));
                        values.Add(new("link-refactor.enabled", links ? "true" : "false"));
                        sets.Add(new OptionSet($"wrap={wrap} width={width} tables={tables} links={(links ? "on" : "off")}", values, options.ConfigFile));
                    }
                }
            }
        }

        return sets;
    }

    /// <summary>
    ///     Describes the single set of a run without a matrix.
    /// </summary>
    /// <param name="options">The command line.</param>
    /// <returns>The label.</returns>
    private static string Describe(VerifyOptions options)
    {
        var parts = new List<string>();
        if (options.ConfigFile is not null)
        {
            parts.Add($"config={Path.GetFileName(options.ConfigFile)}");
        }

        parts.AddRange(options.Overrides.Select(static pair => $"{pair.Key}={pair.Value}"));
        return parts.Count == 0 ? "defaults" : string.Join(' ', parts);
    }
}
