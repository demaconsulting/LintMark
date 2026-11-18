# Usage

## Installation

Install the tool globally with the .NET CLI. The tool targets .NET 10 and .NET 11 and runs on Windows, Linux, and macOS:

```bash
dotnet tool install -g DemaConsulting.LintMark
```

The tool is then available as the `lintmark` command. To update it, run
`dotnet tool update -g DemaConsulting.LintMark`.

## Command Synopsis

```text
lintmark [options] [paths...]
```

When invoked, LintMark prints a banner with its version and copyright to standard output (unless `--silent` is
given), and then does one of the following. Only the first match applies, in this order:

1. `--version` prints the version number only.
2. `--help` prints the banner and the usage text.
3. `--validate` runs self-validation (see *Self-Validation*).
4. Otherwise the selected Markdown files are linted.

## Selecting Files

Name the files to process as arguments. Each argument that does not start with a dash is a path:

```bash
# No arguments: every **/*.md file under the current directory
lintmark

# One file
lintmark README.md

# A directory: every *.md file beneath it, at any depth
lintmark docs

# Several arguments at once
lintmark README.md docs
```

Arguments can also be glob patterns. The wildcard `*` matches within one path segment and `**` matches any number of
directory levels. Patterns may be relative, relative to a parent directory (`../docs/*.md`), or absolute. Both `/` and
`\` are accepted as separators in every argument on every platform, so `docs\guide.md` selects the same file on Linux
as on Windows; on Linux and macOS this means a backslash cannot be part of a file name given on the command line.
Quote patterns so that the shell passes them unchanged:

```bash
lintmark "docs/**/*.md"
```

To leave files out, prefix a pattern with `!` (and quote it). An exclusion also applies to files named explicitly, and
an exclusion that names a directory removes everything beneath it. When only exclusions are given, they are applied to
the default `**/*.md` selection:

```bash
lintmark "docs/**/*.md" "!docs/generated/**"
lintmark "!docs/generated"
```

How the files are selected:

- A file named explicitly is always processed, even if it is not a `.md` file or lies below `node_modules`, and even
  if an `ignore` pattern of the configuration matches it (see *Configuration*).
- A directory or glob that starts inside a `node_modules` or `.git` folder selects nothing, and the default selection
  skips those folders.
- Files that match an `ignore` pattern of the configuration are left alone unless they are named explicitly. By
  default `.git`, `node_modules`, `.venv`, `generated`, `thirdparty`, and `.agent-logs` folders are ignored.
- A file selected by several arguments is processed once.
- A link to a folder (symbolic link or junction) is followed, except a link back into a folder that the search is
  already inside, which is skipped so that the search ends. A folder that cannot be read is skipped.
- A pattern that matches no file is not an error. A name that matches no wildcard pattern and does not exist is
  reported as a `file-read-error`.
- On Windows and macOS, patterns ignore letter case; on other systems they do not. Where case is ignored and one file
  is selected under spellings that differ only in case, it is processed once and reported under the alphabetically
  first spelling (upper case before lower case).

LintMark reads files as UTF-8 text (with or without a byte order mark) that uses LF or CRLF line endings.

## Check Mode

Without `--fix`, LintMark only checks: it reports every problem it finds and never writes a file. Use `--check` to
state this explicitly; the behavior is the same with or without the option:

```bash
lintmark --check docs
```

For this file `notes.md` (the first list item is directly after a paragraph, and the first line ends with spaces):

```markdown
# Notes

Some text   
* one
* two
```

the output is:

```text
notes.md:3:10: trailing-whitespace A line ends with trailing whitespace, or a hard break is not exactly two spaces.
notes.md:4:1: bullet A bullet list item does not use the configured marker character.
notes.md:4:1: list-blank-lines A list is not surrounded by blank lines.
notes.md:5:1: bullet A bullet list item does not use the configured marker character.
```

A formatting problem is reported at the line and column where `--fix` would change the file, with the identifier of the
rule and its description. A file whose line endings are not the ones chosen for it is reported as `line-ending` at line
1, column 1 (see *Line Endings and .editorconfig* in *Configuration*). A rule that only reports, such as
`heading-increment`, gives a message that describes the specific problem (see *Lint Rules*).

## Fixing Files

Use `--fix` to rewrite files into their canonical form:

```bash
lintmark --fix docs
```

- A file is rewritten only when the rewrite changes it. A file that is already compliant is not touched at all: its
  content and its modification time stay as they are.
- Only what can be fixed is rewritten. A problem that rewriting removes is no longer reported after the file was
  written; problems that remain (such as every lint-only finding) and files that were not rewritten are reported as
  usual. The exit code is `0` when nothing remains to report and `1` otherwise, so a second `--fix` run over the same
  files reports nothing and changes nothing.
- A file is never rewritten when its configuration is invalid or its editor settings cannot be read, because the
  settings it would be rewritten with are not known. The configuration problem is reported instead.
- A file that contains a line that looks like a grid table border but is not part of a valid grid table is never
  rewritten (see *Tables and Links*).
- The byte order mark of a file, if it has one, is kept.
- A file is written atomically: the new content is written to a temporary file in the same directory and then replaces
  the original in one step, so a crash or a full disk never leaves a half-written file and no temporary file remains.
  The permission bits of the file are kept on Linux and macOS, and a symbolic link stays a link (the file it points to
  is rewritten). A file that was changed by another program after LintMark read it, and a read-only file, are not
  overwritten.
- When a file cannot be written, it stays as it was and the problem is reported as `file-write-error` at line 1, column
  1, together with the other findings of the file; the other files are still processed:

```text
docs/guide.md:1:1: file-write-error Cannot write file: The file changed after it was read; it was not rewritten.
```

- Files are rewritten one after the other, in path order, after all files have been processed.

`--fix` cannot be combined with `--check` or `--diff`.

Running `--fix` on `notes.md` from the previous section reports nothing, exits with `0`, and leaves:

```markdown
# Notes

Some text

- one
- two
```

## Previewing Fixes

Use `--diff` to see what `--fix` would change, without writing anything:

```bash
lintmark --diff docs
```

For every file that `--fix` would rewrite, a unified diff is written to standard output, in path order, after the
findings, which are reported on standard error as in check mode. For `notes.md` the diff is:

```text
--- a/notes.md
+++ b/notes.md
@@ -1,5 +1,6 @@
 # Notes
 
-Some text   
-* one
-* two
+Some text
+
+- one
+- two
```

The paths are relative to the current directory. A line ending change, which a line-based diff cannot show, is
described by a note line:

```text
--- a/docs/guide.md
+++ b/docs/guide.md
\ Line endings change from CRLF to LF
```

When the text changes as well, the note is followed by the hunks (with three lines of context, and `\ No newline at
end of file` where it applies). A file that would not be rewritten is not shown.

`--diff` writes no file. The exit code is `1` when any finding is reported, and the diff itself does not change the
exit code. `--diff` can be combined with `--check`, which has no further effect, but not with `--fix`. `--silent`
suppresses the diff as well as the findings.

## Switching Rules On or Off for One Run

`--enable <ids>` and `--disable <ids>` force rules on or off for the whole run. Each value is a comma-separated list of
rule identifiers or rule numbers such as `MD013`, in any letter case; both options can be repeated. They win over the
`rules` setting of every configuration file:

```bash
# Check the opt-in link text rule, and skip the line length rule
lintmark --enable descriptive-link-text --disable MD013 docs

# Convert long inline links to reference links in this run
lintmark --fix --enable link-refactor docs
```

- Only the 27 rules that a configuration can switch can be named (see *Rules* in *Configuration*). Naming a rule that
  is always applied, such as `bullet`, or an unknown name is a usage error.
- A rule named by both options is a usage error.
- The numbers of rules that LintMark no longer has (`MD002`, `MD006`, `MD008`, `MD025`, `MD033`, `MD041`, `MD043`,
  `MD044`, and `MD057`) are accepted and ignored.
- `--enable link-refactor` also turns on the `link-refactor` setting for the run, because that rule only switches the
  conversion off (see *Tables and Links*).

## Output

Files that are read and parsed successfully, with a valid configuration and nothing to report, produce no output, and
there is no summary line. Each problem is reported on standard error, one line per problem, in the form
`path:line:col: rule-id message`:

```text
.lintmark.yaml:2:7: config-error Invalid value 'paragraph' for setting 'wrap'; expected one of: fill, sentence, semantic
docs/broken.md:2:3: file-encoding-error Invalid UTF-8 byte sequence
docs/guide.md:1:1: line-ending Line endings are CRLF; expected LF
missing.md:1:1: file-read-error File not found
```

- Paths are relative to the current directory and use `/` separators. For a configuration problem the path is the
  configuration file, and the line and column are positions in that file.
- Lines and columns start at 1; a column counts UTF-16 code units and a tab counts as one column.
- Problems are ordered by path, then by line, column, and rule identifier. Files are processed in parallel, but the
  order of the output never depends on that.
- With `--fix`, the problems that rewriting removed are not reported.

The failures that LintMark reports about files, formatting, and configuration are:

| Rule identifier | Meaning |
| --- | --- |
| `file-read-error` | The file does not exist, cannot be accessed, or cannot be read (1:1) |
| `file-encoding-error` | The file is not valid UTF-8, or starts with a UTF-16 or UTF-32 byte order mark |
| `file-line-ending-error` | The file contains a carriage return that is not part of a CRLF line ending |
| `file-parse-error` | The Markdown parser failed for the file, or inline markup is nested over 128 levels (1:1) |
| `file-format-error` | Formatting the file failed (1:1) |
| `file-write-error` | The rewritten file could not be written (1:1) |
| `config-error` | A configuration file or `.editorconfig` problem (see *Configuration*) |

These identifiers always apply and cannot be switched off. `file-format-error` shows a defect in a formatting rule,
never a mistake in the document, and is worth reporting as a bug. All other identifiers are the rules of *Formatting
Rules* and *Lint Rules*, plus `line-ending`.

## Exit Codes

| Exit code | Meaning |
| --- | --- |
| `0` | Every file was processed and nothing is left to report |
| `1` | A problem was reported (also a lint-only finding), an argument was invalid, or validation failed |

There is no other exit code for problems. A `--fix` run that rewrote every file it had to and has nothing left to
report exits with `0`. `--silent` suppresses the output but not the exit code. An unexpected internal error is written
to standard error as `Unexpected error: <message>` and ends the process with the runtime's failure code.

## Silent Mode and Logging

`--silent` suppresses console output (the banner, the findings, and the diff):

```bash
lintmark --silent docs
```

`--log <file>` also writes the output to a log file, and it can be combined with `--silent` to write the log only:

```bash
lintmark --silent --log output.log docs
```

The log file is opened, and any existing content is replaced, as soon as LintMark starts. Choose a log path that is not
one of your Markdown files: LintMark does not check whether the log file is also one of the files it would process, so
`lintmark --log README.md` empties `README.md` before reading it. A log file that cannot be opened (for example,
because its directory does not exist) is reported as an error and nothing is processed:

```text
Error: Failed to open log file 'missing/output.log': Could not find a part of the path '/work/missing/output.log'.
```

## Self-Validation

Self-validation produces a report that shows that LintMark is functioning correctly. This is useful in regulated
industries where tool validation evidence is required:

```bash
lintmark --validate
```

The report contains the tool version, machine name, operating system version, .NET runtime version, a timestamp, and
the test results:

```text
# DEMA Consulting LintMark

| Information         | Value                                              |
| :------------------ | :------------------------------------------------- |
| Tool Version        | 1.0.0                                              |
| Machine Name        | BUILD-SERVER                                       |
| OS Version          | Ubuntu 22.04.3 LTS                                 |
| DotNet Runtime      | .NET 10.0.0                                        |
| Time Stamp          | 2024-01-15 10:30:00 UTC                            |

✓ LintMark_VersionDisplay - Passed
✓ LintMark_HelpDisplay - Passed
✓ LintMark_CleanFileNoFindings - Passed
✓ LintMark_ViolationsReported - Passed
✓ LintMark_FixRewritesToCanonicalForm - Passed
✓ LintMark_DiffPreviewsFixes - Passed
✓ LintMark_ConfigurationHonored - Passed

Total Tests: 7
Passed: 7
Failed: 0
```

Each test proves specific functionality:

- **`LintMark_VersionDisplay`**: `--version` outputs a valid version string.
- **`LintMark_HelpDisplay`**: `--help` outputs usage and options information.
- **`LintMark_CleanFileNoFindings`**: checking a compliant file reports nothing, exits with `0`, and leaves the file
  unchanged.
- **`LintMark_ViolationsReported`**: checking a file with deliberate violations reports exactly the expected rule
  identifiers at the expected lines and columns (`heading-increment`, `trailing-whitespace`, `bullet`,
  `list-blank-lines`, `heading-trailing-punctuation`, `code-fence-language`, `table-column-style`, and `emphasis`),
  exits with `1`, and leaves the file unchanged.
- **`LintMark_FixRewritesToCanonicalForm`**: `--fix` rewrites a fixable file into exactly its canonical form (list
  markers and spacing, table layout, emphasis delimiter, trailing whitespace, and a rewrapped over-long line), leaves
  a compliant file untouched, and a second check of both reports nothing.
- **`LintMark_DiffPreviewsFixes`**: `--diff` prints a unified diff of the fixable file only and writes no file.
- **`LintMark_ConfigurationHonored`**: a discovered `.lintmark.yaml` that changes the bullet marker, switches
  `heading-increment` off, and ignores a folder makes the files clean, and the same files report those findings with
  `--no-config`.

Each test writes its own files to a new temporary folder, which is removed afterwards. The folder contains an empty
`.git` folder and an `.editorconfig` with `root = true`, so no configuration of the machine or of the current
repository can change the results.

To save the results, give `--results <file>` (or its alias `--result <file>`). The format follows the file extension:
`.trx` for TRX (MSTest) format and `.xml` for JUnit format; any other extension is an error:

```bash
lintmark --validate --results results.trx
lintmark --validate --result results.xml
```

Use `--depth <#>` (1 to 6, default `1`) to control the heading level of the report, for example to embed it in a larger
Markdown document:

```bash
lintmark --validate --depth 2
```

`--validate` cannot be combined with paths, `--check`, `--fix`, `--diff`, `--config`, `--no-config`, `--enable`, or
`--disable`. When a validation test fails, the tool exits with `1`.

## Usage Errors

An argument that LintMark cannot accept is reported on standard error as `Error: <message>`, nothing is processed, and
the exit code is `1`; no `path:line:col` line is printed. Every argument that starts with a dash and is not a
supported option is rejected, including a lone `-`; an empty argument is rejected as well:

```text
Error: Unsupported argument '--unknown' (Parameter 'args')
```

The combinations that are rejected are `--validate` with any path or any of `--check`, `--fix`, `--diff`, `--config`,
`--no-config`, `--enable`, and `--disable`, `--fix` with `--check` or `--diff`, and `--config` with `--no-config`:

```text
Error: --fix cannot be combined with --check or --diff (Parameter 'args')
Error: --config cannot be combined with --no-config (Parameter 'args')
```

An option that needs a value but has none, or has an invalid one, is rejected as well:

```text
Error: --config requires a configuration filename argument (Parameter 'args')
Error: --depth requires a heading depth argument (Parameter 'args')
Error: --depth requires an integer between 1 and 6 for a heading depth argument (Parameter 'args')
Error: --enable requires a comma-separated list of rule ids (Parameter 'args')
Error: --enable cannot switch rule 'bullet'; it is always applied
Error: Rule 'line-length' cannot be named by both --enable and --disable
```

A configuration file name that starts with a dash is taken for another option and rejected; write `./-name.yaml`
instead. Problems in the configuration itself are different: they are reported as `config-error` lines (see
*Configuration*). These failures let automated scripts and CI/CD pipelines detect misconfiguration automatically.

## Command-Line Options

| Option | Description |
| --- | --- |
| `paths...` | Files, directories, or globs to process; `!pattern` excludes |
| `--check` | Report problems without writing any file (the default) |
| `--fix` | Rewrite files into canonical form (not with `--check` or `--diff`) |
| `--diff` | Print a unified diff of what `--fix` would change; writes no file |
| `--config <file>` | Use this configuration file instead of discovering `.lintmark.yaml` |
| `--no-config` | Ignore `.lintmark.yaml` and `.editorconfig`; use the built-in defaults |
| `--enable <ids>` | Force rules on (comma-separated ids or legacy rule numbers; repeatable); wins over configuration |
| `--disable <ids>` | Force rules off (same syntax as `--enable`) |
| `-v`, `--version` | Display version information |
| `-?`, `-h`, `--help` | Display the help message |
| `--silent` | Suppress console output |
| `--validate` | Run self-validation |
| `--results <file>`, `--result <file>` | Write validation results to a `.trx` (TRX) or `.xml` (JUnit XML) file |
| `--depth <#>` | Set the heading depth (1 to 6) of the validation report (default: 1) |
| `--log <file>` | Write output to a log file |

`lintmark --help` prints the banner, the usage line `lintmark [options] [paths...]`, and one line for each of these
options.

## Examples

Check the whole repository, then preview and apply the fixes:

```bash
lintmark
lintmark --diff
lintmark --fix
```

Process the documentation with explicit exclusions:

```bash
lintmark README.md "docs/**/*.md" "!docs/generated/**"
```

Use an explicit configuration file, or ignore all configuration:

```bash
lintmark --config config/lintmark.yaml "docs/**/*.md"
lintmark --no-config README.md docs
```

Run self-validation and save the results:

```bash
lintmark --validate --results validation-results.trx
```

Write only a log file:

```bash
lintmark --silent --log tool-output.log
```
