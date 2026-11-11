## Cli

### Verification Approach

The `Cli` subsystem is verified by integration tests defined in `CliSubsystemTests.cs`. Each
test exercises `Context.Create` and `Program.Run` together, treating the pair as the observable
subsystem interface. Tests pass controlled argument arrays and assert on captured console output,
file-system side-effects, and exit codes. `Validation` (part of the `SelfTest` subsystem)
executes its real logic in scenarios that exercise the `--validate` path; no mocking is applied
at any level.

### Test Environment

N/A - standard test environment.

### Acceptance Criteria

- All integration tests pass with zero failures.
- All supported flags produce the correct observable behavior when passed through `Context.Create`
  and `Program.Run`.
- Invalid arguments produce exit code 1 and an error message.
- Log files and results files are created at the specified paths when the corresponding flags
  are used.
- Positional paths flow from `Context.Create` to the engine; an undecodable file gives a diagnostic and
  exit code 1, and `--check` leaves files unchanged.
- `--validate` combined with paths is rejected with exit code 1.
- Concurrent writers to one `Context` produce intact lines and exit code 1.

### Test Scenarios

**CliSubsystem_VersionFlow_ContextAndProgram_DisplaysVersionAndExits**: Arguments `["--version"]`
are passed through `Context.Create` and `Program.Run`; standard output contains the version
string and exit code is 0. This scenario is tested by
`CliSubsystem_VersionFlow_ContextAndProgram_DisplaysVersionAndExits`.

**CliSubsystem_VersionFlow_ContextAndProgram_DisplaysVersionAndExits_WithShortVFlag**: Arguments
`["-v"]` are passed through `Context.Create` and `Program.Run`; standard output contains the
version string and exit code is 0. This scenario is tested by
`CliSubsystem_VersionFlow_ContextAndProgram_DisplaysVersionAndExits_WithShortVFlag`.

**CliSubsystem_HelpFlow_ContextAndProgram_DisplaysHelpAndExits**: Arguments `["--help"]` are
passed through `Context.Create` and `Program.Run`; standard output contains help text and exit
code is 0. This scenario is tested by
`CliSubsystem_HelpFlow_ContextAndProgram_DisplaysHelpAndExits`.

**CliSubsystem_HelpFlow_ContextAndProgram_DisplaysHelpAndExits_WithShortQuestionFlag**: Arguments
`["-?"]` are passed; standard output contains help text and exit code is 0. This scenario is
tested by
`CliSubsystem_HelpFlow_ContextAndProgram_DisplaysHelpAndExits_WithShortQuestionFlag`.

**CliSubsystem_HelpFlow_ContextAndProgram_DisplaysHelpAndExits_WithShortHFlag**: Arguments
`["-h"]` are passed; standard output contains help text and exit code is 0. This scenario is
tested by `CliSubsystem_HelpFlow_ContextAndProgram_DisplaysHelpAndExits_WithShortHFlag`.

**CliSubsystem_ValidateFlow_ContextAndProgram_RunsValidationAndExits**: Arguments `["--validate"]`
are passed; standard output contains `"Total Tests:"` and exit code is 0. This scenario is
tested by `CliSubsystem_ValidateFlow_ContextAndProgram_RunsValidationAndExits`.

**CliSubsystem_SilentFlow_ContextAndProgram_SuppressesOutput**: Arguments `["--version", "--silent"]`
are passed; standard output is empty and exit code is 0, confirming `--silent` suppresses all
console output. This scenario is tested by
`CliSubsystem_SilentFlow_ContextAndProgram_SuppressesOutput`.

**CliSubsystem_ResultsFlow_ContextAndProgram_WritesResultsFile**: Arguments
`["--validate", "--results", "<tmp>.trx"]` are passed; a results file is created at the
specified path and exit code is 0. This scenario is tested by
`CliSubsystem_ResultsFlow_ContextAndProgram_WritesResultsFile`.

**CliSubsystem_LogFlow_ContextAndProgram_WritesLogFile**: Arguments `["--log", "<tmp>.log"]` are
passed; a log file is created at the specified path and exit code is 0. This scenario is tested
by `CliSubsystem_LogFlow_ContextAndProgram_WritesLogFile`.

**CliSubsystem_InvalidArgs_ContextAndProgram_RejectsUnknownArgumentsAndExitsNonZero**: Arguments
`["--unknown-flag"]` are passed directly to `Program.Main`; exit code is 1 and standard error
contains an error message including the unknown flag. This scenario is tested by
`CliSubsystem_InvalidArgs_ContextAndProgram_RejectsUnknownArgumentsAndExitsNonZero`.

**CliSubsystem_ErrorOutput_ContextAndProgram_WritesErrorToStderr**: A `Context` is created with
no arguments and `WriteError` is called with a known message; standard error receives the
message and exit code is non-zero. This scenario is tested by
`CliSubsystem_ErrorOutput_ContextAndProgram_WritesErrorToStderr`.

**CliSubsystem_ResultAliasFlow_ContextAndProgram_WritesResultsFile**: Arguments
`["--validate", "--result", "<tmp>.trx"]` (legacy alias) are passed; a results file is created
at the specified path and exit code is 0. This scenario is tested by
`CliSubsystem_ResultAliasFlow_ContextAndProgram_WritesResultsFile`.

**CliSubsystem_DepthFlow_ContextAndProgram_AdjustsHeadingDepth**: Arguments `["--depth", "2"]`
are passed; the heading depth is set to 2 and exit code is 0, confirming the `--depth` flag is
correctly parsed and propagated through `Context`. This scenario is tested by
`CliSubsystem_DepthFlow_ContextAndProgram_AdjustsHeadingDepth`.

**CliSubsystem_PathsFlow_ContextAndProgram_ParsesFilesAndExitsZero**: A valid temporary Markdown file is
passed as a positional argument together with `--silent` and `--log` through `Context.Create` and
`Program.Run`; `Context.Paths` holds the file, the real engine processes it, the log contains no
diagnostic, and exit code is 0. This scenario is tested by
`CliSubsystem_PathsFlow_ContextAndProgram_ParsesFilesAndExitsZero`.

**CliSubsystem_PathsFlow_UnreadableFile_ReportsDiagnosticAndExitsNonZero**: A file that is not valid UTF-8
is passed as a positional argument with `--silent` and `--log`; the log contains the diagnostic
`bad.md:1:2: file-encoding-error Invalid UTF-8 byte sequence` and exit code is 1. This scenario is tested by
`CliSubsystem_PathsFlow_UnreadableFile_ReportsDiagnosticAndExitsNonZero`.

**CliSubsystem_CheckFlow_ContextAndProgram_LeavesFilesUnchanged**: `--check` and a file path are passed;
`Context.Check` is true, exit code is 0, and the file's bytes and last-write time are unchanged. This
scenario is tested by `CliSubsystem_CheckFlow_ContextAndProgram_LeavesFilesUnchanged`.

**CliSubsystem_ValidateWithPaths_ContextAndProgram_RejectsAndExitsNonZero**: `["--validate", "docs"]` is
passed to `Program.Main`; exit code is 1 and the error stream contains an error message naming
`--validate`. This scenario is tested by
`CliSubsystem_ValidateWithPaths_ContextAndProgram_RejectsAndExitsNonZero`.

**CliSubsystem_OutputFlow_ConcurrentWriters_KeepsLinesIntactAndSetsExitCode**: Many threads write lines and
errors to one silent `Context` with a log file; all 200 lines are present and intact in the log and exit
code is 1. This scenario is tested by
`CliSubsystem_OutputFlow_ConcurrentWriters_KeepsLinesIntactAndSetsExitCode`.
