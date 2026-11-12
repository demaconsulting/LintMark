## Program

### Verification Approach

`Program` is verified with unit tests defined in `ProgramTests.cs`. Because `Program` directly
instantiates `Context` from real arguments, calls `Validation.Run` when needed, and hands path arguments to the
real `LintMarkEngine`, no mocking is required. Tests pass controlled argument arrays and assert on captured
console output and exit codes; the path scenarios use real temporary files (the `TemporaryDirectory` test
helper).

### Test Environment

N/A - standard test environment.

### Acceptance Criteria

- All unit tests pass with zero failures.
- Exit code 0 is returned for all valid flag combinations.
- Exit code 1 is returned when invalid arguments are supplied to `Program.Main`.
- `Program.Version` returns a non-empty, non-null string.
- With a path argument, `Program.Run` processes the file with the real engine and reports failures through
  the context (exit code 1).
- A log file that is also a selected file is refused before the banner is written, so it is never created or
  replaced.

### Test Scenarios

#### Normal operation

**Long version flag displays the version only**: `Program.Run` with a context from `["--version"]` outputs the
version string without a banner or copyright notice, and the exit code is 0.

**Short version flag displays the version**: `["-v"]` outputs the version string and exits with 0.

**Help flag displays the usage information**: `["--help"]` outputs "Usage:", "Options:", "--version", and
"--help", and the exit code is 0.

**Short help flags display the usage information**: `["-h"]` and `["-?"]` output "Usage:" and "Options:" and
exit with 0.

**Help lists the check option and the path arguments**: `["--help"]` outputs `[paths...]`, `--check`, and the
default pattern `**/*.md`, and the exit code is 0.

**Validate flag runs the self-validation**: `["--validate"]` outputs "Total Tests:" and exits with 0.

**No arguments display the banner and process the current directory**: An empty argument array outputs the tool
name and the copyright notice and exits with 0; the default pattern is applied to the test process's current
directory.

**Version is resolvable from the assembly**: `Program.Version` returns a non-empty string.

**Path argument runs the engine**: A context for a valid file leaves the exit code at 0.

#### Boundary conditions

N/A - the program has no numeric or size limits; its argument boundaries are verified in the `Context` unit.

#### Error handling

**Invalid arguments are caught and give exit code 1**: `Program.Main` with `["--invalid-argument"]` returns exit
code 1 without throwing.

**Undecodable file is reported through the context**: A context for an undecodable file makes the exit code 1
and writes `file-encoding-error` to the error stream; the former "not yet implemented" text no longer appears.

**A log file that is also an input is refused before any output**: With an existing Markdown file, and with a
file that does not exist yet, each named both as the log and as the input, `Program.Run` throws
`InvalidOperationException`, writes nothing to the console, leaves the existing file's content unchanged, and
creates no file. `Program.Main` with the same arguments returns 1 and writes a message that contains
"refusing to overwrite" to standard error, and the file is unchanged.
