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

using DemaConsulting.LintMark.Cli;

namespace DemaConsulting.LintMark.Tests.Cli;

/// <summary>
///     Subsystem tests for the CLI subsystem covering Context and Program integration.
/// </summary>
[Collection("Sequential")]
public class CliSubsystemTests
{
    /// <summary>
    ///     Test that Context and Program work together to handle version flag workflow.
    /// </summary>
    [Fact]
    public void CliSubsystem_VersionFlow_ContextAndProgram_DisplaysVersionAndExits()
    {
        // Arrange: command line arguments with version flag; capture console output
        var args = new[] { "--version" };
        var originalOut = Console.Out;
        using var capturedOut = new StringWriter();

        try
        {
            Console.SetOut(capturedOut);

            // Act: create context and run program logic
            using var context = Context.Create(args);
            Program.Run(context);

            // Assert: version flag is parsed, version text is displayed, and exit code is success
            Assert.True(context.Version, "Context should parse version flag");
            Assert.Equal(0, context.ExitCode);
            Assert.Contains(Program.Version, capturedOut.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that Context and Program work together to handle the -v short version flag.
    /// </summary>
    [Fact]
    public void CliSubsystem_VersionFlow_ContextAndProgram_DisplaysVersionAndExits_WithShortVFlag()
    {
        // Arrange: command line arguments with -v short version flag; capture console output
        var args = new[] { "-v" };
        var originalOut = Console.Out;
        using var capturedOut = new StringWriter();

        try
        {
            Console.SetOut(capturedOut);

            // Act: create context and run program logic
            using var context = Context.Create(args);
            Program.Run(context);

            // Assert: version flag is parsed, version text is displayed, and exit code is success
            Assert.True(context.Version, "Context should parse -v flag as version");
            Assert.Equal(0, context.ExitCode);
            Assert.Contains(Program.Version, capturedOut.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that Context and Program work together to handle help flag workflow.
    /// </summary>
    [Fact]
    public void CliSubsystem_HelpFlow_ContextAndProgram_DisplaysHelpAndExits()
    {
        // Arrange: command line arguments with help flag; capture console output
        var args = new[] { "--help" };
        var originalOut = Console.Out;
        using var capturedOut = new StringWriter();

        try
        {
            Console.SetOut(capturedOut);

            // Act: create context and run program logic
            using var context = Context.Create(args);
            Program.Run(context);

            // Assert: help flag is parsed, usage text is displayed, and exit code is success
            Assert.True(context.Help, "Context should parse help flag");
            Assert.Equal(0, context.ExitCode);
            var output = capturedOut.ToString();
            Assert.Contains("Usage:", output);
            Assert.Contains("Options:", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that Context and Program work together to handle the -? short help flag.
    /// </summary>
    [Fact]
    public void CliSubsystem_HelpFlow_ContextAndProgram_DisplaysHelpAndExits_WithShortQuestionFlag()
    {
        // Arrange: command line arguments with -? short help flag; capture console output
        var args = new[] { "-?" };
        var originalOut = Console.Out;
        using var capturedOut = new StringWriter();

        try
        {
            Console.SetOut(capturedOut);

            // Act: create context and run program logic
            using var context = Context.Create(args);
            Program.Run(context);

            // Assert: help flag is parsed, usage text is displayed, and exit code is success
            Assert.True(context.Help, "Context should parse -? flag as help");
            Assert.Equal(0, context.ExitCode);
            var output = capturedOut.ToString();
            Assert.Contains("Usage:", output);
            Assert.Contains("Options:", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that Context and Program work together to handle the -h short help flag.
    /// </summary>
    [Fact]
    public void CliSubsystem_HelpFlow_ContextAndProgram_DisplaysHelpAndExits_WithShortHFlag()
    {
        // Arrange: command line arguments with -h short help flag; capture console output
        var args = new[] { "-h" };
        var originalOut = Console.Out;
        using var capturedOut = new StringWriter();

        try
        {
            Console.SetOut(capturedOut);

            // Act: create context and run program logic
            using var context = Context.Create(args);
            Program.Run(context);

            // Assert: help flag is parsed, usage text is displayed, and exit code is success
            Assert.True(context.Help, "Context should parse -h flag as help");
            Assert.Equal(0, context.ExitCode);
            var output = capturedOut.ToString();
            Assert.Contains("Usage:", output);
            Assert.Contains("Options:", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that Context and Program work together to handle validation flag workflow.
    /// </summary>
    [Fact]
    public void CliSubsystem_ValidateFlow_ContextAndProgram_RunsValidationAndExits()
    {
        // Arrange: command line arguments with validate flag; capture console output
        var args = new[] { "--validate" };
        var originalOut = Console.Out;
        using var capturedOut = new StringWriter();

        try
        {
            Console.SetOut(capturedOut);

            // Act: create context and run program logic
            using var context = Context.Create(args);
            Program.Run(context);

            // Assert: validate flag is parsed, summary is output, and exit code is success
            Assert.True(context.Validate, "Context should parse validate flag");
            Assert.Equal(0, context.ExitCode);
            Assert.Contains("Total Tests:", capturedOut.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that Context and Program work together to handle silent flag workflow.
    /// </summary>
    [Fact]
    public void CliSubsystem_SilentFlow_ContextAndProgram_SuppressesOutput()
    {
        // Arrange: command line arguments with version and silent flags; capture console streams
        var args = new[] { "--version", "--silent" };
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var capturedOut = new StringWriter();
        using var capturedError = new StringWriter();

        try
        {
            Console.SetOut(capturedOut);
            Console.SetError(capturedError);

            // Act: create context and run program logic
            using var context = Context.Create(args);
            Program.Run(context);

            // Assert: silent flag is parsed, exit code is success, and no console output is produced
            Assert.True(context.Silent, "Context should parse silent flag");
            Assert.Equal(0, context.ExitCode);
            Assert.Equal(string.Empty, capturedOut.ToString());
            Assert.Equal(string.Empty, capturedError.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     Test that Context and Program work together to handle results flag workflow.
    /// </summary>
    [Fact]
    public void CliSubsystem_ResultsFlow_ContextAndProgram_WritesResultsFile()
    {
        // Arrange: temporary results file path and validation command with results output
        var tempDir = Path.GetTempPath();
        var resultsFile = Path.Combine(tempDir, $"cli_test_{Guid.NewGuid()}.trx");
        var args = new[] { "--validate", "--silent", "--results", resultsFile };

        try
        {
            // Act: create context and run program logic
            using var context = Context.Create(args);
            Program.Run(context);

            // Assert: results flag is parsed, validation runs, and results file is written
            Assert.Equal(resultsFile, context.ResultsFile);
            Assert.Equal(0, context.ExitCode);
            Assert.True(File.Exists(resultsFile), "Results file should be written to specified path");
        }
        finally
        {
            // Cleanup
            if (File.Exists(resultsFile))
            {
                File.Delete(resultsFile);
            }
        }
    }

    /// <summary>
    ///     Test that Context and Program work together to handle log flag workflow.
    /// </summary>
    [Fact]
    public void CliSubsystem_LogFlow_ContextAndProgram_WritesLogFile()
    {
        // Arrange: temporary log file path and version command with log output
        var tempDir = Path.GetTempPath();
        var logFile = Path.Combine(tempDir, $"cli_test_{Guid.NewGuid()}.log");
        var args = new[] { "--version", "--log", logFile };

        try
        {
            // Act: create context and run program logic
            using (var context = Context.Create(args))
            {
                Program.Run(context);

                // Assert: version flag is parsed and exit code is success
                Assert.True(context.Version, "Context should parse version flag");
                Assert.Equal(0, context.ExitCode);
            }

            // Assert: log file is written with version output
            Assert.True(File.Exists(logFile), "Log file should be created at specified path");
            var logContent = File.ReadAllText(logFile);
            Assert.False(string.IsNullOrWhiteSpace(logContent), "Log file should contain version output");
        }
        finally
        {
            // Cleanup
            if (File.Exists(logFile))
            {
                File.Delete(logFile);
            }
        }
    }

    /// <summary>
    ///     Test that Program rejects unknown arguments, writes an error to stderr, and exits non-zero.
    /// </summary>
    [Fact]
    public void CliSubsystem_InvalidArgs_ContextAndProgram_RejectsUnknownArgumentsAndExitsNonZero()
    {
        // Arrange: unknown command-line argument
        var args = new[] { "--unknown-flag" };

        var originalError = Console.Error;
        try
        {
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);

            // Act: invoke the actual CLI entry point with an unknown flag
            var result = Program.Main(args);

            // Assert: invalid arguments produce a non-zero exit code and an error on stderr
            Assert.Equal(1, result);
            var errorOutput = errWriter.ToString();
            Assert.False(string.IsNullOrWhiteSpace(errorOutput), "Program should write an error to stderr for unknown arguments");
            Assert.Contains("--unknown-flag", errorOutput);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     Test that Context writes error messages to stderr.
    /// </summary>
    [Fact]
    public void CliSubsystem_ErrorOutput_ContextAndProgram_WritesErrorToStderr()
    {
        // Arrange: redirect stderr to capture error output
        var originalError = Console.Error;
        try
        {
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);
            using var context = Context.Create([]);

            // Act: write an error message through the context
            context.WriteError("Test error message");

            // Assert: error is written to stderr and exit code reflects failure
            var errorOutput = errWriter.ToString();
            Assert.Contains("Test error message", errorOutput);
            Assert.Equal(1, context.ExitCode);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     Test that Context and Program work together to handle the --result legacy alias for results.
    /// </summary>
    [Fact]
    public void CliSubsystem_ResultAliasFlow_ContextAndProgram_WritesResultsFile()
    {
        // Arrange: temporary results file path and validation command with legacy --result alias
        var tempDir = Path.GetTempPath();
        var resultsFile = Path.Combine(tempDir, $"cli_test_{Guid.NewGuid()}.trx");
        var args = new[] { "--validate", "--silent", "--result", resultsFile };

        try
        {
            // Act: create context and run program logic
            using var context = Context.Create(args);
            Program.Run(context);

            // Assert: legacy --result alias is parsed, validation runs, and results file is written
            Assert.Equal(resultsFile, context.ResultsFile);
            Assert.Equal(0, context.ExitCode);
            Assert.True(File.Exists(resultsFile), "Results file should be written to specified path");
        }
        finally
        {
            // Cleanup
            if (File.Exists(resultsFile))
            {
                File.Delete(resultsFile);
            }
        }
    }

    /// <summary>
    ///     Test that Context and Program work together to handle depth flag with self-validation.
    /// </summary>
    [Fact]
    public void CliSubsystem_DepthFlow_ContextAndProgram_AdjustsHeadingDepth()
    {
        // Arrange: command line with --validate, --depth 2, and a log file to capture output
        var tempDir = Path.GetTempPath();
        var logFile = Path.Combine(tempDir, $"cli_test_{Guid.NewGuid()}.log");
        var args = new[] { "--validate", "--silent", "--depth", "2", "--log", logFile };

        try
        {
            // Act: create context and run program logic
            using (var context = Context.Create(args))
            {
                Program.Run(context);

                // Assert: depth is parsed correctly
                Assert.Equal(2, context.HeadingDepth);
                Assert.Equal(0, context.ExitCode);
            }

            // Assert: log contains level-2 heading
            var logContent = File.ReadAllText(logFile);
            Assert.Contains("## DEMA Consulting", logContent);
        }
        finally
        {
            // Cleanup
            if (File.Exists(logFile))
            {
                File.Delete(logFile);
            }
        }
    }

    /// <summary>
    ///     Test that Context and Program work together to process positional paths and exit successfully.
    /// </summary>
    [Fact]
    public void CliSubsystem_PathsFlow_ContextAndProgram_ParsesFilesAndExitsZero()
    {
        // Arrange: a valid Markdown file given as a positional argument; the log captures output
        using var directory = new TemporaryDirectory();
        var file = directory.WriteFile("ok.md");
        var logFile = directory.PathOf("run.log");

        // Act: create context and run program logic
        int exitCode;
        using (var context = Context.Create(["--silent", "--log", logFile, file]))
        {
            Program.Run(context);
            exitCode = context.ExitCode;
            Assert.Equal([file], context.Paths);
        }

        // Assert: success and no failure output (only the banner is logged)
        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("file-", File.ReadAllText(logFile));
    }

    /// <summary>
    ///     Test that a file that cannot be decoded is reported as a diagnostic and gives a non-zero exit code.
    /// </summary>
    [Fact]
    public void CliSubsystem_PathsFlow_UnreadableFile_ReportsDiagnosticAndExitsNonZero()
    {
        // Arrange: a file that is not valid UTF-8
        using var directory = new TemporaryDirectory();
        var file = directory.WriteBytes("bad.md", [0x61, 0xFF]);
        var logFile = directory.PathOf("run.log");

        // Act: create context and run program logic
        int exitCode;
        using (var context = Context.Create(["--silent", "--log", logFile, file]))
        {
            Program.Run(context);
            exitCode = context.ExitCode;
        }

        // Assert: non-zero exit code and a diagnostic in path:line:col: rule-id message form
        Assert.Equal(1, exitCode);
        Assert.Matches(@"bad\.md:1:2: file-encoding-error Invalid UTF-8", File.ReadAllText(logFile));
    }

    /// <summary>
    ///     Test that the check flag flows through Context and Program without writing or failing.
    /// </summary>
    [Fact]
    public void CliSubsystem_CheckFlow_ContextAndProgram_LeavesFilesUnchanged()
    {
        // Arrange: a valid file and its original bytes
        using var directory = new TemporaryDirectory();
        var file = directory.WriteFile("ok.md", "# Title\r\n\r\ntext\r\n");
        var before = File.ReadAllBytes(file);
        var timestamp = File.GetLastWriteTimeUtc(file);

        // Act: run with --check
        int exitCode;
        using (var context = Context.Create(["--silent", "--check", file]))
        {
            Program.Run(context);
            exitCode = context.ExitCode;
            Assert.True(context.Check);
        }

        // Assert: success and the file is untouched
        Assert.Equal(0, exitCode);
        Assert.Equal(before, File.ReadAllBytes(file));
        Assert.Equal(timestamp, File.GetLastWriteTimeUtc(file));
    }

    /// <summary>
    ///     Test that combining validate with paths is rejected by Context and reported by Main with a non-zero exit code.
    /// </summary>
    [Fact]
    public void CliSubsystem_ValidateWithPaths_ContextAndProgram_RejectsAndExitsNonZero()
    {
        // Arrange: capture stderr produced by Main
        var originalError = Console.Error;
        try
        {
            using var errorWriter = new StringWriter();
            Console.SetError(errorWriter);

            // Act: invoke Main with the invalid combination
            var exitCode = Program.Main(["--validate", "docs"]);

            // Assert: usage error and exit code one
            Assert.Equal(1, exitCode);
            Assert.Contains("--validate", errorWriter.ToString());
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     Test that the fix and diff flags are parsed by Context alongside paths.
    /// </summary>
    [Fact]
    public void CliSubsystem_FixAndDiff_Flags_Parsed()
    {
        // Act: parse the two modes separately, each with a path
        using var fix = Context.Create(["--silent", "--fix", "a.md"]);
        using var diff = Context.Create(["--silent", "--diff", "a.md"]);

        // Assert: each flag is set only in its own context and the path is kept
        Assert.True(fix.Fix);
        Assert.False(fix.Diff);
        Assert.True(diff.Diff);
        Assert.False(diff.Fix);
        Assert.Equal(["a.md"], fix.Paths);
        Assert.Equal(["a.md"], diff.Paths);
    }

    /// <summary>
    ///     Test that combining fix with check is rejected by Context and reported by Main with a non-zero exit code.
    /// </summary>
    [Fact]
    public void CliSubsystem_FixWithCheck_Arguments_Throws()
    {
        // Arrange: the combination is rejected by Context directly, then capture stderr produced by Main
        Assert.Throws<ArgumentException>(() => Context.Create(["--fix", "--check"]));
        var originalError = Console.Error;
        try
        {
            using var errorWriter = new StringWriter();
            Console.SetError(errorWriter);

            // Act: invoke Main with the contradictory flags
            var exitCode = Program.Main(["--fix", "--check"]);

            // Assert: usage error and exit code one
            Assert.Equal(1, exitCode);
            Assert.Contains("--fix cannot be combined with --check or --diff", errorWriter.ToString());
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     Test that the config option flows from Context through Program and reports an invalid configuration file.
    /// </summary>
    [Fact]
    public void CliSubsystem_ConfigFlow_ContextAndProgram_ReportsConfigErrorAndExitsNonZero()
    {
        // Arrange: a Markdown file and a configuration file with an unknown setting
        using var directory = new TemporaryDirectory();
        var file = directory.WriteFile("ok.md");
        var config = directory.WriteFile("custom.yaml", "color: red\n");
        var logFile = directory.PathOf("run.log");

        // Act: create context and run program logic
        int exitCode;
        using (var context = Context.Create(["--silent", "--log", logFile, "--config", config, file]))
        {
            Program.Run(context);
            exitCode = context.ExitCode;
            Assert.Equal(config, context.ConfigFile);
        }

        // Assert: non-zero exit code and a positioned config error against the named file
        Assert.Equal(1, exitCode);
        Assert.Matches(@"custom\.yaml:1:1: config-error Unknown setting 'color'", File.ReadAllText(logFile));
    }

    /// <summary>
    ///     Test that the no-config flag flows from Context through Program and ignores an invalid configuration file.
    /// </summary>
    [Fact]
    public void CliSubsystem_NoConfigFlow_ContextAndProgram_IgnoresInvalidConfig()
    {
        // Arrange: a Markdown file beside an invalid configuration file
        using var directory = new TemporaryDirectory();
        var file = directory.WriteFile("ok.md");
        directory.WriteFile(".lintmark.yaml", "color: red\n");
        var logFile = directory.PathOf("run.log");

        // Act: create context and run program logic
        int exitCode;
        using (var context = Context.Create(["--silent", "--log", logFile, "--no-config", file]))
        {
            Program.Run(context);
            exitCode = context.ExitCode;
            Assert.True(context.NoConfig);
        }

        // Assert: success and nothing about configuration is reported
        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("config-error", File.ReadAllText(logFile));
    }

    /// <summary>
    ///     Test that combining the config option with the no-config flag is rejected by Context and reported by Main.
    /// </summary>
    [Fact]
    public void CliSubsystem_ConfigAndNoConfig_ContextAndProgram_RejectsAndExitsNonZero()
    {
        // Arrange: capture stderr produced by Main
        var originalError = Console.Error;
        try
        {
            using var errorWriter = new StringWriter();
            Console.SetError(errorWriter);

            // Act: invoke Main with the contradictory options
            var exitCode = Program.Main(["--config", "a.yaml", "--no-config"]);

            // Assert: usage error and exit code one
            Assert.Equal(1, exitCode);
            Assert.Contains("--no-config", errorWriter.ToString());
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     Test that combining validate with the config option is rejected by Context and reported by Main.
    /// </summary>
    [Fact]
    public void CliSubsystem_ValidateWithConfig_ContextAndProgram_RejectsAndExitsNonZero()
    {
        // Arrange: capture stderr produced by Main
        var originalError = Console.Error;
        try
        {
            using var errorWriter = new StringWriter();
            Console.SetError(errorWriter);

            // Act: invoke Main with the invalid combination
            var exitCode = Program.Main(["--validate", "--config", "a.yaml"]);

            // Assert: usage error and exit code one
            Assert.Equal(1, exitCode);
            Assert.Contains("--config", errorWriter.ToString());
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     Test that combining validate with check is rejected by Context and reported by Main.
    /// </summary>
    [Fact]
    public void CliSubsystem_ValidateWithCheck_ContextAndProgram_RejectsAndExitsNonZero() =>
        AssertValidateCombinationRejected("--check");

    /// <summary>
    ///     Test that combining validate with fix is rejected by Context and reported by Main.
    /// </summary>
    [Fact]
    public void CliSubsystem_ValidateWithFix_ContextAndProgram_RejectsAndExitsNonZero() =>
        AssertValidateCombinationRejected("--fix");

    /// <summary>
    ///     Test that combining validate with diff is rejected by Context and reported by Main.
    /// </summary>
    [Fact]
    public void CliSubsystem_ValidateWithDiff_ContextAndProgram_RejectsAndExitsNonZero() =>
        AssertValidateCombinationRejected("--diff");

    /// <summary>
    ///     Test that combining validate with no-config is rejected by Context and reported by Main.
    /// </summary>
    [Fact]
    public void CliSubsystem_ValidateWithNoConfig_ContextAndProgram_RejectsAndExitsNonZero() =>
        AssertValidateCombinationRejected("--no-config");

    /// <summary>
    ///     Asserts that Main rejects validation combined with the given option: usage error on standard error and exit code one.
    /// </summary>
    /// <param name="option">The option that is combined with validation.</param>
    private static void AssertValidateCombinationRejected(string option)
    {
        // Arrange: capture stderr produced by Main
        var originalError = Console.Error;
        try
        {
            using var errorWriter = new StringWriter();
            Console.SetError(errorWriter);

            // Act: invoke Main with the invalid combination
            var exitCode = Program.Main(["--validate", option]);

            // Assert: the combination rule was reported on standard error and the exit code is one
            Assert.Equal(1, exitCode);
            Assert.Contains("--validate cannot be combined with", errorWriter.ToString());
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     Test that concurrent writers on one context keep the output intact end to end.
    /// </summary>
    [Fact]
    public void CliSubsystem_OutputFlow_ConcurrentWriters_KeepsLinesIntactAndSetsExitCode()
    {
        // Arrange: a context writing to a log file
        using var directory = new TemporaryDirectory();
        var logFile = directory.PathOf("out.log");

        // Act: write mixed lines and errors from many threads
        int exitCode;
        using (var context = Context.Create(["--silent", "--log", logFile]))
        {
            Parallel.For(0, 200, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
            {
                if (i % 2 == 0)
                {
                    context.WriteLine($"info-{i}");
                }
                else
                {
                    context.WriteError($"error-{i}");
                }
            });
            exitCode = context.ExitCode;
        }

        // Assert: all lines present and intact; the exit code reflects the errors
        var lines = File.ReadAllLines(logFile);
        Assert.Equal(1, exitCode);
        Assert.Equal(200, lines.Length);
        Assert.All(lines, l => Assert.Matches(@"^(info|error)-\d+$", l));
    }
}
