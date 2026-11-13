# LintMark

## Verification Approach

System-level verification uses end-to-end integration tests that invoke the tool as a real
process via the `Runner.Run` helper in `IntegrationTests.cs`. Each test exercises the full
stack — argument parsing, dispatch, file selection, parsing, linting, rewriting, reporting, and output — and
validates both exit code and combined console output. The tests treat the tool as a black box and assert only on
observable outputs and on the bytes of files; no internal implementation details are assumed. Tests that process
files create them in a temporary directory (the `TemporaryDirectory` test helper) and pass their paths as
arguments.

The configuration scenarios write `.lintmark.yaml` and `.editorconfig` files into the temporary directory, run
the tool with absolute paths for the Markdown files and for any file named with `--config`, and assert on the
combined output with the path tail that the report prints (for example `docs/.lintmark.yaml:1:14`). The report
prints each path relative to the current directory of the tool process, so the assertions match on the part of the
path that lies inside the temporary directory. The scenarios about the effect of a configuration file therefore
check whether a configuration problem is reported, where, in what form, and with what exit status. The settings that
change what the tool does to a file are the line ending (`end-of-line`), `final-newline`, `max-blank-lines`,
`code-fence`, and `thematic-break`, so the scenarios that name them in a configuration file or in editor settings check
the effect on the bytes of the rewritten file.

The scenarios about the `--fix` and `--diff` options write a file with known bytes, run the tool, and then read
the bytes of the file back, so that they observe exactly what the tool did to it. The scenarios that must show that
a file was not written also compare the last write time with a time set far in the past, and the scenario about
temporary files lists the directory after the rewrite. The scenarios about the formatting rules write a file that
breaks one rule or several, run the tool with `--check`, `--diff`, or `--fix` (with a configuration file where a setting
selects the form), and compare the report and the bytes of the file afterward. Refusals that a faulty rule would
cause cannot be reached through the command line, because no rule of the tool is faulty; the Engine subsystem
tests with deliberately faulty rules verify them, and the system scenarios show the refusals that do not depend on
a rule.

`Runner.Run` merges stdout and stderr into a single combined output string. Per-stream assertions
(e.g., "standard error is empty") are not possible at this level; all assertions are made
against the combined output.

## Test Environment

Integration tests run on .NET 10.0 and .NET 11.0 across Windows, Linux, and macOS.
All scenarios are expected to produce identical results on all supported runtime and platform
combinations. Temporary files and directories are created and cleaned up within each test. Each temporary directory
holds an empty `.git` directory, so configuration discovery that starts inside it stops there and never reaches a
`.lintmark.yaml` or `.editorconfig` file that exists above the system temporary directory; the scenario about the
repository boundary builds a tree without that marker and adds its own `.git` directory below an invalid
configuration file. Every tree that holds editor settings starts them with `root = true`. The scenario about
unreadable editor settings makes the settings file unreadable by opening it without sharing on Windows and by
removing every permission from it on the other systems; it is skipped on a file system that still lets the user read
a file without permissions, such as when the tests run as a privileged user.

## Acceptance Criteria

- All integration tests pass with zero failures across all supported runtimes and platforms.
- Exit code 0 is returned for all valid invocations.
- Exit code non-zero is returned for all invalid argument combinations.
- Results files are created at the specified paths when `--results` is used with `--validate`.
- Silent mode (`--silent`) produces empty or whitespace-only combined output.
- Files named by path, directory, or glob are read, parsed, and linted; each problem found, such as a file that
  cannot be read or decoded, a mixed line ending, or an invalid grid table, is reported as
  `path:line:col: rule-id message` with exit code 1, and files without a problem produce no output.
- Files from `node_modules` and `.git` folders are processed only when named explicitly.
- Problems are reported in path order, and `--check` leaves files byte-identical.
- No file is modified unless `--fix` is given; `--diff` prints a unified diff and leaves files byte-identical.
- `--fix` rewrites a file with the line ending that its configuration, then its editor settings, then most of its
  lines give; keeps the byte order mark and all other text; leaves a compliant file unwritten; leaves no temporary
  file behind; and a second run finds nothing to repair.
- The formatting rules are applied: trailing whitespace goes except for a hard break, tabs in text become spaces, runs
  of blank lines are reduced to the configured maximum, a file ends with exactly one line ending or none as configured,
  headings are ATX headings with one space, headings, fences, lists, and tables are separated from their neighbors by
  blank lines, thematic breaks and code fences take the configured style, and indented code becomes fenced code.
- Code, HTML, math, and front matter keep their content, no blank line is added or removed inside quotes and lists, and
  a rule that a suppression comment disables by its legacy number is neither fixed nor reported.
- `--check` reports each place that a formatting rule would change under the rule identifier at its line and column,
  `--diff` shows the change of such a rule, a second `--fix` run finds nothing, and a configuration that names a
  formatting rule or its legacy number in `rules` is reported as an unknown rule.
- A file with an invalid grid table, with an invalid configuration, with editor settings that cannot be read, or whose
  conversion to another line ending would change how the document renders is never rewritten; the reason is reported,
  except that an invalid grid table is reported only while the
  `grid-table-invalid` rule is not switched off, and the file is not rewritten in either case.
- `--fix` combined with `--check` or `--diff` is rejected with exit code 1.
- `--validate` combined with paths, `--check`, `--fix`, or `--diff` is rejected with exit code 1 and without running
  validation.
- `--validate` combined with `--config` or `--no-config` is rejected with exit code 1 and without running
  validation, and `--config` combined with `--no-config` is rejected with exit code 1.
- A selected file that matches an `ignore` pattern of its configuration is skipped, unless it is named on the
  command line.
- The width of a line is measured in display columns (verified at the Utilities subsystem level).
- Each Markdown file is processed with the nearest `.lintmark.yaml` file up to the repository root: an invalid
  nearer file is reported, an invalid farther file is never read, and a file above a `.git` directory is never used.
- `--config` makes the named file govern every file instead of discovery, and a missing named file is reported
  at line 1, column 1 with exit code 1, also when no Markdown file is selected; `--no-config` ignores an invalid
  configuration file.
- A tree without a configuration file, an empty file, a file with some settings, and the documented full sample are
  all accepted with exit code 0 and no report.
- A malformed file, a file holding two configurations, a file saved as UTF-16, an unknown setting, an invalid value,
  and a setting given twice are each reported as `path:line:col: config-error message` at their position, once per
  problem and one per line, with exit code 1; the files themselves are still read and their own failures reported.
- A configuration file outside the current directory is reported with a forward-slash path relative to the current
  directory.
- A configuration file larger than 1 MiB and one nested 100,000 levels deep are each reported as a configuration
  problem with exit code 1, and the nested file within 10 seconds.
- A configuration problem is reported once however many Markdown files use that configuration.
- Configuration problems are reported in path order whatever the order of the arguments.
- Editor settings that LintMark cannot use (`cr`, `unset`, settings meant for other tools) are accepted with exit
  code 0 and no report; editor settings that apply to a file but cannot be read are reported at line 1, column 1
  of the Markdown file with exit code 1.
- `--help` lists `--config <file>` and `--no-config`.

## Test Scenarios

### Normal operation

**Version flag outputs the version**: The `--version` flag is passed as the sole argument; the tool outputs the
version string and exits with code 0.

**Help flag outputs usage information**: The `--help` flag is passed as the sole argument; the combined output
contains "Usage" and "Options" and the tool exits with code 0.

**Help lists the path arguments and the check option**: The `--help` output contains `[paths...]` and
`--check`, and the exit code is 0.

**Validate flag runs the self-validation**: The `--validate` flag is passed as the sole argument; the combined
output contains "Total Tests:" and the tool exits with code 0.

**Validation results are written as TRX**: `--validate` combined with `--results <path>.trx` creates a file that
contains a `<TestRun` element, and the tool exits with code 0.

**Validation results are written as JUnit XML**: `--validate` combined with `--results <path>.xml` creates a file
that contains a `<testsuites` element, and the tool exits with code 0.

**Legacy result alias writes the results file**: `--validate` combined with `--result <path>.trx` creates a TRX
file, and the tool exits with code 0.

**Depth option sets the heading level of the validation output**: `--validate` combined with `--depth 3` gives
output that contains `###`, and the tool exits with code 0.

**Silent flag suppresses output**: `--version` and `--silent` together give empty or whitespace-only combined
output, and the tool exits with code 0.

**Log flag writes the output to a file**: `--log <path>` creates the file, and it contains output that also
appears in the combined console output.

**Invocation without arguments displays the banner**: The combined output contains the tool name and the
copyright notice, and the exit code is 0.

**Valid Markdown file is processed without output**: A valid file in a temporary directory is passed as the only
argument; the exit code is 0 and no rule identifier appears in the output.

**Directory selects every Markdown file beneath it**: A directory with a valid file, a nested invalid Markdown
file, and an invalid non-Markdown file is passed; the exit code is 1, only the nested Markdown file is
reported, and the non-Markdown file is never read.

**Explicit file inside a third-party folder is processed**: An undecodable file under `node_modules/pkg` that is
named on the command line is read; the exit code is 1 and the output contains
`bad.md:1:1: file-encoding-error`.

**File with every supported construct is processed without output**: A file that contains front matter,
suppression comments, a reference definition, a footnote, a table, and an unclosed fence is passed; the exit
code is 0 and nothing is reported.

**Repository documentation is processed without output**: The repository's `docs` directory and `README.md` are
passed; every file is read, parsed, and linted, the exit code is 0, and nothing is reported.

**Check flag lints without writing**: `--check` is passed with a valid file that has CRLF line endings and no
final newline; the exit code is 0 and the file's bytes and last-write time are unchanged.

**Help lists the configuration options**: The `--help` output contains `--config <file>` and `--no-config`, and the
exit code is 0.

**Nearest configuration file is used and its problems are reported**: A valid `.lintmark.yaml` in the root and an
invalid one in `docs` (with `line-length: wide`) are created, and `docs/a.md` is passed; the exit code is 1 and the
output contains `docs/.lintmark.yaml:1:14: config-error Invalid value 'wide' for setting 'line-length'`, so the
nearer file governs the file.

**Farther invalid configuration is never read when a nearer valid one exists**: An invalid file with an unknown
setting in the root and a valid file in `docs` are created, and `docs/a.md` is passed; the exit code is 0 and the
output contains no `config-error`.

**Configuration above the repository root is not used**: An invalid `.lintmark.yaml` is created above a folder that
holds a `.git` directory, and a file inside that folder is passed; the exit code is 0 and nothing is reported.

**Named configuration file is used instead of discovery**: An invalid discoverable file and a valid file in
`custom/settings.yaml` are created; with `--config` naming the valid file by its absolute path, the exit code is 0
and nothing is reported.

**No-config option ignores an invalid configuration file**: An invalid file sits beside the Markdown file; with
`--no-config` the exit code is 0 and no `config-error` appears, while the same run without the flag exits with 1
and reports a `config-error`.

**Tree without a configuration file runs with the defaults**: A valid file in a tree without any configuration
file gives exit code 0 and no report.

**Empty configuration file means the defaults**: An empty `.lintmark.yaml` gives exit code 0 and no report.

**Configuration file with only some settings is accepted**: A file that sets only `wrap: semantic` gives exit code
0 and no report.

**Documented full sample configuration is accepted**: The sample that sets every formatting and linting setting,
nested link conversion options, rule toggles by name and by legacy number, and ignore patterns is saved beside a
file that has the CRLF line endings that the sample asks for; the exit code is 0 and nothing is reported.

**Editor settings that cannot be used are accepted without error**: A root `.editorconfig` gives `cr` to one file,
`unset` to another, and unrelated settings (tab indentation and a maximum line length) to all; both files are
passed, and the exit code is 0 and nothing is reported.

**Fix option rewrites a CRLF file with the configured line ending**: A configuration asks for `end-of-line: lf` and
a file has CRLF line endings; with `--fix` the exit code is 0, nothing is reported, and the file holds the same text
with LF line endings.

**Fix option leaves a compliant file untouched**: A file that already has the configured LF line endings and a last
write time in 2020 is passed with `--fix`; the exit code is 0, the bytes are the same, and the last write time is
unchanged, so the file was not written.

**Second fix run finds nothing to repair**: A file with mixed line endings is fixed by a first run; a second run
with `--fix` gives exit code 0, no `line-ending` report, and the same bytes.

**Fix option keeps the byte order mark and the other text**: A CRLF file with a byte order mark and non-ASCII text
(accented letters and Japanese characters) and a configuration that asks for LF is passed with `--fix`; the exit
code is 0 and the file holds the byte order mark, the identical text, and LF line endings.

**Rewrite leaves no temporary file behind**: After `--fix` rewrites a CRLF file, the directory holds only the
configuration file and the Markdown file.

**Diff option prints the change and writes nothing**: A CRLF file and a configuration that asks for LF are passed
with `--diff`; the exit code is 1, the output contains the `--- a/` and `+++ b/` header lines and the note
`\ Line endings change from CRLF to LF`, and the file's bytes are unchanged.

**Diff option prints nothing for a compliant file**: A compliant file is passed with `--diff`; the exit code is 0
and the output contains neither a diff header nor a hunk marker.

**Line ending of the configuration wins**: A configuration asks for CRLF, the editor settings ask for LF, and the
file has LF line endings; with `--fix` the exit code is 0 and the file has CRLF line endings.

**Line ending of the editor settings is used when the configuration gives none**: Editor settings ask for CRLF for
Markdown files and the file has LF line endings; with `--fix` the exit code is 0 and the file has CRLF line endings.

**Line ending that most lines use is kept when nothing is configured**: A file with two CRLF terminators and one LF
terminator and no configuration or editor settings is passed with `--fix`; the exit code is 0 and every line ending
is CRLF.

**Mixed line endings are reported by a check and removed by a fix**: A file with two CRLF terminators and one LF
terminator is passed first without options and then with `--fix`; the first run gives exit code 1 and the output
contains `a.md:1:1: line-ending Mixed line endings (2 CRLF, 1 LF); expected CRLF`, and the second run gives exit
code 0 and no `line-ending` report.

**Check reports a formatting violation with its rule and position**: A file `text` (with a trailing space) and
`more` is passed without options; the exit code is 1, the output contains `a.md:1:5: trailing-whitespace`, and the
file's bytes are unchanged.

**Fix removes trailing whitespace and keeps a hard break**: A file with a line that ends with two spaces, one with
one space, one with a backslash, and one with three spaces is passed with `--fix`; the exit code is 0, nothing is
reported, and only the single space and the third space are gone.

**Fix expands tabs in text only**: A file with a tab in text and a tab in a code span is passed with `--fix`; the tab in
text becomes three spaces and the code span is unchanged.

**Fix collapses blank lines to the maximum**: A file with three blank lines between two paragraphs is passed with
`--fix` without a configuration and with `max-blank-lines: 2`; one blank line and two blank lines remain.

**Fix gives the file exactly one final line ending**: A file without a final line ending and one with extra blank lines
at the end are passed with `--fix`; both end with exactly one line ending.

**Fix removes the final line ending when configured**: A file with a final line ending under `final-newline: false` is
passed with `--fix`; it ends with no line ending.

**Fix writes headings as ATX headings with one space**: A file with a setext heading, a closed heading, an indented
heading, and a heading with several spaces is passed with `--fix`; each heading is in canonical form.

**Fix adds blank lines around headings, fences, lists, and tables**: A file whose heading, fence, list, and table touch
their neighbors is passed with `--fix`; every boundary has exactly one blank line.

**Fix writes thematic breaks in the configured style**: A file with breaks written with asterisks, underscores, and
spaced dashes is passed with `--fix` without a configuration and with `thematic-break: asterisks`; the breaks are three
dashes and three asterisks.

**Fix writes code fences with the configured character and fences indented code**: A file with a tilde fence and an
indented block is passed with `--fix` without a configuration and with `code-fence: tilde`; the blocks are fenced with
backticks and with tildes.

**Fix never changes code, HTML, math, or front matter**: A file with trailing whitespace, tabs, and blank lines inside
front matter, fenced code, an HTML block, and a math block is passed with `--fix`; the exit code is 0, nothing is
reported, and the bytes are the same.

**Fix adds no blank lines inside lists and quotes**: A file with a fence in a list item and a heading directly followed
by text in a quote is passed with `--fix`; the exit code is 0 and the bytes are the same.

**A rule disabled by its legacy number is neither fixed nor reported**: A file with trailing whitespace after a comment
that disables `MD009` is passed with `--fix`; nothing is reported and the bytes are the same.

**Diff shows the change of a formatting rule**: A file with a closed heading and a trailing space is passed with
`--diff`; the exit code is 1, the output shows the removed and the added lines of both changes, and the file is
unchanged.

**Second fix run finds nothing after the formatting rules**: A file that breaks many rules is fixed by a first run; a
check run on the result reports nothing, and a second `--fix` run gives exit code 0 and the same bytes.

**A formatting rule cannot be switched off in the configuration**: A configuration lists `MD009: false` under `rules`;
the exit code is 1 and the output reports a `config-error` for the unknown rule `MD009`.

**Ignore patterns skip the matching files of a glob**: A configuration ignores `drafts/**` and both `drafts/bad.md`
and `kept/bad.md` are undecodable; an absolute glob that selects every Markdown file gives exit code 1, a
`kept/bad.md:1:1: file-encoding-error` report, and no mention of `drafts/bad.md`.

**Ignored file that is named on the command line is processed**: The same configuration and an undecodable
`drafts/bad.md` that is named explicitly give exit code 1 and a `drafts/bad.md:1:1: file-encoding-error` report.

**Exclusion on the command line still removes a named file**: An undecodable file is named and also excluded with a
`!` pattern that covers it; the exit code is 0 and no `file-encoding-error` is reported.

### Boundary conditions

**Glob and exclusion skip the excluded files**: An absolute glob and an absolute `!` exclusion are passed so
that an invalid file lies under the excluded directory; the exit code is 0 and the excluded file is not
reported.

**Third-party folder named as an argument selects nothing**: The folders `node_modules` and `.git`, each with an
undecodable file, are passed, and so is an absolute glob inside `node_modules`; nothing is reported and the exit
code is 0 in each case.

**Several failing files are reported in path order**: Three invalid files are passed in non-sorted order; the exit
code is 1 and the diagnostics appear in path order.

**Configuration problems of several folders are reported in path order**: Invalid configurations in `b` and `a` are
used by files that are passed in the order `b`, `a`; the exit code is 1 and the problem of `a` appears before the
problem of `b`.

### Error handling

**Unknown argument is rejected**: An unrecognized argument (`--unknown`) gives a non-zero exit code and an error
message that identifies it.

**Unsupported results extension is rejected**: `--validate` combined with `--results <path>.bad` gives a
non-zero exit code and creates no file.

**Undecodable file is reported with a diagnostic**: A file with an invalid UTF-8 byte on its second line gives
exit code 1 and the output contains `bad.md:2:3: file-encoding-error Invalid UTF-8 byte sequence`.

**Missing file is reported as a read error**: A path that does not exist gives exit code 1 and the output
contains `missing.md:1:1: file-read-error File not found`.

**Validate combined with a path is rejected**: The exit code is 1, the output contains an error that names
`--validate`, and validation does not run.

**Validate combined with check is rejected**: The exit code is 1 and the output contains an error.

**Validate combined with fix is rejected**: `--validate --fix` gives exit code 1, an error that names `--fix`, and no
validation output.

**Validate combined with diff is rejected**: `--validate --diff` gives exit code 1, an error that names `--diff`, and
no validation output.

**Validate combined with the config option is rejected**: `--validate --config a.yaml` gives exit code 1, an error
that names `--config`, and no validation output.

**Validate combined with the no-config flag is rejected**: `--validate --no-config` gives exit code 1, an error
that names `--no-config`, and no validation output.

**Config option combined with the no-config flag is rejected**: The exit code is 1 and the error names both
options.

**Missing named configuration file is reported**: `--config` names a file that does not exist; the exit code is 1
and the output contains `missing.yaml:1:1: config-error Configuration file not found`.

**Missing named configuration file is reported when no Markdown file is selected**: `--config` names a file that
does not exist and the only path is a directory without Markdown files; the exit code is 1 and the output contains
`missing.yaml:1:1: config-error Configuration file not found`.

**Unknown setting is rejected instead of ignored**: A configuration with a misspelled name for the line length
setting gives exit code 1 and a line at `.lintmark.yaml:1:1` that starts with `config-error Unknown setting` and
names the misspelled word.

**Invalid value is rejected and the allowed values are named**: `bullet: star` gives exit code 1 and
`.lintmark.yaml:1:9: config-error Invalid value 'star' for setting 'bullet'; expected one of: dash, asterisk, plus`.

**Setting given twice is reported at its second occurrence**: A file that sets `line-length` on lines 1 and 3
gives exit code 1 and `.lintmark.yaml:3:1: config-error Duplicate setting 'line-length' (first defined on line 1)`.

**Malformed YAML is rejected with a position**: A file with an unterminated quote gives exit code 1 and a line that
matches `.lintmark.yaml:<line>:<col>: config-error Invalid YAML`.

**File holding two configurations is rejected at the second**: A file with a document marker between two groups of
settings gives exit code 1 and `.lintmark.yaml:2:1: config-error Only one YAML document is allowed`.

**File saved as UTF-16 is rejected at its start**: A configuration file with a UTF-16 byte order mark gives exit
code 1 and a line that starts `.lintmark.yaml:1:1: config-error Unsupported byte order mark`.

**Oversized configuration file is rejected**: A file one byte larger than 1 MiB gives exit code 1 and a line that
starts `.lintmark.yaml:1:1: config-error` and names the limit of 1 MiB.

**Deeply nested configuration file is rejected promptly**: A file whose value is nested 100,000 levels deep gives
exit code 1 and a `config-error Nesting is too deep` line on line 1, and the whole run takes less than 10 seconds.

**Configuration problems are reported one per line in the documented form**: A file with an unknown setting on line
1 and an invalid value on line 2 gives exit code 1 and exactly two `config-error` lines, in that order: one ending
in `:1:1: config-error Unknown setting` followed by the offending name and one ending in
`:2:7: config-error Invalid value 'nope' for setting 'wrap'; expected one of: fill, sentence, semantic`.

**Configuration problem is reported once for many files**: One invalid configuration is used by twelve Markdown
files in three folders, all passed in one run; the exit code is 1 and exactly one `config-error` line appears.

**Configuration file outside the current directory is reported with a relative, forward-slash path**: An invalid
configuration in a temporary directory, which lies outside the current directory of the tool process, is named with
`--config` by its absolute path; the exit code is 1 and exactly one `config-error` line appears, whose path is the
path relative to the current directory with forward slashes and without backslashes, followed by
`:1:1: config-error Unknown setting` and the offending name.

**Files are still checked when the configuration is invalid**: An invalid configuration and a file that cannot be
decoded are processed; the exit code is 1 and the output contains both the configuration problem and
`bad.md:1:1: file-encoding-error`.

**Reported configuration problem gives a non-zero exit code**: A file with an invalid value (`final-newline: maybe`)
beside a valid Markdown file gives exit code 1.

**Fix option combined with the check option is rejected**: `--fix --check` and a file give exit code 1 and an error
that says `--fix cannot be combined with --check or --diff`.

**Fix option combined with the diff option is rejected**: `--fix --diff` and a file give exit code 1 and the same
error.

**Unreadable editor settings are reported at the Markdown file**: Editor settings for Markdown files are made
unreadable and a CRLF file is passed with `--fix`; the exit code is 1, the output contains
`a.md:1:1: config-error Cannot read .editorconfig settings`, and the file's bytes are unchanged. The scenario is
skipped where the file system lets the user read a file without permissions.

**Invalid grid table is reported at the offending line**: A file whose fourth line is a table border outside any
valid grid table gives exit code 1 and `a.md:4:1: grid-table-invalid`.

**Document with an invalid grid table is never rewritten**: A CRLF file with an invalid grid table and a
configuration that asks for LF is passed with `--fix`; the exit code is 1, the output contains
`a.md:2:1: grid-table-invalid`, and the file's bytes are unchanged.

**Switching off the grid table report does not allow a rewrite**: The same file is passed with `--fix` while the
configuration switches the `grid-table-invalid` rule off; the exit code is 0, the output does not mention
`grid-table-invalid`, and the file's bytes are unchanged.

**File with an invalid configuration is never rewritten**: A configuration with an unknown setting and a file with
mixed line endings are passed with `--fix`; the exit code is 1, the output contains
`config-error Unknown setting 'bogus'`, and the file's bytes are unchanged.

**A conversion of the line endings that changes the rendering is never written**: A file with a code span framed by
line breaks and LF line endings and a configuration that asks for CRLF are passed with `--fix`; the exit code is 1, the
output contains `a.md:1:1: safety-html-mismatch`, and the file's bytes are unchanged.

### Display width

**Display width of a mixed-script line is measured in columns**: No command-line behavior depends on the width of a
line yet, because the rules that apply a width limit come later. The requirement is therefore verified at the
Utilities subsystem level: a line with accented, combining, East Asian, and emoji characters is measured, and the
width counts display columns and not UTF-16 code units; a line padded to a fixed number of columns measures exactly
that number of columns.
