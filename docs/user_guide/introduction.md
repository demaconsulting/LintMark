# Introduction

The workflow below is a placeholder: replace its TODO boxes to show how users prepare inputs, run your
tool locally or in a CI pipeline, get its outputs, and use them.

![Placeholder workflow diagram: replace the TODO boxes to show how users prepare inputs, run your tool, get
its outputs, and use them](workflow.svg)

## Purpose

LintMark is a .NET tool, distributed as a NuGet tool package, for linting and optionally reformatting
markdown files into a preferred format, with optional tweaks via a `.lintmark.yaml` configuration file.
This release selects Markdown files by path, directory, or glob pattern, reads and parses each one, and reports
the files it cannot read or parse. It finds, reads, and validates the `.lintmark.yaml` configuration (and the
line ending from `.editorconfig`) that applies to each file, and reports every configuration problem. It checks
and, with `--fix`, rewrites each file with 14 formatting rules and the line ending rule (whitespace, blank lines,
headings, code blocks, thematic breaks, and the end of the file; see *Formatting Rules*), and `--diff` previews what
`--fix` would change. A rewrite is written only when every safety check passes, and it is written atomically. The
`ignore` patterns of the configuration are applied. The remaining Markdown rules (for example paragraph wrapping,
lists, tables, links, and the lint-only rules) are planned but not yet implemented, so their settings are validated and
stored but do not change any file yet. LintMark also provides the standard command-line interface and self-validation.

## Scope

This user guide covers:

- Installation instructions
- Usage examples for common tasks
- Command-line options reference
- Configuration file (`.lintmark.yaml`) reference
- Formatting rules reference, with the identifier, legacy number, setting, and an example of each rule
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

Without `--fix`, LintMark only checks: it reports every problem it finds and never writes a file. Use `--check` to
state this explicitly; the behavior is the same with or without the option:

```bash
lintmark --check docs
```

A file whose line endings are not the ones chosen for it (see *Line Endings and .editorconfig*) is reported with
the rule identifier `line-ending`, at line 1, column 1, and every place at which a formatting rule would change the file
is reported with the identifier of that rule, at the line and column of the change (see *Formatting Rules*):

```text
docs/guide.md:1:1: line-ending Line endings are CRLF; expected LF
docs/notes.md:1:1: line-ending Mixed line endings (2 CRLF, 5 LF); expected LF
docs/start.md:7:12: trailing-whitespace A line ends with trailing whitespace, or a hard break is not exactly two spaces.
docs/start.md:9:1: heading-blank-lines A heading is not surrounded by blank lines.
```

Every problem of this kind can be corrected with `--fix`. The exit code is `1` when any problem is reported.

## Fixing Files

Use `--fix` to rewrite files into their canonical form:

```bash
lintmark --fix docs
```

- A file is rewritten only when the rewrite changes it and every safety check passes (see *Safety Checks*). A file
  that is already compliant is not touched at all: its content and its modification time stay as they are.
- Only what can be fixed is rewritten. A problem that rewriting removes, such as `line-ending` or a formatting rule, is
  no longer reported after the file was written; problems that remain, and files that were refused, are reported as
  usual.
  The exit code is `0` when nothing remains to report and `1` otherwise, so a second `--fix` run over the same
  files reports nothing and changes nothing.
- A file is never rewritten when its configuration is invalid or its editor settings cannot be read, because the
  settings it would be rewritten with are not known. The configuration problem is reported instead.
- The byte order mark of a file, if it has one, is kept.
- A file is written atomically: the new content is written to a temporary file in the same directory and then
  replaces the original in one step, so a crash or a full disk never leaves a half-written file and no temporary
  file remains. The permission bits of the file are kept on Linux and macOS, and a symbolic link stays a link (the
  file it points to is rewritten). A file that was changed by another program after LintMark read it, and a
  read-only file, are not overwritten.
- When a file cannot be written, it stays as it was and the problem is reported as `file-write-error` at line 1,
  column 1, together with the other findings of the file; the other files are still processed:

```text
docs/guide.md:1:1: file-write-error Cannot write file: The file changed after it was read; it was not rewritten.
```

- Files are rewritten one after the other, in path order, after all files have been processed.

`--fix` cannot be combined with `--check` or `--diff` (see *Error Handling*).

## Previewing Fixes

Use `--diff` to see what `--fix` would change, without writing anything:

```bash
lintmark --diff docs
```

For every file that `--fix` would rewrite, a unified diff is written to standard output, in path order. The
paths are relative to the current directory. A line ending change, which a line-based diff cannot show, is
described by a note line:

```text
--- a/docs/guide.md
+++ b/docs/guide.md
\ Line endings change from CRLF to LF
```

When the text changes as well, the note is followed by the hunks (`@@ -1,2 +1,2 @@` with three lines of context,
and `\ No newline at end of file` where it applies). A file that would not be rewritten, including every refused
file, is not shown.

`--diff` writes no file. The findings are reported on standard error as in check mode, so the exit code is `1` when
anything is reported, and the diff itself does not change the exit code. `--diff` can be combined with `--check`,
which has no further effect, but not with `--fix`.

## Safety Checks

A rewrite replaces a file, so LintMark proves that it cannot damage the document before anything is written. Each
proposed rewrite must pass these checks, in this order, also a rewrite that only changes the line endings (the
renderer shows some text, such as a code span that spans lines, differently for CRLF and LF, so the HTML of the text
with its line endings is compared as well). A file that
fails one is **refused**: it is never written, the finding is reported with the rule identifier below at the line
where the text would first differ, and the other files are not affected. The refusal is reported by `--check` and
`--diff` as well, so a problem in the formatting is visible before `--fix` is used.

| Rule identifier             | Refused because                                                           |
| --------------------------- | ------------------------------------------------------------------------- |
| `safety-html-mismatch`      | The rewrite would render to different HTML (also with other line endings) |
| `safety-structure-mismatch` | The rewrite would change Pandoc structure that the HTML does not show     |
| `safety-not-idempotent`     | Formatting the rewritten text again would change it again                 |

The Pandoc structure that is compared consists of code fences, page break commands, display math delimiters, grid
table borders, attribute groups, and citations. `safety-structure-mismatch` is also reported when the rewritten text
would no longer be read as before, for example because the rewrite creates a line that looks like the border of a
grid table that is not valid.

```text
docs/guide.md:12:1: safety-html-mismatch The rewrite would change how the document renders (...); the file is not rewritten
```

Two further problems stop a rewrite and are reported with the file left as it was:

| Rule identifier     | Meaning                                                                          |
| ------------------- | -------------------------------------------------------------------------------- |
| `file-format-error` | Formatting the file failed, for example a formatter threw an exception (1:1)     |
| `file-write-error`  | The rewritten file could not be written (1:1; see *Fixing Files*)                |

The refusals and `file-format-error` show a defect in a formatting rule, never a mistake in the document, and are
worth reporting as a bug, with one exception: a refused conversion of the line endings is caused by the document
itself. The safety diagnostics, `file-format-error`, and `file-write-error` cannot be switched
off, and are not listed under `rules`. A file whose conversion to another line ending would change how it renders,
such as a code span that spans lines, is refused too (`safety-html-mismatch` at line 1, column 1) and keeps its
line endings; the `line-ending` finding stays.

## Configuration

LintMark is configured with an optional `.lintmark.yaml` file. Every setting has a built-in default, so a
configuration file only needs to list the settings it changes. This release finds, reads, and validates the
configuration that applies to each Markdown file and reports every problem in it. The settings that this release
applies are `end-of-line` (see *Line Endings and .editorconfig*), `final-newline`, `max-blank-lines`, `code-fence`,
`thematic-break`, `heading-style`, `code-block-style`, and `trailing-whitespace` (see *Formatting Rules*), `ignore` (see
*Ignore Patterns*), and the `grid-table-invalid` entry of `rules` (see *Grid Table Parse Problems*). The other settings
are validated and stored, but no linting or formatting rule uses them yet, so a mistake in a configuration is reported
now rather than when the rules are introduced.

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
  exist so that the canonical form is explicit. `hard-break` is validated but does not convert hard breaks yet (a break
  written with spaces is only normalized to exactly two spaces; see *Formatting Rules*).

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
- Rules that the formatter always applies cannot be switched off, so their identifiers and aliases (for example
  `line-ending`, `trailing-whitespace`, and `MD009`) are unknown names in `rules`. They are switched off for a passage
  with a suppression comment (see *Formatting Rules*).
- This release evaluates only `grid-table-invalid` (see *Grid Table Parse Problems*). The other toggles are
  validated and stored; no other rule that `rules` can switch runs yet.

### Ignore Patterns

The `ignore` setting is a list of glob patterns for files that LintMark leaves alone:

```yaml
ignore:
  - "docs/generated/**"
  - "**/vendor/**"
```

- A file that matches a pattern is not read, not checked, not rewritten, and never reported, also not for read or
  parse problems.
- Patterns are relative to the directory of the configuration file that applies to the file (the current directory
  when no configuration file applies, as with `--no-config`). `*` matches within one path segment and `**` matches
  any number of directory levels, as on the command line; backslashes are accepted as separators. A file outside
  that directory is never ignored.
- The patterns replace the defaults; they do not extend them. The defaults are `.git/**`, `node_modules/**`,
  `**/.venv/**`, `**/generated/**`, `**/thirdparty/**`, and `.agent-logs/**`. To keep a default, list it again.
- Each item must be a non-blank, single-line piece of text. Backslashes are converted to forward slashes, and an
  exact duplicate of an earlier pattern is removed.
- `ignore: []` is valid and ignores nothing.
- **A file named on the command line is always processed**, even when an `ignore` pattern matches it. `ignore`
  applies only to files that were found through a directory, a glob, or the default selection. Only a `!`
  exclusion on the command line (see *Processing Markdown Files*) removes a file that was named explicitly.
- Patterns are judged with the configuration that applies to the file. A file in a folder with an invalid
  configuration has no usable patterns, so it is still read and checked, but never rewritten.

### Line Endings and .editorconfig

The line ending that a file is written with is decided per file, in this order:

1. The `end-of-line` setting in the configuration that applies to the file (`lf` or `crlf`).
2. The `end_of_line` property of the `.editorconfig` settings that apply to the file.
3. The file's own line endings: the one that occurs most often (LF when both occur equally often or the file has
   none).

The decision is applied: `--check` and `--diff` report a file whose line endings differ from it as `line-ending`
(see *Check Mode*), and `--fix` rewrites every line ending of the file to it. A file without any line ending, and
a file that already uses only the chosen one, is compliant. A file that mixes CRLF and LF is a violation of
whichever ending is chosen for it, and its message names the mix. The `line-ending` rule always applies: it is not
configurable and cannot be suppressed, so it is not an entry of `rules`; choose the ending with `end-of-line`
or `.editorconfig` instead.

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

### Grid Table Parse Problems

A grid table is a table drawn with `+---+---+` border lines. LintMark reads a document that contains a line that
looks like the border of a grid table, but that is not part of a valid grid table, without its grid tables, and
because it can then no longer tell what the document means, it never rewrites such a file:

```text
a.md:3:1: grid-table-invalid Line looks like a grid table border but the table is not valid; the file is never rewritten
```

- The finding is reported at the offending line, by `--check`, `--diff`, and `--fix` alike, and makes the exit code
  `1`. The file is left exactly as it was by `--fix`, also its line endings, and `--diff` shows nothing for it.
- Make the table a valid grid table, or, if the line is not meant to be a table, change it so that it no longer
  looks like a border (for example by moving it into a code block).
- A valid grid table is formatted normally and is not reported. Border-looking lines inside a code block or other
  literal text are not problems.
- `rules: { grid-table-invalid: false }` (or its identifier in a suppression comment at that line) hides the finding
  only. The file is still never rewritten: the protection does not depend on the report.

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

Files that are read and parsed successfully, with a valid configuration and nothing to fix, produce no output, and
there is no summary line. Each problem is reported on standard error, one line per problem, in the form
`path:line:col: rule-id message`:

```text
.lintmark.yaml:2:7: config-error Invalid value 'paragraph' for setting 'wrap'; expected one of: fill, sentence, semantic
docs/broken.md:2:3: file-encoding-error Invalid UTF-8 byte sequence
docs/guide.md:1:1: line-ending Line endings are CRLF; expected LF
missing.md:1:1: file-read-error File not found
```

Paths are relative to the current directory and use `/` separators; for a configuration problem the path is the
configuration file, and the line and column are positions in that file. Lines and columns start at 1; a column
counts UTF-16 code units and a tab counts as one column. Problems are ordered by path, then by line, column,
and rule identifier. Files are processed in parallel, but the order of the output never depends on that. The
unified diff of `--diff` is written to standard output, after the problems (see *Previewing Fixes*). With `--fix`,
the problems that rewriting removed are not reported.

| Rule identifier             | Meaning                                                                            |
| --------------------------- | ---------------------------------------------------------------------------------- |
| `file-read-error`           | The file does not exist, cannot be accessed, or cannot be read (1:1)               |
| `file-encoding-error`       | The file is not valid UTF-8, or starts with a UTF-16 or UTF-32 byte order mark     |
| `file-line-ending-error`    | The file contains a carriage return that is not part of a CRLF line ending         |
| `file-parse-error`          | The Markdown parser failed for the file (1:1)                                      |
| `config-error`              | A configuration file or `.editorconfig` problem (see *Configuration Errors*)       |
| `line-ending`               | A line ending differs from the one chosen for the file (1:1; fixable)              |
| formatting rules            | A formatting rule would change the file at this place (fixable; see below)         |
| `grid-table-invalid`        | A line looks like a grid table border but the table is not valid (never rewritten) |
| `safety-html-mismatch`      | A rewrite would change how the document renders; the file is not rewritten         |
| `safety-structure-mismatch` | A rewrite would change Pandoc structure; the file is not rewritten                 |
| `safety-not-idempotent`     | Formatting would not be stable; the file is not rewritten                          |
| `file-format-error`         | Formatting the file failed (1:1)                                                   |
| `file-write-error`          | The rewritten file could not be written (1:1)                                      |

Every identifier in the table except `grid-table-invalid` always applies and cannot be switched off; switching
`grid-table-invalid` off in `rules` hides its report only. The formatting rules are the identifiers of *Formatting
Rules* (`trailing-whitespace`, `hard-tabs`, `max-blank-lines`, `heading-style`, `heading-space`, `heading-start-left`,
`heading-blank-lines`, `fence-blank-lines`, `list-blank-lines`, `table-blank-lines`, `thematic-break`,
`code-block-style`, `code-fence`, and `final-newline`); each is reported at the line and column that its change starts
at (for an inserted blank line, the line before which it is inserted), at most once for each rule and line, with the
description of the rule as the message.

The exit code is `0` when no problem was reported and `1` when at least one problem was reported, including any
configuration problem, or when an argument is invalid. There is no other exit code. A `--fix` run that rewrote every
file it had to and has nothing left to report exits with `0`; a `--diff` run exits with `1` when it reports a
problem, and the diff itself never changes the exit code.

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
  --fix                      Rewrite files into canonical form when every safety check passes,
                             then report the problems that remain (not with --check or --diff)
  --diff                     Print a unified diff of what --fix would change; writes no file
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
`-`; an empty argument is rejected as well. `--validate` cannot be combined with paths, `--check`, `--fix`,
`--diff`, `--config`, or `--no-config`:

```text
Error: --validate cannot be combined with paths, --check, --fix, --diff, --config, or --no-config (Parameter 'args')
```

`--fix` rewrites files, so it cannot be combined with `--check` or `--diff`, which promise not to:

```text
Error: --fix cannot be combined with --check or --diff (Parameter 'args')
```

`--diff` can be combined with `--check`.

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
| `--fix`                               | Rewrite files when every safety check passes                 |
| `--diff`                              | Print a unified diff of what `--fix` would change            |
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

## Example 3: Preview and Apply Fixes

```bash
lintmark --diff docs
lintmark --fix docs
```

## Example 4: Self-Validation with Results

```bash
lintmark --validate --results validation-results.trx
```

## Example 5: Silent Mode with Logging

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
