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
    ///     Test that combining validate with the config option is a usage error.
    /// </summary>
    [Fact]
    public void LintMark_ValidateWithConfig_Combined_ReturnsError()
    {
        // Arrange: (none — constructor initializes _dllPath)

        // Act: run the tool with validate and a configuration file
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--validate", "--config", "a.yaml");

        // Assert: error exit code and a message naming the option; validation did not run
        Assert.Equal(1, exitCode);
        Assert.Contains("Error", output);
        Assert.Contains("--config", output);
        Assert.DoesNotContain("Total Tests", output);
    }

    /// <summary>
    ///     Test that combining validate with the no-config flag is a usage error.
    /// </summary>
    [Fact]
    public void LintMark_ValidateWithNoConfig_Combined_ReturnsError()
    {
        // Arrange: (none — constructor initializes _dllPath)

        // Act: run the tool with validate and no-config
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--validate", "--no-config");

        // Assert: error exit code and a message naming the flag; validation did not run
        Assert.Equal(1, exitCode);
        Assert.Contains("Error", output);
        Assert.Contains("--no-config", output);
        Assert.DoesNotContain("Total Tests", output);
    }

    /// <summary>
    ///     Test that validate combined with fix is an error and validation does not run.
    /// </summary>
    [Fact]
    public void LintMark_ValidateWithFix_Combined_ReturnsError()
    {
        // Arrange: (none — constructor initializes _dllPath)

        // Act: run the tool with validate and fix
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--validate", "--fix");

        // Assert: error exit code and a message naming the flag; validation did not run
        Assert.Equal(1, exitCode);
        Assert.Contains("Error", output);
        Assert.Contains("--fix", output);
        Assert.DoesNotContain("Total Tests", output);
    }

    /// <summary>
    ///     Test that validate combined with diff is an error and validation does not run.
    /// </summary>
    [Fact]
    public void LintMark_ValidateWithDiff_Combined_ReturnsError()
    {
        // Arrange: (none — constructor initializes _dllPath)

        // Act: run the tool with validate and diff
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--validate", "--diff");

        // Assert: error exit code and a message naming the flag; validation did not run
        Assert.Equal(1, exitCode);
        Assert.Contains("Error", output);
        Assert.Contains("--diff", output);
        Assert.DoesNotContain("Total Tests", output);
    }

    /// <summary>
    ///     Test that help lists the configuration options.
    /// </summary>
    [Fact]
    public void LintMark_HelpFlag_Provided_ListsConfigOptions()
    {
        // Arrange: (none — constructor initializes _dllPath)

        // Act: run the tool with help flag
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--help");

        // Assert: usage mentions both configuration options
        Assert.Equal(0, exitCode);
        Assert.Contains("--config <file>", output);
        Assert.Contains("--no-config", output);
    }

    /// <summary>
    ///     Test that combining the config option with the no-config flag is a usage error.
    /// </summary>
    [Fact]
    public void LintMark_ConfigAndNoConfig_Combined_ReturnsError()
    {
        // Arrange: (none — constructor initializes _dllPath)

        // Act: run the tool with both options
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--config", "a.yaml", "--no-config");

        // Assert: error exit code and a message naming both options
        Assert.Equal(1, exitCode);
        Assert.Contains("Error", output);
        Assert.Contains("--config", output);
        Assert.Contains("--no-config", output);
    }

    /// <summary>
    ///     Test that the nearest configuration is used and its problems are reported with their position.
    /// </summary>
    [Fact]
    public void LintMark_ConfigDiscovery_NearestConfigInvalid_ReportsItsErrors()
    {
        // Arrange: a valid configuration in the root and an invalid one nearer the file
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "line-length: 100\n");
        directory.WriteFile("docs/.lintmark.yaml", "line-length: wide\n");
        var file = directory.WriteFile("docs/a.md");

        // Act: run the tool with the file
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, file);

        // Assert: the nearer file's problem is reported
        Assert.Equal(1, exitCode);
        Assert.Contains("docs/.lintmark.yaml:1:14: config-error Invalid value 'wide' for setting 'line-length'", output);
    }

    /// <summary>
    ///     Test that a farther invalid configuration is never read when a nearer valid one exists.
    /// </summary>
    [Fact]
    public void LintMark_ConfigDiscovery_FartherConfigInvalidNearestValid_ReturnsSuccess()
    {
        // Arrange: an invalid configuration in the root and a valid one nearer the file
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "bogus: 1\n");
        directory.WriteFile("docs/.lintmark.yaml", "line-length: 90\n");
        var file = directory.WriteFile("docs/a.md");

        // Act: run the tool with the file
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, file);

        // Assert: success without a report
        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("config-error", output);
    }

    /// <summary>
    ///     Test that a configuration above the repository root is not used.
    /// </summary>
    [Fact]
    public void LintMark_ConfigDiscovery_ConfigAboveRepositoryRoot_IsNotUsed()
    {
        // Arrange: an invalid configuration above a folder that holds a .git directory
        using var directory = new TemporaryDirectory(repositoryRoot: false);
        directory.WriteFile(".lintmark.yaml", "bogus: 1\n");
        Directory.CreateDirectory(directory.PathOf("repo/.git"));
        var file = directory.WriteFile("repo/docs/a.md");

        // Act: run the tool with the file inside the repository
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, file);

        // Assert: the file above the repository is not used
        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("config-error", output);
    }

    /// <summary>
    ///     Test that the config option uses the named file instead of discovery.
    /// </summary>
    [Fact]
    public void LintMark_ConfigOption_FileProvided_UsesItInsteadOfDiscovery()
    {
        // Arrange: an invalid discoverable file and a valid explicit file
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "bogus: 1\n");
        var config = directory.WriteFile("custom/settings.yaml", "line-length: 100\n");
        var file = directory.WriteFile("a.md");

        // Act: run the tool with the explicit file
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--config", config, file);

        // Assert: the discoverable file is not used
        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("config-error", output);
    }

    /// <summary>
    ///     Test that a missing explicit configuration is reported and gives a non-zero exit code.
    /// </summary>
    [Fact]
    public void LintMark_ConfigOption_MissingFile_ReportsErrorAndReturnsError()
    {
        // Arrange: a Markdown file and a configuration path that does not exist
        using var directory = new TemporaryDirectory();
        var file = directory.WriteFile("a.md");
        var missing = directory.PathOf("missing.yaml");

        // Act: run the tool with the missing file
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--config", missing, file);

        // Assert: reported at 1:1
        Assert.Equal(1, exitCode);
        Assert.Contains("missing.yaml:1:1: config-error Configuration file not found", output);
    }

    /// <summary>
    ///     Test that a missing explicit configuration is reported even when no Markdown file is selected.
    /// </summary>
    [Fact]
    public void LintMark_ConfigOption_MissingFileNoMarkdownFiles_ReportsErrorAndReturnsError()
    {
        // Arrange: a directory without Markdown files and a configuration path that does not exist
        using var directory = new TemporaryDirectory();
        var missing = directory.PathOf("missing.yaml");

        // Act: run the tool on the empty directory with the missing file
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--config", missing, directory.FullPath);

        // Assert: the problem is still reported
        Assert.Equal(1, exitCode);
        Assert.Contains("missing.yaml:1:1: config-error Configuration file not found", output);
    }

    /// <summary>
    ///     Test that no-config ignores an invalid configuration.
    /// </summary>
    [Fact]
    public void LintMark_NoConfigOption_InvalidConfigPresent_IgnoresIt()
    {
        // Arrange: an invalid configuration beside the Markdown file
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "bogus: 1\n");
        var file = directory.WriteFile("a.md");

        // Act: run the tool with and without --no-config
        var ignoredExit = Runner.Run(out var ignored, "dotnet", _dllPath, "--no-config", file);
        var usedExit = Runner.Run(out var used, "dotnet", _dllPath, file);

        // Assert: ignored with the flag, reported without it
        Assert.Equal(0, ignoredExit);
        Assert.DoesNotContain("config-error", ignored);
        Assert.Equal(1, usedExit);
        Assert.Contains("config-error", used);
    }

    /// <summary>
    ///     Test that an empty configuration file means the defaults.
    /// </summary>
    [Fact]
    public void LintMark_ConfigDefaults_EmptyConfigFile_ReturnsSuccess()
    {
        // Arrange: an empty configuration file
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", string.Empty);
        var file = directory.WriteFile("a.md");

        // Act: run the tool with the file
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, file);

        // Assert: success without a report
        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("config-error", output);
    }

    /// <summary>
    ///     Test that a configuration with only some settings is accepted.
    /// </summary>
    [Fact]
    public void LintMark_ConfigDefaults_PartialConfigFile_ReturnsSuccess()
    {
        // Arrange: a configuration that sets one setting
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "wrap: semantic\n");
        var file = directory.WriteFile("a.md");

        // Act: run the tool with the file
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, file);

        // Assert: success without a report
        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("config-error", output);
    }

    /// <summary>
    ///     Test that a tree without any configuration file works.
    /// </summary>
    [Fact]
    public void LintMark_ConfigDefaults_NoConfigFile_ReturnsSuccess()
    {
        // Arrange: a tree without configuration files
        using var directory = new TemporaryDirectory();
        var file = directory.WriteFile("a.md");

        // Act: run the tool with the file
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, file);

        // Assert: success without a report
        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("config-error", output);
    }

    /// <summary>
    ///     Test that the documented sample configuration is accepted.
    /// </summary>
    [Fact]
    public void LintMark_ConfigSettings_FullSampleFile_ReturnsSuccess()
    {
        // Arrange: the documented sample with every setting, rule alias and ignore patterns (it asks for CRLF line endings, so the file has them)
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", ConfigurationSamples.FullSample);
        var file = directory.WriteFile("a.md", "# Title\r\n\r\nText.\r\n");

        // Act: run the tool with the file
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, file);

        // Assert: success without a report
        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("config-error", output);
    }

    /// <summary>
    ///     Test that an unknown setting is rejected instead of ignored.
    /// </summary>
    [Fact]
    public void LintMark_ConfigValidation_UnknownKey_ReportsErrorAndReturnsError()
    {
        // Arrange: a configuration with a misspelled setting
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "line-width: 100\n");
        var file = directory.WriteFile("a.md");

        // Act: run the tool with the file
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, file);

        // Assert: reported at the key
        Assert.Equal(1, exitCode);
        Assert.Contains(".lintmark.yaml:1:1: config-error Unknown setting 'line-width'", output);
    }

    /// <summary>
    ///     Test that an invalid value is rejected with the allowed values.
    /// </summary>
    [Fact]
    public void LintMark_ConfigValidation_InvalidValue_ReportsErrorAndReturnsError()
    {
        // Arrange: a configuration with an invalid value
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "bullet: star\n");
        var file = directory.WriteFile("a.md");

        // Act: run the tool with the file
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, file);

        // Assert: reported at the value, naming the allowed values
        Assert.Equal(1, exitCode);
        Assert.Contains(".lintmark.yaml:1:9: config-error Invalid value 'star' for setting 'bullet'; expected one of: dash, asterisk, plus", output);
    }

    /// <summary>
    ///     Test that malformed YAML is reported with a position.
    /// </summary>
    [Fact]
    public void LintMark_ConfigValidation_MalformedYaml_ReportsErrorAndReturnsError()
    {
        // Arrange: a configuration with an unterminated quote
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "wrap: \"fill\n");
        var file = directory.WriteFile("a.md");

        // Act: run the tool with the file
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, file);

        // Assert: reported as invalid YAML with a position
        Assert.Equal(1, exitCode);
        Assert.Matches(@"\.lintmark\.yaml:\d+:\d+: config-error Invalid YAML", output);
    }

    /// <summary>
    ///     Test that a file holding more than one configuration is reported with a position.
    /// </summary>
    [Fact]
    public void LintMark_ConfigValidation_TwoDocuments_ReportsErrorAndReturnsError()
    {
        // Arrange: a configuration file with a second configuration after a document marker
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "line-length: 80\n---\nwrap: fill\n");
        var file = directory.WriteFile("a.md");

        // Act: run the tool with the file
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, file);

        // Assert: reported at the second configuration
        Assert.Equal(1, exitCode);
        Assert.Contains(".lintmark.yaml:2:1: config-error Only one YAML document is allowed", output);
    }

    /// <summary>
    ///     Test that a configuration file saved in a UTF-16 encoding is reported.
    /// </summary>
    [Fact]
    public void LintMark_ConfigValidation_Utf16File_ReportsErrorAndReturnsError()
    {
        // Arrange: a configuration file saved as UTF-16 with its byte order mark
        using var directory = new TemporaryDirectory();
        var encoding = System.Text.Encoding.Unicode;
        directory.WriteBytes(".lintmark.yaml", [.. encoding.GetPreamble(), .. encoding.GetBytes("wrap: fill\n")]);
        var file = directory.WriteFile("a.md");

        // Act: run the tool with the file
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, file);

        // Assert: reported at the start of the file
        Assert.Equal(1, exitCode);
        Assert.Contains(".lintmark.yaml:1:1: config-error Unsupported byte order mark", output);
    }

    /// <summary>
    ///     Test that a setting given twice is reported at its second occurrence.
    /// </summary>
    [Fact]
    public void LintMark_ConfigValidation_DuplicateSetting_ReportsErrorAndReturnsError()
    {
        // Arrange: a configuration that sets one setting twice
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "line-length: 80\nwrap: fill\nline-length: 90\n");
        var file = directory.WriteFile("a.md");

        // Act: run the tool with the file
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, file);

        // Assert: reported at the second occurrence
        Assert.Equal(1, exitCode);
        Assert.Contains(".lintmark.yaml:3:1: config-error Duplicate setting 'line-length' (first defined on line 1)", output);
    }

    /// <summary>
    ///     Test that a configuration file larger than the documented limit is reported.
    /// </summary>
    [Fact]
    public void LintMark_ConfigLimits_OversizedFile_ReportsErrorAndReturnsError()
    {
        // Arrange: a configuration file one byte larger than the supported size
        using var directory = new TemporaryDirectory();
        var content = new byte[1_048_577];
        Array.Fill(content, (byte)'#');
        directory.WriteBytes(".lintmark.yaml", content);
        var file = directory.WriteFile("a.md");

        // Act: run the tool with the file
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, file);

        // Assert: reported at the start of the file
        Assert.Equal(1, exitCode);
        Assert.Contains(".lintmark.yaml:1:1: config-error", output);
        Assert.Contains("larger than 1 MiB", output);
    }

    /// <summary>
    ///     Test that a configuration file nested deeper than the documented limit is reported promptly.
    /// </summary>
    [Fact]
    public void LintMark_ConfigLimits_DeeplyNestedFile_ReportsErrorWithinTimeBound()
    {
        // Arrange: a configuration file whose value is nested 100,000 levels deep
        using var directory = new TemporaryDirectory();
        const int depth = 100_000;
        directory.WriteFile(".lintmark.yaml", "a: " + new string('[', depth) + new string(']', depth) + "\n");
        var file = directory.WriteFile("a.md");

        // Act: run the tool with the file and time it
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, file);
        stopwatch.Stop();

        // Assert: reported as a configuration problem, well before an unguarded parse could finish
        Assert.Equal(1, exitCode);
        Assert.Contains(".lintmark.yaml:1:", output);
        Assert.Contains("config-error Nesting is too deep", output);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"The run took {stopwatch.Elapsed}");
    }

    /// <summary>
    ///     Test that configuration problems are reported in the documented one-line form.
    /// </summary>
    [Fact]
    public void LintMark_ConfigErrors_Reported_AsPathLineColRuleMessage()
    {
        // Arrange: a configuration with two problems
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "color: red\nwrap: nope\n");
        var file = directory.WriteFile("a.md");

        // Act: run the tool with the file
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, file);

        // Assert: one line per problem, in the documented form
        Assert.Equal(1, exitCode);
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => l.Contains("config-error", StringComparison.Ordinal))
            .ToList();
        Assert.Equal(2, lines.Count);
        Assert.Matches(@"\.lintmark\.yaml:1:1: config-error Unknown setting 'color'$", lines[0]);
        Assert.Matches(@"\.lintmark\.yaml:2:7: config-error Invalid value 'nope' for setting 'wrap'; expected one of: fill, sentence, semantic$", lines[1]);
    }

    /// <summary>
    ///     Test that a configuration file outside the current directory is reported with a relative forward-slash path.
    /// </summary>
    [Fact]
    public void LintMark_ConfigErrors_ConfigOutsideCurrentDirectory_ReportedWithRelativeForwardSlashPath()
    {
        // Arrange: an invalid configuration in a temporary directory, which lies outside the current directory
        using var directory = new TemporaryDirectory();
        var config = directory.WriteFile("settings/outside.yaml", "bogus: 1\n");
        var file = directory.WriteFile("a.md");
        var expectedPath = Path.GetRelativePath(Environment.CurrentDirectory, config).Replace('\\', '/');

        // Act: run the tool with the absolute configuration path
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--config", config, file);

        // Assert: reported once, relative to the current directory and with forward slashes only
        Assert.Equal(1, exitCode);
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => l.Contains("config-error", StringComparison.Ordinal))
            .ToList();
        var line = Assert.Single(lines);
        Assert.Equal($"{expectedPath}:1:1: config-error Unknown setting 'bogus'", line);
        Assert.DoesNotContain('\\', line);
    }

    /// <summary>
    ///     Test that a configuration problem is reported once however many files use that configuration.
    /// </summary>
    [Fact]
    public void LintMark_ConfigErrors_SharedByManyFiles_ReportedOnce()
    {
        // Arrange: one invalid configuration used by many Markdown files in several folders
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "bogus: 1\n");
        var files = Enumerable.Range(0, 12).Select(i => directory.WriteFile($"d{i % 3}/f{i}.md")).ToArray();

        // Act: run the tool with all the files
        var exitCode = Runner.Run(out var output, "dotnet", [_dllPath, .. files]);

        // Assert: the problem appears on exactly one line
        Assert.Equal(1, exitCode);
        var reported = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Count(l => l.Contains("config-error", StringComparison.Ordinal));
        Assert.Equal(1, reported);
    }

    /// <summary>
    ///     Test that problems of several configuration files are reported in path order.
    /// </summary>
    [Fact]
    public void LintMark_ConfigErrors_MultipleFiles_ReportedInPathOrder()
    {
        // Arrange: invalid configurations in two folders, passed in reverse order
        using var directory = new TemporaryDirectory();
        directory.WriteFile("b/.lintmark.yaml", "bogus: 1\n");
        directory.WriteFile("a/.lintmark.yaml", "bogus: 1\n");
        var fileB = directory.WriteFile("b/x.md");
        var fileA = directory.WriteFile("a/x.md");

        // Act: run the tool
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, fileB, fileA);

        // Assert: reported in path order
        Assert.Equal(1, exitCode);
        var indexA = output.IndexOf("a/.lintmark.yaml:1:1", StringComparison.Ordinal);
        var indexB = output.IndexOf("b/.lintmark.yaml:1:1", StringComparison.Ordinal);
        Assert.True(indexA >= 0 && indexA < indexB, output);
    }

    /// <summary>
    ///     Test that files are still checked when their configuration has a problem.
    /// </summary>
    [Fact]
    public void LintMark_ConfigErrors_InvalidConfig_StillReportsFileFailures()
    {
        // Arrange: an invalid configuration and a file that cannot be decoded
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "bogus: 1\n");
        var file = directory.WriteBytes("bad.md", [0xFF]);

        // Act: run the tool with the file
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, file);

        // Assert: both the configuration problem and the file failure are reported
        Assert.Equal(1, exitCode);
        Assert.Contains("config-error Unknown setting 'bogus'", output);
        Assert.Contains("bad.md:1:1: file-encoding-error", output);
    }

    /// <summary>
    ///     Test that a reported configuration problem gives a non-zero exit code.
    /// </summary>
    [Fact]
    public void LintMark_ConfigErrors_Reported_ReturnsError()
    {
        // Arrange: an invalid configuration and a valid Markdown file
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "final-newline: maybe\n");
        var file = directory.WriteFile("a.md");

        // Act: run the tool with the file
        var exitCode = Runner.Run(out _, "dotnet", _dllPath, file);

        // Assert: the exit code is one
        Assert.Equal(1, exitCode);
    }

    /// <summary>
    ///     Test that unsupported or unrelated editor settings are accepted without an error.
    /// </summary>
    [Fact]
    public void LintMark_EditorConfig_UnsupportedOrUnusedSettings_Provided_ReturnsSuccess()
    {
        // Arrange: editor settings with cr, unset, an unknown word and unrelated settings
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".editorconfig", "root = true\n\n[a.md]\nend_of_line = cr\n\n[b.md]\nend_of_line = unset\n\n[*]\nindent_style = tab\nmax_line_length = 80\n");
        var fileA = directory.WriteFile("a.md");
        var fileB = directory.WriteFile("b.md");

        // Act: run the tool with both files
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, fileA, fileB);

        // Assert: success without a report
        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("config-error", output);
    }

    /// <summary>
    ///     Test that editor settings that cannot be read are reported at the Markdown file.
    /// </summary>
    [Fact]
    public void LintMark_EditorConfig_UnreadableSettings_ReportedAtMarkdownFile()
    {
        // Arrange: editor settings for Markdown files and a CRLF Markdown file
        using var directory = new TemporaryDirectory();
        var settings = directory.WriteFile(".editorconfig", "root = true\n\n[*.md]\nend_of_line = lf\n");
        var file = directory.WriteBytes("a.md", Bytes("# Title\r\n"));

        // Arrange: make the settings unreadable, by locking them on Windows and by removing every permission elsewhere
        FileStream? hold = null;
        if (OperatingSystem.IsWindows())
        {
            hold = new FileStream(settings, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        else
        {
            File.SetUnixFileMode(settings, UnixFileMode.None);
            if (CanRead(settings))
            {
                Assert.Skip("The file system does not stop this user from reading a file without permissions.");
                return;
            }
        }

        try
        {
            // Act: run the tool with the fix option
            var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--fix", file);

            // Assert: the problem is reported at line 1 and column 1 of the Markdown file, and the file is as it was
            Assert.Equal(1, exitCode);
            Assert.Contains("a.md:1:1: config-error Cannot read .editorconfig settings", output);
            Assert.Equal(Bytes("# Title\r\n"), File.ReadAllBytes(file));
        }
        finally
        {
            // Restore access so that the directory can be deleted
            hold?.Dispose();
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(settings, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
        }
    }

    /// <summary>
    ///     Determines whether a file can be opened for reading.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <returns><see langword="true"/> when the file can be read.</returns>
    private static bool CanRead(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
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

    /// <summary>
    ///     Encodes text as UTF-8 without a byte order mark.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The bytes.</returns>
    private static byte[] Bytes(string text) => new System.Text.UTF8Encoding(false).GetBytes(text);

    /// <summary>
    ///     Test that the fix option rewrites a CRLF file with LF line endings when the configuration asks for them.
    /// </summary>
    [Fact]
    public void LintMark_FixFlag_CrlfFile_RewritesToLf()
    {
        // Arrange: a CRLF file and a configuration that asks for LF
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "end-of-line: lf\n");
        var file = directory.WriteBytes("a.md", Bytes("# Title\r\n\r\nText.\r\n"));

        // Act: run the tool with the fix option
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--fix", file);

        // Assert: the file has LF line endings and nothing is reported
        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("line-ending", output);
        Assert.Equal(Bytes("# Title\n\nText.\n"), File.ReadAllBytes(file));
    }

    /// <summary>
    ///     Test that the fix option leaves a file that already complies byte for byte as it was.
    /// </summary>
    [Fact]
    public void LintMark_FixFlag_CleanFile_LeavesBytesUnchanged()
    {
        // Arrange: a compliant file with an old last write time
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "end-of-line: lf\n");
        var file = directory.WriteBytes("a.md", Bytes("# Title\n\nText.\n"));
        var writeTime = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(file, writeTime);

        // Act: run the tool with the fix option
        var exitCode = Runner.Run(out _, "dotnet", _dllPath, "--fix", file);

        // Assert: success, same bytes, and the file was not even written
        Assert.Equal(0, exitCode);
        Assert.Equal(Bytes("# Title\n\nText.\n"), File.ReadAllBytes(file));
        Assert.Equal(writeTime, File.GetLastWriteTimeUtc(file));
    }

    /// <summary>
    ///     Test that a second run of the fix option reports nothing and changes nothing.
    /// </summary>
    [Fact]
    public void LintMark_FixFlag_Twice_SecondRunReportsNothing()
    {
        // Arrange: a mixed file, fixed by a first run
        using var directory = new TemporaryDirectory();
        var file = directory.WriteBytes("a.md", Bytes("# Title\r\n\r\nText.\n"));
        Runner.Run(out _, "dotnet", _dllPath, "--fix", file);
        var afterFirst = File.ReadAllBytes(file);

        // Act: run again
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--fix", file);

        // Assert: success, no report, same bytes
        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("line-ending", output);
        Assert.Equal(afterFirst, File.ReadAllBytes(file));
    }

    /// <summary>
    ///     Test that the diff option shows the line ending change and writes nothing.
    /// </summary>
    [Fact]
    public void LintMark_DiffFlag_CrlfFile_PrintsNoteAndWritesNothing()
    {
        // Arrange: a CRLF file and a configuration that asks for LF
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "end-of-line: lf\n");
        var file = directory.WriteBytes("a.md", Bytes("# Title\r\n\r\nText.\r\n"));

        // Act: run the tool with the diff option
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--diff", file);

        // Assert: the note is printed, the violation makes the exit code one, and the file is unchanged
        Assert.Equal(1, exitCode);
        Assert.Contains("--- a/", output);
        Assert.Contains("+++ b/", output);
        Assert.Contains("\\ Line endings change from CRLF to LF", output);
        Assert.Equal(Bytes("# Title\r\n\r\nText.\r\n"), File.ReadAllBytes(file));
    }

    /// <summary>
    ///     Test that the diff option prints nothing for a compliant file.
    /// </summary>
    [Fact]
    public void LintMark_DiffFlag_CleanFile_PrintsNothing()
    {
        // Arrange: a compliant file
        using var directory = new TemporaryDirectory();
        var file = directory.WriteFile("a.md");

        // Act: run the tool with the diff option
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--diff", file);

        // Assert: success without a diff
        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("--- a/", output);
        Assert.DoesNotContain("@@", output);
    }

    /// <summary>
    ///     Test that the fix option cannot be combined with the check option.
    /// </summary>
    [Fact]
    public void LintMark_FixWithCheck_Combined_ReturnsError()
    {
        // Arrange: a file
        using var directory = new TemporaryDirectory();
        var file = directory.WriteFile("a.md");

        // Act: run the tool with both options
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--fix", "--check", file);

        // Assert: a usage error
        Assert.Equal(1, exitCode);
        Assert.Contains("--fix cannot be combined with --check or --diff", output);
    }

    /// <summary>
    ///     Test that the fix option cannot be combined with the diff option.
    /// </summary>
    [Fact]
    public void LintMark_FixWithDiff_Combined_ReturnsError()
    {
        // Arrange: a file
        using var directory = new TemporaryDirectory();
        var file = directory.WriteFile("a.md");

        // Act: run the tool with both options
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--fix", "--diff", file);

        // Assert: a usage error
        Assert.Equal(1, exitCode);
        Assert.Contains("--fix cannot be combined with --check or --diff", output);
    }

    /// <summary>
    ///     Test that the line ending of the configuration wins and is written by the fix option.
    /// </summary>
    [Fact]
    public void LintMark_LineEnding_ConfigWins_FixWritesCrlf()
    {
        // Arrange: an LF file, a configuration that asks for CRLF and editor settings that ask for LF
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "end-of-line: crlf\n");
        directory.WriteFile(".editorconfig", "root = true\n\n[*.md]\nend_of_line = lf\n");
        var file = directory.WriteBytes("a.md", Bytes("# Title\n\nText.\n"));

        // Act: run the tool with the fix option
        var exitCode = Runner.Run(out _, "dotnet", _dllPath, "--fix", file);

        // Assert: the configuration's ending is written
        Assert.Equal(0, exitCode);
        Assert.Equal(Bytes("# Title\r\n\r\nText.\r\n"), File.ReadAllBytes(file));
    }

    /// <summary>
    ///     Test that the editor settings supply the line ending when the configuration gives none.
    /// </summary>
    [Fact]
    public void LintMark_LineEnding_EditorConfigUsed_FixWritesCrlf()
    {
        // Arrange: an LF file and editor settings that ask for CRLF
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".editorconfig", "root = true\n\n[*.md]\nend_of_line = crlf\n");
        var file = directory.WriteBytes("a.md", Bytes("# Title\n\nText.\n"));

        // Act: run the tool with the fix option
        var exitCode = Runner.Run(out _, "dotnet", _dllPath, "--fix", file);

        // Assert: the editor settings' ending is written
        Assert.Equal(0, exitCode);
        Assert.Equal(Bytes("# Title\r\n\r\nText.\r\n"), File.ReadAllBytes(file));
    }

    /// <summary>
    ///     Test that the majority line ending is kept when nothing else is configured.
    /// </summary>
    [Fact]
    public void LintMark_LineEnding_MajorityUsed_FixWritesMajority()
    {
        // Arrange: a file with two CRLF and one LF terminator, and no configuration
        using var directory = new TemporaryDirectory();
        var file = directory.WriteBytes("a.md", Bytes("# Title\r\n\r\nText.\n"));

        // Act: run the tool with the fix option
        var exitCode = Runner.Run(out _, "dotnet", _dllPath, "--fix", file);

        // Assert: CRLF everywhere
        Assert.Equal(0, exitCode);
        Assert.Equal(Bytes("# Title\r\n\r\nText.\r\n"), File.ReadAllBytes(file));
    }

    /// <summary>
    ///     Test that a mixed file is reported as such and is fixed.
    /// </summary>
    [Fact]
    public void LintMark_LineEnding_MixedFile_ReportsAndFixes()
    {
        // Arrange: a file with mixed line endings
        using var directory = new TemporaryDirectory();
        var file = directory.WriteBytes("a.md", Bytes("# Title\r\n\r\nText.\n"));

        // Act: check, then fix
        var checkExit = Runner.Run(out var checkOutput, "dotnet", _dllPath, file);
        var fixExit = Runner.Run(out var fixOutput, "dotnet", _dllPath, "--fix", file);

        // Assert: the check reports the violation; the fix removes it
        Assert.Equal(1, checkExit);
        Assert.Contains("a.md:1:1: line-ending Mixed line endings (2 CRLF, 1 LF); expected CRLF", checkOutput);
        Assert.Equal(0, fixExit);
        Assert.DoesNotContain("line-ending", fixOutput);
    }

    /// <summary>
    ///     Test that the fix option keeps the byte order mark and all text it does not change.
    /// </summary>
    [Fact]
    public void LintMark_FixFlag_BomFile_KeepsBomAndText()
    {
        // Arrange: a CRLF file with a byte order mark and non-ASCII text
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "end-of-line: lf\n");
        var file = directory.WriteBytes("a.md", [0xEF, 0xBB, 0xBF, .. Bytes("# 日本語\r\n\r\n日本語 text\r\n")]);

        // Act: run the tool with the fix option
        var exitCode = Runner.Run(out _, "dotnet", _dllPath, "--fix", file);

        // Assert: the mark and the text are intact; only the line endings changed
        Assert.Equal(0, exitCode);
        Assert.Equal((byte[])[0xEF, 0xBB, 0xBF, .. Bytes("# 日本語\n\n日本語 text\n")], File.ReadAllBytes(file));
    }

    /// <summary>
    ///     Test that a rewrite leaves no temporary file behind.
    /// </summary>
    [Fact]
    public void LintMark_FixFlag_Rewrite_LeavesNoTemporaryFiles()
    {
        // Arrange: a CRLF file and a configuration that asks for LF
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "end-of-line: lf\n");
        var file = directory.WriteBytes("a.md", Bytes("# Title\r\n\r\nText.\r\n"));

        // Act: run the tool with the fix option
        var exitCode = Runner.Run(out _, "dotnet", _dllPath, "--fix", file);

        // Assert: the directory holds the same files as before
        Assert.Equal(0, exitCode);
        Assert.Equal(
            [".lintmark.yaml", "a.md"],
            Directory.GetFiles(directory.FullPath).Select(Path.GetFileName).Order(StringComparer.Ordinal));
    }

    /// <summary>
    ///     Test that a document with an invalid grid table is never rewritten by the fix option.
    /// </summary>
    [Fact]
    public void LintMark_FixFlag_GridTableFallback_LeavesFileUntouched()
    {
        // Arrange: a CRLF document with an invalid grid table and a configuration that asks for LF
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "end-of-line: lf\n");
        var file = directory.WriteBytes("a.md", Bytes("para\r\n+---+---+\r\n"));

        // Act: run the tool with the fix option
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--fix", file);

        // Assert: reported, exit code one, bytes identical
        Assert.Equal(1, exitCode);
        Assert.Contains("a.md:2:1: grid-table-invalid", output);
        Assert.Equal(Bytes("para\r\n+---+---+\r\n"), File.ReadAllBytes(file));
    }

    /// <summary>
    ///     Test that a conversion of the line endings that would change how the document renders is never written.
    /// </summary>
    [Fact]
    public void LintMark_FixFlag_CodeSpanLineEndingChange_LeavesFileUntouched()
    {
        // Arrange: a code span framed by line breaks and a configuration that asks for CRLF
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "end-of-line: crlf\n");
        var file = directory.WriteBytes("a.md", Bytes("`\nbb\n`\n"));

        // Act: run the tool with the fix option
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--fix", file);

        // Assert: reported, exit code one, bytes identical
        Assert.Equal(1, exitCode);
        Assert.Contains("a.md:1:1: safety-html-mismatch", output);
        Assert.Equal(Bytes("`\nbb\n`\n"), File.ReadAllBytes(file));
    }

    /// <summary>
    ///     Test that a file with an invalid configuration is never rewritten by the fix option.
    /// </summary>
    [Fact]
    public void LintMark_FixFlag_InvalidConfig_LeavesFileUntouched()
    {
        // Arrange: an invalid configuration and a file with mixed line endings
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "bogus: 1\n");
        var file = directory.WriteBytes("a.md", Bytes("# Title\r\n\r\nText.\n"));

        // Act: run the tool with the fix option
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--fix", file);

        // Assert: the configuration problem is reported and the file is untouched
        Assert.Equal(1, exitCode);
        Assert.Contains("config-error Unknown setting 'bogus'", output);
        Assert.Equal(Bytes("# Title\r\n\r\nText.\n"), File.ReadAllBytes(file));
    }

    /// <summary>
    ///     Test that an invalid grid table is reported at the offending line.
    /// </summary>
    [Fact]
    public void LintMark_GridTableInvalid_Provided_ReportsAtOffendingLine()
    {
        // Arrange: a document with an invalid grid table on line 4
        using var directory = new TemporaryDirectory();
        var file = directory.WriteFile("a.md", "# Title\n\npara\n+---+---+\n");

        // Act: run the tool
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, file);

        // Assert: reported at line 4, exit code one
        Assert.Equal(1, exitCode);
        Assert.Contains("a.md:4:1: grid-table-invalid", output);
    }

    /// <summary>
    ///     Test that switching the grid table report off never lets the file be rewritten.
    /// </summary>
    [Fact]
    public void LintMark_GridTableInvalid_Disabled_StillNotRewritten()
    {
        // Arrange: a CRLF document with an invalid grid table; the report is switched off and LF is asked for
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "end-of-line: lf\nrules:\n  grid-table-invalid: false\n");
        var file = directory.WriteBytes("a.md", Bytes("para\r\n+---+---+\r\n"));

        // Act: run the tool with the fix option
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--fix", file);

        // Assert: nothing is reported, and the file is untouched
        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("grid-table-invalid", output);
        Assert.Equal(Bytes("para\r\n+---+---+\r\n"), File.ReadAllBytes(file));
    }

    /// <summary>
    ///     Test that the files of a glob that the ignore patterns match are skipped.
    /// </summary>
    [Fact]
    public void LintMark_IgnorePatterns_GlobSelected_SkipsMatches()
    {
        // Arrange: a configuration that ignores a folder, a broken file in it and a broken file outside it
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "ignore:\n  - \"drafts/**\"\n");
        directory.WriteBytes("drafts/bad.md", [0xFF]);
        directory.WriteBytes("kept/bad.md", [0xFF]);

        // Act: run the tool with a glob
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, Path.Combine(directory.FullPath, "**", "*.md"));

        // Assert: only the file that is not ignored is reported
        Assert.Equal(1, exitCode);
        Assert.Contains("kept/bad.md:1:1: file-encoding-error", output);
        Assert.DoesNotContain("drafts/bad.md", output);
    }

    /// <summary>
    ///     Test that a file named on the command line is processed although an ignore pattern matches it.
    /// </summary>
    [Fact]
    public void LintMark_IgnorePatterns_ExplicitFile_IsProcessed()
    {
        // Arrange: a configuration that ignores a folder and a broken file in it
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "ignore:\n  - \"drafts/**\"\n");
        var file = directory.WriteBytes("drafts/bad.md", [0xFF]);

        // Act: run the tool with the file named
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, file);

        // Assert: the file is read and reported
        Assert.Equal(1, exitCode);
        Assert.Contains("drafts/bad.md:1:1: file-encoding-error", output);
    }

    /// <summary>
    ///     Test that an exclude on the command line still applies to a file that is named.
    /// </summary>
    [Fact]
    public void LintMark_IgnorePatterns_ExcludeOnCommandLine_StillApplies()
    {
        // Arrange: a broken file in a folder
        using var directory = new TemporaryDirectory();
        var file = directory.WriteBytes("drafts/bad.md", [0xFF]);

        // Act: run the tool with the file named and excluded
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, file, "!" + Path.Combine(directory.FullPath, "drafts", "**"));

        // Assert: the exclude removes the file
        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("file-encoding-error", output);
    }
}

