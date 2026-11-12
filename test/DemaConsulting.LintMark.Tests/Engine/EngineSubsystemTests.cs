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

using System.Text.RegularExpressions;
using DemaConsulting.LintMark.Engine;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Subsystem tests for the Engine subsystem: file discovery, parallel processing and failure
///     reporting working together through the real document units.
/// </summary>
[Collection("Sequential")]
public class EngineSubsystemTests
{
    /// <summary>
    ///     The format every reported failure must have.
    /// </summary>
    private static readonly Regex FailureFormat = new(
        @"^[^:]+:\d+:\d+: [a-z]+(?:-[a-z]+)+ .+$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));

    /// <summary>
    ///     Test that a directory of valid Markdown is discovered, parsed and reported clean.
    /// </summary>
    [Fact]
    public void EngineSubsystem_DiscoveryAndParse_DirectoryWithMarkdown_ReportsNothing()
    {
        // Arrange: valid Markdown in nested directories plus ignored files
        using var directory = new TemporaryDirectory();
        directory.WriteFile("README.md");
        directory.WriteFile("docs/a.md", "# A\n\n> quote\n\n1. one\n2. two\n\n| a | b |\n|---|---|\n| 1 | 2 |\n");
        directory.WriteFile("node_modules/x/bad.md");
        directory.WriteBytes("node_modules/x/binary.md", [0xFF, 0xFE, 0x00]);
        directory.WriteFile("notes.txt", "ignored");

        // Act: run the engine on the directory
        var (exitCode, lines) = LintMarkEngineTests.Execute(directory, new LintMarkEngine(), ".");

        // Assert: clean (the invalid file in node_modules is not even read)
        Assert.Equal(0, exitCode);
        Assert.Empty(lines);
    }

    /// <summary>
    ///     Test that failures from several files are reported with position, rule and message.
    /// </summary>
    [Fact]
    public void EngineSubsystem_ParseFailure_ReportedWithPositionAndRule()
    {
        // Arrange: valid and invalid files
        using var directory = new TemporaryDirectory();
        directory.WriteFile("good.md");
        directory.WriteBytes("bad/utf8.md", [.. "# T\n\nxy"u8, 0xC3, 0x28, .. "\n"u8]);
        directory.WriteBytes("bad/cr.md", "a\r\nb\rc\n"u8.ToArray());

        // Act: run on the default pattern
        var (exitCode, lines) = LintMarkEngineTests.Execute(directory, new LintMarkEngine());

        // Assert: failures in path order, each in the documented format, exit code one
        Assert.Equal(1, exitCode);
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("bad/cr.md:2:2: file-line-ending-error ", lines[0], StringComparison.Ordinal);
        Assert.StartsWith("bad/utf8.md:3:3: file-encoding-error ", lines[1], StringComparison.Ordinal);
        Assert.All(lines, l => Assert.Matches(FailureFormat, l));
    }

    /// <summary>
    ///     Test that sequential and parallel runs of the real engine produce identical output and exit code.
    /// </summary>
    [Fact]
    public void EngineSubsystem_ParallelAndSequential_ProduceIdenticalOutput()
    {
        // Arrange: many files in several directories, some failing
        using var directory = new TemporaryDirectory();
        for (var i = 0; i < 150; i++)
        {
            var name = $"dir{i % 9}/f{i:D3}.md";
            if (i % 13 == 0)
            {
                directory.WriteBytes(name, [(byte)'a', 0xFF]);
            }
            else if (i % 17 == 0)
            {
                directory.WriteBytes(name, "a\rb"u8.ToArray());
            }
            else
            {
                directory.WriteFile(name, $"# F{i}\n\ntext\n");
            }
        }

        // Act: run with one worker and with many
        var sequential = LintMarkEngineTests.Execute(directory, new LintMarkEngine(1));
        var parallel = LintMarkEngineTests.Execute(directory, new LintMarkEngine(8));

        // Assert: identical lines (same order) and exit code
        Assert.Equal(1, sequential.ExitCode);
        Assert.True(sequential.Lines.Length > 10);
        Assert.Equal(sequential.ExitCode, parallel.ExitCode);
        Assert.Equal(sequential.Lines, parallel.Lines);
        Assert.Equal(sequential.Lines.Order(StringComparer.Ordinal).ToArray(), sequential.Lines);
    }

    /// <summary>
    ///     Test that with no arguments the current directory is searched for Markdown files.
    /// </summary>
    [Fact]
    public void EngineSubsystem_DefaultPattern_UsesCurrentDirectory()
    {
        // Arrange: a failing file in the current directory tree
        using var directory = new TemporaryDirectory();
        directory.WriteBytes("sub/bad.md", [0xFF]);

        // Act: run without path arguments
        var (exitCode, lines) = LintMarkEngineTests.Execute(directory, new LintMarkEngine());

        // Assert: the file under the current directory was found and reported
        Assert.Equal(1, exitCode);
        Assert.Equal(["sub/bad.md:1:1: file-encoding-error Invalid UTF-8 byte sequence"], lines);
    }

    /// <summary>
    ///     Test that exclude arguments keep files from being processed.
    /// </summary>
    [Fact]
    public void EngineSubsystem_ExcludePattern_SkipsExcludedFiles()
    {
        // Arrange: a failing file in a directory that will be excluded
        using var directory = new TemporaryDirectory();
        directory.WriteFile("ok.md");
        directory.WriteBytes("generated/bad.md", [0xFF]);

        // Act: run with an exclude
        var (exitCode, lines) = LintMarkEngineTests.Execute(directory, new LintMarkEngine(), "**/*.md", "!generated/**");

        // Assert: the excluded file is never read
        Assert.Equal(0, exitCode);
        Assert.Empty(lines);
    }

    /// <summary>
    ///     Test that a failing file inside a named third-party folder is never read.
    /// </summary>
    [Fact]
    public void EngineSubsystem_ThirdPartyFolderNamed_IsNotProcessed()
    {
        // Arrange: undecodable files inside node_modules and .git
        using var directory = new TemporaryDirectory();
        directory.WriteFile("ok.md");
        directory.WriteBytes("node_modules/pkg/bad.md", [0xFF]);
        directory.WriteBytes(".git/bad.md", [0xFF]);

        // Act: name the folders and a glob inside one of them
        var nodeModules = LintMarkEngineTests.Execute(directory, new LintMarkEngine(), "node_modules");
        var git = LintMarkEngineTests.Execute(directory, new LintMarkEngine(), ".git");
        var glob = LintMarkEngineTests.Execute(directory, new LintMarkEngine(), "node_modules/**/*.md");

        // Assert: nothing is read, so nothing is reported
        Assert.Equal(0, nodeModules.ExitCode);
        Assert.Empty(nodeModules.Lines);
        Assert.Equal(0, git.ExitCode);
        Assert.Empty(git.Lines);
        Assert.Equal(0, glob.ExitCode);
        Assert.Empty(glob.Lines);
    }

    /// <summary>
    ///     Test that a file inside a third-party folder is processed when it is named explicitly.
    /// </summary>
    [Fact]
    public void EngineSubsystem_ExplicitFileInThirdPartyFolder_IsProcessed()
    {
        // Arrange: an undecodable file inside node_modules
        using var directory = new TemporaryDirectory();
        directory.WriteBytes("node_modules/pkg/bad.md", [0xFF]);

        // Act: name the file explicitly
        var (exitCode, lines) = LintMarkEngineTests.Execute(directory, new LintMarkEngine(), "node_modules/pkg/bad.md");

        // Assert: it is read and its failure is reported
        Assert.Equal(1, exitCode);
        Assert.Equal(["node_modules/pkg/bad.md:1:1: file-encoding-error Invalid UTF-8 byte sequence"], lines);
    }

    /// <summary>
    ///     Test that a file selected by several arguments is processed and reported once.
    /// </summary>
    [Fact]
    public void EngineSubsystem_OverlappingArguments_ReportEachFailureOnce()
    {
        // Arrange: one failing file
        using var directory = new TemporaryDirectory();
        directory.WriteBytes("docs/bad.md", [0xFF]);

        // Act: select it by file, directory and glob
        var (exitCode, lines) = LintMarkEngineTests.Execute(directory, new LintMarkEngine(), "docs/bad.md", "docs", "**/*.md");

        // Assert: reported exactly once
        Assert.Equal(1, exitCode);
        Assert.Single(lines);
    }
}
