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

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Locates the Markdown files of this repository, the corpus of the span-tiling tests.
/// </summary>
/// <remarks>
///     The corpus is found by walking up from the test assembly's directory to the directory that
///     contains the solution file. A missing repository root is a hard failure rather than a skipped
///     test, so the corpus coverage cannot silently disappear in CI. Generated and third-party
///     directories are pruned during the walk (a plain recursive scan of the repository root is very
///     slow because of dependency folders).
/// </remarks>
internal static class RepositoryMarkdownFiles
{
    /// <summary>
    ///     The solution file that marks the repository root.
    /// </summary>
    private const string SolutionFileName = "DemaConsulting.LintMark.slnx";

    /// <summary>
    ///     Directory names that are never searched.
    /// </summary>
    private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", "bin", "obj", ".git", ".venv", "venv", "generated", ".agent-logs", "TestResults", "artifacts"
    };

    /// <summary>
    ///     Finds the repository root.
    /// </summary>
    /// <returns>The full path of the directory that contains the solution file.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no parent directory contains the solution file.</exception>
    public static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, SolutionFileName)))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException($"Repository root (containing {SolutionFileName}) not found above {AppContext.BaseDirectory}");
    }

    /// <summary>
    ///     Enumerates every Markdown file of the repository.
    /// </summary>
    /// <returns>Full paths in ordinal order.</returns>
    public static IReadOnlyList<string> Enumerate()
    {
        var files = new List<string>();
        Walk(FindRoot(), files);
        files.Sort(StringComparer.Ordinal);
        return files;
    }

    /// <summary>
    ///     Adds the Markdown files of a directory tree, skipping excluded directories.
    /// </summary>
    /// <param name="directory">The directory to search.</param>
    /// <param name="files">Receives the files.</param>
    private static void Walk(string directory, List<string> files)
    {
        files.AddRange(Directory.EnumerateFiles(directory, "*.md"));
        foreach (var child in Directory.EnumerateDirectories(directory))
        {
            if (!ExcludedDirectories.Contains(Path.GetFileName(child)))
            {
                Walk(child, files);
            }
        }
    }
}
