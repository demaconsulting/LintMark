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

The configuration scenarios use the real `ConfigLoader`, `LintMarkConfig`, and `EditorConfigResolver` that the
engine creates itself (so the real editorconfig library too), and drive them through the same helper: the
`--config` and `--no-config` options are passed as command-line arguments to the `Context`, and the configuration
problems that the engine writes are asserted as lines, together with the file failures. The line ending that a file
ends up with is not visible in the report, so the supporting scenario about it calls the engine's `Process` method
with a real loader and a real resolver and asserts on the configuration attached to each result; no requirement
asserts it until the formatting phase makes the line ending observable.

### Test Environment

Temporary directories are created and removed by each test. The directory trees avoid names that differ
only by case, so the tests behave the same on case-insensitive and case-sensitive file systems. Each temporary
directory holds an empty `.git` directory, so configuration discovery that starts inside it never reaches a
`.lintmark.yaml` or `.editorconfig` file above the system temporary directory, and every editor-settings file that a
scenario writes declares `root = true`.

### Acceptance Criteria

- All integration tests pass with zero failures.
- A clean tree reports nothing and exits with 0.
- Files below `node_modules` and `.git` are not read when a directory, glob, or default expansion would select
  them, including when the folder itself is named by the argument or is the root of a glob; the same file is
  processed when it is named explicitly.
- Failures are reported in the documented format, in path order, with exit code 1.
- One worker and eight workers produce identical output and exit code.
- A file selected by several arguments is reported once, and exclusions keep files from being read.
- Each file is processed with the nearest configuration file of its own folder, and an invalid configuration is
  reported once, as `path:line:col: config-error message`, however many files use it, with exit code 1; the files
  are still read.
- `--config` makes one named file govern every file and its problems are reported once, also when no file is
  selected; `--no-config` reads no configuration file and no editor settings, so invalid ones are never reported.
- Editor-settings values that cannot be used are never an error.
- Configuration problems and file failures come out in the same order and with the same exit code for one worker
  and for eight workers.
- Supporting: the line ending of a file comes from its configuration, else from the editor settings, else stays
  unspecified.

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

**Each folder is processed with its nearest configuration and a broken one is reported once**: A valid root
configuration, an invalid configuration in `bad` (`wrap: nope`), and ten files below each of `good` and
`bad/deeper` are processed; the exit code is 1 and the only output is one line,
`bad/.lintmark.yaml:1:7: config-error Invalid value 'nope' for setting 'wrap'; expected one of: fill, sentence,
semantic`, in the documented failure format.

**A named configuration file governs every file and is reported once**: A valid discoverable file, an invalid
`custom.yaml`, and nine Markdown files in three folders are processed with `--config custom.yaml`; the exit code
is 1 and the only output is `custom.yaml:1:1: config-error Unknown setting` followed by the offending name, so the
discoverable file is not involved and the problem is not repeated for every file.

**The no-config option reads no configuration and no editor settings**: An invalid `.lintmark.yaml` and an
`.editorconfig` with a malformed line and an unknown `end_of_line` value are present; with `--no-config` the
exit code is 0 and nothing is written, and the same run without the option exits with 1 and writes one line.

**Sequential and parallel runs give identical configuration reports**: Six folders hold twelve files each, three of
them with an invalid configuration that has two problems, and one failing file; with one worker and with eight
workers the seven lines are identical, in ordinal path order, with the same exit code 1.

**Editor settings that cannot be used are not an error**: An `.editorconfig` that gives `cr`, `unset`, an unknown
word, an unrelated setting, and has malformed lines is present for five files; the exit code is 0 and nothing is
written.

**The line ending of each file comes from its configuration, else the editor settings, else stays unspecified
(supporting)**: Editor
settings give `crlf` to Markdown files and `cr` to `notes`, and one folder's configuration chooses `lf`. Processing a
file of that folder, a file of a folder without configuration, and a file in `notes` with a real loader and a real
resolver gives `lf` (the configuration wins), `crlf` (the editor settings fill in), and no line ending (the
unsupported `cr` leaves it unspecified).

#### Boundary conditions

**A problem with the named configuration is reported when no Markdown file is selected**: A directory that holds
only a non-Markdown file is processed with `--config missing.yaml`; the exit code is 1 and the only output is
`missing.yaml:1:1: config-error Configuration file not found`.

**A third-party folder named as an argument selects nothing**: The folders `node_modules` and `.git`, each with an
undecodable file, are named as arguments, and so is the glob `node_modules/**/*.md`; nothing is read, nothing is
reported, and the exit code is 0.

**A file selected by several arguments is reported once**: One failing file is selected by file name, by
directory, and by glob; its failure is reported exactly once.

#### Error handling

**Failures from several files are reported with position, rule, and message**: A valid file, a file with invalid
UTF-8, and a file with a bare carriage return are processed; the two failures are reported in path order in the
format `path:line:col: rule-id message` and the exit code is 1. The failure of one file does not stop the
others.
