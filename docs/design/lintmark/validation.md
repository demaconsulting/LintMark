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
that holds an empty `.git` folder and an `.editorconfig` with `root = true`, so that configuration discovery and editor
settings stop inside it and the results do not depend on the machine. `WriteFile` writes a fixture file into it with
exactly the given text (UTF-8, LF line endings). On disposal the directory is deleted with its content; cleanup errors
are ignored.

The fixtures are constant strings in the class: a compliant document, a document with violations of several rule
categories, a fixable document and its exact canonical form, a configuration, a document compliant only under that
configuration, and a non-compliant document placed in the folder the configuration ignores. The expected findings are
constant lists of `path:line:col: rule-id` (the message text is not compared).

### Key Methods

**Run(context)**: prints the header, runs the seven tests, prints `Total Tests`, `Passed` and `Failed` (the `Failed`
line goes through `WriteError` when there is a failure, which makes the exit code 1), and writes the results file when
`context.ResultsFile` is set. The header is a Markdown heading at `context.HeadingDepth` and a table of tool version,
machine name, OS, runtime and UTC time stamp.

**RunTest(context, results, name, test)**: times one test body, records `Passed` when the body returns no failure
reason and `Failed` with the reason otherwise, and writes the outcome line (`✓ ... - Passed` or `✗ ... - Failed:
reason`) to the real context; a failure uses `WriteError`.

**RunLintMark(workspace, args)**: switches the current directory to the workspace, builds a context from `--silent
--log <file>` plus the arguments, runs `Program.Run` with it, restores the current directory in a `finally`, and
returns the exit code and the log content. The tool reports paths relative to the current directory and discovers
files from it, which is why the directory is switched; the log is read after the context is disposed so that it is
complete.

**Tests**: each test creates its own workspace, writes its fixtures, runs the tool through `RunLintMark`, and compares
the exit code, the findings in report order (`CheckFindings`), and the file contents (`CheckContent`) with the
expected values:

- `LintMark_VersionDisplay` uses `--version`; it passes when the exit code is 0 and the log holds a
  `major.minor.patch` number.
- `LintMark_HelpDisplay` uses `--help`; it passes when the exit code is 0 and the log holds `Usage:` and `Options:`.
- `LintMark_CleanFileNoFindings` checks the compliant document; it passes when the exit code is 0, nothing is
  reported, and the file is unchanged.
- `LintMark_ViolationsReported` checks the document with violations; it passes when the exit code is 1, the findings
  are exactly `heading-increment`, `trailing-whitespace`, `bullet`, `list-blank-lines`,
  `heading-trailing-punctuation`, `code-fence-language`, `table-column-style` and `emphasis` at their expected lines
  and columns, and the file is unchanged.
- `LintMark_FixRewritesToCanonicalForm` runs `--fix` on the fixable document and the compliant document; it passes
  when the exit code is 0, nothing is reported, the fixable document equals its canonical form exactly (list markers
  and spacing, table layout, emphasis delimiter, trailing whitespace, and the rewrapped over-long line), the compliant
  document keeps its content and its modification time, and a second check of both reports nothing.
- `LintMark_DiffPreviewsFixes` runs `--diff` on the same two documents; it passes when the exit code is 1, the log
  holds the unified diff header and changed lines of the fixable document but no diff of the compliant one, and
  neither file is changed.
- `LintMark_ConfigurationHonored` writes `.lintmark.yaml` (`bullet: asterisk`, `heading-increment` off, `skipped/**`
  ignored) and checks the workspace with the default selection; it passes when that run reports nothing with exit
  code 0, and a run with `--no-config` reports the heading increment and bullet findings of the configured document
  and the findings of the ignored document with exit code 1.

**WriteResultsFile(context, results)**: takes the lower-cased extension of the results path. `.trx` is serialized
with `TrxSerializer`, `.xml` with `JUnitSerializer` (both from the OTS package), and the text is written to the file,
followed by `Results written to {path}`. Any other extension is an error message and nothing is written.

### Error Handling

Each test runs in a `try`; any exception, including a failure to create the temporary directory or an invalid
context, is recorded as a failed test with the message `Exception: {message}` and does not stop the other tests. The
current directory is restored even when a run throws.
`WriteResultsFile` writes an error line through `WriteError` for an unsupported extension and for any file write
failure. In all these cases the exit code becomes 1; `Run` itself throws only `ArgumentNullException` for a null
context. Results are still written when a test failed, so the failure is visible in the file.

### Dependencies

`Context` (output, `HeadingDepth`, `ResultsFile`, and creating test contexts), `Program` (`Run` and `Version`; through
it the functional tests exercise the whole tool, including the engine, the configuration, the formatter, and the lint
rules), and the `DemaConsulting.TestResults` OTS package (result model and the TRX and JUnit serializers).

### Concurrency

The functional tests change the current directory of the process for the duration of each run, so `Run` must not run
concurrently with other code that depends on the current directory. The command line runs it alone; the unit tests
run it in the sequential test collection.

### Callers

`Program.Run` calls `Validation.Run` when `--validate` is the highest-priority action.
