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

using System.Collections.Immutable;

namespace DemaConsulting.LintMark.Cli;

/// <summary>
///     Context class that handles command-line arguments and program output.
/// </summary>
/// <remarks>
///     <para>
///         Argument properties are immutable after <see cref="Create"/>. Output (<see cref="WriteLine"/>,
///         <see cref="WriteError"/>), the error flag behind <see cref="ExitCode"/> and <see cref="Dispose"/>
///         are guarded by one lock, so a context may be written from several threads without
///         interleaving lines on the console or in the log file. The lock is a defensive measure: the
///         engine itself reports results from a single thread after its workers have finished.
///     </para>
/// </remarks>
internal sealed class Context : IDisposable
{
    /// <summary>
    ///     Guards console output, log file output, the error flag and disposal.
    /// </summary>
    private readonly Lock _outputLock = new();

    /// <summary>
    ///     Log file stream writer (if logging is enabled); opened by the first write, never by <see cref="Create"/>.
    /// </summary>
    private StreamWriter? _logWriter;

    /// <summary>
    ///     Indicates whether the context has been disposed (the log file is never reopened afterwards).
    /// </summary>
    private bool _disposed;

    /// <summary>
    ///     Indicates whether errors have been reported.
    /// </summary>
    private bool _hasErrors;

    /// <summary>
    ///     Gets a value indicating whether the version flag was specified.
    /// </summary>
    public bool Version { get; private init; }

    /// <summary>
    ///     Gets a value indicating whether the help flag was specified.
    /// </summary>
    public bool Help { get; private init; }

    /// <summary>
    ///     Gets a value indicating whether the silent flag was specified.
    /// </summary>
    public bool Silent { get; private init; }

    /// <summary>
    ///     Gets a value indicating whether the validate flag was specified.
    /// </summary>
    public bool Validate { get; private init; }

    /// <summary>
    ///     Gets a value indicating whether the check flag was specified.
    /// </summary>
    /// <remarks>
    ///     Check mode reports problems without writing any file. It is the default behavior, so the
    ///     flag is currently an explicit statement of the default and never changes processing.
    /// </remarks>
    public bool Check { get; private init; }

    /// <summary>
    ///     Gets the positional path arguments (files, directories, globs and <c>!</c> excludes) in the order given.
    /// </summary>
    public IReadOnlyList<string> Paths { get; private init; } = ImmutableArray<string>.Empty;

    /// <summary>
    ///     Gets the validation results file path.
    /// </summary>
    public string? ResultsFile { get; private init; }

    /// <summary>
    ///     Gets the full path of the log file, or <see langword="null"/> when logging is not enabled.
    /// </summary>
    /// <remarks>
    ///     The file is not touched until the first line is written, so a caller can compare this path with
    ///     the files it is about to read and refuse the run before an input file would be overwritten.
    /// </remarks>
    public string? LogFile { get; private init; }

    /// <summary>
    ///     Gets the heading depth for markdown output (default is 1).
    /// </summary>
    public int HeadingDepth { get; private init; } = 1;

    /// <summary>
    ///     Gets the proposed exit code for the application (0 for success, 1 for errors).
    /// </summary>
    public int ExitCode
    {
        get
        {
            lock (_outputLock)
            {
                return _hasErrors ? 1 : 0;
            }
        }
    }

    /// <summary>
    ///     Private constructor - use Create factory method instead.
    /// </summary>
    private Context()
    {
    }

    /// <summary>
    ///     Creates a Context instance from command-line arguments.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>A new Context instance.</returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when arguments are invalid, including when <c>--validate</c> is combined with paths or <c>--check</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the specified log file path is unusable (invalid, a directory, or in a missing directory).
    ///     The file itself is not opened here; see <see cref="WriteLine"/>.
    /// </exception>
    public static Context Create(string[] args)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(args);

        var parser = new ArgumentParser();
        parser.ParseArguments(args);

        // Self-validation is a separate mode: lint arguments would be silently ignored, so reject them
        if (parser.Validate && (parser.Check || parser.Paths.Count > 0))
        {
            throw new ArgumentException("--validate cannot be combined with paths or --check", nameof(args));
        }

        // Check the log path now but do not open it: opening truncates, and the file may turn out to be one of the
        // inputs, which the caller has to be able to detect (and refuse) before it is overwritten
        var logFile = parser.LogFile != null ? ResolveLogFile(parser.LogFile) : null;

        return new Context
        {
            Version = parser.Version,
            Help = parser.Help,
            Silent = parser.Silent,
            Validate = parser.Validate,
            Check = parser.Check,
            Paths = parser.Paths.ToImmutableArray(),
            ResultsFile = parser.ResultsFile,
            LogFile = logFile,
            HeadingDepth = parser.HeadingDepth
        };
    }

    /// <summary>
    ///     Resolves and checks the log file path without touching the file.
    /// </summary>
    /// <param name="logFile">Log file path as given.</param>
    /// <returns>The full path.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the path cannot be a log file.</exception>
    private static string ResolveLogFile(string logFile)
    {
        try
        {
            var full = Path.GetFullPath(logFile);
            if (Directory.Exists(full))
            {
                throw new IOException("The path is a directory");
            }

            var directory = Path.GetDirectoryName(full);
            if (directory != null && !Directory.Exists(directory))
            {
                throw new DirectoryNotFoundException("The directory does not exist");
            }

            return full;
        }
        // Generic catch is justified here to wrap any file system exception with context.
        // Expected exceptions include IOException, ArgumentException, NotSupportedException and
        // other path-related exceptions.
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to open log file '{logFile}': {ex.Message}", ex);
        }
    }

    /// <summary>
    ///     Opens the log file for writing (creating it, or replacing its content).
    /// </summary>
    /// <param name="logFile">Full log file path.</param>
    /// <returns>The writer.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the file cannot be opened.</exception>
    private static StreamWriter OpenLogFile(string logFile)
    {
        try
        {
            // Open with AutoFlush enabled so log entries are immediately written to disk
            // even if the application terminates unexpectedly before Dispose is called
            return new StreamWriter(logFile, append: false) { AutoFlush = true };
        }
        // Generic catch is justified here to wrap any file system exception with context.
        // Expected exceptions include IOException, UnauthorizedAccessException, ArgumentException,
        // NotSupportedException, and other file system-related exceptions.
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to open log file '{logFile}': {ex.Message}", ex);
        }
    }

    /// <summary>
    ///     Writes a line to the log file, opening it first if this is the first line.
    /// </summary>
    /// <param name="message">The message to write.</param>
    /// <remarks>
    ///     Must be called with <c>_outputLock</c> held. Does nothing when logging is not enabled or after disposal.
    /// </remarks>
    private void WriteToLog(string message)
    {
        if (LogFile == null || _disposed)
        {
            return;
        }

        _logWriter ??= OpenLogFile(LogFile);
        _logWriter.WriteLine(message);
    }

    /// <summary>
    ///     Helper class for parsing command-line arguments
    /// </summary>
    private sealed class ArgumentParser
    {
        /// <summary>
        ///     Gets a value indicating whether the version flag was specified.
        /// </summary>
        public bool Version { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether the help flag was specified.
        /// </summary>
        public bool Help { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether the silent flag was specified.
        /// </summary>
        public bool Silent { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether the validate flag was specified.
        /// </summary>
        public bool Validate { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether the check flag was specified.
        /// </summary>
        public bool Check { get; private set; }

        /// <summary>
        ///     Gets the positional path arguments in the order given.
        /// </summary>
        public List<string> Paths { get; } = [];

        /// <summary>
        ///     Gets the log file path.
        /// </summary>
        public string? LogFile { get; private set; }

        /// <summary>
        ///     Gets the validation results file path.
        /// </summary>
        public string? ResultsFile { get; private set; }

        /// <summary>
        ///     Gets the heading depth for markdown output.
        /// </summary>
        public int HeadingDepth { get; private set; } = 1;

        /// <summary>
        ///     Parses command-line arguments
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        public void ParseArguments(string[] args)
        {
            // Validate input
            ArgumentNullException.ThrowIfNull(args);

            int i = 0;
            while (i < args.Length)
            {
                var arg = args[i++];
                i = ParseArgument(arg, args, i);
            }
        }

        /// <summary>
        ///     Parses a single argument
        /// </summary>
        /// <param name="arg">Argument to parse</param>
        /// <param name="args">All arguments</param>
        /// <param name="index">Current index</param>
        /// <returns>Updated index</returns>
        private int ParseArgument(string arg, string[] args, int index)
        {
            switch (arg)
            {
                case "-v":
                case "--version":
                    Version = true;
                    return index;

                case "-?":
                case "-h":
                case "--help":
                    Help = true;
                    return index;

                case "--silent":
                    Silent = true;
                    return index;

                case "--validate":
                    Validate = true;
                    return index;

                case "--check":
                    Check = true;
                    return index;

                case "--log":
                    LogFile = GetRequiredStringArgument(arg, args, index, "a filename argument");
                    return index + 1;

                case "--results":
                case "--result":
                    ResultsFile = GetRequiredStringArgument(arg, args, index, "a results filename argument");
                    return index + 1;

                case "--depth":
                    HeadingDepth = GetRequiredIntArgument(arg, args, index, "a heading depth argument", 1, 6);
                    return index + 1;

                default:
                    // Anything that does not start with a dash is a path (file, directory, glob or ! exclude);
                    // every other dashed token (including a lone dash) is an unsupported option
                    if (arg.Length > 0 && arg[0] != '-')
                    {
                        Paths.Add(arg);
                        return index;
                    }

                    throw new ArgumentException($"Unsupported argument '{arg}'", nameof(args));
            }
        }

        /// <summary>
        ///     Gets a required string argument value
        /// </summary>
        /// <param name="arg">Argument name</param>
        /// <param name="args">All arguments</param>
        /// <param name="index">Current index</param>
        /// <param name="description">Description of what's required</param>
        /// <returns>Argument value</returns>
        private static string GetRequiredStringArgument(string arg, string[] args, int index, string description)
        {
            if (index >= args.Length)
            {
                throw new ArgumentException($"{arg} requires {description}", nameof(args));
            }

            return args[index];
        }

        /// <summary>
        ///     Gets a required integer argument value
        /// </summary>
        /// <param name="arg">Argument name</param>
        /// <param name="args">All arguments</param>
        /// <param name="index">Current index</param>
        /// <param name="description">Description of what's required</param>
        /// <param name="min">Minimum valid value (inclusive)</param>
        /// <param name="max">Maximum valid value (inclusive)</param>
        /// <returns>Argument value as an integer in [min, max]</returns>
        private static int GetRequiredIntArgument(string arg, string[] args, int index, string description, int min = 1, int max = int.MaxValue)
        {
            var value = GetRequiredStringArgument(arg, args, index, description);
            if (!int.TryParse(value, out var result) || result < min || result > max)
            {
                throw new ArgumentException($"{arg} requires an integer between {min} and {max} for {description}", nameof(args));
            }

            return result;
        }
    }

    /// <summary>
    ///     Writes a line of output to the console and log file (if logging is enabled).
    /// </summary>
    /// <param name="message">The message to write.</param>
    /// <remarks>
    ///     Output is written to stdout. When <see cref="Silent"/> is <c>true</c>, stdout output is
    ///     suppressed, but the message is still written to the log file when logging is enabled. The log file is
    ///     created (or its content replaced) by the first line written, not by <see cref="Create"/>.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown by the first write when the log file cannot be opened.</exception>
    public void WriteLine(string message)
    {
        // Serialize so concurrent callers cannot interleave lines on the console or in the log
        lock (_outputLock)
        {
            // Write to console unless silent mode is enabled
            if (!Silent)
            {
                Console.WriteLine(message);
            }

            // Write to log file if logging is enabled
            WriteToLog(message);
        }
    }

    /// <summary>
    ///     Writes an error message to the error console and log file (if logging is enabled).
    /// </summary>
    /// <param name="message">The error message to write.</param>
    /// <remarks>
    ///     <c>_hasErrors</c> is set to <c>true</c> unconditionally, so <see cref="ExitCode"/> will
    ///     return 1 regardless of whether <see cref="Silent"/> suppresses the console output.
    ///     Stderr output is suppressed when <see cref="Silent"/> is <c>true</c>, but the message
    ///     is still written to the log file when one is open.
    /// </remarks>
    public void WriteError(string message)
    {
        // Serialize so concurrent callers cannot interleave lines or tear the console color changes
        lock (_outputLock)
        {
            // Mark that we have encountered errors
            _hasErrors = true;

            // Write to error console unless silent mode is enabled
            if (!Silent)
            {
                var previousColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine(message);
                Console.ForegroundColor = previousColor;
            }

            // Write to log file if logging is enabled
            WriteToLog(message);
        }
    }

    /// <summary>
    ///     Disposes resources used by the Context.
    /// </summary>
    public void Dispose()
    {
        // Close and dispose the log file writer if it exists (not while another thread is writing to it)
        lock (_outputLock)
        {
            _disposed = true;
            _logWriter?.Dispose();
            _logWriter = null;
        }
    }
}
