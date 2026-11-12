# Introduction

The workflow below is a placeholder: replace its TODO boxes to show how users prepare inputs, run your
tool locally or in a CI pipeline, get its outputs, and use them.

![Placeholder workflow diagram: replace the TODO boxes to show how users prepare inputs, run your tool, get
its outputs, and use them](workflow.svg)

## Purpose

LintMark is a .NET tool, distributed as a NuGet tool package, for linting and optionally reformatting
markdown files into a preferred format, with optional tweaks via a `.lintmark.yaml` configuration file.
Markdown linting and formatting rules are planned but not yet implemented. This release selects Markdown files
by path, directory, or glob pattern, reads and parses each one, and reports the files it cannot read or
parse. It also provides the standard command-line interface and self-validation.

## Scope

This user guide covers:

- Installation instructions
- Usage examples for common tasks
- Command-line options reference
- Practical examples for various scenarios

# Continuous Compliance

LintMark follows the
[Continuous Compliance](https://github.com/demaconsulting/ContinuousCompliance) methodology, which ensures
compliance evidence is generated automatically on every CI run.

## Key Practices

- **Requirements Traceability**: Every requirement is linked to passing tests, and a trace matrix is
  auto-generated on each release
- **Linting Enforcement**: markdownlint, TextLint, and yamllint are enforced before any build proceeds
- **Automated Audit Documentation**: Each release ships with generated requirements, justifications,
  trace matrix, and quality reports
- **CodeQL and SonarCloud**: Security and quality analysis runs on every build

# Installation

Install the tool globally using the .NET CLI:

```bash
dotnet tool install -g DemaConsulting.LintMark
```

# Usage

## Display Version

Display the tool version:

```bash
lintmark --version
```

## Default Behavior

When invoked with no arguments, the tool displays the banner and copyright notice, then reads and parses
every `**/*.md` file under the current directory:

```bash
lintmark
```

Files below `node_modules` and `.git` folders are skipped.

## Processing Markdown Files

Name the files to process as arguments. Each argument that does not start with a dash is a path:

```bash
# One file
lintmark README.md

# A directory: every *.md file beneath it, at any depth
lintmark docs

# Several arguments at once
lintmark README.md docs
```

Arguments can also be glob patterns. The wildcard `*` matches within one path segment and `**` matches any
number of directory levels. Patterns may be relative, relative to a parent directory (`../docs/*.md`),
or absolute. Both `/` and `\` are accepted as separators in every argument (patterns, file names, directories,
and exclusions) on every platform, so `docs\guide.md` selects the same file on Linux as on Windows; on Linux
and macOS this means a backslash cannot be part of a file name given on the command line. Quote patterns so
that the shell passes them unchanged:

```bash
lintmark "docs/**/*.md"
```

To leave files out, prefix a pattern with `!` (and quote it). An exclusion also applies to files named
explicitly, and an exclusion that names a directory removes everything beneath it. When only exclusions are
given, they are applied to the default `**/*.md` selection:

```bash
lintmark "docs/**/*.md" "!docs/generated/**"
lintmark "!docs/generated"
```

How the files are selected:

- A file named explicitly is always processed, even if it is not a `.md` file or lies below `node_modules`.
- Only an explicitly named file is processed from a `node_modules` or `.git` folder: a directory or glob that
  starts inside such a folder (for example `node_modules` or `"node_modules/**/*.md"`) selects nothing.
- A file selected by several arguments is processed once.
- A pattern that matches no file is not an error.
- A name that matches no wildcard pattern and does not exist is reported as a read problem.
- On Windows and macOS, patterns ignore letter case; on other systems they do not. Where case is ignored and
  one file is selected under spellings that differ only in case (for example `docs/guide.md` and
  `docs/GUIDE.md`), it is processed once and reported under the alphabetically first spelling (upper case
  before lower case), whatever the order of the arguments.

LintMark reads files as UTF-8 text (with or without a byte order mark) that uses LF or CRLF line endings.

## Check Mode

Use `--check` to state that files are only to be checked and never modified:

```bash
lintmark --check docs
```

No file is modified in this release, with or without `--check`, so the option is currently an explicit
statement of the default behavior.

## Output and Exit Codes

Files that are read and parsed successfully produce no output, and there is no summary line. Each file that
cannot be processed is reported on standard error, one line per problem, in the form
`path:line:col: rule-id message`:

```text
docs/broken.md:2:3: file-encoding-error Invalid UTF-8 byte sequence
missing.md:1:1: file-read-error File not found
```

Paths are relative to the current directory and use `/` separators. Lines and columns start at 1; a column
counts UTF-16 code units and a tab counts as one column. Problems are ordered by path, then by line, column,
and rule identifier. Files are processed in parallel, but the order of the output never depends on that.

| Rule identifier          | Meaning                                                                          |
| ------------------------ | -------------------------------------------------------------------------------- |
| `file-read-error`        | The file does not exist, cannot be accessed, or cannot be read (position 1:1)    |
| `file-encoding-error`    | The file is not valid UTF-8, or starts with a UTF-16 or UTF-32 byte order mark   |
| `file-line-ending-error` | The file contains a carriage return that is not part of a CRLF line ending       |
| `file-parse-error`       | The Markdown parser failed for the file (position 1:1)                           |

The exit code is `0` when no problem was reported and `1` when at least one problem was reported or when an
argument is invalid.

## Display Help

Display usage information:

```bash
lintmark --help
```

## Self-Validation

Self-validation produces a report showing that LintMark is functioning
correctly. This is useful in regulated industries where tool validation evidence is required.

### Running Validation

To perform self-validation:

```bash
lintmark --validate
```

To save validation results to a file:

```bash
lintmark --validate --results results.trx
```

The `--result` option is an accepted alias for `--results`.

The results file format is determined by the file extension: `.trx` for TRX (MSTest) format,
or `.xml` for JUnit format.

### Heading Depth

Use `--depth <#>` to control the heading level of the validation output (default: `1`).
This is useful when embedding the validation report into a larger markdown document:

```bash
# Embed validation at heading level 2
lintmark --validate --depth 2
```

### Validation Report

The validation report contains the tool version, machine name, operating system version,
.NET runtime version, timestamp, and test results.

Example validation report:

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

Total Tests: 2
Passed: 2
Failed: 0
```

### Validation Tests

Each test proves specific functionality works correctly:

- **`LintMark_VersionDisplay`** - `--version` outputs a valid version string.
- **`LintMark_HelpDisplay`** - `--help` outputs usage and options information.

## Silent Mode

Suppress console output:

```bash
lintmark --silent
```

## Logging

Write output to a log file:

```bash
lintmark --log output.log
```

The log file is opened, and any existing content is replaced, as soon as LintMark starts. This is the same
behavior as the other DEMA Consulting tools. Choose a log path that is not one of your Markdown files: LintMark does
not check whether the log file is also one of the files it would process, so `lintmark --log README.md` empties
`README.md` before reading it. A log file that cannot be opened (for example, because its directory does not exist)
is reported as an error and nothing is processed:

```text
Error: Failed to open log file 'missing/output.log': Could not find a part of the path '/work/missing/output.log'.
```

## Error Handling

Unrecognized arguments cause the tool to print an error message to standard error and exit
with a non-zero exit code. For example:

```text
Error: Unsupported argument '--unknown'
```

Every argument that starts with a dash and is not a supported option is rejected this way, including a lone
`-`; an empty argument is rejected as well. `--validate` cannot be combined with paths or `--check`:

```text
Error: --validate cannot be combined with paths or --check (Parameter 'args')
```

This behavior enables automated scripts and CI/CD pipelines to detect and surface
misconfiguration failures automatically.

# Command-Line Options

The following command-line options are supported:

```text
lintmark [options] [paths...]
```

| Option                                | Description                                                  |
| ------------------------------------- | ------------------------------------------------------------ |
| `paths...`                            | Files, directories, or globs to process; `!pattern` excludes |
| `--check`                             | Report problems without writing any file (the default)       |
| `-v`, `--version`                     | Display version information                                  |
| `-?`, `-h`, `--help`                  | Display help message                                         |
| `--silent`                            | Suppress console output                                      |
| `--validate`                          | Run self-validation (cannot be combined with paths)          |
| `--results <file>`, `--result <file>` | Write validation results to file (TRX or JUnit format)       |
| `--depth <#>`                         | Set heading depth for markdown output (default: 1)           |
| `--log <file>`                        | Write output to log file                                     |

# Examples

## Example 1: Basic Usage

```bash
lintmark
```

## Example 2: Process Documentation

```bash
lintmark README.md "docs/**/*.md" "!docs/generated/**"
```

## Example 3: Self-Validation with Results

```bash
lintmark --validate --results validation-results.trx
```

## Example 4: Silent Mode with Logging

```bash
lintmark --silent --log tool-output.log
```

## References

N/A
