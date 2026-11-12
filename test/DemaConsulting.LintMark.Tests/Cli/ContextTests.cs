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

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Unit tests for the Context class.
/// </summary>
[Collection("Sequential")]
public class ContextTests
{
    /// <summary>
    ///     Test creating a context with no arguments.
    /// </summary>
    [Fact]
    public void Context_Create_NoArguments_ReturnsDefaultContext()
    {
        // Act: execute the operation being tested
        using var context = Context.Create([]);

        // Assert: verify expected behavior
        Assert.False(context.Version);
        Assert.False(context.Help);
        Assert.False(context.Silent);
        Assert.False(context.Validate);
        Assert.Null(context.ResultsFile);
        Assert.Equal(1, context.HeadingDepth);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test creating a context with the version flag.
    /// </summary>
    [Fact]
    public void Context_Create_VersionFlag_SetsVersionTrue()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["--version"]);

        // Assert: verify expected behavior
        Assert.True(context.Version);
        Assert.False(context.Help);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test creating a context with the short version flag.
    /// </summary>
    [Fact]
    public void Context_Create_ShortVersionFlag_SetsVersionTrue()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["-v"]);

        // Assert: verify expected behavior
        Assert.True(context.Version);
        Assert.False(context.Help);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test creating a context with the help flag.
    /// </summary>
    [Fact]
    public void Context_Create_HelpFlag_SetsHelpTrue()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["--help"]);

        // Assert: verify expected behavior
        Assert.False(context.Version);
        Assert.True(context.Help);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test creating a context with the short help flag -h.
    /// </summary>
    [Fact]
    public void Context_Create_ShortHelpFlag_H_SetsHelpTrue()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["-h"]);

        // Assert: verify expected behavior
        Assert.False(context.Version);
        Assert.True(context.Help);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test creating a context with the short help flag -?.
    /// </summary>
    [Fact]
    public void Context_Create_ShortHelpFlag_Question_SetsHelpTrue()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["-?"]);

        // Assert: verify expected behavior
        Assert.False(context.Version);
        Assert.True(context.Help);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test creating a context with the silent flag.
    /// </summary>
    [Fact]
    public void Context_Create_SilentFlag_SetsSilentTrue()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["--silent"]);

        // Assert: verify expected behavior
        Assert.True(context.Silent);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test creating a context with the validate flag.
    /// </summary>
    [Fact]
    public void Context_Create_ValidateFlag_SetsValidateTrue()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["--validate"]);

        // Assert: verify expected behavior
        Assert.True(context.Validate);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test creating a context with the results flag.
    /// </summary>
    [Fact]
    public void Context_Create_ResultsFlag_SetsResultsFile()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["--results", "test.trx"]);

        // Assert: verify expected behavior
        Assert.Equal("test.trx", context.ResultsFile);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test creating a context with the log flag.
    /// </summary>
    [Fact]
    public void Context_Create_LogFlag_OpensLogFile()
    {
        // Arrange: setup test conditions
        var logFile = Path.GetTempFileName();
        try
        {
            // Act: execute the operation being tested
            using (var context = Context.Create(["--log", logFile]))
            {
                context.WriteLine("Test message");
                Assert.Equal(0, context.ExitCode);
            }

            // Assert: verify expected behavior
            // Verify log file was written
            Assert.True(File.Exists(logFile));
            var logContent = File.ReadAllText(logFile);
            Assert.Contains("Test message", logContent);
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
    ///     Test creating a context with an unknown argument throws exception.
    /// </summary>
    [Fact]
    public void Context_Create_UnknownArgument_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["--unknown"]));
        Assert.Contains("Unsupported argument", exception.Message);
    }

    /// <summary>
    ///     Test creating a context with --log flag but no value throws exception.
    /// </summary>
    [Fact]
    public void Context_Create_LogFlag_WithoutValue_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["--log"]));
        Assert.Contains("--log", exception.Message);
    }

    /// <summary>
    ///     Test creating a context with --results flag but no value throws exception.
    /// </summary>
    [Fact]
    public void Context_Create_ResultsFlag_WithoutValue_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["--results"]));
        Assert.Contains("--results", exception.Message);
    }

    /// <summary>
    ///     Test creating a context with the --result alias flag (legacy alias for --results).
    /// </summary>
    [Fact]
    public void Context_Create_ResultAliasFlag_SetsResultsFile()
    {
        // Act: execute the operation using the legacy --result alias
        using var context = Context.Create(["--result", "test.trx"]);

        // Assert: verify --result sets ResultsFile identically to --results
        Assert.Equal("test.trx", context.ResultsFile);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test creating a context with --result flag but no value throws exception.
    /// </summary>
    [Fact]
    public void Context_Create_ResultAliasFlag_WithoutValue_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["--result"]));
        Assert.Contains("--result", exception.Message);
    }

    /// <summary>
    ///     Test creating a context with the depth flag.
    /// </summary>
    [Fact]
    public void Context_Create_DepthFlag_SetsHeadingDepth()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["--depth", "3"]);

        // Assert: verify expected behavior
        Assert.Equal(3, context.HeadingDepth);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test creating a context with no depth flag returns default heading depth of 1.
    /// </summary>
    [Fact]
    public void Context_Create_NoDepthFlag_ReturnsDefaultHeadingDepth()
    {
        // Act: execute the operation being tested
        using var context = Context.Create([]);

        // Assert: verify default depth is 1
        Assert.Equal(1, context.HeadingDepth);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test creating a context with --depth flag but no value throws exception.
    /// </summary>
    [Fact]
    public void Context_Create_DepthFlag_WithoutValue_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["--depth"]));
        Assert.Contains("--depth", exception.Message);
    }

    /// <summary>
    ///     Test creating a context with --depth flag and non-integer value throws exception.
    /// </summary>
    [Fact]
    public void Context_Create_DepthFlag_NonIntegerValue_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["--depth", "abc"]));
        Assert.Contains("--depth", exception.Message);
    }

    /// <summary>
    ///     Test creating a context with --depth flag and zero value throws exception.
    /// </summary>
    [Fact]
    public void Context_Create_DepthFlag_ZeroValue_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["--depth", "0"]));
        Assert.Contains("--depth", exception.Message);
    }

    /// <summary>
    ///     Test creating a context with --depth flag and value exceeding maximum throws exception.
    /// </summary>
    [Fact]
    public void Context_Create_DepthFlag_ExceedsMaxValue_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["--depth", "7"]));
        Assert.Contains("--depth", exception.Message);
    }

    /// <summary>
    ///     Test WriteLine writes to console output when not silent.
    /// </summary>
    [Fact]
    public void Context_WriteLine_NotSilent_WritesToConsole()
    {
        // Arrange: setup test conditions
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create([]);

            // Act: execute the operation being tested
            context.WriteLine("Test message");

            // Assert: verify expected behavior
            var output = outWriter.ToString();
            Assert.Contains("Test message", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test WriteLine does not write to console when silent.
    /// </summary>
    [Fact]
    public void Context_WriteLine_Silent_DoesNotWriteToConsole()
    {
        // Arrange: setup test conditions
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create(["--silent"]);

            // Act: execute the operation being tested
            context.WriteLine("Test message");

            // Assert: verify expected behavior
            var output = outWriter.ToString();
            Assert.DoesNotContain("Test message", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test WriteError does not write to console when silent.
    /// </summary>
    [Fact]
    public void Context_WriteError_Silent_DoesNotWriteToConsole()
    {
        // Arrange: setup test conditions
        var originalError = Console.Error;
        try
        {
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);
            using var context = Context.Create(["--silent"]);

            // Act: execute the operation being tested
            context.WriteError("Test error message");

            // Assert - error output should be suppressed in silent mode
            var output = errWriter.ToString();
            Assert.DoesNotContain("Test error message", output);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     Test WriteError sets exit code to 1.
    /// </summary>
    [Fact]
    public void Context_WriteError_SetsErrorExitCode()
    {
        // Arrange: setup test conditions
        var originalError = Console.Error;
        try
        {
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);
            using var context = Context.Create([]);

            // Act: execute the operation being tested
            context.WriteError("Test error message");

            // Assert: verify expected behavior
            Assert.Equal(1, context.ExitCode);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     Test WriteError writes message to console when not silent.
    /// </summary>
    [Fact]
    public void Context_WriteError_NotSilent_WritesToConsole()
    {
        // Arrange: setup test conditions
        var originalError = Console.Error;
        try
        {
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);
            using var context = Context.Create([]);

            // Act: execute the operation being tested
            context.WriteError("Test error message");

            // Assert: verify expected behavior
            var output = errWriter.ToString();
            Assert.Contains("Test error message", output);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     Test WriteError writes message to log file when logging is enabled.
    /// </summary>
    [Fact]
    public void Context_WriteError_WritesToLogFile()
    {
        // Arrange: setup test conditions
        var logFile = Path.GetTempFileName();
        try
        {
            // Act - use silent to avoid console output; verify the error still goes to the log
            using (var context = Context.Create(["--silent", "--log", logFile]))
            {
                context.WriteError("Test error in log");
                Assert.Equal(1, context.ExitCode);
            }

            // Assert - log file should contain the error message
            Assert.True(File.Exists(logFile));
            var logContent = File.ReadAllText(logFile);
            Assert.Contains("Test error in log", logContent);
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
    ///     Test creating a context with --log flag pointing to an invalid path throws InvalidOperationException.
    /// </summary>
    [Fact]
    public void Context_Create_LogFlag_InvalidPath_ThrowsInvalidOperationException()
    {
        // Arrange: a path that cannot be opened as a file (directory or invalid characters)
        // Use a directory path so it cannot be opened as a file
        var invalidLogPath = Path.GetTempPath(); // temp directory itself, not a file

        // Act & Assert: the failure is wrapped with the path and keeps the original exception
        var exception = Assert.Throws<InvalidOperationException>(() => Context.Create(["--log", invalidLogPath]));
        Assert.Contains("Failed to open log file", exception.Message);
        Assert.Contains(invalidLogPath, exception.Message);
        Assert.NotNull(exception.InnerException);
    }

    /// <summary>
    ///     Test that a log path in a directory that does not exist is reported as a wrapped failure.
    /// </summary>
    [Fact]
    public void Context_Create_LogFlag_MissingDirectory_ThrowsInvalidOperationException()
    {
        // Arrange: a log path below a directory that does not exist
        using var directory = new TemporaryDirectory();
        var logFile = directory.PathOf("missing/dir/run.log");

        // Act / Assert: the failure is wrapped, and nothing is created
        var exception = Assert.Throws<InvalidOperationException>(() => Context.Create(["--log", logFile]));
        Assert.Contains("Failed to open log file", exception.Message);
        Assert.False(Directory.Exists(directory.PathOf("missing")));
    }

    /// <summary>
    ///     Test that an existing file named as the log is truncated when the context is created, as in the other tools.
    /// </summary>
    [Fact]
    public void Context_Create_LogFlag_ExistingFile_TruncatesFile()
    {
        // Arrange: an existing file with content
        using var directory = new TemporaryDirectory();
        var logFile = directory.WriteFile("existing.md", "# Existing\n");

        // Act: create the context, and inspect the file before anything is written
        long lengthAfterCreate;
        using (var context = Context.Create(["--silent", "--log", logFile]))
        {
            lengthAfterCreate = new FileInfo(logFile).Length;
            context.WriteLine("log line");
        }

        // Assert: the content was replaced when the context was created, and the log holds only the new line
        Assert.Equal(0, lengthAfterCreate);
        Assert.Equal("log line" + Environment.NewLine, File.ReadAllText(logFile));
    }

    /// <summary>
    ///     Test that Dispose closes the log file, so it can be deleted, and that later writes do not fail.
    /// </summary>
    [Fact]
    public void Context_Dispose_ClosesLogFile()
    {
        // Arrange: a context that has written a line
        using var directory = new TemporaryDirectory();
        var logFile = directory.PathOf("run.log");
        var context = Context.Create(["--silent", "--log", logFile]);
        context.WriteLine("first");

        // Act: dispose, then write again
        context.Dispose();
        context.WriteLine("second");

        // Assert: the content is what was written before disposal, and the closed file can be deleted
        Assert.Equal("first" + Environment.NewLine, File.ReadAllText(logFile));
        File.Delete(logFile);
        Assert.False(File.Exists(logFile));
    }

    /// <summary>
    ///     Test that the exposed path list cannot be changed through a cast.
    /// </summary>
    [Fact]
    public void Context_Paths_CastToMutableTypes_CannotBeModified()
    {
        // Arrange / Act: create a context with paths
        using var context = Context.Create(["a.md", "docs", "!docs/old"]);

        // Assert: a caller cannot change the list
        ReadOnlyAssertions.AssertCannotBeModified(context.Paths);
        Assert.Equal(new[] { "a.md", "docs", "!docs/old" }, context.Paths);
    }

    /// <summary>
    ///     Test creating a context with positional arguments sets the paths in the order given.
    /// </summary>
    [Fact]
    public void Context_Create_PositionalPaths_SetsPathsInOrder()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["docs/**/*.md", "README.md", "!docs/generated/**"]);

        // Assert: paths are kept in order, including the exclude pattern, and no flag is set
        Assert.Equal(["docs/**/*.md", "README.md", "!docs/generated/**"], context.Paths);
        Assert.False(context.Check);
        Assert.False(context.Validate);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test creating a context without positional arguments leaves the paths empty.
    /// </summary>
    [Fact]
    public void Context_Create_NoPaths_PathsEmpty()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["--silent"]);

        // Assert: no paths
        Assert.Empty(context.Paths);
    }

    /// <summary>
    ///     Test creating a context with the check flag.
    /// </summary>
    [Fact]
    public void Context_Create_CheckFlag_SetsCheckTrue()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["--check"]);

        // Assert: check is set, nothing else changes
        Assert.True(context.Check);
        Assert.Empty(context.Paths);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test that paths can be mixed with flags in any order and that option values are not mistaken for paths.
    /// </summary>
    [Fact]
    public void Context_Create_PathsMixedWithFlags_ParsesBoth()
    {
        // Arrange: a log file whose name must not be taken as a path
        var logFile = Path.Combine(Path.GetTempPath(), $"context_paths_{Guid.NewGuid()}.log");
        try
        {
            // Act: execute the operation being tested
            using var context = Context.Create(["a.md", "--silent", "--log", logFile, "--check", "b.md"]);

            // Assert: only the positional arguments are paths
            Assert.Equal(["a.md", "b.md"], context.Paths);
            Assert.True(context.Silent);
            Assert.True(context.Check);
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
    ///     Test that version and help may be combined with paths (they take priority in the program).
    /// </summary>
    [Fact]
    public void Context_Create_HelpOrVersionWithPaths_DoesNotThrow()
    {
        // Act: execute the operation being tested
        using var help = Context.Create(["--help", "a.md"]);
        using var version = Context.Create(["a.md", "--version", "--check"]);

        // Assert: both are accepted
        Assert.True(help.Help);
        Assert.True(version.Version);
        Assert.Equal(["a.md"], version.Paths);
    }

    /// <summary>
    ///     Test that combining the validate flag with paths is a usage error.
    /// </summary>
    [Fact]
    public void Context_Create_ValidateWithPaths_ThrowsArgumentException()
    {
        // Act & Assert: the combination is rejected with a message naming the flag
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["--validate", "docs"]));
        Assert.Contains("--validate", exception.Message);
    }

    /// <summary>
    ///     Test that combining the validate flag with the check flag is a usage error.
    /// </summary>
    [Fact]
    public void Context_Create_ValidateWithCheck_ThrowsArgumentException()
    {
        // Act & Assert: the combination is rejected
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["--check", "--validate"]));
        Assert.Contains("--check", exception.Message);
    }

    /// <summary>
    ///     Test that the exclusivity check happens before a log file is created.
    /// </summary>
    [Fact]
    public void Context_Create_ValidateWithPaths_DoesNotCreateLogFile()
    {
        // Arrange: a log path that does not exist yet
        var logFile = Path.Combine(Path.GetTempPath(), $"context_exclusive_{Guid.NewGuid()}.log");

        // Act & Assert: the usage error is raised and the log file is never created
        Assert.Throws<ArgumentException>(() => Context.Create(["--validate", "--log", logFile, "a.md"]));
        Assert.False(File.Exists(logFile));
    }

    /// <summary>
    ///     Test that validate still combines with its own options.
    /// </summary>
    [Fact]
    public void Context_Create_ValidateWithItsOwnOptions_DoesNotThrow()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["--validate", "--silent", "--depth", "2", "--results", "out.trx"]);

        // Assert: parsed as before
        Assert.True(context.Validate);
        Assert.Equal(2, context.HeadingDepth);
        Assert.Empty(context.Paths);
    }

    /// <summary>
    ///     Test that the config option exposes the file it was given.
    /// </summary>
    [Fact]
    public void Context_Create_ConfigFlag_SetsConfigFile()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["--config", "conf/lint.yaml", "a.md"]);

        // Assert: the value is exposed as written and is not mistaken for a path
        Assert.Equal("conf/lint.yaml", context.ConfigFile);
        Assert.False(context.NoConfig);
        Assert.Equal(["a.md"], context.Paths);
    }

    /// <summary>
    ///     Test that the config option without a value is a usage error.
    /// </summary>
    [Fact]
    public void Context_Create_ConfigFlag_WithoutValue_ThrowsArgumentException()
    {
        // Act & Assert: the missing value is reported by name
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["--config"]));
        Assert.Contains("--config requires a configuration filename argument", exception.Message);
    }

    /// <summary>
    ///     Test that a blank value for the config option is a usage error.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Context_Create_ConfigFlag_BlankValue_ThrowsArgumentException(string value)
    {
        // Act & Assert: the blank value is rejected like a missing one
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["--config", value]));
        Assert.Contains("--config requires a configuration filename argument", exception.Message);
    }

    /// <summary>
    ///     Test that repeating the config option keeps the last value.
    /// </summary>
    [Fact]
    public void Context_Create_ConfigFlag_Repeated_LastWins()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["--config", "first.yaml", "--config", "second.yaml"]);

        // Assert: the last value is used
        Assert.Equal("second.yaml", context.ConfigFile);
    }

    /// <summary>
    ///     Test that the no-config flag is exposed.
    /// </summary>
    [Fact]
    public void Context_Create_NoConfigFlag_SetsNoConfig()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["--no-config"]);

        // Assert: the flag is set and no configuration file is named
        Assert.True(context.NoConfig);
        Assert.Null(context.ConfigFile);
        Assert.Empty(context.Paths);
    }

    /// <summary>
    ///     Test that neither configuration option is set by default.
    /// </summary>
    [Fact]
    public void Context_Create_NoArguments_ConfigFileNullAndNoConfigFalse()
    {
        // Act: execute the operation being tested
        using var context = Context.Create([]);

        // Assert: both are at their defaults
        Assert.Null(context.ConfigFile);
        Assert.False(context.NoConfig);
    }

    /// <summary>
    ///     Test that naming a configuration file and disabling configuration contradict each other.
    /// </summary>
    [Fact]
    public void Context_Create_ConfigWithNoConfig_ThrowsArgumentException()
    {
        // Act & Assert: the contradiction is a usage error naming both options
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["--config", "a.yaml", "--no-config"]));
        Assert.Contains("--config", exception.Message);
        Assert.Contains("--no-config", exception.Message);
    }

    /// <summary>
    ///     Test that combining the validate flag with the config option is a usage error.
    /// </summary>
    [Fact]
    public void Context_Create_ValidateWithConfig_ThrowsArgumentException()
    {
        // Act & Assert: the combination is rejected with a message naming the option
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["--validate", "--config", "a.yaml"]));
        Assert.Contains("--config", exception.Message);
    }

    /// <summary>
    ///     Test that combining the validate flag with the no-config flag is a usage error.
    /// </summary>
    [Fact]
    public void Context_Create_ValidateWithNoConfig_ThrowsArgumentException()
    {
        // Act & Assert: the combination is rejected with a message naming the flag
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["--no-config", "--validate"]));
        Assert.Contains("--no-config", exception.Message);
    }

    /// <summary>
    ///     Test that version and help may be combined with the configuration options.
    /// </summary>
    [Fact]
    public void Context_Create_ConfigWithHelpOrVersion_DoesNotThrow()
    {
        // Act: execute the operation being tested
        using var help = Context.Create(["--help", "--config", "a.yaml"]);
        using var version = Context.Create(["--no-config", "--version"]);

        // Assert: both are accepted
        Assert.True(help.Help);
        Assert.Equal("a.yaml", help.ConfigFile);
        Assert.True(version.Version);
        Assert.True(version.NoConfig);
    }

    /// <summary>
    ///     Test that the config option combines with paths and other options in any order.
    /// </summary>
    [Fact]
    public void Context_Create_ConfigAndPaths_ParsesBoth()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["a.md", "--config", "c.yaml", "--silent", "b.md"]);

        // Assert: the option value is not a path and both paths are kept
        Assert.Equal("c.yaml", context.ConfigFile);
        Assert.Equal(["a.md", "b.md"], context.Paths);
        Assert.True(context.Silent);
    }

    /// <summary>
    ///     Test that a lone dash and other unknown dashed tokens are still unsupported arguments rather than paths.
    /// </summary>
    [Theory]
    [InlineData("-")]
    [InlineData("-x")]
    [InlineData("--unknown")]
    public void Context_Create_DashedUnknownToken_ThrowsArgumentException(string token)
    {
        // Act & Assert: not a path
        var exception = Assert.Throws<ArgumentException>(() => Context.Create([token]));
        Assert.Contains(token, exception.Message);
    }

    /// <summary>
    ///     Test that an empty positional argument is rejected rather than treated as a path.
    /// </summary>
    [Fact]
    public void Context_Create_EmptyArgument_ThrowsArgumentException()
    {
        // Act & Assert: an empty token is unsupported
        Assert.Throws<ArgumentException>(() => Context.Create([string.Empty]));
    }

    /// <summary>
    ///     Test that concurrent error writers produce intact lines in the log file and set the exit code.
    /// </summary>
    [Fact]
    public void Context_WriteError_ConcurrentWriters_AllLinesWrittenIntact()
    {
        // Arrange: a log file and many long lines
        var logFile = Path.Combine(Path.GetTempPath(), $"context_concurrent_err_{Guid.NewGuid()}.log");
        try
        {
            int exitCode;
            using (var context = Context.Create(["--silent", "--log", logFile]))
            {
                // Act: write from many threads at once
                Parallel.For(0, 400, new ParallelOptions { MaxDegreeOfParallelism = 16 }, i =>
                    context.WriteError($"error-{i}-{new string('x', 300)}"));
                exitCode = context.ExitCode;
            }

            // Assert: every line is present exactly once and intact
            var lines = File.ReadAllLines(logFile);
            Assert.Equal(1, exitCode);
            Assert.Equal(400, lines.Length);
            Assert.Equal(
                Enumerable.Range(0, 400).Select(i => $"error-{i}-{new string('x', 300)}").Order(StringComparer.Ordinal).ToList(),
                lines.Order(StringComparer.Ordinal).ToList());
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
    ///     Test that concurrent line writers produce intact lines in the log file and leave the exit code at zero.
    /// </summary>
    [Fact]
    public void Context_WriteLine_ConcurrentWriters_AllLinesWrittenIntact()
    {
        // Arrange: a log file and many long lines
        var logFile = Path.Combine(Path.GetTempPath(), $"context_concurrent_line_{Guid.NewGuid()}.log");
        try
        {
            int exitCode;
            using (var context = Context.Create(["--silent", "--log", logFile]))
            {
                // Act: write from many threads at once
                Parallel.For(0, 400, new ParallelOptions { MaxDegreeOfParallelism = 16 }, i =>
                    context.WriteLine($"line-{i}-{new string('y', 300)}"));
                exitCode = context.ExitCode;
            }

            // Assert: every line is present exactly once and intact; no error was recorded
            var lines = File.ReadAllLines(logFile);
            Assert.Equal(0, exitCode);
            Assert.Equal(400, lines.Length);
            Assert.Equal(
                Enumerable.Range(0, 400).Select(i => $"line-{i}-{new string('y', 300)}").Order(StringComparer.Ordinal).ToList(),
                lines.Order(StringComparer.Ordinal).ToList());
        }
        finally
        {
            if (File.Exists(logFile))
            {
                File.Delete(logFile);
            }
        }
    }
}


