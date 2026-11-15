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

using System.Globalization;

namespace DemaConsulting.LintMark.Verify.Cli;

/// <summary>
///     The parsed command line of the verifier.
/// </summary>
/// <remarks>
///     Positional arguments are directories, files or globs (with <c>!</c> excludes). <c>--option key=value</c>
///     entries use dotted keys for nested settings (for example <c>link-refactor.enabled=true</c>). Nothing here
///     touches the file system; <see cref="Parse"/> only validates the shape of the arguments.
/// </remarks>
internal sealed class VerifyOptions
{
    /// <summary>
    ///     The default root of the DEMA Consulting repositories for <c>--dema</c>.
    /// </summary>
    public const string DefaultDemaRoot = @"C:\work\DemaConsulting";

    /// <summary>
    ///     The usage text.
    /// </summary>
    public const string Usage = """
        LintMark.Verify - verifies that LintMark formatting preserves rendering, structure and stability.

        Usage: dotnet run --project tools/LintMark.Verify -- <dir-or-glob>... [options]

        Every Markdown file is formatted in memory with the full set of fixes (nothing is ever written to an
        input) and the result is checked for: the same rendered HTML (also with the line endings of the file),
        the same Pandoc structure tokens, a stable second formatting (byte-identical), crashes and timeouts.
        A refusal by design (invalid grid table, an ignored file, an undecodable file) counts as a pass.

        Arguments:
          <dir-or-glob>              Directories, files or globs; a leading '!' excludes. Folders named generated,
                                     node_modules, bin, obj, .git, .venv, .corpus, .clones, TestResults and
                                     .agent-logs below the given root are skipped.

        Options:
          --config <file>            Base LintMark configuration (YAML) for every run.
          --option <key=value>       Override a setting, dotted keys for nested ones (repeatable), for example
                                     --option wrap=semantic --option link-refactor.enabled=true
          --matrix                   Run every combination of wrap {fill,sentence,semantic} x line-length
                                     {120,80,40} x tables {compact,aligned} x link-refactor {off,on}; the matrix
                                     values replace the same keys given with --option.
          --repo-config              Use the .lintmark.yaml and .editorconfig found for each file (not with
                                     --config, --option or --matrix).
          --dema                     Add every Git repository found under the DEMA root (read-only).
          --dema-root <dir>          The DEMA root (default C:\work\DemaConsulting).
          --report <file>            Write a Markdown report of the run to this file.
          --sample <n>               Verify a deterministic subset of n files (seeded), useful with --matrix.
          --jobs <n>                 Degree of parallelism (default: processor count).
          --timeout <seconds>        Per file and option set (default 300).
          --max-diff-lines <n>       Longest diff shown per failure (default 60).
          -h, --help                 Show this help.

        Exit code: 0 all checks passed, 1 at least one failure, 2 usage or setup error.
        """;

    /// <summary>
    ///     Gets the paths, directories, files and globs to verify.
    /// </summary>
    public List<string> Paths { get; } = [];

    /// <summary>
    ///     Gets or sets the base configuration file, or <see langword="null"/> for the defaults.
    /// </summary>
    public string? ConfigFile { get; set; }

    /// <summary>
    ///     Gets the setting overrides, in command-line order.
    /// </summary>
    public List<KeyValuePair<string, string>> Overrides { get; } = [];

    /// <summary>
    ///     Gets or sets the path of the Markdown report, or <see langword="null"/> for none.
    /// </summary>
    public string? ReportPath { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the option matrix is run.
    /// </summary>
    public bool Matrix { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the DEMA repositories are added.
    /// </summary>
    public bool Dema { get; set; }

    /// <summary>
    ///     Gets or sets the root of the DEMA repositories.
    /// </summary>
    public string DemaRoot { get; set; } = DefaultDemaRoot;

    /// <summary>
    ///     Gets or sets a value indicating whether the configuration found for each file is used.
    /// </summary>
    public bool RepoConfig { get; set; }

    /// <summary>
    ///     Gets or sets the degree of parallelism.
    /// </summary>
    public int Jobs { get; set; } = Environment.ProcessorCount;

    /// <summary>
    ///     Gets or sets the size of the deterministic subset, or <see langword="null"/> for every file.
    /// </summary>
    public int? Sample { get; set; }

    /// <summary>
    ///     Gets or sets the time limit per file and option set in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>
    ///     Gets or sets the longest diff shown per failure, in lines.
    /// </summary>
    public int MaxDiffLines { get; set; } = 60;

    /// <summary>
    ///     Gets or sets a value indicating whether the help text was requested.
    /// </summary>
    public bool Help { get; set; }

    /// <summary>
    ///     Parses the command line.
    /// </summary>
    /// <param name="args">The arguments. Must not be null.</param>
    /// <returns>The options.</returns>
    /// <exception cref="UsageException">Thrown when an argument is unknown, lacks its value or is out of range.</exception>
    public static VerifyOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var options = new VerifyOptions();
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "-h" or "--help" or "-?":
                    options.Help = true;
                    break;
                case "--matrix":
                    options.Matrix = true;
                    break;
                case "--dema":
                    options.Dema = true;
                    break;
                case "--repo-config":
                    options.RepoConfig = true;
                    break;
                case "--config":
                    options.ConfigFile = Value(args, ref i, arg);
                    break;
                case "--report":
                    options.ReportPath = Value(args, ref i, arg);
                    break;
                case "--dema-root":
                    options.DemaRoot = Value(args, ref i, arg);
                    break;
                case "--option":
                    options.Overrides.Add(ParseOverride(Value(args, ref i, arg)));
                    break;
                case "--sample":
                    options.Sample = Number(Value(args, ref i, arg), arg);
                    break;
                case "--jobs":
                    options.Jobs = Number(Value(args, ref i, arg), arg);
                    break;
                case "--timeout":
                    options.TimeoutSeconds = Number(Value(args, ref i, arg), arg);
                    break;
                case "--max-diff-lines":
                    options.MaxDiffLines = Number(Value(args, ref i, arg), arg);
                    break;
                default:
                    if (arg.StartsWith('-') && arg.Length > 1)
                    {
                        throw new UsageException($"Unknown option '{arg}'.");
                    }

                    options.Paths.Add(arg);
                    break;
            }
        }

        options.Validate();
        return options;
    }

    /// <summary>
    ///     Checks the combinations of options.
    /// </summary>
    /// <exception cref="UsageException">Thrown when the options contradict each other or nothing is selected.</exception>
    private void Validate()
    {
        if (Help)
        {
            return;
        }

        if (RepoConfig && (ConfigFile is not null || Overrides.Count > 0 || Matrix))
        {
            throw new UsageException("--repo-config cannot be combined with --config, --option or --matrix.");
        }

        if (Paths.Count == 0 && !Dema)
        {
            throw new UsageException("Nothing to verify: give at least one directory or glob, or --dema.");
        }
    }

    /// <summary>
    ///     Takes the value of an option from the next argument.
    /// </summary>
    /// <param name="args">The arguments.</param>
    /// <param name="index">The index of the option; advanced to the value.</param>
    /// <param name="option">The option name, for the message.</param>
    /// <returns>The value.</returns>
    /// <exception cref="UsageException">Thrown when there is no further argument.</exception>
    private static string Value(IReadOnlyList<string> args, ref int index, string option)
    {
        if (index + 1 >= args.Count)
        {
            throw new UsageException($"Option '{option}' needs a value.");
        }

        index++;
        return args[index];
    }

    /// <summary>
    ///     Parses a positive number.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="option">The option name, for the message.</param>
    /// <returns>The number, at least 1.</returns>
    /// <exception cref="UsageException">Thrown when the text is not a positive integer.</exception>
    private static int Number(string text, string option)
    {
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value < 1)
        {
            throw new UsageException($"Option '{option}' needs a positive integer, not '{text}'.");
        }

        return value;
    }

    /// <summary>
    ///     Parses a <c>key=value</c> override.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The key and value.</returns>
    /// <exception cref="UsageException">Thrown when there is no key or no equals sign.</exception>
    private static KeyValuePair<string, string> ParseOverride(string text)
    {
        var split = text.IndexOf('=', StringComparison.Ordinal);
        if (split < 1)
        {
            throw new UsageException($"Option '--option' needs key=value, not '{text}'.");
        }

        return new KeyValuePair<string, string>(text[..split].Trim(), text[(split + 1)..].Trim());
    }
}
