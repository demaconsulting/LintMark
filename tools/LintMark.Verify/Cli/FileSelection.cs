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

using DemaConsulting.LintMark.Engine;

namespace DemaConsulting.LintMark.Verify.Cli;

/// <summary>
///     Finds the Markdown files to verify: directories, files and globs, and the DEMA repositories.
/// </summary>
/// <remarks>
///     <para>
///         Folders that hold generated or third-party content are skipped wherever they occur below the root
///         of a directory argument, glob or repository: <c>generated</c>, <c>node_modules</c>, <c>bin</c>,
///         <c>obj</c>, <c>.git</c>, <c>.venv</c>, <c>.corpus</c>, <c>TestResults</c> and <c>.agent-logs</c>. The
///         root itself is never judged, so the corpus folder can be named as the argument. Directory links are
///         not followed.
///     </para>
///     <para>
///         The class only enumerates; it never opens a file. Stateless and thread-safe.
///     </para>
/// </remarks>
internal static class FileSelection
{
    /// <summary>
    ///     The names of the folders that are never searched (compared ignoring case).
    /// </summary>
    internal static readonly string[] ExcludedFolders =
        ["generated", "node_modules", "bin", "obj", ".git", ".venv", ".corpus", "TestResults", ".agent-logs"];

    /// <summary>
    ///     How deep below the DEMA root a repository is looked for.
    /// </summary>
    private const int MaxRepositoryDepth = 4;

    /// <summary>
    ///     The seed of the sample, fixed so that a sample can be repeated.
    /// </summary>
    private const int SampleSeed = 20260101;

    /// <summary>
    ///     The characters that make a path argument a glob.
    /// </summary>
    private static readonly char[] WildcardCharacters = ['*', '?', '[', '{'];

    /// <summary>
    ///     The characters that separate the segments of a path.
    /// </summary>
    private static readonly char[] PathSeparators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    /// <summary>
    ///     Selects the files of a run.
    /// </summary>
    /// <param name="options">The command line. Must not be null.</param>
    /// <param name="baseDirectory">The directory that relative arguments are resolved against. Must not be null.</param>
    /// <returns>Distinct files in ordinal order of the path.</returns>
    /// <exception cref="UsageException">Thrown when the DEMA root does not exist.</exception>
    public static IReadOnlyList<DiscoveredFile> Select(VerifyOptions options, string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(baseDirectory);

        // Every argument contributes its own files, so the exclusions are judged against its own root
        var found = new Dictionary<string, DiscoveredFile>(StringComparer.OrdinalIgnoreCase);
        var excludes = options.Paths.Where(static p => p.StartsWith('!')).ToList();
        foreach (var include in options.Paths.Where(static p => !p.StartsWith('!')))
        {
            foreach (var file in Expand(include, excludes, baseDirectory))
            {
                found[file.Path] = found.TryGetValue(file.Path, out var known) && known.IsExplicit ? known : file;
            }
        }

        // The DEMA repositories are added whole
        if (options.Dema)
        {
            foreach (var repository in FindRepositories(options.DemaRoot))
            {
                foreach (var path in Walk(repository))
                {
                    found.TryAdd(path, new DiscoveredFile(path, isExplicit: false));
                }
            }
        }

        return [.. found.Values.OrderBy(static f => f.Path, StringComparer.Ordinal)];
    }

    /// <summary>
    ///     Picks a deterministic subset of files.
    /// </summary>
    /// <param name="files">The files in ordinal order. Must not be null.</param>
    /// <param name="count">The size of the subset; at least the number of files means all of them.</param>
    /// <returns>The subset in ordinal order; the same input and count always give the same subset.</returns>
    public static IReadOnlyList<DiscoveredFile> Sample(IReadOnlyList<DiscoveredFile> files, int count)
    {
        ArgumentNullException.ThrowIfNull(files);
        if (count >= files.Count)
        {
            return files;
        }

        // A seeded Fisher-Yates shuffle of the positions; the first count positions are kept. The seed makes the
        // sample repeatable, which is the point: the generator is not used for anything that needs to be unpredictable
        var positions = Enumerable.Range(0, files.Count).ToArray();
#pragma warning disable S2245
        var random = new Random(SampleSeed);
#pragma warning restore S2245
        for (var i = positions.Length - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (positions[i], positions[j]) = (positions[j], positions[i]);
        }

        return [.. positions.Take(count).Order().Select(position => files[position])];
    }

    /// <summary>
    ///     Finds the Git repositories below a root.
    /// </summary>
    /// <param name="root">The root directory.</param>
    /// <returns>The repository directories in ordinal order; a repository is not searched for further repositories.</returns>
    /// <exception cref="UsageException">Thrown when the root is not a directory.</exception>
    public static IReadOnlyList<string> FindRepositories(string root)
    {
        if (!Directory.Exists(root))
        {
            throw new UsageException($"The DEMA root '{root}' does not exist.");
        }

        var repositories = new List<string>();
        FindRepositories(Path.GetFullPath(root), 0, repositories);
        repositories.Sort(StringComparer.Ordinal);
        return repositories;
    }

    /// <summary>
    ///     Enumerates the Markdown files below a directory, skipping the excluded folders and links.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <returns>The full paths of the files, in no particular order.</returns>
    public static IEnumerable<string> Walk(string directory)
    {
        var pending = new Stack<string>();
        pending.Push(Path.GetFullPath(directory));
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            foreach (var file in SafeEnumerate(() => Directory.EnumerateFiles(current, "*.md")))
            {
                yield return file;
            }

            foreach (var child in SafeEnumerate(() => Directory.EnumerateDirectories(current)).Where(IsSearchable))
            {
                pending.Push(child);
            }
        }
    }

    /// <summary>
    ///     Expands one include argument.
    /// </summary>
    /// <param name="include">The directory, file or glob.</param>
    /// <param name="excludes">The exclude arguments (with their leading <c>!</c>).</param>
    /// <param name="baseDirectory">The directory that relative arguments are resolved against.</param>
    /// <returns>The files; a directory yields files that are not explicit, a named file an explicit one.</returns>
    private static IEnumerable<DiscoveredFile> Expand(string include, List<string> excludes, string baseDirectory)
    {
        var full = Path.GetFullPath(Path.Combine(baseDirectory, include));

        // A directory is walked by this class, which also prunes the excluded folders early
        if (Directory.Exists(full))
        {
            return Walk(full).Select(static p => new DiscoveredFile(p, isExplicit: false));
        }

        // Everything else is a file or a glob; the product's discovery expands it
        var root = SearchRoot(include, baseDirectory);
        return FileDiscovery.DiscoverFiles([include, .. excludes], baseDirectory)
            .Where(file => !HasExcludedFolder(root, file.Path));
    }

    /// <summary>
    ///     Gets the directory a glob or file argument is searched from: the part before the first wildcard.
    /// </summary>
    /// <param name="include">The argument.</param>
    /// <param name="baseDirectory">The directory that relative arguments are resolved against.</param>
    /// <returns>The full path of the directory.</returns>
    private static string SearchRoot(string include, string baseDirectory)
    {
        var normalized = include.Replace('\\', '/');
        var wildcard = normalized.IndexOfAny(WildcardCharacters);
        var literal = wildcard < 0 ? normalized : normalized[..wildcard];
        var slash = literal.LastIndexOf('/');
        var directory = slash < 0 ? string.Empty : literal[..(slash + 1)];
        return Path.GetFullPath(Path.Combine(baseDirectory, directory.Length == 0 ? "." : directory));
    }

    /// <summary>
    ///     Determines whether a file lies below an excluded folder, judged from a root.
    /// </summary>
    /// <param name="root">The directory the search started in; its own name is not judged.</param>
    /// <param name="path">The full path of the file.</param>
    /// <returns><see langword="true"/> when a folder between the root and the file is excluded.</returns>
    internal static bool HasExcludedFolder(string root, string path)
    {
        var relative = Path.GetRelativePath(root, Path.GetDirectoryName(path) ?? root);
        return relative
            .Split(PathSeparators)
            .Any(static segment => ExcludedFolders.Contains(segment, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Determines whether a directory is searched.
    /// </summary>
    /// <param name="directory">The full path of the directory.</param>
    /// <returns><see langword="false"/> for an excluded folder and for a link.</returns>
    private static bool IsSearchable(string directory)
    {
        if (ExcludedFolders.Contains(Path.GetFileName(directory), StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            return !new DirectoryInfo(directory).Attributes.HasFlag(FileAttributes.ReparsePoint);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    ///     Collects the repositories below a directory.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <param name="depth">How far below the root the directory is.</param>
    /// <param name="repositories">Receives the repositories.</param>
    private static void FindRepositories(string directory, int depth, List<string> repositories)
    {
        // A directory with a .git entry (a folder, or a file for a worktree) is a repository
        var git = Path.Combine(directory, ".git");
        if (Directory.Exists(git) || File.Exists(git))
        {
            repositories.Add(directory);
            return;
        }

        if (depth >= MaxRepositoryDepth)
        {
            return;
        }

        foreach (var child in SafeEnumerate(() => Directory.EnumerateDirectories(directory)).Where(IsSearchable))
        {
            FindRepositories(child, depth + 1, repositories);
        }
    }

    /// <summary>
    ///     Enumerates entries, treating an unreadable directory as empty.
    /// </summary>
    /// <param name="enumerate">The enumeration.</param>
    /// <returns>The entries; none when the directory cannot be read.</returns>
    private static IEnumerable<string> SafeEnumerate(Func<IEnumerable<string>> enumerate)
    {
        try
        {
            return enumerate().ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
