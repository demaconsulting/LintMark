## Engine

### Verification Approach

The `Engine` subsystem is verified by integration tests in `EngineSubsystemTests.cs`. Each test builds a
temporary directory tree of Markdown and non-Markdown files (using the `TemporaryDirectory` test helper), runs
the real `LintMarkEngine` (with the real `FileDiscovery`, `SourceDocument`, and `DocumentParser`) against a real
`Context` that writes to a log file, and asserts on the failure lines and the exit code. The run helper
`LintMarkEngineTests.Execute` creates the `Context`, changes the current directory to the temporary tree, and
returns the exit code and the lines written. Undecodable files (a single byte that is not valid UTF-8) stand for
files that must or must not be read: a reported failure proves that a file was read, and silence proves that it
was not. No mocking is used. The tests change the current directory and are therefore run sequentially with the
other tests that touch process-wide state.

### Test Environment

Temporary directories are created and removed by each test. The directory trees avoid names that differ
only by case, so the tests behave the same on case-insensitive and case-sensitive file systems.

### Acceptance Criteria

- All integration tests pass with zero failures.
- A clean tree reports nothing and exits with 0.
- Files below `node_modules` and `.git` are not read when a directory, glob, or default expansion would select
  them, including when the folder itself is named by the argument or is the root of a glob; the same file is
  processed when it is named explicitly.
- Failures are reported in the documented format, in path order, with exit code 1.
- One worker and eight workers produce identical output and exit code.
- A file selected by several arguments is reported once, and exclusions keep files from being read.
- A log file inside the selection is refused with its content intact, and accepted when it is excluded.

### Test Scenarios

#### Normal operation

**A directory of valid Markdown is discovered, parsed, and reported clean**: A directory with valid Markdown in
nested folders, an undecodable file under `node_modules`, and a non-Markdown file is processed; nothing is
reported, the exit code is 0, and the `node_modules` file is never read.

**Sequential and parallel runs give identical output**: 150 files in several directories, some of them failing,
are processed with one worker and with eight workers; the reported lines (in the same order) and the exit codes
are identical, and the lines are in ordinal order.

**With no argument the current directory is searched**: A failing file in a subfolder is found and reported with
a relative path.

**Exclusions keep files from being processed**: `**/*.md` with `!generated/**` is processed; the failing file
under `generated` is never read and the exit code is 0.

**A file named explicitly inside a third-party folder is processed**: An undecodable file under
`node_modules/pkg` that is named on the command line is read, and its failure is reported with its relative
path and the exit code is 1.

#### Boundary conditions

**A third-party folder named as an argument selects nothing**: The folders `node_modules` and `.git`, each with an
undecodable file, are named as arguments, and so is the glob `node_modules/**/*.md`; nothing is read, nothing is
reported, and the exit code is 0.

**A file selected by several arguments is reported once**: One failing file is selected by file name, by
directory, and by glob; its failure is reported exactly once.

#### Error handling

**A log file inside the selection is refused unless it is excluded**: A tree with an undecodable `bad.md` and a
valid `notes.md` that is given as the log with default discovery: the run throws `InvalidOperationException` and
`notes.md` is unchanged. With `!notes.md` added the run reports the `bad.md` failure, exits with 1, and `notes.md`
then holds the log.

**Failures from several files are reported with position, rule, and message**: A valid file, a file with invalid
UTF-8, and a file with a bare carriage return are processed; the two failures are reported in path order in the
format `path:line:col: rule-id message` and the exit code is 1. The failure of one file does not stop the
others.
