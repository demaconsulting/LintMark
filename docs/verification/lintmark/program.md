## Program

### Verification Approach

`Program` is verified with unit tests in `ProgramTests.cs`. It has no seams to replace: it builds a real `Context`
from argument arrays, calls the real `Validation` and the real `LintMarkEngine`, and reports through the context.
The tests therefore use no mocks. They pass controlled argument arrays to `Program.Run` and `Program.Main`, capture
the console output with the real context, and assert on the text and the exit code. Scenarios that need files use
real temporary files created by the `TemporaryDirectory` test helper. The behavior of each option is verified in
the `Context` unit and the engine units; here only the dispatch and the error policy are verified.

### Test Environment

N/A - standard test environment. The scenario that starts without arguments runs in the current directory of the
test process, so these tests run sequentially with other tests that use process-wide state.

### Acceptance Criteria

- All unit tests pass with zero failures.
- Version, help, and validation requests are answered without processing any file.
- A run without arguments prints the banner and exits with 0.
- A path argument is processed by the real engine and its findings set the exit code to 1.
- Invalid arguments never escape `Program.Main` as exceptions and give exit code 1.

### Test Scenarios

#### Normal operation

**Version flag prints only the version**: `--version` prints the version text, no banner, and exits with 0.

**Help flag prints the usage**: `--help` prints the usage and option list and exits with 0.

**Validate flag runs the self-validation**: `--validate` prints the validation summary and exits with 0.

**No arguments print the banner and process the current directory**: An empty argument list prints the tool name
and copyright notice and exits with 0.

**A path argument runs the engine**: A valid Markdown file is processed and the exit code stays 0.

#### Boundary conditions

N/A - the program has no numeric or size limits; its argument boundaries are verified in the `Context` unit.

#### Error handling

**Invalid arguments give exit code 1**: `Program.Main` with an unknown option returns 1 and does not throw.
