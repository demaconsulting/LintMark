## Reporting

### Verification Approach

The `Reporting` subsystem is verified by integration tests in `ReportingSubsystemTests.cs`. Each test builds
diagnostics and file diffs for files in a temporary directory (the `TemporaryDirectory` test helper) and runs the real
`TextReporter` and the real `DiffReporter` together against a real `Context`. The run helper creates the `Context`
with the `--silent` and `--log` options, so that nothing reaches the console and the complete output of both
reporters, in order, is read back from the log file, together with the exit code of the context. The scenario about
the standard output channel instead replaces the console output and the console error stream with string writers, to
show on which stream the diff arrives.

A diff is judged by whether it works as a patch, not by comparing its text with a fixed copy. The `PatchApplier`
helper in the tests applies the unified diff lines that were written to the log to the original text, and fails when a
context line or a removed line does not match the original, when the line counts of a hunk header disagree with the
hunk body, when the new-side start does not match the position in the output, when a number of a hunk header is not
valid, or when a line that starts with a backslash is anywhere but directly after a hunk body line (the no-newline
marker) or directly after the `+++` header (the line-ending note); each is reported as an `InvalidOperationException`.
The patched text must equal the rewritten text exactly. The test document has thirty numbered lines that are changed
near the start, deleted and inserted in the middle, and shortened at the end without a final newline, so that it
needs three separate hunks and a missing-newline marker. No mocking framework is used; the reporters have no
dependency other than the `Context`, which is real. Because the tests change the console streams, they run
sequentially with the other tests that use process-wide state.

### Test Environment

Temporary directories are created and removed by each test, and the log file is written inside them. The tests that
replace the console streams restore them before they finish, and are run sequentially with the other tests that
touch process-wide state.

### Acceptance Criteria

- All integration tests pass with zero failures.
- Diagnostics given out of order are written one per line as `path:line:col: rule-id message`, with paths relative to
  the base directory, ordered by path, line, column, and rule identifier, and the exit code is 1; a repeated
  diagnostic is written once, and nothing is written when there is nothing to report.
- The diff of a change at the start, in the middle, and at the end of a file is written as separate hunks, and
  applying the written lines to the original text gives the rewritten text exactly.
- A change of line endings alone is named by a note line in the diff, which does not disturb applying the diff.
- The same diagnostics and diffs given in reverse order produce identical output and the same exit code.
- A diff is written to standard output, nothing is written to standard error, and writing only a diff leaves the exit
  code at 0.

### Test Scenarios

#### Normal operation

**Diagnostics are written sorted with paths relative to the base directory**: Four diagnostics for files at several
depths, given out of order, are reported. The log holds four lines in the order of path, then line, then rule
identifier (`README.md` before `docs/a.md`, and `rule-a` before `rule-b` at the same position), each in the format
`path:line:col: rule-id message`, and the exit code is 1.

**The diff of a multi-hunk change applies cleanly to the old text**: A document of thirty lines that changes near the
start, in the middle, and at the end is reported as a diff. The log holds exactly three hunk headers, and applying the
logged lines to the old text with the patch helper gives the new text exactly, including the missing final newline.

**Shuffled input gives identical output**: Four diagnostics and three diffs (one of which changes only the line
endings and has no text change) are reported in the given order and in the reverse order. The two logs are identical
line by line and the two exit codes are equal.

**A diff alone goes to standard output and does not fail the run**: One changed file is reported to a context that is
not silent, with the console streams captured. The diff, with its `--- a/a.md` header and its `+y` line, is on
standard output, nothing is on standard error, and the exit code is 0.

**A repeated diagnostic is written once**: Two equal diagnostics and a third one are reported. The log holds two
lines, one for the repeated finding and one for the other, and the exit code is 1.

**Nothing to report writes nothing**: No diagnostics and no diffs are reported. The log is empty and the exit code
is 0.

**A change of line endings alone is named in the diff**: A file whose text is equal after normalization but whose
line endings change from CRLF to LF is reported as a diff. The log holds the two headers and the line
`\ Line endings change from CRLF to LF` and no hunk, and applying the logged lines to the text leaves it unchanged.

#### Boundary conditions

N/A - the boundary behavior of each reporter (empty input, hunk merging at the limit, lines that look like diff
syntax, and very large texts) is verified by the unit tests of `TextReporter` and `DiffReporter`; the subsystem tests
cover only the cooperation of the two reporters.

#### Error handling

N/A - invalid arguments of the reporters are verified by the unit tests of `TextReporter` and `DiffReporter`; the
subsystem has no failure path of its own, because it holds no state and does no file-system access.
