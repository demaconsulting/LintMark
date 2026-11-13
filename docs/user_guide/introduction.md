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
parse. It also finds, reads, and validates the `.lintmark.yaml` configuration (and the line ending from
`.editorconfig`) that applies to each file, and reports every configuration problem. The validated settings
are not applied yet, because no linting or formatting rule runs in this release. LintMark also provides the
standard command-line interface and self-validation.

## Scope

This user guide covers:

- Installation instructions
- Usage examples for common tasks
- Command-line options reference
- Configuration file (`.lintmark.yaml`) reference
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

## Configuration

LintMark is configured with an optional `.lintmark.yaml` file. Every setting has a built-in default, so a
configuration file only needs to list the settings it changes. This release finds, reads, and validates the
configuration that applies to each Markdown file and reports every problem in it; the validated settings are not
applied yet, because no linting or formatting rule runs in this release. A mistake in a configuration is
therefore reported now rather than when formatting is introduced.

### Configuration Files

The configuration file is named `.lintmark.yaml`. The tool finds it separately for each Markdown file it
processes:

- The search starts in the directory that contains the Markdown file and walks up through the parent
  directories.
- The nearest `.lintmark.yaml` wins. Files are never merged: a nearer file does not inherit anything from a
  farther one, and a farther file is not even read, so problems in it are not reported.
- The search stops after the repository root, which is the first directory that contains a `.git` entry (a
  directory, or a file as used by Git work trees and submodules). The repository root itself is searched; nothing
  above it is. When there is no `.git` entry, the search continues to the root of the file system.
- A directory that is named `.lintmark.yaml` is not a configuration file and is skipped.
- When no configuration file is found, the built-in defaults apply.

Two options override discovery:

```bash
# Use exactly this file for every Markdown file; discovery is not used
lintmark --config config/lintmark.yaml docs

# Ignore .lintmark.yaml and .editorconfig; use the built-in defaults
lintmark --no-config docs
```

- `--config <file>` names the configuration file. A relative path is relative to the current directory, and both
  `/` and `\` are accepted on every platform. When the option is repeated, the last one wins. A named file that
  does not exist or cannot be read is a configuration error, reported even when no Markdown file is selected.
  A file name that starts with a dash is taken for another option and rejected; write `./-name.yaml` instead.
  `--config` does not disable `.editorconfig` (see *Line Endings and .editorconfig*).
- `--no-config` ignores both `.lintmark.yaml` and `.editorconfig`, and uses the built-in defaults. Nothing is
  searched for or read.

`--config` cannot be combined with `--no-config`, and `--validate` cannot be combined with `--config` or
`--no-config`; each combination is a usage error (see *Error Handling*).

### Settings Reference

A configuration file is a single YAML document whose root is a mapping of settings. An empty file, a file that
contains only comments, and a file that contains only `---` all mean "no settings" and give the defaults.

| Setting               | Allowed values                                        | Default                    |
| --------------------- | ----------------------------------------------------- | -------------------------- |
| `line-length`         | whole number from `0` to `100000` (`0` is unlimited)  | `120`                      |
| `wrap`                | `fill`, `sentence`, `semantic`                        | `fill`                     |
| `end-of-line`         | `lf`, `crlf`                                          | not set                    |
| `final-newline`       | `true`, `false`                                       | `true`                     |
| `heading-style`       | `atx`                                                 | `atx`                      |
| `bullet`              | `dash`, `asterisk`, `plus`                            | `dash`                     |
| `list-indent`         | `2`, `4`                                              | `2`                        |
| `ordered-style`       | `incrementing`, `one`                                 | `incrementing`             |
| `emphasis`            | `asterisk`, `underscore`                              | `asterisk`                 |
| `strong`              | `asterisk`, `underscore`                              | `asterisk`                 |
| `code-fence`          | `backtick`, `tilde`                                   | `backtick`                 |
| `code-block-style`    | `fenced`                                              | `fenced`                   |
| `hard-break`          | `spaces`, `backslash`                                 | `spaces`                   |
| `thematic-break`      | `dashes`, `asterisks`, `underscores`                  | `dashes`                   |
| `bare-urls`           | `autolink`, `leave`                                   | `autolink`                 |
| `tables`              | `compact`, `aligned`                                  | `compact`                  |
| `max-blank-lines`     | whole number from `1` to `100000`                     | `1`                        |
| `trailing-whitespace` | `strip`                                               | `strip`                    |
| `link-refactor`       | a mapping of the options listed below                 | see below                  |
| `rules`               | a mapping of rule names to `true` or `false`          | see *Rules*                |
| `ignore`              | a list of glob patterns                               | see *Ignore Patterns*      |

The `link-refactor` setting is a mapping with these options; options that are not listed keep their defaults:

| Option               | Allowed values                                    | Default                      |
| -------------------- | ------------------------------------------------- | ---------------------------- |
| `enabled`            | `true`, `false`                                   | `false`                      |
| `threshold`          | whole number from `1` to `100000`                 | `60`                         |
| `in-tables`          | `true`, `false`                                   | `true`                       |
| `break-link-text`    | `true`, `false`                                   | `false`                      |
| `label-max-length`   | whole number from `1` to `100000`                 | `40`                         |
| `definitions-marker` | a single-line HTML comment (`<!--` to `-->`)      | `<!-- Link References -->`   |

What the settings describe:

- `wrap` selects how paragraphs are wrapped: `fill` re-wraps only paragraphs that contain a line wider than
  `line-length`, `sentence` starts every sentence on a new line, and `semantic` breaks lines at semantic
  boundaries.
- `bare-urls` selects whether a bare URL is written as an autolink (`<https://example.com>`) or left as it is.
- `tables` selects compact cells (`| a | b |`) or columns padded to a common width.
- `ordered-style` selects numbering such as `1. 2. 3.` (`incrementing`) or `1. 1. 1.` (`one`).
- `hard-break` selects trailing spaces or a trailing backslash for a hard line break.
- `thematic-break` selects `---`, `***`, or `___`.
- `link-refactor` is an opt-in conversion of inline links to reference links; `threshold` is the width of the
  inline link that triggers the conversion.
- `heading-style`, `code-block-style`, and `trailing-whitespace` currently accept only their default value; they
  exist so that the canonical form is explicit.

Every value is checked strictly, so that a typo can never silently fall back to a default:

- Setting names and word values are lower case and case-sensitive: `Line-Length`, `LF`, and `Fill` are errors.
- Numbers are plain decimal whole numbers and booleans are plain `true` or `false`, both without quotes:
  `"120"`, `1.5`, `0x10`, `yes`, `True`, and `"true"` are errors. A number outside the listed range is an error,
  and `list-indent` accepts only `2` or `4`.
- A setting with no value (`key:`, `~`, or `null`) is an error, including an empty `rules:`, `link-refactor:`, or
  `ignore:`. Write `rules: {}` or `ignore: []` for an explicit empty value.
- An unknown setting is an error, and so is a setting that appears twice. The same applies to the options of
  `link-refactor`, which are reported as `link-refactor.<option>`.
- The file contains one YAML document; a second document is an error. Anchors, aliases, and tags are not
  supported.
- The file must be UTF-8 (with or without a byte order mark) with LF or CRLF line endings.
- Limits: a file larger than 1 MiB (1,048,576 bytes) is an error, and so is a file in which `[` and `{` brackets
  are nested more than 64 levels deep (brackets inside comments and quoted text are counted too). Both are
  reported with a position and are rejected promptly, however far the limit is exceeded.

### Rules

The `rules` setting switches individual rules on or off. Each entry names a rule by its identifier or by its
markdownlint alias (such as `MD013`), in any letter case, and has the value `true` or `false`. Rules that are not
listed keep their default:

```yaml
rules:
  MD013: false
  heading-increment: false
  descriptive-link-text: true
```

| Rule identifier                | Alias   | Default |
| ------------------------------ | ------- | ------- |
| `line-length`                  | `MD013` | on      |
| `paragraph-wrap`               |         | on      |
| `heading-increment`            | `MD001` | on      |
| `heading-duplicate`            | `MD024` | on      |
| `heading-trailing-punctuation` | `MD026` | on      |
| `emphasis-as-heading`          | `MD036` | on      |
| `image-alt-text`               | `MD045` | on      |
| `link-empty`                   | `MD042` | on      |
| `link-fragment`                | `MD051` | on      |
| `link-reference-undefined`     | `MD052` | on      |
| `link-definition-unused`       | `MD053` | on      |
| `link-reversed`                | `MD011` | on      |
| `descriptive-link-text`        | `MD059` | off     |
| `code-fence-language`          | `MD040` | on      |
| `table-too-wide`               |         | on      |
| `table-column-count`           | `MD056` | on      |
| `table-grid-unformatted`       |         | off     |
| `fence-unclosed`               |         | on      |
| `grid-table-invalid`           |         | on      |
| `footnote-unused`              |         | on      |
| `blockquote-blank-line`        | `MD028` | on      |
| `dollar-sign-in-shell-code`    | `MD014` | off     |

- An unknown rule name is an error.
- Naming the same rule twice, whether by identifier and alias or in different letter case, is an error. It is
  reported at the later entry and names the earlier one.
- The aliases of rules that LintMark no longer has are accepted and ignored: `MD002`, `MD006`, `MD008`, `MD025`,
  `MD033`, `MD041`, `MD043`, `MD044`, and `MD057`. A configuration that still contains them stays valid, but
  repeating one of them is still a duplicate.
- Rules that the formatter always applies cannot be switched off, so their aliases (for example `MD009`) are
  unknown names in `rules`.
- The toggles are validated and stored in this release. No rule runs yet.

### Ignore Patterns

The `ignore` setting is a list of glob patterns for files that LintMark leaves alone:

```yaml
ignore:
  - "docs/generated/**"
  - "**/vendor/**"
```

- The patterns replace the defaults; they do not extend them. The defaults are `.git/**`, `node_modules/**`,
  `**/.venv/**`, `**/generated/**`, `**/thirdparty/**`, and `.agent-logs/**`.
- Each item must be a non-blank, single-line piece of text. Backslashes are converted to forward slashes, and an
  exact duplicate of an earlier pattern is removed.
- `ignore: []` is valid and ignores nothing.

The patterns are accepted and checked, but they are **not yet applied** by this release: the files to process are
selected only by the command line (see *Processing Markdown Files*), and a later release applies the patterns.

### Line Endings and .editorconfig

The line ending that a file will be written with is decided per file, in this order:

1. The `end-of-line` setting in the configuration that applies to the file (`lf` or `crlf`).
2. The `end_of_line` property of the `.editorconfig` settings that apply to the file.
3. The file's own line endings: the one that occurs most often. Formatting releases apply this step, because
   this release never writes a file.

Details of the `.editorconfig` step:

- Only the values `lf` and `crlf`, in any letter case, are used. `cr`, `unset`, any other value, or no
  `end_of_line` property means "not specified" and moves on to the next step. It is never an error, because an
  `.editorconfig` file is shared with other kinds of files.
- The usual `.editorconfig` rules apply: the nearest file that sets the property wins for each Markdown file, a
  file with `root = true` ends the search, and a later section in a file overrides an earlier one.
- Section patterns such as `[*.md]` are case-sensitive on every platform, so `[*.MD]` does not match `a.md`.
- An `.editorconfig` that cannot be read is reported as a configuration error at the Markdown file it affects.
  Malformed content in an `.editorconfig` is ignored and is not an error.
- `.editorconfig` is consulted only when the configuration does not set `end-of-line`. `--config` does not
  disable it; `--no-config` does, together with `.lintmark.yaml`.

### Configuration Errors

Every configuration problem is reported on standard error, in the same form as the other problems:

```text
path:line:col: config-error message
```

- The path is the configuration file, relative to the current directory with `/` separators. It can start with
  `../` when the file is outside the current directory. The line and column are 1-based positions in the
  configuration file. A problem that is not tied to a position, such as a file that is missing or cannot be read,
  is reported at `1:1`.
- The rule identifier is always `config-error`, and any configuration problem makes the exit code `1`. There is no
  other exit code for configuration problems.
- All problems in a file are reported in one run, ordered by position, so a configuration can be fixed in one pass.
  Syntax problems and problems with the structure of the document (such as a second document or a root that is
  not a mapping) end the checking of that file at that point.
- An invalid configuration is reported once, even when many Markdown files use it.
- A Markdown file with an invalid configuration is still read and checked, so its own read and parse problems are
  still reported.

For this configuration file:

```yaml
line-length: 80
wrap: paragraph
list-indent: 3
tabs: true
rules:
  MD013: false
  line-length: true
  MD009: false
```

`lintmark --config .lintmark.yaml docs` reports:

```text
.lintmark.yaml:2:7: config-error Invalid value 'paragraph' for setting 'wrap'; expected one of: fill, sentence, semantic
.lintmark.yaml:3:14: config-error Invalid value '3' for setting 'list-indent'; expected one of: 2, 4
.lintmark.yaml:4:1: config-error Unknown setting 'tabs'
.lintmark.yaml:7:3: config-error Rule 'line-length' is already configured as 'MD013' on line 6
.lintmark.yaml:8:3: config-error Unknown rule 'MD009'
```

Syntax errors, a configuration file that does not exist, and a configuration file outside the current directory
look like this:

```text
../broken.yaml:3:1: config-error Invalid YAML: While parsing a flow sequence, did not find expected ',' or ']'.
missing.yaml:1:1: config-error Configuration file not found
```

### Sample Configuration

This sample sets every setting to a value that differs from its default (`heading-style`, `code-block-style`, and
`trailing-whitespace` accept only one value, so they keep it):

```yaml
line-length: 100
wrap: sentence
end-of-line: crlf
final-newline: false
heading-style: atx
bullet: asterisk
list-indent: 4
ordered-style: one
emphasis: underscore
strong: underscore
code-fence: tilde
code-block-style: fenced
hard-break: backslash
thematic-break: asterisks
bare-urls: leave
tables: aligned
max-blank-lines: 2
trailing-whitespace: strip
link-refactor:
  enabled: true
  threshold: 80
  in-tables: false
  break-link-text: true
  label-max-length: 25
  definitions-marker: "<!-- Links -->"
rules:
  MD013: false
  heading-increment: false
  descriptive-link-text: true
  MD025: false
ignore:
  - "docs/generated/**"
  - "**/vendor/**"
```

Most configuration files are much smaller. This one changes the line length and the bullet marker and keeps every
other default:

```yaml
line-length: 100
bullet: asterisk
```

## Output and Exit Codes

Files that are read and parsed successfully, with a valid configuration, produce no output, and there is no
summary line. Each file that cannot be processed, and each configuration problem, is reported on standard error,
one line per problem, in the form `path:line:col: rule-id message`:

```text
.lintmark.yaml:2:7: config-error Invalid value 'paragraph' for setting 'wrap'; expected one of: fill, sentence, semantic
docs/broken.md:2:3: file-encoding-error Invalid UTF-8 byte sequence
missing.md:1:1: file-read-error File not found
```

Paths are relative to the current directory and use `/` separators; for a configuration problem the path is the
configuration file, and the line and column are positions in that file. Lines and columns start at 1; a column
counts UTF-16 code units and a tab counts as one column. Problems are ordered by path, then by line, column,
and rule identifier. Files are processed in parallel, but the order of the output never depends on that.

| Rule identifier          | Meaning                                                                          |
| ------------------------ | -------------------------------------------------------------------------------- |
| `file-read-error`        | The file does not exist, cannot be accessed, or cannot be read (position 1:1)    |
| `file-encoding-error`    | The file is not valid UTF-8, or starts with a UTF-16 or UTF-32 byte order mark   |
| `file-line-ending-error` | The file contains a carriage return that is not part of a CRLF line ending       |
| `file-parse-error`       | The Markdown parser failed for the file (position 1:1)                           |
| `config-error`           | A configuration file or `.editorconfig` problem (see *Configuration Errors*)     |

The exit code is `0` when no problem was reported and `1` when at least one problem was reported, including any
configuration problem, or when an argument is invalid. There is no other exit code.

## Display Help

Display usage information:

```bash
lintmark --help
```

The output is:

```text
LintMark version <version>
Copyright (c) DEMA Consulting

Usage: lintmark [options] [paths...]

Paths:
  paths...                   Files, directories, or globs of Markdown files to process;
                             prefix a pattern with ! to exclude matches
                             (default: **/*.md under the current directory)

Options:
  -v, --version              Display version information
  -?, -h, --help             Display this help message
  --check                    Report problems without writing any file (default)
  --config <file>            Use this configuration file instead of discovering .lintmark.yaml
  --no-config                Ignore .lintmark.yaml and .editorconfig; use the built-in defaults
  --silent                   Suppress console output
  --validate                 Run self-validation
  --results <file>           Write validation results to file (.trx or .xml)
  --depth <#>                Set heading depth for markdown output (default: 1)
  --log <file>               Write output to log file
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
`-`; an empty argument is rejected as well. `--validate` cannot be combined with paths, `--check`, `--config`,
or `--no-config`:

```text
Error: --validate cannot be combined with paths, --check, --config, or --no-config (Parameter 'args')
```

`--config` needs a file name, and it cannot be combined with `--no-config`:

```text
Error: --config requires a configuration filename argument (Parameter 'args')
Error: --config cannot be combined with --no-config (Parameter 'args')
```

These are usage errors, so the tool exits with code `1` and no `path:line:col` line is printed. Problems in the
configuration itself are different: they are reported as `config-error` lines (see *Configuration Errors*).

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
| `--config <file>`                     | Use this configuration file instead of discovering it        |
| `--no-config`                         | Ignore `.lintmark.yaml` and `.editorconfig`; use defaults    |
| `-v`, `--version`                     | Display version information                                  |
| `-?`, `-h`, `--help`                  | Display help message                                         |
| `--silent`                            | Suppress console output                                      |
| `--validate`                          | Run self-validation (see Error Handling for exclusions)      |
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

## Example 5: Use an Explicit Configuration File

```bash
lintmark --config config/lintmark.yaml "docs/**/*.md"
```

## Example 6: Ignore All Configuration

```bash
lintmark --no-config README.md docs
```

## References

N/A
