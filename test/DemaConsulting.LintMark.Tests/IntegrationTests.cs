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

using DemaConsulting.LintMark.Utilities;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     System-level integration tests that run the LintMark application via dotnet.
/// </summary>
[Collection("Sequential")]
public class IntegrationTests
{
    private readonly string _dllPath;

    /// <summary>
    ///     Initialize test by locating the LintMark DLL.
    /// </summary>
    public IntegrationTests()
    {
        // The DLL should be in the same directory as the test assembly
        // because the test project references the main project
        var baseDir = AppContext.BaseDirectory;
        _dllPath = PathHelpers.SafePathCombine(baseDir, "DemaConsulting.LintMark.dll");

        Assert.True(File.Exists(_dllPath), $"Could not find LintMark DLL at {_dllPath}");
    }

    /// <summary>
    ///     Test that version flag outputs version information.
    /// </summary>
    [Fact]
    public void LintMark_VersionFlag_Provided_OutputsVersion()
    {
        // Arrange: (none — constructor initializes _dllPath)

        // Act: run the tool with version flag
        var exitCode = Runner.Run(
            out var output,
            "dotnet",
            _dllPath,
            "--version");

        // Assert: version string is printed; no banner or errors
        Assert.Equal(0, exitCode);
        Assert.Matches(@"\d+\.\d+\.\d+", output);
        Assert.DoesNotContain("Error", output);
        Assert.DoesNotContain("Copyright", output);
    }

    /// <summary>
    ///     Test that help flag outputs usage information.
    /// </summary>
    [Fact]
    public void LintMark_HelpFlag_Provided_OutputsUsageInformation()
    {
        // Arrange: (none — constructor initializes _dllPath)

        // Act: run the tool with help flag
        var exitCode = Runner.Run(
            out var output,
            "dotnet",
            _dllPath,
            "--help");

        // Assert: usage text contains required sections and key flags
        Assert.Equal(0, exitCode);
        Assert.Contains("Usage:", output);
        Assert.Contains("Options:", output);
        Assert.Contains("--version", output);
        Assert.Contains("--help", output);
    }

    /// <summary>
    ///     Test that no arguments displays the tool banner and runs default logic.
    /// </summary>
    [Fact]
    public void LintMark_NoArguments_Invoked_DisplaysBanner()
    {
        // Arrange: (none — constructor initializes _dllPath)

        // Act: run the tool with no arguments
        var exitCode = Runner.Run(
            out var output,
            "dotnet",
            _dllPath);

        // Assert: banner is displayed with tool name and copyright; exit code is success
        Assert.Equal(0, exitCode);
        Assert.Contains("LintMark version", output);
        Assert.Contains("Copyright", output);
    }

    /// <summary>
    ///     Test that validate flag runs self-validation and outputs summary.
    /// </summary>
    [Fact]
    public void LintMark_ValidateFlag_Provided_RunsValidation()
    {
        // Arrange: (none — constructor initializes _dllPath)

        // Act: run the tool with validate flag
        var exitCode = Runner.Run(
            out var output,
            "dotnet",
            _dllPath,
            "--validate");

        // Assert: validation summary is present; exit code is success
        Assert.Equal(0, exitCode);
        Assert.Contains("Total Tests:", output);
        Assert.Contains("Passed:", output);
    }

    /// <summary>
    ///     Test that validate with --results flag generates a TRX file.
    /// </summary>
    [Fact]
    public void LintMark_ValidateWithTrxResults_Requested_GeneratesTrxFile()
    {
        // Arrange: temporary TRX results file path
        var resultsFile = Path.Combine(Path.GetTempPath(), $"integration_test_{Guid.NewGuid()}.trx");

        try
        {
            // Act: run validation with TRX results output
            var exitCode = Runner.Run(
                out var _,
                "dotnet",
                _dllPath,
                "--validate",
                "--results",
                resultsFile);

            // Assert: results file is created with valid TRX structure
            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(resultsFile), "Results file was not created");

            var trxContent = File.ReadAllText(resultsFile);
            Assert.Contains("<TestRun", trxContent);
            Assert.Contains("</TestRun>", trxContent);
        }
        finally
        {
            if (File.Exists(resultsFile))
            {
                File.Delete(resultsFile);
            }
        }
    }

    /// <summary>
    ///     Test that validate with --result (legacy alias) flag generates a results file.
    /// </summary>
    [Fact]
    public void LintMark_ResultAlias_LegacyFlag_WritesResultsFile()
    {
        // Arrange: temporary TRX results file path; use legacy --result alias
        var resultsFile = Path.Combine(Path.GetTempPath(), $"integration_test_{Guid.NewGuid()}.trx");

        try
        {
            // Act: run validation with legacy --result alias
            var exitCode = Runner.Run(
                out var _,
                "dotnet",
                _dllPath,
                "--validate",
                "--result",
                resultsFile);

            // Assert: results file is created; exit code is success
            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(resultsFile), "Results file was not created for legacy --result alias");
        }
        finally
        {
            if (File.Exists(resultsFile))
            {
                File.Delete(resultsFile);
            }
        }
    }

    /// <summary>
    ///     Test that validate with an unsupported results extension returns a non-zero exit code.
    /// </summary>
    [Fact]
    public void LintMark_ValidateWithBadExtension_ExtensionInvalid_ReturnsNonZero()
    {
        // Arrange: unsupported results file extension triggers WriteError → ExitCode 1
        var resultsFile = Path.Combine(Path.GetTempPath(), $"integration_test_{Guid.NewGuid()}.bad");

        try
        {
            // Act: run validation with unsupported extension
            var exitCode = Runner.Run(
                out var _,
                "dotnet",
                _dllPath,
                "--validate",
                "--results",
                resultsFile);

            // Assert: non-zero exit code indicates the error path was triggered
            Assert.NotEqual(0, exitCode);
            Assert.False(File.Exists(resultsFile), "No file should be created for unsupported extension");
        }
        finally
        {
            if (File.Exists(resultsFile))
            {
                File.Delete(resultsFile);
            }
        }
    }

    /// <summary>
    ///     Test that silent flag suppresses output.
    /// </summary>
    [Fact]
    public void LintMark_SilentFlag_Provided_SuppressesOutput()
    {
        // Arrange: (none — constructor initializes _dllPath)

        // Act: run the tool with --version and --silent to produce deterministic silent output
        var exitCode = Runner.Run(
            out var output,
            "dotnet",
            _dllPath,
            "--version",
            "--silent");

        // Assert: no console output in silent mode
        Assert.Equal(0, exitCode);
        Assert.True(string.IsNullOrWhiteSpace(output), $"Expected no output in silent mode but got: {output}");
    }

    /// <summary>
    ///     Test that log flag writes output to a file.
    /// </summary>
    [Fact]
    public void LintMark_LogFlag_Provided_WritesOutputToFile()
    {
        // Arrange: temporary log file path
        var logFile = Path.GetTempFileName();

        try
        {
            // Act: run the tool with log flag
            var exitCode = Runner.Run(
                out var output,
                "dotnet",
                _dllPath,
                "--log",
                logFile);

            // Assert: log file is created and contains tool output; console output matches
            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(logFile), "Log file was not created");

            var logContent = File.ReadAllText(logFile);
            Assert.Contains("LintMark version", logContent);
            Assert.Contains("LintMark version", output);
        }
        finally
        {
            if (File.Exists(logFile))
            {
                File.Delete(logFile);
            }
        }
    }

    /// <summary>
    ///     Test that validate with --results flag generates a JUnit XML file.
    /// </summary>
    [Fact]
    public void LintMark_ValidateWithXmlResults_Requested_GeneratesJUnitFile()
    {
        // Arrange: temporary XML results file path
        var resultsFile = Path.Combine(Path.GetTempPath(), $"integration_test_{Guid.NewGuid()}.xml");

        try
        {
            // Act: run validation with JUnit XML results output
            var exitCode = Runner.Run(
                out var _,
                "dotnet",
                _dllPath,
                "--validate",
                "--results",
                resultsFile);

            // Assert: results file is created with valid JUnit structure
            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(resultsFile), "Results file was not created");

            var xmlContent = File.ReadAllText(resultsFile);
            Assert.Contains("<testsuites", xmlContent);
        }
        finally
        {
            if (File.Exists(resultsFile))
            {
                File.Delete(resultsFile);
            }
        }
    }

    /// <summary>
    ///     Test that an unknown argument causes an error message and non-zero exit code.
    /// </summary>
    [Fact]
    public void LintMark_UnknownArgument_Provided_ReturnsError()
    {
        // Arrange: (none — constructor initializes _dllPath)

        // Act: run the tool with an unknown argument
        var exitCode = Runner.Run(
            out var output,
            "dotnet",
            _dllPath,
            "--unknown");

        // Assert: non-zero exit code and error message naming the unrecognized flag
        Assert.NotEqual(0, exitCode);
        Assert.Contains("Error", output);
        Assert.Contains("--unknown", output);
    }

    /// <summary>
    ///     Test that validate with depth flag outputs headings at the specified depth.
    /// </summary>
    [Fact]
    public void LintMark_ValidateWithDepth_DepthThree_OutputsCorrectHeadingLevel()
    {
        // Arrange: (none — constructor initializes _dllPath)

        // Act: run validation with heading depth 3
        var exitCode = Runner.Run(
            out var output,
            "dotnet",
            _dllPath,
            "--validate",
            "--depth",
            "3");

        // Assert: output contains level-3 markdown headings
        Assert.Equal(0, exitCode);
        Assert.Contains("###", output);
    }

    /// <summary>
    ///     Test that help lists the paths argument and the check option.
    /// </summary>
    [Fact]
    public void LintMark_HelpFlag_Provided_ListsPathsAndCheckOption()
    {
        // Arrange: (none — constructor initializes _dllPath)

        // Act: run the tool with help flag
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--help");

        // Assert: usage mentions paths and --check
        Assert.Equal(0, exitCode);
        Assert.Contains("[paths...]", output);
        Assert.Contains("--check", output);
    }

    /// <summary>
    ///     Test that a valid Markdown file given as a path is processed successfully.
    /// </summary>
    [Fact]
    public void LintMark_PathsProvided_ValidMarkdown_ReturnsSuccess()
    {
        // Arrange: a valid Markdown file
        using var directory = new TemporaryDirectory();
        var file = directory.WriteFile("ok.md", "# Title\n\nSome text.\n\n- a\n- b\n");

        // Act: run the tool with the file
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, file);

        // Assert: success and no diagnostics
        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("file-", output);
    }

    /// <summary>
    ///     Test that an undecodable file is reported as a diagnostic and gives a non-zero exit code.
    /// </summary>
    [Fact]
    public void LintMark_PathsProvided_UnreadableFile_ReportsDiagnosticAndReturnsError()
    {
        // Arrange: a file with an invalid UTF-8 byte on its second line
        using var directory = new TemporaryDirectory();
        var file = directory.WriteBytes("bad.md", [.. "ok\nab"u8, 0xFF, .. "\n"u8]);

        // Act: run the tool with the file
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, file);

        // Assert: error exit code and a diagnostic with position and rule
        Assert.Equal(1, exitCode);
        Assert.Contains("bad.md:2:3: file-encoding-error Invalid UTF-8 byte sequence", output);
    }

    /// <summary>
    ///     Test that a missing file is reported as a read error.
    /// </summary>
    [Fact]
    public void LintMark_PathsProvided_MissingFile_ReportsReadErrorAndReturnsError()
    {
        // Arrange: a path that does not exist
        using var directory = new TemporaryDirectory();
        var file = directory.PathOf("missing.md");

        // Act: run the tool with the file
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, file);

        // Assert: error exit code and a read-error diagnostic
        Assert.Equal(1, exitCode);
        Assert.Contains("missing.md:1:1: file-read-error File not found", output);
    }

    /// <summary>
    ///     Test that the check flag lints without modifying the file.
    /// </summary>
    [Fact]
    public void LintMark_CheckFlag_Provided_LintsWithoutWriting()
    {
        // Arrange: a valid file with CRLF endings and a missing final newline
        using var directory = new TemporaryDirectory();
        var file = directory.WriteBytes("ok.md", "# Title\r\n\r\ntext"u8.ToArray());
        var before = File.ReadAllBytes(file);
        var timestamp = File.GetLastWriteTimeUtc(file);

        // Act: run the tool with --check
        var exitCode = Runner.Run(out _, "dotnet", _dllPath, "--check", file);

        // Assert: success and the file is byte-identical with the same timestamp
        Assert.Equal(0, exitCode);
        Assert.Equal(before, File.ReadAllBytes(file));
        Assert.Equal(timestamp, File.GetLastWriteTimeUtc(file));
    }

    /// <summary>
    ///     Test that a directory argument processes all Markdown files beneath it.
    /// </summary>
    [Fact]
    public void LintMark_DirectoryProvided_ParsesAllMarkdownBeneathIt()
    {
        // Arrange: a valid file and a nested invalid file, plus an invalid non-Markdown file
        using var directory = new TemporaryDirectory();
        directory.WriteFile("docs/ok.md");
        directory.WriteBytes("docs/deep/bad.md", [0xFF]);
        directory.WriteBytes("docs/ignored.txt", [0xFF]);

        // Act: run the tool with the directory
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, directory.PathOf("docs"));

        // Assert: only the nested Markdown file is reported
        Assert.Equal(1, exitCode);
        Assert.Contains("bad.md:1:1: file-encoding-error", output);
        Assert.DoesNotContain("ignored.txt", output);
        Assert.DoesNotContain("ok.md", output);
    }

    /// <summary>
    ///     Test that globs and excludes select the files that are processed.
    /// </summary>
    [Fact]
    public void LintMark_GlobAndExclude_Provided_SkipsExcludedFiles()
    {
        // Arrange: an invalid file below an excluded directory
        using var directory = new TemporaryDirectory();
        directory.WriteFile("docs/ok.md");
        directory.WriteBytes("docs/generated/bad.md", [0xFF]);
        var root = directory.FullPath.Replace('\\', '/');

        // Act: run the tool with an absolute glob and an absolute exclude
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, root + "/docs/**/*.md", "!" + root + "/docs/generated/**");

        // Assert: the excluded file is never read
        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("bad.md", output);
    }

    /// <summary>
    ///     Test that failures from several files are reported in path order.
    /// </summary>
    [Fact]
    public void LintMark_MultipleFiles_Provided_ReportsFailuresInPathOrder()
    {
        // Arrange: three failing files passed in non-sorted order
        using var directory = new TemporaryDirectory();
        var c = directory.WriteBytes("c.md", [0xFF]);
        var a = directory.WriteBytes("a.md", [0xFF]);
        var b = directory.WriteBytes("b.md", [0xFF]);

        // Act: run the tool
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, c, a, b);

        // Assert: diagnostics appear in path order regardless of argument order
        Assert.Equal(1, exitCode);
        var indexA = output.IndexOf("a.md:1:1", StringComparison.Ordinal);
        var indexB = output.IndexOf("b.md:1:1", StringComparison.Ordinal);
        var indexC = output.IndexOf("c.md:1:1", StringComparison.Ordinal);
        Assert.True(indexA >= 0 && indexA < indexB && indexB < indexC, output);
    }

    /// <summary>
    ///     Test that naming a third-party folder, or a pattern inside it, skips its files.
    /// </summary>
    [Fact]
    public void LintMark_ThirdPartyFolderProvided_SkipsItsFiles()
    {
        // Arrange: undecodable files inside node_modules and .git
        using var directory = new TemporaryDirectory();
        directory.WriteBytes("node_modules/pkg/bad.md", [0xFF]);
        directory.WriteBytes(".git/bad.md", [0xFF]);
        var root = directory.FullPath.Replace('\\', '/');

        // Act: run the tool with each folder and with a glob inside one of them
        var nodeModulesExit = Runner.Run(out var nodeModules, "dotnet", _dllPath, directory.PathOf("node_modules"));
        var gitExit = Runner.Run(out var git, "dotnet", _dllPath, directory.PathOf(".git"));
        var globExit = Runner.Run(out var glob, "dotnet", _dllPath, root + "/node_modules/**/*.md");

        // Assert: nothing is read, so nothing is reported
        Assert.Equal(0, nodeModulesExit);
        Assert.DoesNotContain("bad.md", nodeModules);
        Assert.Equal(0, gitExit);
        Assert.DoesNotContain("bad.md", git);
        Assert.Equal(0, globExit);
        Assert.DoesNotContain("bad.md", glob);
    }

    /// <summary>
    ///     Test that a file inside a third-party folder is processed when it is named explicitly.
    /// </summary>
    [Fact]
    public void LintMark_ExplicitFileInThirdPartyFolder_IsProcessed()
    {
        // Arrange: an undecodable file inside node_modules
        using var directory = new TemporaryDirectory();
        var file = directory.WriteBytes("node_modules/pkg/bad.md", [0xFF]);

        // Act: run the tool with the file itself
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, file);

        // Assert: the file is read and its failure reported
        Assert.Equal(1, exitCode);
        Assert.Contains("bad.md:1:1: file-encoding-error", output);
    }

    /// <summary>
    ///     Test that a file containing suppression comments, reference definitions and every extension parses successfully.
    /// </summary>
    [Fact]
    public void LintMark_PathsWithSuppressionAndReferences_Provided_ReturnsSuccess()
    {
        // Arrange: a document with directives, definitions, footnotes, a table, front matter and an unclosed fence
        using var directory = new TemporaryDirectory();
        var file = directory.WriteFile(
            "all.md",
            "---\ntitle: x\n---\n\n# Title\n\n<!-- lintmark-disable line-length -->\n\nSee [ref] and a footnote[^1].\n\n"
            + "<!-- lintmark-enable line-length -->\n\n| a | b |\n|---|---|\n| 1 | 2 |\n\n[ref]: http://example.com \"T\"\n\n[^1]: Note.\n\n```\nunclosed\n");

        // Act: run the tool with the file
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, file);

        // Assert: success without diagnostics
        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("file-", output);
    }

    /// <summary>
    ///     Test that combining validate with paths is a usage error.
    /// </summary>
    [Fact]
    public void LintMark_ValidateWithPaths_Combined_ReturnsError()
    {
        // Arrange: (none — constructor initializes _dllPath)

        // Act: run the tool with validate and a path
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--validate", "docs");

        // Assert: error exit code and an error message naming the flag; validation did not run
        Assert.Equal(1, exitCode);
        Assert.Contains("Error", output);
        Assert.Contains("--validate", output);
        Assert.DoesNotContain("Total Tests", output);
    }

    /// <summary>
    ///     Test that combining validate with the check flag is a usage error.
    /// </summary>
    [Fact]
    public void LintMark_ValidateWithCheck_Combined_ReturnsError()
    {
        // Arrange: (none — constructor initializes _dllPath)

        // Act: run the tool with validate and check
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--validate", "--check");

        // Assert: error exit code and an error message
        Assert.Equal(1, exitCode);
        Assert.Contains("Error", output);
    }

    /// <summary>
    ///     Test that the tool processes this repository's own documentation successfully.
    /// </summary>
    [Fact]
    public void LintMark_RepositoryDocumentation_Provided_ReturnsSuccess()
    {
        // Arrange: the repository's docs directory and README
        var root = RepositoryMarkdownFiles.FindRoot();

        // Act: run the tool with the docs directory and the README
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, Path.Combine(root, "docs"), Path.Combine(root, "README.md"));

        // Assert: every file reads and parses
        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("file-", output);
    }
}

