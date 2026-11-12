# LintMark

## Verification Approach

System-level verification uses end-to-end integration tests that invoke the tool as a real
process via the `Runner.Run` helper in `IntegrationTests.cs`. Each test exercises the full
stack — argument parsing, dispatch, file selection, parsing, reporting, and output — and validates both exit
code and combined console output. The tests treat the tool as a black box and assert only on observable
outputs; no internal implementation details are assumed. Tests that process files create them in a
temporary directory (the `TemporaryDirectory` test helper) and pass their paths as arguments.

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
- Files from `node_modules` and `.git` folders are processed only when named explicitly.
- Failures are reported in path order, and `--check` leaves files byte-identical.
- `--validate` combined with paths or `--check` is rejected with exit code 1 and without running validation.

## Test Scenarios

### Normal operation

**Version flag outputs the version**: The `--version` flag is passed as the sole argument; the tool outputs the
version string and exits with code 0.

**Help flag outputs usage information**: The `--help` flag is passed as the sole argument; the combined output
contains "Usage" and "Options" and the tool exits with code 0.

**Help lists the path arguments and the check option**: The `--help` output contains `[paths...]` and
`--check`, and the exit code is 0.

**Validate flag runs the self-validation**: The `--validate` flag is passed as the sole argument; the combined
output contains "Total Tests:" and the tool exits with code 0.

**Validation results are written as TRX**: `--validate` combined with `--results <path>.trx` creates a file that
contains a `<TestRun` element, and the tool exits with code 0.

**Validation results are written as JUnit XML**: `--validate` combined with `--results <path>.xml` creates a file
that contains a `<testsuites` element, and the tool exits with code 0.

**Legacy result alias writes the results file**: `--validate` combined with `--result <path>.trx` creates a TRX
file, and the tool exits with code 0.

**Depth option sets the heading level of the validation output**: `--validate` combined with `--depth 3` gives
output that contains `###`, and the tool exits with code 0.

**Silent flag suppresses output**: `--version` and `--silent` together give empty or whitespace-only combined
output, and the tool exits with code 0.

**Log flag writes the output to a file**: `--log <path>` creates the file, and it contains output that also
appears in the combined console output.

**Invocation without arguments displays the banner**: The combined output contains the tool name and the
copyright notice, and the exit code is 0.

**Valid Markdown file is processed without output**: A valid file in a temporary directory is passed as the only
argument; the exit code is 0 and no rule identifier appears in the output.

**Directory selects every Markdown file beneath it**: A directory with a valid file, a nested invalid Markdown
file, and an invalid non-Markdown file is passed; the exit code is 1, only the nested Markdown file is
reported, and the non-Markdown file is never read.

**Explicit file inside a third-party folder is processed**: An undecodable file under `node_modules/pkg` that is
named on the command line is read; the exit code is 1 and the output contains
`bad.md:1:1: file-encoding-error`.

**File with every supported construct is processed without output**: A file that contains front matter,
suppression comments, a reference definition, a footnote, a table, and an unclosed fence is passed; the exit
code is 0 and nothing is reported.

**Repository documentation is processed without output**: The repository's `docs` directory and `README.md` are
passed; every file is read and parsed, the exit code is 0, and nothing is reported.

**Check flag lints without writing**: `--check` is passed with a valid file that has CRLF line endings and no
final newline; the exit code is 0 and the file's bytes and last-write time are unchanged.

### Boundary conditions

**Glob and exclusion skip the excluded files**: An absolute glob and an absolute `!` exclusion are passed so
that an invalid file lies under the excluded directory; the exit code is 0 and the excluded file is not
reported.

**Third-party folder named as an argument selects nothing**: The folders `node_modules` and `.git`, each with an
undecodable file, are passed, and so is an absolute glob inside `node_modules`; nothing is reported and the exit
code is 0 in each case.

**Several failing files are reported in path order**: Three invalid files are passed in non-sorted order; the exit
code is 1 and the diagnostics appear in path order.

### Error handling

**Unknown argument is rejected**: An unrecognized argument (`--unknown`) gives a non-zero exit code and an error
message that identifies it.

**Unsupported results extension is rejected**: `--validate` combined with `--results <path>.bad` gives a
non-zero exit code and creates no file.

**Undecodable file is reported with a diagnostic**: A file with an invalid UTF-8 byte on its second line gives
exit code 1 and the output contains `bad.md:2:3: file-encoding-error Invalid UTF-8 byte sequence`.

**Missing file is reported as a read error**: A path that does not exist gives exit code 1 and the output
contains `missing.md:1:1: file-read-error File not found`.

**Validate combined with a path is rejected**: The exit code is 1, the output contains an error that names
`--validate`, and validation does not run.

**Validate combined with check is rejected**: The exit code is 1 and the output contains an error.
