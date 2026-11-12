# LintMark

## Verification Approach

System-level verification uses end-to-end integration tests that invoke the tool as a real
process via the `Runner.Run` helper in `IntegrationTests.cs`. Each test exercises the full
stack — argument parsing, dispatch, file selection, parsing, reporting, and output — and validates both exit
code and combined console output. The tests treat the tool as a black box and assert only on observable
outputs; no internal implementation details are assumed. Tests that process files create them in a
temporary directory (the `TemporaryDirectory` test helper) and pass their paths as arguments.

The configuration scenarios write `.lintmark.yaml` and `.editorconfig` files into the temporary directory, run
the tool with absolute paths for the Markdown files and for any file named with `--config`, and assert on the
combined output with the path tail that the report prints (for example `docs/.lintmark.yaml:1:14`). The report
prints each path relative to the current directory of the tool process, so the assertions match on the part of the
path that lies inside the temporary directory. The scenarios about the effect of a configuration file therefore
check the observable outcome that exists today: whether a configuration problem is reported, where, in what form,
and with what exit status. The line ending that the editor settings give takes effect only once files are
rewritten, so no system requirement asserts its use in this phase: the system scenario shows that settings that
LintMark cannot use never break a run, and the formatting phase adds the system requirement and the scenario that
observe the line ending that the editor settings give. Until then that outcome is verified, without a requirement,
by tests at the Configuration subsystem, `Engine` subsystem, and unit level. The tolerance of settings that cannot be
used is the only system requirement about editor settings: the report of editor settings that cannot be read is
also verified by tests without a requirement until the formatting phase re-adds a requirement for it together with an
observable system-level behavior.

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
configuration file. Every tree that holds editor settings starts them with `root = true`.

## Acceptance Criteria

- All integration tests pass with zero failures across all supported runtimes and platforms.
- Exit code 0 is returned for all valid invocations.
- Exit code non-zero is returned for all invalid argument combinations.
- Results files are created at the specified paths when `--results` is used with `--validate`.
- Silent mode (`--silent`) produces empty combined output.
- Files named by path, directory, or glob are read and parsed; files that cannot be read or decoded are
  reported as `path:line:col: rule-id message` with exit code 1 and valid files produce no output.
- Files from `node_modules` and `.git` folders are processed only when named explicitly.
- Failures are reported in path order, and `--check` leaves files byte-identical.
- `--validate` combined with paths or `--check` is rejected with exit code 1 and without running validation.
- `--validate` combined with `--config` or `--no-config` is rejected with exit code 1 and without running
  validation, and `--config` combined with `--no-config` is rejected with exit code 1.
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
- A configuration file larger than 1 MiB and one nested 100,000 levels deep are each reported as a configuration
  problem with exit code 1, and the nested file within 10 seconds.
- A configuration problem is reported once however many Markdown files use that configuration.
- Configuration problems are reported in path order whatever the order of the arguments.
- Editor settings that LintMark cannot use (`cr`, `unset`, settings meant for other tools) are accepted with exit
  code 0 and no report.
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
passed; every file is read and parsed, the exit code is 0, and nothing is reported.

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
nested link conversion options, rule toggles by name and by legacy number, and ignore patterns gives exit code 0
and no report.

**Editor settings that cannot be used are accepted without error**: A root `.editorconfig` gives `cr` to one file,
`unset` to another, and unrelated settings (tab indentation and a maximum line length) to all; both files are
passed, and the exit code is 0 and nothing is reported. The line ending that editor settings do give is not
observable until formatting exists, so no scenario here asserts its use.

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

**Files are still checked when the configuration is invalid**: An invalid configuration and a file that cannot be
decoded are processed; the exit code is 1 and the output contains both the configuration problem and
`bad.md:1:1: file-encoding-error`.

**Reported configuration problem gives a non-zero exit code**: A file with an invalid value (`final-newline: maybe`)
beside a valid Markdown file gives exit code 1.
