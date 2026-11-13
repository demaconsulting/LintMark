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
///         is replaced and the link stays. On Unix the temporary file is created readable and writable by its
///         owner only, and the permission bits of the target are then copied to it before it replaces the target.
///         Attributes on Windows are left to the replacement operation of the platform and are not a guarantee of
///         this unit.
///     </para>
///     <para>
///         The bytes alone do not identify what was read: a link that is redirected to another file with the
///         same bytes would pass the byte comparison. The caller therefore captures the resolved target with
///         <see cref="ResolveTarget(string)"/> when it reads the file and hands it back; the writer refuses to
///         write when the path resolves to anything else, both when the write starts and again right before the
///         replacement. The temporary file is created next to, and replaces, that one resolved target.
///     </para>
///     <para>
///         The checks and the replacement are separate steps, so a change made by another program in the
///         instant between the last check and the replacement is not detected; closing that window would need
///         file locking or handle-based replacement that portable code cannot rely on. Thread safety: static
///         and stateless; two calls for the same file are not coordinated (the engine writes sequentially).
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
    /// <param name="expectedTarget">The resolved target of <paramref name="path"/> captured with <see cref="ResolveTarget(string)"/> when the file was read. Must not be blank.</param>
    /// <param name="expectedOriginal">The bytes the new content was computed from; the file must still hold exactly these.</param>
    /// <param name="content">The new content (may be empty).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> or <paramref name="expectedTarget"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> or <paramref name="expectedTarget"/> is blank.</exception>
    /// <exception cref="IOException">
    ///     Thrown when <paramref name="path"/> does not resolve to <paramref name="expectedTarget"/> any more,
    ///     the file does not exist, its content is not <paramref name="expectedOriginal"/> any more, or the
    ///     temporary file cannot be written or the replacement fails. The target is left as it was.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the file is read-only or the directory cannot be written.</exception>
    public static void Write(string path, string expectedTarget, ReadOnlySpan<byte> expectedOriginal, ReadOnlySpan<byte> content) =>
        Write(path, expectedTarget, expectedOriginal, content, null);

    /// <summary>
    ///     Replaces the content of a file, calling a hook just before the replacement.
    /// </summary>
    /// <param name="path">Path of the file to replace. Must not be blank; the file must exist.</param>
    /// <param name="expectedTarget">The resolved target of <paramref name="path"/> captured with <see cref="ResolveTarget(string)"/> when the file was read. Must not be blank.</param>
    /// <param name="expectedOriginal">The bytes the new content was computed from; the file must still hold exactly these.</param>
    /// <param name="content">The new content (may be empty).</param>
    /// <param name="beforeReplace">
    ///     Called after the temporary file is complete and before the target is checked a last time and replaced;
    ///     a test seam that simulates a crash (by throwing) or a concurrent change at the critical moment. May be null.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> or <paramref name="expectedTarget"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> or <paramref name="expectedTarget"/> is blank.</exception>
    /// <exception cref="IOException">See <see cref="Write(string, string, ReadOnlySpan{byte}, ReadOnlySpan{byte})"/>; also whatever <paramref name="beforeReplace"/> throws.</exception>
    /// <exception cref="UnauthorizedAccessException">See <see cref="Write(string, string, ReadOnlySpan{byte}, ReadOnlySpan{byte})"/>.</exception>
    internal static void Write(string path, string expectedTarget, ReadOnlySpan<byte> expectedOriginal, ReadOnlySpan<byte> content, Action? beforeReplace)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(expectedTarget);
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("The path must not be blank.", nameof(path));
        }

        if (string.IsNullOrWhiteSpace(expectedTarget))
        {
            throw new ArgumentException("The expected target must not be blank.", nameof(expectedTarget));
        }

        // Replace the file a link points to, not the link, and only the one that was read
        var target = ResolveTarget(path);
        EnsureSameTarget(expectedTarget, target);

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
            // Create the file readable and writable by its owner only, so the content is never exposed to others
            // while the target's own permissions are not yet applied (the option is ignored on Windows)
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None
            };
            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            }

            using (var stream = new FileStream(temporary, options))
            {
                stream.Write(content);
                stream.Flush(flushToDisk: true);
            }

            // The new file takes over the permissions of the old one (Unix only)
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(temporary, File.GetUnixFileMode(target));
            }

            beforeReplace?.Invoke();

            // Check the link again as late as possible, so a redirect during the write of the temporary file is
            // caught; the replacement then goes to the very target the temporary file was created next to
            EnsureSameTarget(target, ResolveTarget(path));
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
    /// <remarks>
    ///     The caller captures this when it reads the file and passes it to <c>Write</c>, which then refuses to
    ///     write when the path resolves to another file.
    /// </remarks>
    /// <exception cref="IOException">Thrown when the links cannot be followed (for instance a cycle).</exception>
    internal static string ResolveTarget(string path)
    {
        var full = Path.GetFullPath(path);
        var link = new FileInfo(full).ResolveLinkTarget(returnFinalTarget: true);
        return link?.FullName ?? full;
    }

    /// <summary>
    ///     Refuses a write when the file that a path refers to is not the one that was read.
    /// </summary>
    /// <param name="expectedTarget">The resolved target captured when the file was read.</param>
    /// <param name="currentTarget">The resolved target now.</param>
    /// <exception cref="IOException">Thrown when the two differ.</exception>
    internal static void EnsureSameTarget(string expectedTarget, string currentTarget)
    {
        if (!string.Equals(expectedTarget, currentTarget, StringComparison.Ordinal))
        {
            throw new IOException("The file link now points elsewhere than when it was read; it was not rewritten.");
        }
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
