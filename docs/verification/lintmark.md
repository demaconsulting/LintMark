# LintMark

## Verification Approach

System-level verification uses end-to-end integration tests that invoke the tool as a real
process via the `Runner.Run` helper in `IntegrationTests.cs`. Each test exercises the full
stack — argument parsing, dispatch, execution, and output — and validates both exit code and
combined console output. The tests treat the tool as a black box and assert only on observable
outputs; no internal implementation details are assumed.

`Runner.Run` merges stdout and stderr into a single combined output string. Per-stream assertions
(e.g., "standard error is empty") are not possible at this level; all assertions are made
against the combined output.

## Test Environment

Integration tests run on .NET 10.0 and .NET 11.0 across Windows, Linux, and macOS.
All scenarios are expected to produce identical results on all supported runtime and platform
combinations. Temporary files and directories are created and cleaned up within each test.

## Acceptance Criteria

- All integration tests pass with zero failures across all supported runtimes and platforms.
- Exit code 0 is returned for all valid invocations.
- Exit code non-zero is returned for all invalid argument combinations.
- Results files are created at the specified paths when `--results` is used with `--validate`.
- Silent mode (`--silent`) produces empty combined output.
- Files named by path, directory, or glob are read and parsed; files that cannot be read or decoded are
  reported as `path:line:col: rule-id message` with exit code 1 and valid files produce no output.
- Failures are reported in path order, and `--check` leaves files byte-identical.
- `--validate` combined with paths or `--check` is rejected with exit code 1 and without running validation.

## Test Scenarios

**LintMark_VersionFlag_Provided_OutputsVersion**: The `--version` flag is passed as
the sole argument; the tool outputs the version string and exits with code 0. This scenario is
tested by `LintMark_VersionFlag_Provided_OutputsVersion`.

**LintMark_HelpFlag_Provided_OutputsUsageInformation**: The `--help` flag is passed
as the sole argument; the combined output contains "Usage" and "Options" and the tool exits
with code 0. This scenario is tested by
`LintMark_HelpFlag_Provided_OutputsUsageInformation`.

**LintMark_ValidateFlag_Provided_RunsValidation**: The `--validate` flag is passed as
the sole argument; the combined output contains "Total Tests:" and the tool exits with code 0,
confirming the self-validation suite runs and completes successfully. This scenario is tested by
`LintMark_ValidateFlag_Provided_RunsValidation`.

**LintMark_ValidateWithTrxResults_Requested_GeneratesTrxFile**: The `--validate` flag
is combined with `--results <path>.trx`; a TRX file containing a `<TestRun` XML element is
created at the specified path and the tool exits with code 0. This scenario is tested by
`LintMark_ValidateWithTrxResults_Requested_GeneratesTrxFile`.

**LintMark_ValidateWithXmlResults_Requested_GeneratesJUnitFile**: The `--validate` flag
is combined with `--results <path>.xml`; a JUnit XML file containing a `<testsuites` XML element
is created at the specified path and the tool exits with code 0. This scenario is tested by
`LintMark_ValidateWithXmlResults_Requested_GeneratesJUnitFile`.

**LintMark_SilentFlag_Provided_SuppressesOutput**: The `--version` and `--silent`
flags are passed together; the combined output is empty or whitespace-only while the tool exits
with code 0, confirming silent mode suppresses all console output. This scenario is tested by
`LintMark_SilentFlag_Provided_SuppressesOutput`.

**LintMark_LogFlag_Provided_WritesOutputToFile**: The `--log <path>` flag is passed
pointing to a temporary file; the tool exits with code 0 and the log file is created containing
output that also appears in the combined console output. This scenario is tested by
`LintMark_LogFlag_Provided_WritesOutputToFile`.

**LintMark_UnknownArgument_Provided_ReturnsError**: An unrecognized argument
(`--unknown`) is passed; the tool exits with a non-zero code and the combined output contains
an error message identifying the unknown argument. This scenario is tested by
`LintMark_UnknownArgument_Provided_ReturnsError`.

**LintMark_ValidateWithDepth_DepthThree_OutputsCorrectHeadingLevel**: The `--validate`
flag is combined with `--depth 3`; the combined output contains `###` (heading at depth 3) and
the tool exits with code 0. This scenario is tested by
`LintMark_ValidateWithDepth_DepthThree_OutputsCorrectHeadingLevel`.

**LintMark_NoArguments_Invoked_DisplaysBanner**: The tool is invoked with no
arguments; the combined output contains the tool name and copyright notice and the exit code is
0. This scenario is tested by `LintMark_NoArguments_Invoked_DisplaysBanner`.

**LintMark_ResultAlias_LegacyFlag_WritesResultsFile**: The `--validate` flag is
combined with `--result <path>.trx` (the legacy alias); a TRX file is created at the specified
path and the tool exits with code 0. This scenario is tested by
`LintMark_ResultAlias_LegacyFlag_WritesResultsFile`.

**LintMark_ValidateWithBadExtension_ExtensionInvalid_ReturnsNonZero**: The `--validate`
flag is combined with `--results <path>.bad` (unsupported extension); the tool exits with a
non-zero code and no file is created at the specified path. This scenario is tested by
`LintMark_ValidateWithBadExtension_ExtensionInvalid_ReturnsNonZero`.

**LintMark_HelpFlag_Provided_ListsPathsAndCheckOption**: The `--help` flag is passed; the combined output
contains `[paths...]` and `--check` and the exit code is 0. This scenario is tested by
`LintMark_HelpFlag_Provided_ListsPathsAndCheckOption`.

**LintMark_PathsProvided_ValidMarkdown_ReturnsSuccess**: A valid Markdown file in a temporary directory is
passed as the only argument; the exit code is 0 and no rule identifier appears in the output. This scenario
is tested by `LintMark_PathsProvided_ValidMarkdown_ReturnsSuccess`.

**LintMark_PathsProvided_UnreadableFile_ReportsDiagnosticAndReturnsError**: A file with an invalid UTF-8
byte on its second line is passed; the exit code is 1 and the output contains
`bad.md:2:3: file-encoding-error Invalid UTF-8 byte sequence`. This scenario is tested by
`LintMark_PathsProvided_UnreadableFile_ReportsDiagnosticAndReturnsError`.

**LintMark_PathsProvided_MissingFile_ReportsReadErrorAndReturnsError**: A path that does not exist is
passed; the exit code is 1 and the output contains `missing.md:1:1: file-read-error File not found`. This
scenario is tested by `LintMark_PathsProvided_MissingFile_ReportsReadErrorAndReturnsError`.

**LintMark_DirectoryProvided_ParsesAllMarkdownBeneathIt**: A directory with a valid file, a nested invalid
Markdown file, and an invalid non-Markdown file is passed; the exit code is 1, only the nested Markdown
file is reported, and the non-Markdown file is never read. This scenario is tested by
`LintMark_DirectoryProvided_ParsesAllMarkdownBeneathIt`.

**LintMark_GlobAndExclude_Provided_SkipsExcludedFiles**: An absolute glob and an absolute `!` exclusion are
passed so that an invalid file lies under the excluded directory; the exit code is 0 and the excluded file
is not reported. This scenario is tested by `LintMark_GlobAndExclude_Provided_SkipsExcludedFiles`.

**LintMark_MultipleFiles_Provided_ReportsFailuresInPathOrder**: Three invalid files are passed in
non-sorted order; the exit code is 1 and the diagnostics appear in path order. This scenario is tested by
`LintMark_MultipleFiles_Provided_ReportsFailuresInPathOrder`.

**LintMark_PathsWithSuppressionAndReferences_Provided_ReturnsSuccess**: A file containing front matter,
suppression comments, a reference definition, a footnote, a table, and an unclosed fence is passed; the
exit code is 0 and nothing is reported. This scenario is tested by
`LintMark_PathsWithSuppressionAndReferences_Provided_ReturnsSuccess`.

**LintMark_RepositoryDocumentation_Provided_ReturnsSuccess**: The repository's `docs` directory and
`README.md` are passed; every file is read and parsed, the exit code is 0, and nothing is reported. This
scenario is tested by `LintMark_RepositoryDocumentation_Provided_ReturnsSuccess`.

**LintMark_CheckFlag_Provided_LintsWithoutWriting**: `--check` is passed with a valid file with CRLF line
endings and no final newline; the exit code is 0 and the file's bytes and last-write time are unchanged.
This scenario is tested by `LintMark_CheckFlag_Provided_LintsWithoutWriting`.

**LintMark_ValidateWithPaths_Combined_ReturnsError**: `--validate` is combined with a path; the exit code
is 1, the output contains an error naming `--validate`, and validation does not run. This scenario is tested
by `LintMark_ValidateWithPaths_Combined_ReturnsError`.

**LintMark_ValidateWithCheck_Combined_ReturnsError**: `--validate` is combined with `--check`; the exit code
is 1 and the output contains an error. This scenario is tested by
`LintMark_ValidateWithCheck_Combined_ReturnsError`.
