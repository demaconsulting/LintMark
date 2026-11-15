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
///         directories, whether the folder is named by the argument (or by the leading, wildcard-free
///         part of a glob) or found beneath the folder searched; only explicitly named files are exempt
///         from that built-in exclusion, and they are not exempt from user excludes. A pattern that
///         matches nothing is not an error. Links to folders are followed, except a link whose final target is
///         a folder the search is already inside (a cycle), which is skipped. <see cref="DiscoverFiles(IReadOnlyList{string}, string, Func{string, IgnoreScope})"/> also reports which files were named
///         (are explicit), because the <c>ignore</c> patterns of a configuration, like the built-in exclusion,
///         do not apply to them (see <see cref="IsIgnored"/>).
///     </para>
///     <para>
///         The result consists of distinct full paths in ordinal order, so it is independent of
///         enumeration order and of the order of the arguments. Name comparison is case-insensitive on
///         Windows and macOS and case-sensitive elsewhere; where it is case-insensitive and one file is
///         found under spellings that differ only in case, the smallest-in-ordinal-order spelling is kept.
///         Both <c>/</c> and <c>\</c> are accepted as separators in every argument on every platform
///         (on Unix, a backslash is therefore never part of a file name). Only directory enumeration
///         touches the file system; the unit is stateless and thread-safe.
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
    ///     Directory patterns that prune excluded folders found beneath a search root.
    /// </summary>
    private static readonly string[] DefaultExcludes = ["**/node_modules/**", "**/.git/**"];

    /// <summary>
    ///     Names of the directories whose files directory, glob and default expansion never select.
    /// </summary>
    private static readonly string[] ExcludedDirectoryNames = ["node_modules", ".git"];

    /// <summary>
    ///     The name of the hypothetical file used to decide whether a directory is pruned.
    /// </summary>
    private const string ProbeName = "__lintmark_probe__";

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
    ///     Directory enumeration is the only side effect. Safe to call concurrently. Equivalent to the paths
    ///     of <see cref="DiscoverFiles(IReadOnlyList{string}, string, Func{string, IgnoreScope})"/>.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when an argument, or an element of <paramref name="patterns"/>, is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a pattern is empty or whitespace.</exception>
    public static IReadOnlyList<string> Discover(IReadOnlyList<string> patterns, string baseDirectory) =>
        [.. DiscoverFiles(patterns, baseDirectory).Select(static file => file.Path)];

    /// <summary>
    ///     Expands path arguments into the files to process, remembering which files the command line named.
    /// </summary>
    /// <param name="patterns">The positional arguments (paths, directories, globs, <c>!</c> excludes). Must not be null.</param>
    /// <param name="baseDirectory">Directory that relative patterns are resolved against. Must not be null.</param>
    /// <returns>
    ///     Distinct files in ordinal order of the full path; empty when nothing matches. A file is explicit when
    ///     an include argument names it as an existing file or as a path without wildcards (also when it does not
    ///     exist); a file found only through a directory, a glob or the default pattern is not. A file that is
    ///     both named and found, also under another spelling that differs only in case, is explicit.
    /// </returns>
    /// <remarks>
    ///     Directory enumeration is the only side effect. Safe to call concurrently.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when an argument, or an element of <paramref name="patterns"/>, is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a pattern is empty or whitespace.</exception>
    public static IReadOnlyList<DiscoveredFile> DiscoverFiles(IReadOnlyList<string> patterns, string baseDirectory) =>
        DiscoverFiles(patterns, baseDirectory, null);

    /// <summary>
    ///     Expands path arguments into the files to process, pruning the directories that the <c>ignore</c>
    ///     patterns of the configuration at each scan root exclude.
    /// </summary>
    /// <param name="patterns">The positional arguments (paths, directories, globs, <c>!</c> excludes). Must not be null.</param>
    /// <param name="baseDirectory">Directory that relative patterns are resolved against. Must not be null.</param>
    /// <param name="scanRootIgnore">
    ///     Supplies, for the full path of a scan root (the base directory by default, a directory argument, or
    ///     the directory named by the leading wildcard-free segments of a glob), the <c>ignore</c> scope of the
    ///     configuration that applies at that root, or <see langword="null"/> when nothing is pruned.
    ///     <see langword="null"/> prunes nothing.
    /// </param>
    /// <returns>As <see cref="DiscoverFiles(IReadOnlyList{string}, string)"/>.</returns>
    /// <remarks>
    ///     A directory beneath a scan root whose files the scope's patterns all ignore is not entered, so the
    ///     walk never reaches (and never reads the configuration of) what is ignored, including a nested
    ///     repository or submodule that has its own <c>.git</c> entry. The scan root itself is never pruned,
    ///     and a file named on the command line is not subject to pruning.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when an argument, or an element of <paramref name="patterns"/>, is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a pattern is empty or whitespace.</exception>
    public static IReadOnlyList<DiscoveredFile> DiscoverFiles(
        IReadOnlyList<string> patterns,
        string baseDirectory,
        Func<string, IgnoreScope?>? scanRootIgnore)
    {
        // Validate input, every element before any is interpreted, so a null is always the null error
        ArgumentNullException.ThrowIfNull(patterns);
        ArgumentNullException.ThrowIfNull(baseDirectory);

        // Split the arguments into includes and excludes; either separator style is accepted on every platform
        var includes = new List<string>();
        var excludes = new List<string>();
        foreach (var pattern in patterns)
        {
            var isExclude = pattern.StartsWith('!');
            var body = isExclude ? pattern[1..] : pattern;
            if (string.IsNullOrWhiteSpace(body))
            {
                throw new ArgumentException("Path arguments must not be empty.", nameof(patterns));
            }

            (isExclude ? excludes : includes).Add(NormalizeSeparators(body));
        }

        // Without an include the default pattern applies (so "lintmark !docs/**" means everything but docs)
        if (includes.Count == 0)
        {
            includes.Add(DefaultPattern);
        }

        // Collect candidates from every include, remembering the ones that an include names itself
        var root = Path.GetFullPath(baseDirectory);
        var candidates = new HashSet<string>(StringComparer.Ordinal);
        var named = new HashSet<string>(StringComparer.Ordinal);
        foreach (var include in includes)
        {
            Expand(include, root, candidates, named, scanRootIgnore);
        }

        // Apply user excludes, then keep one spelling of each file: where names are case-insensitive the
        // spellings of one file differ only in case, and the smallest-in-ordinal-order wins so that the result
        // does not depend on the order of the arguments (or on which expansion found a file first); the file
        // is explicit when any of its spellings was named
        var result = ApplyExcludes(candidates, excludes, root)
            .GroupBy(static path => path, StringComparer.FromComparison(PathComparison))
            .Select(group => new DiscoveredFile(group.Order(StringComparer.Ordinal).First(), group.Any(named.Contains)))
            .ToList();
        result.Sort(static (a, b) => string.CompareOrdinal(a.Path, b.Path));
        return result;
    }

    /// <summary>
    ///     Determines whether the <c>ignore</c> patterns of a configuration match a file.
    /// </summary>
    /// <param name="fullPath">The full path of the file. Must not be null.</param>
    /// <param name="ignoreRoot">
    ///     The directory the patterns are relative to: the directory of the configuration file that applies to
    ///     the file, or the current directory when none does. Must not be null.
    /// </param>
    /// <param name="patterns">The glob patterns (<c>*</c> and <c>**</c>); either separator style is accepted. Must not be null or contain null.</param>
    /// <returns>
    ///     <see langword="true"/> when the file lies below the root and its path relative to the root matches a
    ///     pattern; a file outside the root (reached with <c>..</c>, or on another drive) is never ignored.
    /// </returns>
    /// <remarks>
    ///     Names compare with <see cref="PathComparison"/>. Only the explicit-file rule of the caller decides
    ///     whether the patterns are consulted at all; this method judges the path alone. Pure: no file system access.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when an argument, or an element of <paramref name="patterns"/>, is null.</exception>
    public static bool IsIgnored(string fullPath, string ignoreRoot, IReadOnlyList<string> patterns)
    {
        // Validate input, every element before any is interpreted
        ArgumentNullException.ThrowIfNull(fullPath);
        ArgumentNullException.ThrowIfNull(ignoreRoot);
        ArgumentNullException.ThrowIfNull(patterns);

        if (patterns.Count == 0)
        {
            return false;
        }

        // A file outside the root can never match a pattern that is relative to it
        var root = Path.GetFullPath(ignoreRoot);
        var relative = Path.GetRelativePath(root, Path.GetFullPath(fullPath, root));
        if (IsOutside(relative))
        {
            return false;
        }

        var matcher = new Matcher(PathComparison);
        foreach (var pattern in patterns)
        {
            matcher.AddInclude(NormalizeSeparators(pattern));
        }

        return matcher.Match(root, [relative.Replace(Path.DirectorySeparatorChar, Slash)]).HasMatches;
    }

    /// <summary>
    ///     Converts every backslash of a pattern to a forward slash.
    /// </summary>
    /// <param name="pattern">The pattern or path.</param>
    /// <returns>The pattern with <c>/</c> separators (Windows accepts both styles, so this is lossless there).</returns>
    /// <remarks>
    ///     Applied to every argument before any file system check, so that a backslash path such as
    ///     <c>docs\guide.md</c> names the same file on every platform (on Unix a backslash would otherwise
    ///     be an ordinary file name character).
    /// </remarks>
    private static string NormalizeSeparators(string pattern) => pattern.Replace('\\', Slash);

    /// <summary>
    ///     Expands one include argument into candidate files.
    /// </summary>
    /// <param name="pattern">The include argument.</param>
    /// <param name="baseDirectory">The full base directory.</param>
    /// <param name="candidates">Receives the candidate full paths.</param>
    /// <param name="named">Receives the full paths that the argument names itself (an existing file, or a path without wildcards).</param>
    /// <param name="scanRootIgnore">Supplies the <c>ignore</c> scope that applies at a scan root, or <see langword="null"/>.</param>
    private static void Expand(string pattern, string baseDirectory, HashSet<string> candidates, HashSet<string> named, Func<string, IgnoreScope?>? scanRootIgnore)
    {
        // An existing file is taken as given; an existing directory means all Markdown files beneath it
        pattern = NormalizeSeparators(pattern);
        var full = Path.GetFullPath(pattern, baseDirectory);
        if (File.Exists(full))
        {
            candidates.Add(full);
            named.Add(full);
            return;
        }

        if (Directory.Exists(full))
        {
            Glob(full, DefaultPattern, baseDirectory, candidates, scanRootIgnore);
            return;
        }

        // A path without wildcards that does not exist is passed through to be reported when read
        var (searchRoot, relativePattern) = SplitPattern(pattern, baseDirectory);
        if (relativePattern is null)
        {
            candidates.Add(full);
            named.Add(full);
            return;
        }

        // Search from the directory named by the leading wildcard-free segments
        if (Directory.Exists(searchRoot))
        {
            Glob(searchRoot, relativePattern, baseDirectory, candidates, scanRootIgnore);
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
        pattern = NormalizeSeparators(pattern);
        var segments = pattern.Split(Slash);
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
    /// <param name="baseDirectory">The full base directory the search root is judged against.</param>
    /// <param name="candidates">Receives the matching full paths.</param>
    /// <param name="scanRootIgnore">Supplies the <c>ignore</c> scope that applies at the search root, or <see langword="null"/>.</param>
    /// <remarks>
    ///     The matcher's exclusions are relative to the search root, so they cannot see an excluded
    ///     folder that is, or lies above, the root itself. The root is therefore checked first: a root
    ///     that is inside <c>node_modules</c> or <c>.git</c> yields nothing (judged relative to the base
    ///     directory, or on its full path when it lies outside the base directory). The <c>ignore</c> scope
    ///     of the search root prunes directories beneath it during the walk.
    /// </remarks>
    private static void Glob(string searchRoot, string relativePattern, string baseDirectory, HashSet<string> candidates, Func<string, IgnoreScope?>? scanRootIgnore)
    {
        // Nothing below an excluded folder is selected, whether the folder was named or found
        if (HasExcludedSegment(baseDirectory, searchRoot))
        {
            return;
        }

        var matcher = new Matcher(PathComparison);
        matcher.AddInclude(relativePattern);
        foreach (var exclude in DefaultExcludes)
        {
            matcher.AddExclude(exclude);
        }

        var prune = CreatePruner(scanRootIgnore?.Invoke(searchRoot));
        var result = matcher.Execute(CycleSafeDirectory.Open(new DirectoryInfo(searchRoot), prune));
        foreach (var file in result.Files)
        {
            candidates.Add(Path.GetFullPath(file.Path, searchRoot));
        }
    }

    /// <summary>
    ///     Creates the test that decides whether a directory found during the walk is pruned.
    /// </summary>
    /// <param name="scope">The <c>ignore</c> scope, or <see langword="null"/> for none.</param>
    /// <returns>The test, or <see langword="null"/> when nothing can be pruned.</returns>
    /// <remarks>
    ///     A directory is pruned only when the patterns ignore every file beneath it, which cannot be decided
    ///     without enumerating it; the test is conservative instead: the patterns must match a probe file at
    ///     depth one, two and three below the directory (so <c>dir/**</c> and <c>**/dir/**</c> prune, whereas
    ///     <c>dir/*</c> does not). A directory outside the scope's root is never pruned. Files that a pattern
    ///     ignores in a directory that is not pruned are still skipped afterwards, per file.
    /// </remarks>
    private static Func<string, bool>? CreatePruner(IgnoreScope? scope)
    {
        if (scope is null || scope.Patterns.Count == 0)
        {
            return null;
        }

        var root = Path.GetFullPath(scope.Root);
        var matcher = new Matcher(PathComparison);
        foreach (var pattern in scope.Patterns)
        {
            matcher.AddInclude(NormalizeSeparators(pattern));
        }

        return directory =>
        {
            var relative = Path.GetRelativePath(root, directory);
            if (IsOutside(relative) || relative == ".")
            {
                return false;
            }

            var path = relative.Replace(Path.DirectorySeparatorChar, Slash);
            string[] probes = [$"{path}/{ProbeName}", $"{path}/{ProbeName}/{ProbeName}", $"{path}/{ProbeName}/{ProbeName}/{ProbeName}"];
            return matcher.Match(root, probes).Files.Count() == probes.Length;
        };
    }

    /// <summary>
    ///     Determines whether a directory has an excluded folder as one of its segments, judged relative to the base directory.
    /// </summary>
    /// <param name="baseDirectory">The full base directory.</param>
    /// <param name="directory">The full directory path.</param>
    /// <returns>
    ///     <see langword="true"/> when any segment (including the last) is <c>node_modules</c> or <c>.git</c>.
    ///     A directory at or below the base directory is judged on the path relative to it, so segments of
    ///     the base directory and above are ignored and the tool still works when run from inside such a
    ///     folder. A directory outside the base directory (reached with <c>..</c>, or on another drive) is
    ///     judged on its full path, so climbing into or beside a third-party folder selects nothing.
    /// </returns>
    private static bool HasExcludedSegment(string baseDirectory, string directory)
    {
        var relative = Path.GetRelativePath(baseDirectory, directory);
        return (IsOutside(relative) ? directory : relative)
            .Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries)
            .Any(static segment => ExcludedDirectoryNames.Any(name => string.Equals(segment, name, PathComparison)));
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
        if (IsOutside(relative))
        {
            return false;
        }

        return matcher.Match(root, [relative.Replace(Path.DirectorySeparatorChar, Slash)]).HasMatches;
    }

    /// <summary>
    ///     Determines whether a relative path leaves its origin directory.
    /// </summary>
    /// <param name="relative">The result of <see cref="Path.GetRelativePath(string, string)"/>.</param>
    /// <returns><see langword="true"/> when the path climbs out of the origin or is on another drive.</returns>
    private static bool IsOutside(string relative) =>
        Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    /// <summary>
    ///     A directory for the glob matcher that does not follow a symbolic link or junction back into a directory it is
    ///     already inside, and that skips what cannot be read.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The matcher walks the directory tree it is given, following links, so a link that points at one of its own
    ///         ancestors (or two links that point at each other's folders) makes the walk endless until the path becomes
    ///         too long for the system, and the resulting <see cref="IOException"/> escapes from the middle of an
    ///         enumeration. Each directory here knows the real paths of all directories above it (the folders above the search
    ///         root included), and a link whose final target is one of them is left out, which ends every cycle after one lap at the latest. A link to a folder
    ///         elsewhere is still followed (its files are Markdown files of the project, as far as the user is concerned), and so
    ///         is every file, whatever its kind of link. A directory that cannot be read, or a link that cannot be
    ///         resolved, contributes nothing, as the matcher's own wrapper does for a directory that cannot be opened.
    ///     </para>
    ///     <para>
    ///         Instances are immutable and used by one matcher at a time.
    ///     </para>
    /// </remarks>
    private sealed class CycleSafeDirectory : DirectoryInfoWrapper
    {
        /// <summary>
        ///     The real path of this directory last, preceded by that of every directory above it, from the file system root down.
        /// </summary>
        private readonly string[] _chain;

        /// <summary>
        ///     Decides whether a subdirectory (by full path) is left out of the walk, or <see langword="null"/> to enter every one.
        /// </summary>
        private readonly Func<string, bool>? _prune;

        /// <summary>
        ///     Initializes a new instance of the <see cref="CycleSafeDirectory"/> class.
        /// </summary>
        /// <param name="directory">The directory.</param>
        /// <param name="chain">The real paths of the directories from the file system root down to this one, this one last.</param>
        /// <param name="prune">Decides whether a subdirectory is left out of the walk; <see langword="null"/> enters every one.</param>
        private CycleSafeDirectory(DirectoryInfo directory, string[] chain, Func<string, bool>? prune)
            : base(directory)
        {
            _chain = chain;
            _prune = prune;
        }

        /// <summary>
        ///     Opens the directory a search starts in.
        /// </summary>
        /// <param name="root">The search root.</param>
        /// <param name="prune">Decides whether a subdirectory (by full path) is left out of the walk; <see langword="null"/> enters every one.</param>
        /// <returns>The directory.</returns>
        public static CycleSafeDirectory Open(DirectoryInfo root, Func<string, bool>? prune)
        {
            // The folders above the search root count as directories the search is inside, so a link to one of them is a cycle too
            var real = RealPathOf(root);
            var chain = new List<string> { real };
            for (var above = Path.GetDirectoryName(real); above is not null; above = Path.GetDirectoryName(above))
            {
                chain.Insert(0, Path.TrimEndingDirectorySeparator(above));
            }

            return new CycleSafeDirectory(root, [.. chain], prune);
        }

        /// <summary>
        ///     Lists the entries of the directory, leaving out the links back into the directories above.
        /// </summary>
        /// <returns>The files and the directories that are safe to enter.</returns>
        public override IEnumerable<FileSystemInfoBase> EnumerateFileSystemInfos()
        {
            // The enumeration of a directory fails when it is first read, not when it is created, so each step is guarded
            using var entries = base.EnumerateFileSystemInfos().GetEnumerator();
            while (true)
            {
                try
                {
                    if (!entries.MoveNext())
                    {
                        yield break;
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    yield break;
                }

                var entry = entries.Current;
                if (entry is not DirectoryInfoBase subdirectory)
                {
                    yield return entry;
                    continue;
                }

                if (Descend(new DirectoryInfo(subdirectory.FullName)) is { } safe)
                {
                    yield return safe;
                }
            }
        }

        /// <summary>
        ///     Gets a named subdirectory, as the matcher asks for it when a pattern names a folder.
        /// </summary>
        /// <param name="name">The name of the subdirectory, or <c>..</c>.</param>
        /// <returns>The subdirectory; null when it does not exist or is a link back into a directory above.</returns>
        /// <remarks>
        ///     The matcher accepts null for a missing directory (the base wrapper returns null too) although the wrapper's signature does not say so.
        /// </remarks>
        public override DirectoryInfoBase GetDirectory(string name)
        {
            var found = base.GetDirectory(name);
            return (found is null || name == ".." ? found : Descend(new DirectoryInfo(found.FullName)))!;
        }

        /// <summary>
        ///     Enters a subdirectory unless it leads back into a directory above.
        /// </summary>
        /// <param name="subdirectory">The subdirectory, as found below this directory.</param>
        /// <returns>The safe directory; null when it is pruned, or a link whose final target is this directory or one above it, or cannot be resolved.</returns>
        private CycleSafeDirectory? Descend(DirectoryInfo subdirectory)
        {
            // A pruned directory is never entered, so what is beneath it (a nested repository included) is never read
            if (_prune?.Invoke(subdirectory.FullName) == true)
            {
                return null;
            }

            string real;
            try
            {
                // A plain subdirectory lies where its parent really is, whatever links led the walk to the parent
                real = subdirectory.LinkTarget is null ? Path.Combine(_chain[^1], subdirectory.Name) : RealPathOf(subdirectory);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return null;
            }

            return _chain.Any(above => string.Equals(above, real, PathComparison))
                ? null
                : new CycleSafeDirectory(subdirectory, [.. _chain, real], _prune);
        }

        /// <summary>
        ///     Gets the path a directory really is: the final target of a link, otherwise its own full path.
        /// </summary>
        /// <param name="directory">The directory.</param>
        /// <returns>The full path without a trailing separator.</returns>
        /// <exception cref="IOException">Thrown when the directory is a link that cannot be resolved (a link loop, for instance).</exception>
        private static string RealPathOf(DirectoryInfo directory) => Canonical(directory.FullName, 0);

        /// <summary>
        ///     Resolves every link along a path, so that two spellings of one directory (such as <c>/var/x</c> and
        ///     <c>/private/var/x</c> on macOS, where <c>/var</c> is a link) give the same result.
        /// </summary>
        /// <param name="path">The full path.</param>
        /// <param name="depth">The number of links resolved so far on the way here.</param>
        /// <returns>The full path without a trailing separator and without any link in it.</returns>
        /// <exception cref="IOException">Thrown when a link cannot be resolved or the links are nested too deeply.</exception>
        private static string Canonical(string path, int depth)
        {
            if (depth > 32)
            {
                throw new IOException("Too many levels of links.");
            }

            var root = Path.GetPathRoot(path) ?? string.Empty;
            var current = root;
            foreach (var part in path[root.Length..].Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, part);
                var info = new DirectoryInfo(current);
                if (info.LinkTarget is null)
                {
                    continue;
                }

                var target = info.ResolveLinkTarget(returnFinalTarget: true) ?? throw new IOException("A link could not be resolved.");
                current = Canonical(target.FullName, depth + 1);
            }

            return Path.TrimEndingDirectorySeparator(current);
        }
    }
}

/// <summary>
///     A file selected for processing, and whether the command line named it.
/// </summary>
/// <param name="Path">The full path of the file.</param>
/// <param name="IsExplicit">
///     <see langword="true"/> when the file was named on the command line (as an existing file, or as a path
///     without wildcards); <see langword="false"/> when it was found through a directory, a glob or the default pattern.
/// </param>
/// <remarks>
///     The distinction exists because a file the user names is a deliberate request: the built-in exclusion of
///     <c>node_modules</c> and <c>.git</c> and the <c>ignore</c> patterns of the configuration do not apply to
///     it (only <c>!</c> excludes on the command line do). Immutable value type; safe for concurrent use.
/// </remarks>
internal readonly record struct DiscoveredFile(string Path, bool IsExplicit);

/// <summary>
///     The <c>ignore</c> patterns of a configuration and the directory they are relative to.
/// </summary>
/// <param name="Root">The full path of the directory the patterns are relative to.</param>
/// <param name="Patterns">The glob patterns (<c>*</c> and <c>**</c>).</param>
/// <remarks>
///     Immutable value type; safe for concurrent use.
/// </remarks>
internal sealed record IgnoreScope(string Root, IReadOnlyList<string> Patterns);
