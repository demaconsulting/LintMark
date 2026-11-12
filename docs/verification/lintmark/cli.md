## Cli

### Verification Approach

The `Cli` subsystem is verified by integration tests defined in `CliSubsystemTests.cs`. Each
test exercises `Context.Create` and `Program.Run` together, treating the pair as the observable
subsystem interface. Tests pass controlled argument arrays and assert on captured console output,
file-system side-effects, and exit codes. `Validation` (part of the `SelfTest` subsystem)
executes its real logic in scenarios that exercise the `--validate` path, and the real `LintMarkEngine`
processes the files in the scenarios that pass paths; temporary files come from the `TemporaryDirectory` test
helper. No mocking is applied at any level.

### Test Environment

N/A - standard test environment.

### Acceptance Criteria

- All integration tests pass with zero failures.
- All supported flags produce the correct observable behavior when passed through `Context.Create`
  and `Program.Run`.
- Invalid arguments produce exit code 1 and an error message.
- Log files and results files are created at the specified paths when the corresponding flags
  are used; a log file that is also an input is refused and left intact.
- Positional paths flow from `Context.Create` to the engine; an undecodable file gives a diagnostic and
  exit code 1, and `--check` leaves files unchanged.
- `--validate` combined with paths is rejected with exit code 1.
- Concurrent writers to one `Context` produce intact lines and exit code 1.

### Test Scenarios

#### Normal operation

**Long version flag displays the version**: `["--version"]` is passed through `Context.Create` and `Program.Run`;
standard output contains the version string and the exit code is 0.

**Short version flag displays the version**: `["-v"]` gives the same result.

**Long help flag displays the help text**: `["--help"]` gives help text on standard output and exit code 0.

**Short help flags display the help text**: `["-?"]` and `["-h"]` give help text and exit code 0.

**Validate flag runs the self-validation**: `["--validate"]` gives output that contains `"Total Tests:"` and exit
code 0.

**Silent flag suppresses console output**: `["--version", "--silent"]` gives empty standard output and exit
code 0.

**Results flag writes a results file**: `["--validate", "--results", "<tmp>.trx"]` creates a results file at the
specified path and exits with 0.

**Legacy result alias writes a results file**: `["--validate", "--result", "<tmp>.trx"]` behaves the same.

**Log flag writes a log file**: `["--log", "<tmp>.log"]` creates a log file at the specified path and exits
with 0.

**A log file that is also an input is refused and survives**: A Markdown file is named both as the log and as the
input. Creating the context leaves the file's content intact, and `Program.Main` returns 1 with a message that
contains "refusing to overwrite" while the content is still unchanged.

**Depth flag sets the heading depth**: `["--depth", "2"]` is parsed and propagated through `Context`, and the exit
code is 0.

**Paths flow to the engine and a valid file exits with zero**: A valid temporary Markdown file is passed as a
positional argument with `--silent` and `--log`; `Context.Paths` holds the file, the real engine processes it, the
log contains no diagnostic, and the exit code is 0.

**Check flag leaves files unchanged**: `--check` and a file path are passed; `Context.Check` is true, the exit
code is 0, and the file's bytes and last-write time are unchanged.

#### Boundary conditions

**Concurrent writers keep lines intact**: Many threads write lines and errors to one silent `Context` with a log
file; all 200 lines are present and intact in the log and the exit code is 1.

#### Error handling

**Error output goes to standard error and sets the exit code**: A `Context` created with no arguments writes a
known message with `WriteError`; standard error receives it and the exit code is non-zero.

**Unknown flag is rejected with exit code 1**: `["--unknown-flag"]` passed to `Program.Main` gives exit code 1
and an error on standard error that names the flag.

**Undecodable file is reported and exits non-zero**: A file that is not valid UTF-8 is passed with `--silent` and
`--log`; the log contains `bad.md:1:2: file-encoding-error Invalid UTF-8 byte sequence` and the exit code is 1.

**Validate combined with paths is rejected**: `["--validate", "docs"]` passed to `Program.Main` gives exit code 1
and an error on standard error that names `--validate`.
