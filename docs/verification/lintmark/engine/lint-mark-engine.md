### LintMarkEngine

#### Verification Approach

`LintMarkEngine` is verified with unit tests in `LintMarkEngineTests.cs`. Real temporary files, the real
`FileDiscovery`, `SourceDocument`, and `DocumentParser`, and a real `Context` writing to a log file are used
for the normal scenarios. Two constructor seams isolate behavior that cannot be provoked reliably otherwise:
the `documentParser` seam makes the parser throw for chosen files (parse failure for one file only), and the
`fileProcessor` seam delays chosen files so that completion order is the reverse of input order (ordered
reporting). Determinism is verified by comparing a run with one worker and a run with many workers over many
generated files, some of them failing.

#### Test Environment

Temporary directories are created and removed by each test. Tests that change the current directory or capture
console output run in the sequential test collection.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Valid files and empty selections produce no output and exit code 0.
- Missing files, directories given as files, invalid UTF-8, UTF-16, bare carriage returns, and parser failures
  are reported with the documented rule identifier, position, and message, and exit code 1.
- Results and output are ordered by path and then by position, whatever the input order, the degree of
  parallelism, or the completion order.
- Constructor arguments are validated.

#### Test Scenarios

**LintMarkEngine_Process_ValidFiles_ReturnsNoFailures**: Valid files give results with empty failure lists.
Tested by `LintMarkEngine_Process_ValidFiles_ReturnsNoFailures`.

**LintMarkEngine_Process_InvalidUtf8_ReportsEncodingError**: A file with invalid UTF-8 gives a
`file-encoding-error` at the position of the byte. Tested by
`LintMarkEngine_Process_InvalidUtf8_ReportsEncodingError`.

**LintMarkEngine_Process_LoneCr_ReportsLineEndingError**: A file with a bare carriage return gives a
`file-line-ending-error`. Tested by `LintMarkEngine_Process_LoneCr_ReportsLineEndingError`.

**LintMarkEngine_Process_Utf16File_ReportsEncodingError**: A UTF-16 file gives a `file-encoding-error` at
1:1. Tested by `LintMarkEngine_Process_Utf16File_ReportsEncodingError`.

**LintMarkEngine_Process_MissingFile_ReportsReadError**: A missing file gives a `file-read-error` at 1:1 with
the message `File not found`. Tested by `LintMarkEngine_Process_MissingFile_ReportsReadError`.

**LintMarkEngine_Process_DirectoryAsFile_ReportsReadError**: A directory passed as a file gives a
`file-read-error`. Tested by `LintMarkEngine_Process_DirectoryAsFile_ReportsReadError`.

**LintMarkEngine_Process_ParserThrows_ReportsParseErrorForThatFileOnly**: A parser that throws for one file
gives a `file-parse-error` naming the exception type for that file, and the other files are unaffected.
Tested by `LintMarkEngine_Process_ParserThrows_ReportsParseErrorForThatFileOnly`.

**LintMarkEngine_Process_NoFiles_ReturnsEmpty**: An empty file list gives an empty result. Tested by
`LintMarkEngine_Process_NoFiles_ReturnsEmpty`.

**LintMarkEngine_Process_InputOrder_DoesNotAffectResultOrder**: Shuffling the input does not change the
result order. Tested by `LintMarkEngine_Process_InputOrder_DoesNotAffectResultOrder`.

**LintMarkEngine_Process_CompletionOrderInverted_ResultsStillPathSorted**: With completion order inverted by
the file processor seam, results are still sorted by path. Tested by
`LintMarkEngine_Process_CompletionOrderInverted_ResultsStillPathSorted`.

**LintMarkEngine_Process_ParallelismOne_RunsSequentially**: A limit of one never runs two files at once.
Tested by `LintMarkEngine_Process_ParallelismOne_RunsSequentially`.

**LintMarkEngine_Process_ParallelVersusSequential_ProduceIdenticalResults**: Over at least 200 generated
files, including failing ones, one worker and many workers give identical results. Tested by
`LintMarkEngine_Process_ParallelVersusSequential_ProduceIdenticalResults`.

**LintMarkEngine_Constructor_ParallelismBelowOne_ThrowsArgumentOutOfRangeException**: A limit below one is
rejected. Tested by `LintMarkEngine_Constructor_ParallelismBelowOne_ThrowsArgumentOutOfRangeException`.

**LintMarkEngine_RunAndProcess_NullArguments_Throw**: Null arguments to `Run` and `Process` are rejected.
Tested by `LintMarkEngine_RunAndProcess_NullArguments_Throw`.

**LintMarkEngine_Run_ValidFiles_WritesNothingAndExitsZero**: A clean run writes nothing and the exit code is
0. Tested by `LintMarkEngine_Run_ValidFiles_WritesNothingAndExitsZero`.

**LintMarkEngine_Run_Failures_WritesFormattedLinesInPathOrder**: Failures are written as
`path:line:col: rule-id message` with relative paths, in path order, and the exit code is 1. Tested by
`LintMarkEngine_Run_Failures_WritesFormattedLinesInPathOrder`.

**LintMarkEngine_Run_MissingExplicitFile_ReportsReadError**: A missing file named on the command line is
reported as a read failure. Tested by `LintMarkEngine_Run_MissingExplicitFile_ReportsReadError`.

**LintMarkEngine_Run_GlobWithoutMatches_WritesNothingAndExitsZero**: A glob without matches writes nothing
and the exit code is 0. Tested by `LintMarkEngine_Run_GlobWithoutMatches_WritesNothingAndExitsZero`.

**LintMarkEngine_Run_CheckFlag_BehavesLikeDefault**: With `--check` the output and exit code equal those of
the default. Tested by `LintMarkEngine_Run_CheckFlag_BehavesLikeDefault`.

**LintMarkEngine_Run_ParserFailure_ReportsParseError**: A parser failure is written as a `file-parse-error`
line. Tested by `LintMarkEngine_Run_ParserFailure_ReportsParseError`.

**LintMarkEngine_Run_MultipleFailuresInFile_AreOrderedByPosition**: Several failures of one file are written
ordered by line, column, and rule identifier. Tested by
`LintMarkEngine_Run_MultipleFailuresInFile_AreOrderedByPosition`.
