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

using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace DemaConsulting.LintMark.Engine;

/// <summary>
///     Expands the positional path arguments of the command line into the list of Markdown files to process.
/// </summary>
/// <remarks>
///     <para>
///         Each argument is interpreted as follows: a pattern starting with <c>!</c> excludes matches;
///         an existing file is taken as given; an existing directory means every <c>*.md</c> file beneath
///         it; anything else is a glob (<c>*</c> and <c>**</c>), whose leading wildcard-free segments
///         select the directory to search. A path without wildcards that does not exist is passed through
///         so that the failure to read it is reported uniformly with every other read failure. With no
///         include arguments the default <c>**/*.md</c> under the base directory is used. Files matched
///         by directory, glob or default expansion never come from <c>node_modules</c> or <c>.git</c>
///         directories; explicitly named files are exempt from that built-in exclusion but not from
///         user excludes. A pattern that matches nothing is not an error.
///     </para>
///     <para>
///         The result consists of distinct full paths in ordinal order, so it is independent of
///         enumeration order. Name comparison is case-insensitive on Windows and macOS and
///         case-sensitive elsewhere. Only directory enumeration touches the file system; the unit is
///         stateless and thread-safe.
///     </para>
/// </remarks>
internal static class FileDiscovery
{
    /// <summary>
    ///     The pattern used when no include argument is given, and for directory arguments.
    /// </summary>
    internal const string DefaultPattern = "**/*.md";

    /// <summary>
    ///     The separator used in glob patterns and relative match paths (identical on every platform).
    /// </summary>
    private static readonly char Slash = Path.AltDirectorySeparatorChar;

    /// <summary>
    ///     Directories that are never searched by directory, glob or default expansion.
    /// </summary>
    private static readonly string[] DefaultExcludes = ["**/node_modules/**", "**/.git/**"];

    /// <summary>
    ///     Gets the comparison used for file names (case-insensitive on Windows and macOS).
    /// </summary>
    internal static StringComparison PathComparison { get; } =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>
    ///     Expands path arguments into the files to process.
    /// </summary>
    /// <param name="patterns">The positional arguments (paths, directories, globs, <c>!</c> excludes). Must not be null.</param>
    /// <param name="baseDirectory">Directory that relative patterns are resolved against. Must not be null.</param>
    /// <returns>Distinct full paths in ordinal order; empty when nothing matches.</returns>
    /// <remarks>
    ///     Directory enumeration is the only side effect. Safe to call concurrently.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a pattern is empty or whitespace.</exception>
    public static IReadOnlyList<string> Discover(IReadOnlyList<string> patterns, string baseDirectory)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(patterns);
        ArgumentNullException.ThrowIfNull(baseDirectory);

        // Split the arguments into includes and excludes
        var includes = new List<string>();
        var excludes = new List<string>();
        foreach (var pattern in patterns)
        {
            var isExclude = pattern is not null && pattern.StartsWith('!');
            var body = isExclude ? pattern![1..] : pattern;
            if (string.IsNullOrWhiteSpace(body))
            {
                throw new ArgumentException("Path arguments must not be empty.", nameof(patterns));
            }

            (isExclude ? excludes : includes).Add(body);
        }

        // Without an include the default pattern applies (so "lintmark !docs/**" means everything but docs)
        if (includes.Count == 0)
        {
            includes.Add(DefaultPattern);
        }

        // Collect candidates from every include
        var root = Path.GetFullPath(baseDirectory);
        var candidates = new HashSet<string>(StringComparer.FromComparison(PathComparison));
        foreach (var include in includes)
        {
            Expand(include, root, candidates);
        }

        // Apply user excludes and return a stable order
        var result = ApplyExcludes(candidates, excludes, root);
        result.Sort(StringComparer.Ordinal);
        return result;
    }

    /// <summary>
    ///     Expands one include argument into candidate files.
    /// </summary>
    /// <param name="pattern">The include argument.</param>
    /// <param name="baseDirectory">The full base directory.</param>
    /// <param name="candidates">Receives the candidate full paths.</param>
    private static void Expand(string pattern, string baseDirectory, HashSet<string> candidates)
    {
        // An existing file is taken as given; an existing directory means all Markdown files beneath it
        var full = Path.GetFullPath(pattern, baseDirectory);
        if (File.Exists(full))
        {
            candidates.Add(full);
            return;
        }

        if (Directory.Exists(full))
        {
            Glob(full, DefaultPattern, candidates);
            return;
        }

        // A path without wildcards that does not exist is passed through to be reported when read
        var (searchRoot, relativePattern) = SplitPattern(pattern, baseDirectory);
        if (relativePattern is null)
        {
            candidates.Add(full);
            return;
        }

        // Search from the directory named by the leading wildcard-free segments
        if (Directory.Exists(searchRoot))
        {
            Glob(searchRoot, relativePattern, candidates);
        }
    }

    /// <summary>
    ///     Splits a pattern into the directory named by its leading wildcard-free segments and the remaining glob.
    /// </summary>
    /// <param name="pattern">The pattern (either separator style; relative or absolute).</param>
    /// <param name="baseDirectory">The full base directory that relative patterns are resolved against.</param>
    /// <returns>
    ///     The full path of the root directory and the glob relative to it, or a null glob when the
    ///     pattern has no wildcard (the root is then the full path of the pattern itself).
    /// </returns>
    private static (string Root, string? RelativePattern) SplitPattern(string pattern, string baseDirectory)
    {
        var segments = pattern.Replace('\\', Slash).Split(Slash);
        var wildcard = Array.FindIndex(segments, static s => s.Contains('*', StringComparison.Ordinal));
        if (wildcard < 0)
        {
            return (Path.GetFullPath(pattern, baseDirectory), null);
        }

        var root = wildcard == 0
            ? baseDirectory
            : Path.GetFullPath(string.Join(Slash, segments[..wildcard]) + Slash, baseDirectory);
        return (root, string.Join(Slash, segments[wildcard..]));
    }

    /// <summary>
    ///     Matches a relative glob beneath a directory, skipping the built-in excluded directories.
    /// </summary>
    /// <param name="searchRoot">The directory to search.</param>
    /// <param name="relativePattern">The glob relative to <paramref name="searchRoot"/>.</param>
    /// <param name="candidates">Receives the matching full paths.</param>
    private static void Glob(string searchRoot, string relativePattern, HashSet<string> candidates)
    {
        var matcher = new Matcher(PathComparison);
        matcher.AddInclude(relativePattern);
        foreach (var exclude in DefaultExcludes)
        {
            matcher.AddExclude(exclude);
        }

        var result = matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(searchRoot)));
        foreach (var file in result.Files)
        {
            candidates.Add(Path.GetFullPath(file.Path, searchRoot));
        }
    }

    /// <summary>
    ///     Removes the candidates matched by user exclude patterns.
    /// </summary>
    /// <param name="candidates">The candidate full paths.</param>
    /// <param name="excludes">The exclude patterns (without the leading <c>!</c>).</param>
    /// <param name="baseDirectory">The full base directory.</param>
    /// <returns>The remaining candidates (unsorted).</returns>
    /// <remarks>
    ///     Each exclude is resolved like an include: a root directory (its leading wildcard-free
    ///     segments, relative to the base directory unless absolute) plus a glob relative to that root;
    ///     a candidate is excluded when it lies below the root and matches the glob. An exclude without a
    ///     wildcard names a file, or a directory and everything beneath it. This also works for
    ///     candidates outside the base directory.
    /// </remarks>
    private static List<string> ApplyExcludes(HashSet<string> candidates, List<string> excludes, string baseDirectory)
    {
        if (excludes.Count == 0)
        {
            return [.. candidates];
        }

        // Resolve every exclude to a root directory and a matcher for paths relative to that root
        var rules = new List<(string Root, Matcher Matcher)>();
        foreach (var exclude in excludes)
        {
            var (root, relativePattern) = SplitPattern(exclude, baseDirectory);
            if (relativePattern is null)
            {
                // No wildcard: a directory excludes everything beneath it, otherwise it names one file
                if (Directory.Exists(root))
                {
                    relativePattern = "**";
                }
                else
                {
                    relativePattern = Path.GetFileName(root);
                    root = Path.GetDirectoryName(root) ?? root;
                }
            }

            var matcher = new Matcher(PathComparison);
            matcher.AddInclude(relativePattern);
            rules.Add((Path.TrimEndingDirectorySeparator(root), matcher));
        }

        return [.. candidates.Where(candidate => !rules.Any(rule => IsMatch(rule.Root, rule.Matcher, candidate)))];
    }

    /// <summary>
    ///     Determines whether a candidate lies below a root directory and matches a glob relative to it.
    /// </summary>
    /// <param name="root">The root directory (full path, no trailing separator).</param>
    /// <param name="matcher">A matcher whose include pattern is relative to the root.</param>
    /// <param name="candidate">The candidate full path.</param>
    /// <returns><see langword="true"/> when the candidate is below the root and matches.</returns>
    private static bool IsMatch(string root, Matcher matcher, string candidate)
    {
        // Candidates outside the root (parent segments, or another drive) can never match
        var relative = Path.GetRelativePath(root, candidate);
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            return false;
        }

        return matcher.Match(root, [relative.Replace(Path.DirectorySeparatorChar, Slash)]).HasMatches;
    }
}
