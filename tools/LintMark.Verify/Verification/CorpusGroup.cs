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

namespace DemaConsulting.LintMark.Verify.Verification;

/// <summary>
///     The corpus category and source a file belongs to, derived from its path.
/// </summary>
/// <remarks>
///     <para>
///         A file below <c>.corpus/&lt;category&gt;/&lt;source&gt;/</c> belongs to that source of that category
///         (<c>.corpus/spec/alpha/examples/001-tabs.md</c> is in category <c>spec</c>, source
///         <c>spec/alpha</c>). A file below the DEMA root, when the DEMA repositories are verified, is in
///         category and source <c>dema</c>. Every other file is in <c>other</c>.
///     </para>
///     <para>
///         The derivation looks at the path only; it never touches the file system. Immutable.
///     </para>
/// </remarks>
/// <param name="Category">The category, for example <c>spec</c>.</param>
/// <param name="Source">The source including its category, for example <c>spec/alpha</c>.</param>
internal readonly record struct CorpusGroup(string Category, string Source)
{
    /// <summary>
    ///     The name of the corpus folder.
    /// </summary>
    internal const string CorpusFolder = ".corpus";

    /// <summary>
    ///     The category and source of the DEMA repositories.
    /// </summary>
    internal const string Dema = "dema";

    /// <summary>
    ///     The category and source of every file that is neither in the corpus nor in a DEMA repository.
    /// </summary>
    internal const string Other = "other";

    /// <summary>
    ///     The characters that separate the segments of a path.
    /// </summary>
    private static readonly char[] Separators = ['/', '\\'];

    /// <summary>
    ///     Determines the group of a file.
    /// </summary>
    /// <param name="path">The full path of the file. Must not be null.</param>
    /// <param name="demaRoot">The full path of the DEMA root when the DEMA repositories are verified; otherwise <see langword="null"/>.</param>
    /// <returns>The group.</returns>
    public static CorpusGroup Of(string path, string? demaRoot)
    {
        ArgumentNullException.ThrowIfNull(path);

        // The folders between the corpus folder and the file name give the category and the source
        var segments = path.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        var corpus = Array.FindIndex(segments, static s => string.Equals(s, CorpusFolder, StringComparison.OrdinalIgnoreCase));
        if (corpus >= 0)
        {
            var folders = segments.Length - 1 - (corpus + 1);
            return folders switch
            {
                >= 2 => new CorpusGroup(segments[corpus + 1], $"{segments[corpus + 1]}/{segments[corpus + 2]}"),
                1 => new CorpusGroup(segments[corpus + 1], segments[corpus + 1]),
                _ => new CorpusGroup(Other, Other)
            };
        }

        return demaRoot is not null && IsBelow(path, demaRoot) ? new CorpusGroup(Dema, Dema) : new CorpusGroup(Other, Other);
    }

    /// <summary>
    ///     Determines whether a path lies below a directory.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <param name="directory">The directory.</param>
    /// <returns><see langword="true"/> when the path is inside the directory.</returns>
    private static bool IsBelow(string path, string directory)
    {
        var relative = Path.GetRelativePath(directory, path);
        return !relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative);
    }
}

/// <summary>
///     The results of the files of one corpus group.
/// </summary>
/// <param name="Group">The group.</param>
/// <param name="Files">The number of files in the group.</param>
/// <param name="KindCounts">The number of (file, option set) pairs for each primary outcome kind.</param>
internal sealed record GroupSummary(CorpusGroup Group, int Files, IReadOnlyDictionary<OutcomeKind, int> KindCounts)
{
    /// <summary>
    ///     Gets the number of (file, option set) pairs verified.
    /// </summary>
    public int Total => KindCounts.Values.Sum();

    /// <summary>
    ///     Gets the number of failed pairs.
    /// </summary>
    public int FailureCount => KindCounts.Where(static pair => pair.Key.IsFailure()).Sum(static pair => pair.Value);

    /// <summary>
    ///     Gets the number of passed pairs, refusals by design included.
    /// </summary>
    public int PassCount => Total - FailureCount;

    /// <summary>
    ///     Gets the failed pairs for each failure kind, in kind order.
    /// </summary>
    public IEnumerable<KeyValuePair<OutcomeKind, int>> FailuresByKind =>
        KindCounts.Where(static pair => pair.Key.IsFailure()).OrderBy(static pair => pair.Key);

    /// <summary>
    ///     Combines the groups that share a category into one summary per category.
    /// </summary>
    /// <param name="sources">The summaries of the sources. Must not be null.</param>
    /// <returns>One summary per category in ordinal order; the source of each is the category.</returns>
    public static IReadOnlyList<GroupSummary> ByCategory(IEnumerable<GroupSummary> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        return [.. sources
            .GroupBy(static s => s.Group.Category, StringComparer.Ordinal)
            .OrderBy(static g => g.Key, StringComparer.Ordinal)
            .Select(static g => new GroupSummary(
                new CorpusGroup(g.Key, g.Key),
                g.Sum(static s => s.Files),
                g.SelectMany(static s => s.KindCounts)
                    .GroupBy(static pair => pair.Key)
                    .ToDictionary(static kind => kind.Key, static kind => kind.Sum(static pair => pair.Value))))];
    }
}
