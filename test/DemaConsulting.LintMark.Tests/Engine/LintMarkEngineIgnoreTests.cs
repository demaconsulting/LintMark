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

using DemaConsulting.LintMark.Configuration;
using DemaConsulting.LintMark.Engine;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Unit tests for the ignore pattern behavior of the LintMarkEngine class.
/// </summary>
public partial class LintMarkEngineTests
{
    /// <summary>
    ///     Writes a file that cannot be decoded, so that reading it always produces a report.
    /// </summary>
    /// <param name="directory">The temporary directory.</param>
    /// <param name="name">The relative path of the file.</param>
    /// <returns>The full path.</returns>
    private static string WriteBroken(TemporaryDirectory directory, string name) => directory.WriteBytes(name, [0xFF]);

    /// <summary>
    ///     Test that an ignored file is neither read nor reported.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Process_IgnoredFile_NotReadAndMarkedIgnored()
    {
        // Arrange: a file below a folder that the default patterns ignore, with content that would be reported
        using var directory = new TemporaryDirectory();
        var path = WriteBroken(directory, "node_modules/pkg/a.md");

        // Act: process it as a file that was found, not named
        var result = Assert.Single(new LintMarkEngine(1).Process(
            [new DiscoveredFile(path, IsExplicit: false)], ConfigLoader.ForDefaults(), null, directory.FullPath));

        // Assert: skipped without a report
        Assert.True(result.Ignored);
        Assert.Empty(result.Diagnostics);
        Assert.Null(result.Change);
    }

    /// <summary>
    ///     Test that a file named on the command line is processed even when an ignore pattern matches it.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Process_ExplicitFileMatchingIgnore_IsProcessed()
    {
        // Arrange: the same file as above, but named explicitly
        using var directory = new TemporaryDirectory();
        var path = WriteBroken(directory, "node_modules/pkg/a.md");

        // Act: process it as an explicit file
        var result = Assert.Single(new LintMarkEngine(1).Process(
            [new DiscoveredFile(path, IsExplicit: true)], ConfigLoader.ForDefaults(), null, directory.FullPath));

        // Assert: read and reported
        Assert.False(result.Ignored);
        Assert.Equal("file-encoding-error", Assert.Single(result.Diagnostics).RuleId);
    }

    /// <summary>
    ///     Test that a file whose configuration is invalid is processed (its ignore patterns are unknown) but never formatted.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Process_InvalidConfigIgnoreUnknown_FileProcessedNotFormatted()
    {
        // Arrange: an invalid configuration, and a file in a folder that the default patterns would ignore
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "bogus: 1\n");
        var path = WriteBroken(directory, "generated/a.md");

        // Act: process it as a file that was found
        var result = Assert.Single(new LintMarkEngine(1).Process(
            [new DiscoveredFile(path, IsExplicit: false)], ConfigLoader.ForDiscovery(), null, directory.FullPath));

        // Assert: the file was read and reported, the configuration problem is carried, and it is not ignored
        Assert.False(result.Ignored);
        Assert.Null(result.Config);
        Assert.Equal("file-encoding-error", Assert.Single(result.Diagnostics).RuleId);
        Assert.Single(result.ConfigErrors);
    }

    /// <summary>
    ///     Test that the files of a glob that an ignore pattern matches are skipped.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_IgnoreGlobSelected_SkipsMatches()
    {
        // Arrange: a configuration that ignores a folder, a broken file inside it and a clean file outside
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "ignore:\n  - \"drafts/**\"\n");
        WriteBroken(directory, "drafts/bad.md");
        directory.WriteFile("ok.md");

        // Act: run with a glob
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(2), "**/*.md");

        // Assert: the ignored file is not reported
        Assert.Equal(0, exitCode);
        Assert.Empty(lines);
    }

    /// <summary>
    ///     Test that each directory's own configuration decides which files are ignored, relative to that configuration.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_IgnoreNearestConfig_EachDirectoryUsesItsOwn()
    {
        // Arrange: a root configuration and a nested one with different patterns, and a broken file in every folder
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "ignore:\n  - \"only-root/**\"\n");
        directory.WriteFile("sub/.lintmark.yaml", "ignore:\n  - \"only-sub/**\"\n");
        WriteBroken(directory, "only-root/bad.md");
        WriteBroken(directory, "only-sub/bad.md");
        WriteBroken(directory, "sub/only-root/bad.md");
        WriteBroken(directory, "sub/only-sub/bad.md");

        // Act: run with the default pattern
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(4));

        // Assert: each file is judged by the configuration that applies to it
        Assert.Equal(1, exitCode);
        Assert.Equal(
            [
                "only-sub/bad.md:1:1: file-encoding-error Invalid UTF-8 byte sequence",
                "sub/only-root/bad.md:1:1: file-encoding-error Invalid UTF-8 byte sequence"
            ],
            lines);
    }

    /// <summary>
    ///     Test that the patterns of an explicit configuration are relative to the folder of that configuration.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_IgnoreWithExplicitConfig_RelativeToConfigDirectory()
    {
        // Arrange: a configuration in a sub folder that ignores "x/**", with a broken file below it and one below the current folder
        using var directory = new TemporaryDirectory();
        directory.WriteFile("conf/custom.yaml", "ignore:\n  - \"x/**\"\n");
        WriteBroken(directory, "conf/x/bad.md");
        WriteBroken(directory, "x/bad.md");

        // Act: run with the explicit configuration
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(2), "--config", "conf/custom.yaml");

        // Assert: the file below the configuration's folder is ignored; the other one is reported
        Assert.Equal(1, exitCode);
        Assert.Equal(["x/bad.md:1:1: file-encoding-error Invalid UTF-8 byte sequence"], lines);
    }

    /// <summary>
    ///     Test that a file with an invalid configuration is processed even if it would be ignored, and never rewritten.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_IgnoreInvalidConfig_ProcessedButNeverFormatted()
    {
        // Arrange: an invalid configuration, a broken file in a generated folder, and a mixed-ending file there
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "bogus: 1\n");
        WriteBroken(directory, "generated/bad.md");
        var (mixed, writeTime) = WriteOld(directory, "generated/mixed.md", Utf8("a\r\nb\nc\r\n"));

        // Act: run with the glob and the fix option
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(1), "**/*.md", "--fix");

        // Assert: the configuration problem and the read problem are reported; nothing is rewritten
        Assert.Equal(1, exitCode);
        Assert.Equal(
            [
                ".lintmark.yaml:1:1: config-error Unknown setting 'bogus'",
                "generated/bad.md:1:1: file-encoding-error Invalid UTF-8 byte sequence"
            ],
            lines);
        AssertUntouched(mixed, Utf8("a\r\nb\nc\r\n"), writeTime);
    }
}
