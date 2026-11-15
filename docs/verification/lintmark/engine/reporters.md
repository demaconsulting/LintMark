### Reporters

#### Verification Approach

`Reporters` covers `TextReporter` and `DiffReporter`, verified with unit tests in `TextReporterTests.cs` and
`DiffReporterTests.cs`. The reporters write to a real `Context`, so the tests create contexts (silent or not, with or
without a log file in a `TemporaryDirectory`) and assert on the captured lines, the log content, and the exit status.
The diff tests also apply each produced diff back to the old text with the `PatchApplier` helper and check that the
result is the new text, which proves that the diff is complete and correctly numbered. No mocks are used.

#### Test Environment

Log-file scenarios create and remove temporary files. Console capture changes process-wide state, so these tests run
sequentially with other tests that capture the console.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Each finding is one line `path:line:col: rule-id message`, duplicates are written once, and the order is path then
  line then column (numeric), then rule id, then message.
- Paths are shown relative to the base directory with forward slashes, with `..` where the file is outside it.
- A unified diff has headers, hunks with three lines of context, merged nearby hunks, correct counts, and the markers
  for a missing final newline and a line-ending change; identical text writes nothing.
- A diff applied to the old text yields the new text, also for very different large texts.
- Silent contexts write no console output but still write the log and set the exit status.

#### Test Scenarios

##### Normal operation

**A finding is written as path, line, column, rule, and message**: The documented line format.

**Unsorted findings are written in order**: Path, numeric line and column, rule, then message.

**Equal findings are written once**: Duplicates collapse.

**A single-line change writes headers and a hunk**: Hunk headers omit a count of one.

**Distant changes are two hunks, nearby changes merge**: Seven lines apart stays two.

**Line-ending-only changes write a note line**: CRLF to LF, LF to CRLF, and mixed to LF each name the change.

**Many files are ordered by path**: The diff output follows path order.

##### Boundary conditions

**No findings and identical text write nothing**: Output is empty.

**Insertions at the start, deletions at the end, and into an empty file are correct**: Zero counts are used for
empty ranges.

**Missing final newline is marked**: Added and removed final newlines are marked on the right line.

**Backslash in content is not mistaken for a marker**: The content is written as is.

##### Error handling

**Silent context still sets the exit status and the log**: The console is empty and the log has the lines.

**Findings outside the base directory use a parent-relative path**: The path starts with `../`.
