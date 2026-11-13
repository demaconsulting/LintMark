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
    ///     Test that new content replaces the file.
    /// </summary>
    [Fact]
    public void AtomicFileWriter_Write_NewContent_ReplacesFile()
    {
        // Arrange: a file with known content
        using var directory = new TemporaryDirectory();
        var path = directory.WriteFile("a.md", "old\n");

        // Act: replace it
        AtomicFileWriter.Write(path, Bytes("old\n"), Bytes("new\n"));

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
            AtomicFileWriter.Write(path, original, content, () => throw new IOException("simulated crash")));

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
        Assert.Throws<IOException>(() => AtomicFileWriter.Write(path, read, content));

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
            Assert.Throws<UnauthorizedAccessException>(() => AtomicFileWriter.Write(path, original, content));

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
        Assert.Throws<FileNotFoundException>(() => AtomicFileWriter.Write(path, original, content));
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
        Assert.ThrowsAny<IOException>(() => AtomicFileWriter.Write(path, original, content));
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
        AtomicFileWriter.Write(path, Bytes("old\n"), Bytes("new\n"));

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
        AtomicFileWriter.Write(link, Bytes("old\n"), Bytes("new\n"));

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
        AtomicFileWriter.Write(path, Bytes("old\n"), Bytes("new\n"));

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
        AtomicFileWriter.Write(path, Bytes("old\n"), []);

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
        Assert.Throws<ArgumentNullException>(() => AtomicFileWriter.Write(null!, original, content));
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
        Assert.Throws<ArgumentException>(() => AtomicFileWriter.Write(path, original, content));
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
        AtomicFileWriter.Write(path, Bytes("old\n"), content);

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
        AtomicFileWriter.Write(path, Bytes("old\n"), content);

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
        AtomicFileWriter.Write(path, Bytes("one\n"), Bytes("two\n"));

        // Act: replace it again, expecting what the first write produced
        AtomicFileWriter.Write(path, Bytes("two\n"), Bytes("three\n"));

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
        Assert.Throws<IOException>(() => AtomicFileWriter.Write(path, expectedBytes, content));

        // Assert: the file is untouched
        Assert.Equal(Bytes("abc\n"), File.ReadAllBytes(path));
    }
}
