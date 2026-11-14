## Engine

### Verification Approach

The `Engine` subsystem is verified by integration tests in `EngineSubsystemTests.cs`. Each test builds a
temporary directory tree of Markdown and non-Markdown files (using the `TemporaryDirectory` test helper), runs
the real `LintMarkEngine` (with the real `FileDiscovery`, `SourceDocument`, `DocumentParser`, `DocumentFormatter`,
safety checkers, `TextReporter`, `DiffReporter`, and `AtomicFileWriter`) against a real `Context` that writes to a
log file, and asserts on the diagnostic lines, the exit code, and the bytes of the files afterward. The run helper
`LintMarkEngineTests.Execute` creates the `Context`, changes the current directory to the temporary tree, and
returns the exit code and the lines written (the diff goes to standard output in the same helper, so the `--diff`
scenario sees it in the lines). Undecodable files (a single byte that is not valid UTF-8) stand for files that must
or must not be read: a reported diagnostic proves that a file was read, and silence proves that it was not. The
tests change the current directory and are therefore run sequentially with the other tests that touch process-wide
state.

The default engine is created with the five block formatters of the `Blocks` subsystem and the inline normalizer of the
`Inline` subsystem, so it applies every formatting
rule of the tool to the files it processes. The scenarios that need a change of the text that is independent of those
rules, or a refusal by a safety check, install test doubles through the engine's
`formatter` seam: the paragraph formatters from `Formatting/TestBlockFormatters.cs`, which are the replacing
formatter (collapses a double space) and the broken formatter that drops a hard break (so that the rendering
would change). Editor settings that cannot be read are provoked through the `editorConfigFactory` seam with a
resolver built from two throwing delegates. Everything else is real, including the file system.

The configuration scenarios use the real `ConfigLoader`, `LintMarkConfig`, and `EditorConfigResolver` that the
engine creates itself (so the real editorconfig library too), and drive them through the same helper: the
`--config` and `--no-config` options are passed as command-line arguments to the `Context`, and the configuration
problems that the engine writes are asserted as lines, together with the file diagnostics. The effective line
ending of each file is also checked at the result level by calling the engine's `Process` method with a real
loader and a real resolver and asserting on the configuration attached to each result.

The rewriting scenarios run the engine with `--fix`, `--diff`, and the default check mode and compare the report,
the exit code, and the bytes of the files (including a byte order mark and the absence of leftover files). The
parallelism scenarios run two identical trees, one with one worker and one with many, and compare everything.

### Test Environment

Temporary directories are created and removed by each test. The directory trees avoid names that differ
only by case, so the tests behave the same on case-insensitive and case-sensitive file systems. Each temporary
directory holds an empty `.git` directory, so configuration discovery that starts inside it never reaches a
`.lintmark.yaml` or `.editorconfig` file above the system temporary directory, and every editor-settings file that a
scenario writes declares `root = true`. The files that a `--fix` scenario rewrites are inside the temporary
directory and are removed with it.

### Acceptance Criteria

- All integration tests pass with zero failures.
- A clean tree reports nothing and exits with 0.
- Files below `node_modules` and `.git` are not read when a directory, glob, or default expansion would select
  them, including when the folder itself is named by the argument or is the root of a glob; the same file is
  processed when it is named explicitly.
- A file that an `ignore` pattern of its configuration matches is neither read nor reported when it was found
  through a glob, directory, or the default selection, and is processed when it is named explicitly.
- Diagnostics are reported in the documented format, in path order, with exit code 1.
- One worker and eight workers produce identical output, exit code, and file contents, with and without `--fix`.
- A file selected by several arguments is reported once, and exclusions keep files from being read.
- Each file is processed with the nearest configuration file of its own folder, and an invalid configuration is
  reported once, as `path:line:col: config-error message`, however many files use it, with exit code 1; the files
  are still read.
- `--config` makes one named file govern every file and its problems are reported once, also when no file is
  selected; `--no-config` reads no configuration file and no editor settings, so invalid ones are never reported.
- Editor-settings values that cannot be used are never an error; editor settings that cannot be read are reported at
  line 1, column 1 of the Markdown file, and that file is not rewritten.
- Configuration problems and file diagnostics are merged in path order and, within a file, in position order.
- A configuration file outside the current directory is reported with a forward-slash path relative to the current
  directory, starting with `../`.
- The line ending of a file comes from its configuration, else from the editor settings, else stays unspecified.
- With `--fix` a file with a rewrite that passed every safety check is replaced with the exact bytes (byte order
  mark and chosen line ending included), no temporary file remains, and the diagnostics that the rewrite removed are
  no longer reported; without `--fix` no file is ever written.
- With `--diff` the unified diff of the proposed change is printed and no file is written.
- The default engine checks a file that breaks formatting rules, reporting each violation under the identifier of its
  rule, and writes nothing.
- The default engine previews the change that the fix option would make to such a file, and writes nothing.
- The default engine repairs such a file with `--fix` and finds nothing in the repaired file.
- A rewrite that a safety check refuses is reported as a safety diagnostic and the file is left byte for byte as it
  was; this includes a conversion of the line endings that would change how the document renders.
- A document with an invalid grid table is reported as `grid-table-invalid` at the offending line and is never
  rewritten.

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
`node_modules/pkg` that is named on the command line is read, and its diagnostic is reported with its relative
path and the exit code is 1.

**Files that the ignore patterns match are skipped when found and processed when named**: A configuration with
`ignore: drafts/**` and an undecodable file below `drafts` and another below `kept` are run with the glob
`**/*.md`; only the `kept` file is reported, with exit code 1. When the same ignored file is named on the command
line instead, it is read and its `file-encoding-error` is reported.

**Each folder is processed with its nearest configuration and a broken one is reported once**: A valid root
configuration, an invalid configuration in `bad` (`wrap: nope`), and ten files below each of `good` and
`bad/deeper` are processed; the exit code is 1 and the only output is one line,
`bad/.lintmark.yaml:1:7: config-error Invalid value 'nope' for setting 'wrap'; expected one of: fill, sentence,
semantic`, in the documented diagnostic format.

**A named configuration file governs every file and is reported once**: A valid discoverable file, an invalid
`custom.yaml`, and nine Markdown files in three folders are processed with `--config custom.yaml`; the exit code
is 1 and the only output is `custom.yaml:1:1: config-error Unknown setting` followed by the offending name, so the
discoverable file is not involved and the problem is not repeated for every file.

**A configuration outside the current directory is reported with a parent-relative path**: With a sub folder as the
current directory, an invalid `other.yaml` in its parent named as `../other.yaml` with `--config`, and a Markdown file
in the sub folder, the exit code is 1 and the only output is `../other.yaml:1:1: config-error Unknown setting`
followed by the offending name, in the documented diagnostic format.

**The no-config option reads no configuration and no editor settings**: An invalid `.lintmark.yaml` and an
`.editorconfig` with a malformed line and an unknown `end_of_line` value are present; with `--no-config` the
exit code is 0 and nothing is written, and the same run without the option exits with 1 and writes one line.

**Sequential and parallel runs give identical configuration reports**: Six folders hold twelve files each, three of
them with an invalid configuration that has two problems, and one failing file; with one worker and with eight
workers the seven lines are identical, in ordinal path order, with the same exit code 1.

**Configuration problems and file diagnostics are merged in path and position order**: A root configuration with two
problems (`wrap: nope` and an unknown setting), a nested configuration in `docs` with one problem, and two
undecodable files (`a.md` and `docs/x.md`) are run with four workers; the exit code is 1 and the five lines come out
in ordinal path order (the dotted `.lintmark.yaml` first, then `a.md`, then `docs/.lintmark.yaml`, then
`docs/x.md`) and, within a file, in position order, with the messages of the configuration and file problems.

**Editor settings that cannot be used are not an error**: An `.editorconfig` that gives `cr`, `unset`, an unknown
word, an unrelated setting, and has malformed lines is present for five files; the exit code is 0 and nothing is
written.

**The line ending of each file comes from its configuration, else the editor settings, else stays unspecified**:
Editor settings give `crlf` to Markdown files and `cr` to `notes`, and one folder's configuration chooses `lf`.
Processing a file of that folder, a file of a folder without configuration, and a file in `notes` with a real loader
and a real resolver gives `lf` (the configuration wins), `crlf` (the editor settings fill in), and no line ending
(the unsupported `cr` leaves it unspecified).

**Fix rewrites a CRLF file to the configured line ending and the problem is gone**: A CRLF file below a folder and a
configuration with `end-of-line: lf` give one `line-ending` diagnostic in check mode. After `--fix` the exit code
is 0, the file holds exactly the LF bytes, and a second check run reports nothing.

**Fix keeps the byte order mark and leaves no temporary file**: A CRLF file with a UTF-8 byte order mark and a
configuration that chooses LF is rewritten with `--fix` through the real writer; the exit code is 0, nothing is
reported, the file starts with the same three bytes followed by the LF text, and the directory holds only the
configuration, the file, and the log.

**Diff prints the change and writes nothing**: A file with a double space and the replacing formatter installed
through the `formatter` seam are run with `--diff`; the exit code is 0, the output is the unified diff
(`--- a/a.md`, `+++ b/a.md`, `@@ -1 +1 @@`, `-a  b`, `+a b`), and the file is byte for byte as it was.

**Fix gives the same report and the same files for any degree of parallelism**: Two identical trees of 40 files
(CRLF, clean, and undecodable) with `end-of-line: lf` are fixed, one with one worker and one with sixteen; the
exit code is 1, the lines are identical and in ordinal order, and every file has the same bytes in both trees.

**The default engine reports each formatting violation and writes nothing in check mode**: A file whose heading has two
spaces after the number sign and touches its paragraph, and whose paragraph ends with spaces, is checked by the default
engine. The check reports `heading-space`, `heading-blank-lines`, and `trailing-whitespace`, exits with 1, and the file
is byte for byte as it was.

**The default engine shows the change with the diff option**: The same file is run with `--diff` by the default engine.
The exit code is 1, the output shows the heading and the paragraph before and after the change, and the file is byte
for byte as it was.

**The default engine applies the formatting rules**: The same file is checked, fixed, and checked again by the default
engine. The first check reports `heading-space`, `heading-blank-lines`, and `trailing-whitespace` and exits with 1,
the fix leaves the file with the canonical heading, a blank line, and the paragraph without trailing spaces, and the
last check reports nothing.

#### Boundary conditions

**A problem with the named configuration is reported when no Markdown file is selected**: A directory that holds
only a non-Markdown file is processed with `--config missing.yaml`; the exit code is 1 and the only output is
`missing.yaml:1:1: config-error Configuration file not found`.

**A third-party folder named as an argument selects nothing**: The folders `node_modules` and `.git`, each with an
undecodable file, are named as arguments, and so is the glob `node_modules/**/*.md`; nothing is read, nothing is
reported, and the exit code is 0.

**A file selected by several arguments is reported once**: One failing file is selected by file name, by
directory, and by glob; its diagnostic is reported exactly once.

#### Error handling

**Diagnostics from several files are reported with position, rule, and message**: A valid file, a file with invalid
UTF-8, and a file with a bare carriage return are processed; the two diagnostics are reported in path order in the
format `path:line:col: rule-id message` and the exit code is 1. The failure of one file does not stop the
others.

**Fix refuses a rewrite that the safety checks reject and reports it**: A file with a hard break and the broken
formatter that drops it are run with `--fix`; the exit code is 1, the only line is a `safety-html-mismatch`
diagnostic at line 1, column 2, and the file is byte for byte as it was.

**Fix refuses a conversion of the line endings that changes the rendering**: A code span framed by line breaks in an LF
file and a configuration that asks for CRLF are run with `--fix`; the exit code is 1, a `safety-html-mismatch`
diagnostic at line 1, column 1 is reported, and the file is byte for byte as it was.

**Editor settings that cannot be read are reported at the Markdown file and the file is not rewritten**: A CRLF
Markdown file and an engine whose resolver throws an `IOException` are run with `--fix`; the exit code is 1, the
only line is `a.md:1:1: config-error Cannot read .editorconfig settings: disk failure`, and the file is unchanged.

**A document with an invalid grid table is reported and never rewritten**: A CRLF document with an invalid grid
table border on line 2 and a configuration that asks for LF are run with `--fix`; the exit code is 1, the only
line starts with `a.md:2:1: grid-table-invalid`, and the file has the same bytes as before.
