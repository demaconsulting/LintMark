### Context

#### Verification Approach

`Context` is verified with unit tests defined in `ContextTests.cs`. Because `Context` depends
only on .NET BCL types (`Console`, `StreamWriter`), no mocking or test doubles are required.
Tests call `Context.Create` with controlled argument arrays, inspect the resulting properties
and exit codes, and verify output written to captured streams.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- All flag properties are correctly set for each recognized argument.
- `ArgumentException` is thrown for all unknown or malformed arguments.
- `ExitCode` is 1 after `WriteError` is called and 0 otherwise.
- Silent mode suppresses console output but does not affect the log file.
- Positional arguments are collected in order as paths; dash-leading tokens that are not options, and empty
  arguments, are rejected.
- `--validate` combined with paths or `--check` is rejected before any log file is created.
- Concurrent writers never interleave lines and never lose the error state.

#### Test Scenarios

**Context_Create_NoArguments_ReturnsDefaultContext**: `Context.Create` is called with an empty
argument array; all boolean flags are false, `ResultsFile` is null, `HeadingDepth` is 1, and
exit code is 0. This scenario is tested by `Context_Create_NoArguments_ReturnsDefaultContext`.

**Context_Create_VersionFlag_SetsVersionTrue**: `Context.Create` is called with `["--version"]`;
the `Version` property is true. This scenario is tested by
`Context_Create_VersionFlag_SetsVersionTrue`.

**Context_Create_ShortVersionFlag_SetsVersionTrue**: `Context.Create` is called with `["-v"]`;
the `Version` property is true. This scenario is tested by
`Context_Create_ShortVersionFlag_SetsVersionTrue`.

**Context_Create_HelpFlag_SetsHelpTrue**: `Context.Create` is called with `["--help"]`; the
`Help` property is true. This scenario is tested by `Context_Create_HelpFlag_SetsHelpTrue`.

**Context_Create_ShortHelpFlag_H_SetsHelpTrue**: `Context.Create` is called with `["-h"]`; the
`Help` property is true. This scenario is tested by
`Context_Create_ShortHelpFlag_H_SetsHelpTrue`.

**Context_Create_ShortHelpFlag_Question_SetsHelpTrue**: `Context.Create` is called with `["-?"]`;
the `Help` property is true. This scenario is tested by
`Context_Create_ShortHelpFlag_Question_SetsHelpTrue`.

**Context_Create_SilentFlag_SetsSilentTrue**: `Context.Create` is called with `["--silent"]`;
the `Silent` property is true. This scenario is tested by
`Context_Create_SilentFlag_SetsSilentTrue`.

**Context_Create_ValidateFlag_SetsValidateTrue**: `Context.Create` is called with
`["--validate"]`; the `Validate` property is true. This scenario is tested by
`Context_Create_ValidateFlag_SetsValidateTrue`.

**Context_Create_ResultsFlag_SetsResultsFile**: `Context.Create` is called with
`["--results", "output.trx"]`; `ResultsFile` equals `"output.trx"`. This scenario is tested by
`Context_Create_ResultsFlag_SetsResultsFile`.

**Context_Create_LogFlag_OpensLogFile**: `Context.Create` is called with
`["--log", "<tmp>.log"]` and then `WriteLine` is called with a test message; the log file is
created and contains the test message. This scenario is tested by
`Context_Create_LogFlag_OpensLogFile`.

**Context_Create_UnknownArgument_ThrowsArgumentException**: `Context.Create` is called with
`["--unknown"]`; an `ArgumentException` containing "Unsupported argument" is thrown. This
scenario is tested by `Context_Create_UnknownArgument_ThrowsArgumentException`.

**Context_Create_LogFlag_WithoutValue_ThrowsArgumentException**: `Context.Create` is called
with `["--log"]` (value missing); an `ArgumentException` is thrown. This scenario is tested by
`Context_Create_LogFlag_WithoutValue_ThrowsArgumentException`.

**Context_Create_ResultsFlag_WithoutValue_ThrowsArgumentException**: `Context.Create` is called
with `["--results"]` (value missing); an `ArgumentException` is thrown. This scenario is tested
by `Context_Create_ResultsFlag_WithoutValue_ThrowsArgumentException`.

**Context_Create_ResultAliasFlag_SetsResultsFile**: `Context.Create` is called with
`["--result", "output.trx"]` (legacy alias); `ResultsFile` equals `"output.trx"`, identical to
the `--results` flag behavior. This scenario is tested by
`Context_Create_ResultAliasFlag_SetsResultsFile`.

**Context_Create_ResultAliasFlag_WithoutValue_ThrowsArgumentException**: `Context.Create` is
called with `["--result"]` (value missing); an `ArgumentException` is thrown. This scenario is
tested by `Context_Create_ResultAliasFlag_WithoutValue_ThrowsArgumentException`.

**Context_Create_DepthFlag_SetsHeadingDepth**: `Context.Create` is called with
`["--depth", "3"]`; `HeadingDepth` equals 3. This scenario is tested by
`Context_Create_DepthFlag_SetsHeadingDepth`.

**Context_Create_NoDepthFlag_ReturnsDefaultHeadingDepth**: `Context.Create` is called with an
empty argument array; `HeadingDepth` equals 1 (the default; valid range is 1–6). This scenario
is tested by `Context_Create_NoDepthFlag_ReturnsDefaultHeadingDepth`.

**Context_Create_DepthFlag_WithoutValue_ThrowsArgumentException**: `Context.Create` is called
with `["--depth"]` (value missing); an `ArgumentException` is thrown. This scenario is tested
by `Context_Create_DepthFlag_WithoutValue_ThrowsArgumentException`.

**Context_Create_DepthFlag_NonIntegerValue_ThrowsArgumentException**: `Context.Create` is
called with `["--depth", "abc"]`; an `ArgumentException` is thrown. This scenario is tested by
`Context_Create_DepthFlag_NonIntegerValue_ThrowsArgumentException`.

**Context_Create_DepthFlag_ZeroValue_ThrowsArgumentException**: `Context.Create` is called with
`["--depth", "0"]` (below the minimum of 1); an `ArgumentException` is thrown. This scenario
is tested by `Context_Create_DepthFlag_ZeroValue_ThrowsArgumentException`.

**Context_Create_DepthFlag_ExceedsMaxValue_ThrowsArgumentException**: `Context.Create` is
called with `["--depth", "7"]` (above the maximum of 6); an `ArgumentException` is thrown.
This scenario is tested by `Context_Create_DepthFlag_ExceedsMaxValue_ThrowsArgumentException`.

**Context_WriteLine_NotSilent_WritesToConsole**: A non-silent `Context` calls `WriteLine` with
a test message; the message appears on standard output. This scenario is tested by
`Context_WriteLine_NotSilent_WritesToConsole`.

**Context_WriteLine_Silent_DoesNotWriteToConsole**: A silent `Context` (created with
`["--silent"]`) calls `WriteLine`; standard output receives nothing, confirming `--silent`
suppresses normal output. This scenario is tested by
`Context_WriteLine_Silent_DoesNotWriteToConsole`.

**Context_WriteError_Silent_DoesNotWriteToConsole**: A silent `Context` calls `WriteError`;
standard error receives nothing, confirming `--silent` also suppresses error output. This
scenario is tested by `Context_WriteError_Silent_DoesNotWriteToConsole`.

**Context_WriteError_SetsErrorExitCode**: A `Context` calls `WriteError`; `ExitCode` is 1
after the call. This scenario is tested by `Context_WriteError_SetsErrorExitCode`.

**Context_WriteError_NotSilent_WritesToConsole**: A non-silent `Context` calls `WriteError`
with a test message; the message appears on standard error. This scenario is tested by
`Context_WriteError_NotSilent_WritesToConsole`.

**Context_WriteError_WritesToLogFile**: A `Context` created with
`["--silent", "--log", "<tmp>.log"]` calls `WriteError` with a test message; the message appears
in the log file, confirming errors are always written to the log regardless of `--silent`. This
scenario is tested by `Context_WriteError_WritesToLogFile`.

**Context_Create_LogFlag_InvalidPath_ThrowsInvalidOperationException**:
`Context.Create` is called with `["--log", "/invalid/\x00path.log"]`; an
`InvalidOperationException` is thrown because the log file cannot be opened. This
scenario is tested by `Context_Create_LogFlag_InvalidPath_ThrowsInvalidOperationException`.

**Context_Create_PositionalPaths_SetsPathsInOrder**: `Context.Create` is called with several non-dash
arguments, including a `!` exclusion pattern; `Paths` holds them in the order given and no flag is set. This
scenario is tested by `Context_Create_PositionalPaths_SetsPathsInOrder`.

**Context_Create_NoPaths_PathsEmpty**: `Context.Create` is called without positional arguments; `Paths` is
empty. This scenario is tested by `Context_Create_NoPaths_PathsEmpty`.

**Context_Create_CheckFlag_SetsCheckTrue**: `Context.Create` is called with `["--check"]`; `Check` is true
and no other flag changes. This scenario is tested by `Context_Create_CheckFlag_SetsCheckTrue`.

**Context_Create_PathsMixedWithFlags_ParsesBoth**: Paths and options (including a `--log` value that must
not be taken as a path) are interleaved; only the positional arguments appear in `Paths` and the options are
parsed. This scenario is tested by `Context_Create_PathsMixedWithFlags_ParsesBoth`.

**Context_Create_HelpOrVersionWithPaths_DoesNotThrow**: `--help` and `--version` combined with paths are
accepted. This scenario is tested by `Context_Create_HelpOrVersionWithPaths_DoesNotThrow`.

**Context_Create_ValidateWithPaths_ThrowsArgumentException**: `--validate` combined with a path throws an
`ArgumentException` whose message names the flag. This scenario is tested by
`Context_Create_ValidateWithPaths_ThrowsArgumentException`.

**Context_Create_ValidateWithCheck_ThrowsArgumentException**: `--validate` combined with `--check` throws an
`ArgumentException`. This scenario is tested by `Context_Create_ValidateWithCheck_ThrowsArgumentException`.

**Context_Create_ValidateWithPaths_DoesNotCreateLogFile**: The `--validate` conflict is combined with a
`--log` path that does not yet exist; the error is raised and the log file is never created. This scenario
is tested by `Context_Create_ValidateWithPaths_DoesNotCreateLogFile`.

**Context_Create_ValidateWithItsOwnOptions_DoesNotThrow**: `--validate` combined with its own options
(`--results`, `--depth`, `--silent`) is parsed as before. This scenario is tested by
`Context_Create_ValidateWithItsOwnOptions_DoesNotThrow`.

**Context_Create_DashedUnknownToken_ThrowsArgumentException**: `Context.Create` is called with `-`, `-x`,
and `--unknown`; each throws an `ArgumentException` (a dash-leading token is never a path). This scenario is
tested by `Context_Create_DashedUnknownToken_ThrowsArgumentException`.

**Context_Create_EmptyArgument_ThrowsArgumentException**: `Context.Create` is called with an empty string;
an `ArgumentException` is thrown. This scenario is tested by
`Context_Create_EmptyArgument_ThrowsArgumentException`.

**Context_WriteError_ConcurrentWriters_AllLinesWrittenIntact**: Many threads call `WriteError` with long
lines at the same time; every line appears exactly once and intact in the log file and exit code is 1. This
scenario is tested by `Context_WriteError_ConcurrentWriters_AllLinesWrittenIntact`.

**Context_WriteLine_ConcurrentWriters_AllLinesWrittenIntact**: Many threads call `WriteLine` with long lines
at the same time; every line appears exactly once and intact in the log file and no error is recorded. This
scenario is tested by `Context_WriteLine_ConcurrentWriters_AllLinesWrittenIntact`.
