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

namespace DemaConsulting.LintMark.Engine;

/// <summary>
///     Replaces the content of a file in one step, so a reader (or a crash) never sees a partial file.
/// </summary>
/// <remarks>
///     <para>
///         The new content is first written to a temporary file next to the target (same directory, so the final
///         step stays on one volume) and flushed to disk; the target is then replaced by that file in a single
///         operation. A failure at any point leaves the target as it was and removes the temporary file. The
///         temporary file is created by name with a unique suffix, never overwrites an existing file, and is
///         removed on every path.
///     </para>
///     <para>
///         The writer refuses to replace a file whose bytes are no longer the ones the caller read: the
///         rewrite was computed from those bytes, so applying it to different content would destroy someone
///         else's change. A read-only file is refused. When the path is a symbolic link the file it points to
///         is replaced and the link stays. On Unix the permission bits of the target are copied to the new file
///         (on Windows the replacement operation keeps the attributes of the replaced file).
///     </para>
///     <para>
///         The check and the replacement are two steps, so a change made by another program in the instant
///         between them is not detected; closing that window would need file locking that portable code cannot
///         rely on. Thread safety: static and stateless; two calls for the same file are not coordinated (the
///         engine writes sequentially).
///     </para>
/// </remarks>
internal static class AtomicFileWriter
{
    /// <summary>
    ///     The ending of the temporary file names, so a leftover from a crash is recognizable.
    /// </summary>
    private const string TemporarySuffix = ".lintmark-tmp";

    /// <summary>
    ///     Replaces the content of a file.
    /// </summary>
    /// <param name="path">Path of the file to replace. Must not be blank; the file must exist.</param>
    /// <param name="expectedOriginal">The bytes the new content was computed from; the file must still hold exactly these.</param>
    /// <param name="content">The new content (may be empty).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is blank.</exception>
    /// <exception cref="IOException">
    ///     Thrown when the file does not exist, its content is not <paramref name="expectedOriginal"/> any more,
    ///     or the temporary file cannot be written or the replacement fails. The target is left as it was.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the file is read-only or the directory cannot be written.</exception>
    public static void Write(string path, ReadOnlySpan<byte> expectedOriginal, ReadOnlySpan<byte> content) =>
        Write(path, expectedOriginal, content, null);

    /// <summary>
    ///     Replaces the content of a file, calling a hook just before the replacement.
    /// </summary>
    /// <param name="path">Path of the file to replace. Must not be blank; the file must exist.</param>
    /// <param name="expectedOriginal">The bytes the new content was computed from; the file must still hold exactly these.</param>
    /// <param name="content">The new content (may be empty).</param>
    /// <param name="beforeReplace">
    ///     Called after the temporary file is complete and before it replaces the target; a test seam that
    ///     simulates a crash (by throwing) or a concurrent change at the critical moment. May be null.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is blank.</exception>
    /// <exception cref="IOException">See <see cref="Write(string, ReadOnlySpan{byte}, ReadOnlySpan{byte})"/>; also whatever <paramref name="beforeReplace"/> throws.</exception>
    /// <exception cref="UnauthorizedAccessException">See <see cref="Write(string, ReadOnlySpan{byte}, ReadOnlySpan{byte})"/>.</exception>
    internal static void Write(string path, ReadOnlySpan<byte> expectedOriginal, ReadOnlySpan<byte> content, Action? beforeReplace)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(path);
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("The path must not be blank.", nameof(path));
        }

        // Replace the file a link points to, not the link
        var target = ResolveTarget(path);

        // Refuse to replace a file that is not what the rewrite was computed from
        if (!File.Exists(target))
        {
            throw new FileNotFoundException("The file does not exist.", path);
        }

        if ((File.GetAttributes(target) & FileAttributes.ReadOnly) != 0)
        {
            throw new UnauthorizedAccessException("The file is read-only.");
        }

        if (!File.ReadAllBytes(target).AsSpan().SequenceEqual(expectedOriginal))
        {
            throw new IOException("The file changed after it was read; it was not rewritten.");
        }

        // Write the complete new content next to the target, then swap it in; the temporary file never outlives the call
        var directory = Path.GetDirectoryName(target) ?? ".";
        var temporary = Path.Combine(directory, $".{Path.GetFileName(target)}.{Guid.NewGuid():N}{TemporarySuffix}");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(content);
                stream.Flush(flushToDisk: true);
            }

            // The new file takes over the permissions of the old one (Windows keeps the attributes when replacing)
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(temporary, File.GetUnixFileMode(target));
            }

            beforeReplace?.Invoke();
            File.Replace(temporary, target, null);
        }
        finally
        {
            DeleteQuietly(temporary);
        }
    }

    /// <summary>
    ///     Finds the file that a path finally refers to, following symbolic links.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The full path of the final target, or the full path of the path itself when it is not a link.</returns>
    private static string ResolveTarget(string path)
    {
        var full = Path.GetFullPath(path);
        var link = new FileInfo(full).ResolveLinkTarget(returnFinalTarget: true);
        return link?.FullName ?? full;
    }

    /// <summary>
    ///     Deletes a file if it exists, ignoring failures.
    /// </summary>
    /// <param name="path">The file to delete.</param>
    /// <remarks>
    ///     Used in a <see langword="finally"/> block, where a failure to clean up must not hide the failure that
    ///     is being propagated; a leftover file has a recognizable name.
    /// </remarks>
    private static void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: the original failure is the one to report
        }
    }
}
