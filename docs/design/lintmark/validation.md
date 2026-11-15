## Validation

![LintMark Structure](LintMarkView.svg)

### Purpose

`Validation` runs the tool's built-in self-validation. Its single responsibility is to execute a fixed set of tests
against the tool itself, print a header and a summary, and optionally write the outcomes as a TRX or JUnit results
file that qualification evidence can consume.

### Data Model

The run builds one result collection named `LintMark Self-Validation` (type `TestResults` of the OTS package
`DemaConsulting.TestResults`; see the OTS design "TestResults"). Each test adds one `TestResult` with the name, the
class name `Validation`, the code base `LintMark`, an outcome (`Passed` or `Failed`), an error message when failed,
and a duration. The totals (total, passed, failed) are counted from that collection.

`TemporaryDirectory` is a private disposable class. It creates a uniquely named directory below the system temp path
and deletes it, with its content, on disposal; cleanup errors are ignored.

### Key Methods

**Run(context)**: prints the header, runs the two tests, prints `Total Tests`, `Passed` and `Failed` (the `Failed`
line goes through `WriteError` when there is a failure, which makes the exit code 1), and writes the results file when
`context.ResultsFile` is set. The header is a Markdown heading at `context.HeadingDepth` and a table of tool version,
machine name, OS, runtime and UTC time stamp.

**Tests**: both create a temporary directory, build a context from `--silent --log <file>` plus one action, run
`Program.Run` with it, then check the log:

- `LintMark_VersionDisplay` uses `--version`; it passes when the exit code is 0 and the log holds a
  `major.minor.patch` number.
- `LintMark_HelpDisplay` uses `--help`; it passes when the exit code is 0 and the log holds `Usage:` and `Options:`.

Each outcome line (`✓ ... - Passed` or `✗ ... - Failed: reason`) is written to the real context; a failure uses
`WriteError`.

**WriteResultsFile(context, results)**: takes the lower-cased extension of the results path. `.trx` is serialized
with `TrxSerializer`, `.xml` with `JUnitSerializer` (both from the OTS package), and the text is written to the file,
followed by `Results written to {path}`. Any other extension is an error message and nothing is written.

### Error Handling

Each test runs in a `try`; any exception, including a failure to create the temporary directory or an invalid
context, is recorded as a failed test with the message `Exception: {message}` and does not stop the other test.
`WriteResultsFile` writes an error line through `WriteError` for an unsupported extension and for any file write
failure. In all these cases the exit code becomes 1; `Run` itself throws only `ArgumentNullException` for a null
context. Results are still written when a test failed, so the failure is visible in the file.

### Dependencies

`Context` (output, `HeadingDepth`, `ResultsFile`, and creating test contexts), `Program` (`Run` and `Version`), and the
`DemaConsulting.TestResults` OTS package (result model and the TRX and JUnit serializers).

### Callers

`Program.Run` calls `Validation.Run` when `--validate` is the highest-priority action.
