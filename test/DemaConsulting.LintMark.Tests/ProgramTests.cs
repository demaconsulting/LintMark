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
///     Unit tests for the Program class.
/// </summary>
[Collection("Sequential")]
public class ProgramTests
{
    /// <summary>
    ///     Test that Run with version flag displays version only.
    /// </summary>
    [Fact]
    public void Program_Run_WithVersionFlag_DisplaysVersionOnly()
    {
        // Arrange: setup test conditions
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create(["--version"]);

            // Act: execute the operation being tested
            Program.Run(context);

            // Assert: verify expected behavior
            var output = outWriter.ToString();
            Assert.Contains(Program.Version, output);
            Assert.DoesNotContain("Copyright", output);
            Assert.DoesNotContain("LintMark version", output);
            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that Run with help flag displays usage information.
    /// </summary>
    [Fact]
    public void Program_Run_WithHelpFlag_DisplaysUsageInformation()
    {
        // Arrange: setup test conditions
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create(["--help"]);

            // Act: execute the operation being tested
            Program.Run(context);

            // Assert: verify expected behavior
            var output = outWriter.ToString();
            Assert.Contains("Usage:", output);
            Assert.Contains("Options:", output);
            Assert.Contains("--version", output);
            Assert.Contains("--help", output);
            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that Run with validate flag runs validation.
    /// </summary>
    [Fact]
    public void Program_Run_WithValidateFlag_RunsValidation()
    {
        // Arrange: setup test conditions
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create(["--validate"]);

            // Act: execute the operation being tested
            Program.Run(context);

            // Assert: verify expected behavior
            var output = outWriter.ToString();
            Assert.Contains("Total Tests:", output);
            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that Run with no arguments displays default behavior.
    /// </summary>
    [Fact]
    public void Program_Run_NoArguments_DisplaysDefaultBehavior()
    {
        // Arrange: setup test conditions
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create([]);

            // Act: execute the operation being tested
            Program.Run(context);

            // Assert: verify expected behavior
            var output = outWriter.ToString();
            Assert.Contains("LintMark version", output);
            Assert.Contains("Copyright", output);
            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that version property returns non-empty version string.
    /// </summary>
    [Fact]
    public void Program_Version_ReturnsNonEmptyString()
    {
        // Act: execute the operation being tested
        var version = Program.Version;

        // Assert: verify expected behavior
        Assert.False(string.IsNullOrWhiteSpace(version));
    }

    /// <summary>
    ///     Test that Main with invalid arguments returns non-zero exit code.
    /// </summary>
    [Fact]
    public void Program_Main_WithInvalidArgs_ReturnsNonZeroExitCode()
    {
        // Arrange: redirect stderr to suppress error output during test
        var originalError = Console.Error;
        try
        {
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);

            // Act: invoke Main with an invalid argument
            var result = Program.Main(["--invalid-argument"]);

            // Assert: invalid arguments produce a non-zero exit code
            Assert.Equal(1, result);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     Test that Run with short version flag -v displays version.
    /// </summary>
    [Fact]
    public void Program_Run_WithShortVersionFlag_DisplaysVersion()
    {
        // Arrange: setup test conditions
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create(["-v"]);

            // Act: execute the operation being tested
            Program.Run(context);

            // Assert: verify expected behavior
            var output = outWriter.ToString();
            Assert.Contains(Program.Version, output);
            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that Run with short help flag -h displays usage.
    /// </summary>
    [Fact]
    public void Program_Run_WithShortHelpFlag_DisplaysUsage()
    {
        // Arrange: setup test conditions
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create(["-h"]);

            // Act: execute the operation being tested
            Program.Run(context);

            // Assert: verify expected behavior
            var output = outWriter.ToString();
            Assert.Contains("Usage:", output);
            Assert.Contains("Options:", output);
            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that Run with short help flag -? displays usage.
    /// </summary>
    [Fact]
    public void Program_Run_WithQuestionMarkFlag_DisplaysUsage()
    {
        // Arrange: setup test conditions
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create(["-?"]);

            // Act: execute the operation being tested
            Program.Run(context);

            // Assert: verify expected behavior
            var output = outWriter.ToString();
            Assert.Contains("Usage:", output);
            Assert.Contains("Options:", output);
            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that Run with help flag lists the paths argument and the check option.
    /// </summary>
    [Fact]
    public void Program_Run_WithHelpFlag_ListsCheckOptionAndPaths()
    {
        // Arrange: setup test conditions
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create(["--help"]);

            // Act: execute the operation being tested
            Program.Run(context);

            // Assert: verify expected behavior
            var output = outWriter.ToString();
            Assert.Contains("[paths...]", output);
            Assert.Contains("--check", output);
            Assert.Contains("**/*.md", output);
            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that Run with a path argument runs the engine on that file.
    /// </summary>
    [Fact]
    public void Program_Run_WithPathArgument_RunsEngine()
    {
        // Arrange: a valid file and an invalid file; capture the error stream
        using var directory = new TemporaryDirectory();
        var good = directory.WriteFile("good.md");
        var bad = directory.WriteBytes("bad.md", [0xFF]);
        var originalOut = Console.Out;
        var originalError = Console.Error;
        try
        {
            using var outWriter = new StringWriter();
            using var errorWriter = new StringWriter();
            Console.SetOut(outWriter);
            Console.SetError(errorWriter);
            using var cleanContext = Context.Create([good]);
            using var failingContext = Context.Create([bad]);

            // Act: execute the operation being tested
            Program.Run(cleanContext);
            Program.Run(failingContext);

            // Assert: the valid file is clean; the invalid file is reported and sets the exit code
            Assert.Equal(0, cleanContext.ExitCode);
            Assert.Equal(1, failingContext.ExitCode);
            Assert.Contains("file-encoding-error", errorWriter.ToString());
            Assert.DoesNotContain("not yet implemented", outWriter.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     Test that Run refuses a log file that is also an input before the banner opens (and so truncates) it.
    /// </summary>
    [Fact]
    public void Program_Run_LogFileIsSelectedInput_ThrowsBeforeAnyOutputAndKeepsFile()
    {
        // Arrange: an existing source file and a not yet existing file, each named as log and as input
        using var directory = new TemporaryDirectory();
        var existing = directory.WriteFile("README.md", "# Precious\n");
        var missing = directory.PathOf("new.md");
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var existingContext = Context.Create(["--log", existing, existing]);
            using var missingContext = Context.Create(["--log", missing, missing]);

            // Act / Assert: both runs are refused
            Assert.Throws<InvalidOperationException>(() => Program.Run(existingContext));
            Assert.Throws<InvalidOperationException>(() => Program.Run(missingContext));

            // Assert: nothing was written to the console, the source is intact and no file was created
            Assert.Empty(outWriter.ToString());
            Assert.Equal("# Precious\n", File.ReadAllText(existing));
            Assert.False(File.Exists(missing));
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that Main reports a log file that is also an input as an error with exit code one.
    /// </summary>
    [Fact]
    public void Program_Main_LogFileIsSelectedInput_ReturnsOneAndKeepsFile()
    {
        // Arrange: an existing source file named as the log and as the input
        using var directory = new TemporaryDirectory();
        var existing = directory.WriteFile("README.md", "# Precious\n");
        var originalError = Console.Error;
        try
        {
            using var errorWriter = new StringWriter();
            Console.SetError(errorWriter);

            // Act: run the entry point
            var exitCode = Program.Main(["--silent", "--log", existing, existing]);

            // Assert: failure with an explanation, and the file is untouched
            Assert.Equal(1, exitCode);
            Assert.Contains("refusing to overwrite", errorWriter.ToString());
            Assert.Equal("# Precious\n", File.ReadAllText(existing));
        }
        finally
        {
            Console.SetError(originalError);
        }
    }
}

