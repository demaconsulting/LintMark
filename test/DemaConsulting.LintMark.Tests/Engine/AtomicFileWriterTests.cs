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

using System.Text;
using DemaConsulting.LintMark.Engine;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Test that the AtomicFileWriter class replaces files completely or not at all.
/// </summary>
public class AtomicFileWriterTests
{
    /// <summary>
    ///     The ending of the temporary files the writer creates.
    /// </summary>
    private const string TemporarySuffix = ".lintmark-tmp";

    /// <summary>
    ///     Encodes text as UTF-8 bytes.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The bytes.</returns>
    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

    /// <summary>
    ///     Finds the temporary files the writer may have left anywhere under a directory.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <returns>The paths of the leftovers.</returns>
    private static string[] Leftovers(TemporaryDirectory directory) =>
        Directory.GetFiles(directory.FullPath, "*" + TemporarySuffix, SearchOption.AllDirectories);

    /// <summary>
    ///     Replaces a file the way the engine does: the target the path resolves to now is the one that was read.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="original">The bytes the content was computed from.</param>
    /// <param name="content">The new content.</param>
    /// <param name="beforeReplace">The hook called just before the replacement, or null.</param>
    private static void WriteResolved(string path, byte[] original, byte[] content, Action? beforeReplace = null) =>
        AtomicFileWriter.Write(path, AtomicFileWriter.ResolveTarget(path), original, content, beforeReplace);

    /// <summary>
    ///     Points a symbolic link at another file by replacing the link.
    /// </summary>
    /// <param name="link">The path of the link.</param>
    /// <param name="target">The new target.</param>
    private static void Redirect(string link, string target)
    {
        File.Delete(link);
        File.CreateSymbolicLink(link, target);
    }

    /// <summary>
    ///     Test that new content replaces the file.
    /// </summary>
    [Fact]
    public void AtomicFileWriter_Write_NewContent_ReplacesFile()
    {
        // Arrange: a file with known content
        using var directory = new TemporaryDirectory();
        var path = directory.WriteFile("a.md", "old\n");

        // Act: replace it
        WriteResolved(path, Bytes("old\n"), Bytes("new\n"));

        // Assert: the file holds the new content
        Assert.Equal(Bytes("new\n"), File.ReadAllBytes(path));
    }

    /// <summary>
    ///     Test that a failure just before the replacement leaves the original and removes the temporary file.
    /// </summary>
    [Fact]
    public void AtomicFileWriter_Write_BeforeReplaceThrows_OriginalIntactAndTempRemoved()
    {
        // Arrange: a file, and a hook that simulates a crash after the temporary file is complete
        using var directory = new TemporaryDirectory();
        var path = directory.WriteBytes("a.md", Bytes("old\n"));
        var original = Bytes("old\n");
        var content = Bytes("new\n");

        // Act: write with a failing hook
        var exception = Assert.Throws<IOException>(() =>
            WriteResolved(path, original, content, () => throw new IOException("simulated crash")));

        // Assert: the hook's failure propagates, the original bytes are intact and no temporary file remains
        Assert.Equal("simulated crash", exception.Message);
        Assert.Equal(original, File.ReadAllBytes(path));
        Assert.Empty(Leftovers(directory));
    }

    /// <summary>
    ///     Test that a file that changed since it was read is refused and left as the other writer made it.
    /// </summary>
    [Fact]
    public void AtomicFileWriter_Write_FileChangedSinceRead_ThrowsAndLeavesFile()
    {
        // Arrange: a file that now holds something else than the bytes the rewrite was computed from
        using var directory = new TemporaryDirectory();
        var path = directory.WriteBytes("a.md", Bytes("changed by someone else\n"));
        var read = Bytes("what was read\n");
        var content = Bytes("rewrite\n");

        // Act / Assert: the write is refused
        Assert.Throws<IOException>(() => WriteResolved(path, read, content));

        // Assert: the other writer's content is still there and nothing is left over
        Assert.Equal(Bytes("changed by someone else\n"), File.ReadAllBytes(path));
        Assert.Empty(Leftovers(directory));
    }

    /// <summary>
    ///     Test that a read-only file is refused and left as it was.
    /// </summary>
    [Fact]
    public void AtomicFileWriter_Write_ReadOnlyFile_ThrowsAndLeavesFile()
    {
        // Arrange: a read-only file
        using var directory = new TemporaryDirectory();
        var path = directory.WriteBytes("a.md", Bytes("old\n"));
        var original = Bytes("old\n");
        var content = Bytes("new\n");
        File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
        try
        {
            // Act / Assert: the write is refused
            Assert.Throws<UnauthorizedAccessException>(() => WriteResolved(path, original, content));

            // Assert: the content is unchanged and nothing is left over
            Assert.Equal(original, File.ReadAllBytes(path));
            Assert.Empty(Leftovers(directory));
        }
        finally
        {
            File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
        }
    }

    /// <summary>
    ///     Test that a file that does not exist is refused.
    /// </summary>
    [Fact]
    public void AtomicFileWriter_Write_MissingFile_Throws()
    {
        // Arrange: a path in an existing directory with no file
        using var directory = new TemporaryDirectory();
        var path = directory.PathOf("missing.md");
        var original = Bytes("old\n");
        var content = Bytes("new\n");

        // Act / Assert: the write is refused and no file is created
        Assert.Throws<FileNotFoundException>(() => WriteResolved(path, original, content));
        Assert.False(File.Exists(path));
    }

    /// <summary>
    ///     Test that a path in a directory that does not exist is refused.
    /// </summary>
    [Fact]
    public void AtomicFileWriter_Write_MissingDirectory_Throws()
    {
        // Arrange: a path whose directory does not exist
        using var directory = new TemporaryDirectory();
        var path = directory.PathOf("no-such-folder/a.md");
        var original = Bytes("old\n");
        var content = Bytes("new\n");

        // Act / Assert: the write is refused with an I/O failure and the directory is not created
        Assert.ThrowsAny<IOException>(() => WriteResolved(path, original, content));
        Assert.False(Directory.Exists(directory.PathOf("no-such-folder")));
    }

    /// <summary>
    ///     Test that a successful write leaves no temporary file next to the target.
    /// </summary>
    [Fact]
    public void AtomicFileWriter_Write_Success_LeavesNoTemporaryFile()
    {
        // Arrange: a file
        using var directory = new TemporaryDirectory();
        var path = directory.WriteBytes("a.md", Bytes("old\n"));

        // Act: replace it
        WriteResolved(path, Bytes("old\n"), Bytes("new\n"));

        // Assert: the target is the only file in the directory
        Assert.Equal([path], Directory.GetFiles(directory.FullPath));
        Assert.Empty(Leftovers(directory));
    }

    /// <summary>
    ///     Test that writing to a symbolic link replaces the file it points to and keeps the link.
    /// </summary>
    [Fact]
    public void AtomicFileWriter_Write_SymlinkTarget_WritesThroughLink()
    {
        // Arrange: a symbolic link to a file (skipped where creating links needs special rights)
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Creating symbolic links on Windows needs privileges that a test run may not have.");
            return;
        }

        using var directory = new TemporaryDirectory();
        var real = directory.WriteBytes("real.md", Bytes("old\n"));
        var link = directory.PathOf("link.md");
        File.CreateSymbolicLink(link, real);

        // Act: write through the link
        WriteResolved(link, Bytes("old\n"), Bytes("new\n"));

        // Assert: the real file has the new content, and the link is still a link to it
        Assert.Equal(Bytes("new\n"), File.ReadAllBytes(real));
        Assert.NotNull(new FileInfo(link).LinkTarget);
        Assert.Equal(Bytes("new\n"), File.ReadAllBytes(link));
    }

    /// <summary>
    ///     Test that an executable file stays executable.
    /// </summary>
    [Fact]
    public void AtomicFileWriter_Write_ExecutableFile_KeepsMode()
    {
        // Arrange: an executable file (Unix permission bits do not exist on Windows)
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix file modes do not exist on Windows.");
            return;
        }

        using var directory = new TemporaryDirectory();
        var path = directory.WriteBytes("run.md", Bytes("old\n"));
        const UnixFileMode Mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                                  | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                                  | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
        File.SetUnixFileMode(path, Mode);

        // Act: replace the content
        WriteResolved(path, Bytes("old\n"), Bytes("new\n"));

        // Assert: the mode is as it was
        Assert.Equal(Mode, File.GetUnixFileMode(path));
    }

    /// <summary>
    ///     Test that empty content produces an empty file.
    /// </summary>
    [Fact]
    public void AtomicFileWriter_Write_EmptyContent_WritesEmptyFile()
    {
        // Arrange: a file with content
        using var directory = new TemporaryDirectory();
        var path = directory.WriteBytes("a.md", Bytes("old\n"));

        // Act: replace it with nothing
        WriteResolved(path, Bytes("old\n"), []);

        // Assert: the file exists and is empty
        Assert.True(File.Exists(path));
        Assert.Empty(File.ReadAllBytes(path));
    }

    /// <summary>
    ///     Test that a null path is refused.
    /// </summary>
    [Fact]
    public void AtomicFileWriter_Write_NullPath_Throws()
    {
        // Arrange: valid bytes
        var original = Bytes("old\n");
        var content = Bytes("new\n");

        // Act / Assert: the null path is refused
        Assert.Throws<ArgumentNullException>(() => AtomicFileWriter.Write(null!, "target.md", original, content));
    }

    /// <summary>
    ///     Test that an empty or blank path is refused.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AtomicFileWriter_Write_BlankPath_Throws(string path)
    {
        // Arrange: valid bytes
        var original = Bytes("old\n");
        var content = Bytes("new\n");

        // Act / Assert: the blank path is refused
        Assert.Throws<ArgumentException>(() => AtomicFileWriter.Write(path, "target.md", original, content));
    }

    /// <summary>
    ///     Test that a byte order mark and CRLF line endings are written exactly.
    /// </summary>
    [Fact]
    public void AtomicFileWriter_Write_ContentWithBomAndCrlf_WrittenByteForByte()
    {
        // Arrange: a file and content with a UTF-8 BOM, CRLF endings and a non-ASCII character
        using var directory = new TemporaryDirectory();
        var path = directory.WriteBytes("a.md", Bytes("old\n"));
        byte[] content = [0xEF, 0xBB, 0xBF, .. "caf\u00E9\r\nb\r\n"u8];

        // Act: replace the content
        WriteResolved(path, Bytes("old\n"), content);

        // Assert: every byte is as given
        Assert.Equal(content, File.ReadAllBytes(path));
    }

    /// <summary>
    ///     Test that large content is written completely.
    /// </summary>
    [Fact]
    public void AtomicFileWriter_Write_LargeContent_WritesAllBytes()
    {
        // Arrange: a file and five megabytes of patterned content
        using var directory = new TemporaryDirectory();
        var path = directory.WriteBytes("a.md", Bytes("old\n"));
        var content = new byte[5 * 1024 * 1024];
        for (var i = 0; i < content.Length; i++)
        {
            content[i] = (byte)(i % 251);
        }

        // Act: replace the content
        WriteResolved(path, Bytes("old\n"), content);

        // Assert: the file has the same bytes
        Assert.Equal(content, File.ReadAllBytes(path));
    }

    /// <summary>
    ///     Test that a second write expects the bytes the first one wrote.
    /// </summary>
    [Fact]
    public void AtomicFileWriter_Write_TwoSequentialWrites_SecondUsesNewBytesAsExpected()
    {
        // Arrange: a file that is replaced once
        using var directory = new TemporaryDirectory();
        var path = directory.WriteBytes("a.md", Bytes("one\n"));
        WriteResolved(path, Bytes("one\n"), Bytes("two\n"));

        // Act: replace it again, expecting what the first write produced
        WriteResolved(path, Bytes("two\n"), Bytes("three\n"));

        // Assert: the file holds the last content
        Assert.Equal(Bytes("three\n"), File.ReadAllBytes(path));
    }

    /// <summary>
    ///     Test that an expected original that matches only a prefix or an extension of the file is refused.
    /// </summary>
    [Theory]
    [InlineData("abc")]
    [InlineData("abc\n\n")]
    public void AtomicFileWriter_Write_OriginalBytesEqualButDifferentLength_Throws(string expected)
    {
        // Arrange: a file whose bytes begin like the expected original but differ in length
        using var directory = new TemporaryDirectory();
        var path = directory.WriteBytes("a.md", Bytes("abc\n"));
        var expectedBytes = Bytes(expected);
        var content = Bytes("new\n");

        // Act / Assert: the write is refused
        Assert.Throws<IOException>(() => WriteResolved(path, expectedBytes, content));

        // Assert: the file is untouched
        Assert.Equal(Bytes("abc\n"), File.ReadAllBytes(path));
    }

    /// <summary>
    ///     Test that a path that resolves to another file than the one that was read is refused, also when both hold the same bytes.
    /// </summary>
    [Fact]
    public void AtomicFileWriter_Write_TargetDiffersFromExpected_ThrowsAndLeavesBothFiles()
    {
        // Arrange: two files with identical bytes, and an expected target that is the other one
        using var directory = new TemporaryDirectory();
        var path = directory.WriteBytes("a.md", Bytes("same\n"));
        var other = directory.WriteBytes("b.md", Bytes("same\n"));
        var content = Bytes("rewrite\n");

        // Act / Assert: the write is refused because the path is not the file that was read
        Assert.Throws<IOException>(() => AtomicFileWriter.Write(path, AtomicFileWriter.ResolveTarget(other), Bytes("same\n"), content));

        // Assert: both files are untouched and nothing is left over
        Assert.Equal(Bytes("same\n"), File.ReadAllBytes(path));
        Assert.Equal(Bytes("same\n"), File.ReadAllBytes(other));
        Assert.Empty(Leftovers(directory));
    }

    /// <summary>
    ///     Test that a link redirected to a file with the same bytes after it was read is refused.
    /// </summary>
    [Fact]
    public void AtomicFileWriter_Write_LinkRedirectedAfterRead_ThrowsAndLeavesBothFiles()
    {
        // Arrange: a link to a file whose target is captured, then redirected to another file with the same bytes
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Creating symbolic links on Windows needs privileges that a test run may not have.");
            return;
        }

        using var directory = new TemporaryDirectory();
        var first = directory.WriteBytes("first.md", Bytes("same\n"));
        var second = directory.WriteBytes("second.md", Bytes("same\n"));
        var link = directory.PathOf("link.md");
        File.CreateSymbolicLink(link, first);
        var captured = AtomicFileWriter.ResolveTarget(link);
        Redirect(link, second);

        // Act / Assert: the write is refused
        Assert.Throws<IOException>(() => AtomicFileWriter.Write(link, captured, Bytes("same\n"), Bytes("rewrite\n")));

        // Assert: neither file was rewritten and nothing is left over
        Assert.Equal(Bytes("same\n"), File.ReadAllBytes(first));
        Assert.Equal(Bytes("same\n"), File.ReadAllBytes(second));
        Assert.Empty(Leftovers(directory));
    }

    /// <summary>
    ///     Test that a link redirected to a file with the same bytes just before the replacement is refused and the temporary file is removed.
    /// </summary>
    [Fact]
    public void AtomicFileWriter_Write_LinkRedirectedBeforeReplace_ThrowsAndRemovesTemp()
    {
        // Arrange: a link that is redirected by the hook once the temporary file is complete
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Creating symbolic links on Windows needs privileges that a test run may not have.");
            return;
        }

        using var directory = new TemporaryDirectory();
        var first = directory.WriteBytes("first.md", Bytes("same\n"));
        var second = directory.WriteBytes("second.md", Bytes("same\n"));
        var link = directory.PathOf("link.md");
        File.CreateSymbolicLink(link, first);

        // Act / Assert: the write is refused by the check right before the replacement
        Assert.Throws<IOException>(() => WriteResolved(link, Bytes("same\n"), Bytes("rewrite\n"), () => Redirect(link, second)));

        // Assert: neither file was rewritten, the link points where the other program put it, and nothing is left over
        Assert.Equal(Bytes("same\n"), File.ReadAllBytes(first));
        Assert.Equal(Bytes("same\n"), File.ReadAllBytes(second));
        Assert.Equal(second, new FileInfo(link).LinkTarget);
        Assert.Empty(Leftovers(directory));
    }

    /// <summary>
    ///     Test that the resolved target of a regular file is its full path.
    /// </summary>
    [Fact]
    public void AtomicFileWriter_ResolveTarget_RegularFile_ReturnsFullPath()
    {
        // Arrange: a regular file
        using var directory = new TemporaryDirectory();
        var path = directory.WriteBytes("a.md", Bytes("old\n"));

        // Act: resolve it
        var target = AtomicFileWriter.ResolveTarget(path);

        // Assert: the full path comes back
        Assert.Equal(Path.GetFullPath(path), target);
    }

    /// <summary>
    ///     Test that the resolved target of a link is the file it finally points to.
    /// </summary>
    [Fact]
    public void AtomicFileWriter_ResolveTarget_SymlinkToFile_ReturnsFinalTarget()
    {
        // Arrange: a link to a file
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Creating symbolic links on Windows needs privileges that a test run may not have.");
            return;
        }

        using var directory = new TemporaryDirectory();
        var real = directory.WriteBytes("real.md", Bytes("old\n"));
        var link = directory.PathOf("link.md");
        File.CreateSymbolicLink(link, real);

        // Act: resolve the link
        var target = AtomicFileWriter.ResolveTarget(link);

        // Assert: the real file comes back
        Assert.Equal(Path.GetFullPath(real), target);
    }

    /// <summary>
    ///     Test that the same resolved target is accepted.
    /// </summary>
    [Fact]
    public void AtomicFileWriter_EnsureSameTarget_SameTarget_DoesNotThrow()
    {
        // Act / Assert: equal paths are accepted
        var exception = Record.Exception(() => AtomicFileWriter.EnsureSameTarget("/work/a.md", "/work/a.md"));
        Assert.Null(exception);
    }

    /// <summary>
    ///     Test that a different resolved target is refused.
    /// </summary>
    [Fact]
    public void AtomicFileWriter_EnsureSameTarget_DifferentTarget_Throws()
    {
        // Act / Assert: different paths are refused
        Assert.Throws<IOException>(() => AtomicFileWriter.EnsureSameTarget("/work/a.md", "/work/b.md"));
    }

    /// <summary>
    ///     Test that a missing or blank expected target is refused.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AtomicFileWriter_Write_MissingExpectedTarget_Throws(string? expectedTarget)
    {
        // Arrange: an existing file
        using var directory = new TemporaryDirectory();
        var path = directory.WriteBytes("a.md", Bytes("old\n"));
        var original = Bytes("old\n");
        var content = Bytes("new\n");

        // Act / Assert: the missing identity is refused and the file is untouched
        Assert.ThrowsAny<ArgumentException>(() => AtomicFileWriter.Write(path, expectedTarget!, original, content));
        Assert.Equal(original, File.ReadAllBytes(path));
    }
}
