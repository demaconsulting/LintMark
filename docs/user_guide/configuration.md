# Configuration

LintMark is configured with an optional `.lintmark.yaml` file. Every setting has a built-in default, so a
configuration file only needs to list the settings it changes. A mistake in a configuration is always reported rather
than silently replaced by a default.

## Finding the Configuration File

The configuration file is named `.lintmark.yaml`. The tool finds it separately for each Markdown file it processes:

- The search starts in the directory that contains the Markdown file and walks up through the parent directories.
- The nearest `.lintmark.yaml` wins. Files are never merged: a nearer file does not inherit anything from a farther
  one, and a farther file is not even read, so problems in it are not reported.
- The search stops after the repository root, which is the first directory that contains a `.git` entry (a directory,
  or a file as used by Git work trees and submodules). The repository root itself is searched; nothing above it is.
  When there is no `.git` entry, the search continues to the root of the file system.
- A directory that is named `.lintmark.yaml` is not a configuration file and is skipped.
- When no configuration file is found, the built-in defaults apply.

Two options override discovery:

```bash
# Use exactly this file for every Markdown file; discovery is not used
lintmark --config config/lintmark.yaml docs

# Ignore .lintmark.yaml and .editorconfig; use the built-in defaults
lintmark --no-config docs
```

- `--config <file>` names the configuration file. A relative path is relative to the current directory, and both `/`
  and `\` are accepted on every platform. When the option is repeated, the last one wins. A named file that does not
  exist or cannot be read is a configuration error, reported even when no Markdown file is selected. A file name that
  starts with a dash is taken for another option and rejected; write `./-name.yaml` instead. `--config` does not
  disable `.editorconfig`.
- `--no-config` ignores both `.lintmark.yaml` and `.editorconfig`, and uses the built-in defaults. Nothing is searched
  for or read.

`--config` cannot be combined with `--no-config`, and `--validate` cannot be combined with either.

The `--enable` and `--disable` options of the command line win over the `rules` setting of every configuration file
(see *Usage*).

## Settings Reference

A configuration file is a single YAML document whose root is a mapping of settings. An empty file, a file that
contains only comments, and a file that contains only `---` all mean "no settings" and give the defaults.

| Setting | Allowed values | Default |
| --- | --- | --- |
| `line-length` | whole number from `0` to `100000` (`0` is unlimited) | `120` |
| `wrap` | `fill`, `sentence`, `semantic` | `fill` |
| `end-of-line` | `lf`, `crlf` | not set |
| `final-newline` | `true`, `false` | `true` |
| `heading-style` | `atx` | `atx` |
| `bullet` | `dash`, `asterisk`, `plus` | `dash` |
| `list-indent` | `2`, `4` | `2` |
| `ordered-style` | `incrementing`, `one` | `incrementing` |
| `emphasis` | `asterisk`, `underscore` | `asterisk` |
| `strong` | `asterisk`, `underscore` | `asterisk` |
| `code-fence` | `backtick`, `tilde` | `backtick` |
| `code-block-style` | `fenced` | `fenced` |
| `hard-break` | `spaces`, `backslash` | `spaces` |
| `thematic-break` | `dashes`, `asterisks`, `underscores` | `dashes` |
| `bare-urls` | `autolink`, `leave` | `autolink` |
| `tables` | `compact`, `aligned` | `compact` |
| `max-blank-lines` | whole number from `1` to `100000` | `1` |
| `trailing-whitespace` | `strip` | `strip` |
| `link-refactor` | a mapping of the options listed below | disabled (see below) |
| `rules` | a mapping of rule names to `true` or `false` | see *Rules* |
| `ignore` | a list of glob patterns | six built-in patterns (see *Ignore Patterns*) |

What the settings describe:

- `line-length` is the width, in display columns, above which a paragraph is rewrapped, and `wrap` selects the method:
  `fill` rewraps only paragraphs that contain an over-wide line, `sentence` starts every sentence on a new line, and
  `semantic` does the same and breaks a sentence that is wider than the line only at the latest clause break that fits
  (see *Paragraph Wrapping*).
- `end-of-line` is the line ending of every file; when it is not set, `.editorconfig` and then the file itself decide
  (see *Line Endings and .editorconfig*). `final-newline` selects whether a file ends with one line ending.
- `bullet` selects the marker of bullet list items (`-`, `*`, or `+`), `list-indent` the indentation (`2` or `4`
  columns) of a bullet list nested in a bullet item, `ordered-style` the numbering (`1. 2. 3.` for `incrementing`, or
  `1. 1. 1.` for `one`, which repeats the number of the first item), and `emphasis` and `strong` the delimiter (`*` or
  `_`) of emphasis and strong emphasis.
- `code-fence` selects backticks or tildes for fenced code blocks, and `thematic-break` selects `---`, `***`, or `___`.
- `bare-urls` selects whether a bare `http://`, `https://`, or `ftp://` address is written as an autolink
  (`<https://example.com>`) or left as it is.
- `tables` selects compact cells (`| a | b |`) or columns padded to a common width (see *Tables and Links*).
- `max-blank-lines` is the largest number of consecutive blank lines kept.
- `link-refactor` is the opt-in conversion of long inline links to reference links (see *Tables and Links*).
- `rules` switches individual rules on or off (see *Rules*), and `ignore` lists the files to leave alone (see *Ignore
  Patterns*).
- `heading-style`, `code-block-style`, and `trailing-whitespace` currently accept only their default value; they exist
  so that the canonical form is explicit. `hard-break` is validated but has no effect yet: a hard break written with
  spaces is only normalized to exactly two spaces, and a hard break written with a backslash is kept.

The `link-refactor` setting is a mapping with these options; options that are not listed keep their defaults:

| Option | Allowed values | Default |
| --- | --- | --- |
| `enabled` | `true`, `false` | `false` |
| `threshold` | whole number from `1` to `100000` | `60` |
| `in-tables` | `true`, `false` | `true` |
| `break-link-text` | `true`, `false` | `false` |
| `label-max-length` | whole number from `1` to `100000` | `40` |
| `definitions-marker` | a single-line HTML comment (`<!--` to `-->`) | `<!-- Link References -->` |

### Strict Checking

Every value is checked strictly, so that a typo can never silently fall back to a default:

- Setting names and word values are lower case and case-sensitive: `Line-Length`, `LF`, and `Fill` are errors.
- Numbers are plain decimal whole numbers and booleans are plain `true` or `false`, both without quotes: `"120"`,
  `1.5`, `0x10`, `yes`, `True`, and `"true"` are errors. A number outside the listed range is an error, and
  `list-indent` accepts only `2` or `4`.
- A setting with no value (`key:`, `~`, or `null`) is an error, including an empty `rules:`, `link-refactor:`, or
  `ignore:`. Write `rules: {}` or `ignore: []` for an explicit empty value.
- An unknown setting is an error, and so is a setting that appears twice. The same applies to the options of
  `link-refactor`, which are reported as `link-refactor.<option>`.
- The file contains one YAML document; a second document is an error. Anchors, aliases, and tags are not supported.
- The file must be UTF-8 (with or without a byte order mark) with LF or CRLF line endings.
- Limits: a file larger than 1 MiB (1,048,576 bytes) is an error, and so is a file in which `[` and `{` brackets are
  nested more than 64 levels deep (brackets inside comments and quoted text are counted too).

## Rules

The `rules` setting switches individual rules on or off. Each entry names a rule by its identifier or by its
markdownlint alias (such as `MD013`), in any letter case, and has the value `true` or `false`. Rules that are not
listed keep their default:

```yaml
rules:
  MD013: false
  heading-increment: false
  descriptive-link-text: true
```

These are the 27 rules that a configuration can switch. A rule of kind "fixable" is applied by `--fix`; a rule of kind
"lint only" only reports (see *Lint Rules*):

| Rule identifier | Alias | Kind | Default |
| --- | --- | --- | --- |
| `blockquote-blank-line` | `MD028` | lint only | on |
| `code-fence-language` | `MD040` | lint only | on |
| `descriptive-link-text` | `MD059` | lint only | off |
| `dollar-sign-in-shell-code` | `MD014` | lint only | off |
| `emphasis-as-heading` | `MD036` | lint only | on |
| `emphasis-space` | `MD037` | lint only | on |
| `fence-unclosed` | | lint only | on |
| `footnote-unused` | | lint only | on |
| `grid-table-invalid` | | lint only | on |
| `heading-closed-missing-space` | `MD020` | lint only | on |
| `heading-duplicate` | `MD024` | lint only | on |
| `heading-increment` | `MD001` | lint only | on |
| `heading-missing-space` | `MD018` | lint only | on |
| `heading-trailing-punctuation` | `MD026` | lint only | on |
| `image-alt-text` | `MD045` | lint only | on |
| `line-length` | `MD013` | fixable | on |
| `link-definition-unused` | `MD053` | lint only | on |
| `link-empty` | `MD042` | lint only | on |
| `link-fragment` | `MD051` | lint only | on |
| `link-refactor` | `MD054` | fixable | on |
| `link-reference-undefined` | `MD052` | lint only | on |
| `link-reversed` | `MD011` | lint only | on |
| `link-space` | `MD039` | lint only | on |
| `paragraph-wrap` | | fixable | on |
| `table-column-count` | `MD056` | lint only | on |
| `table-grid-unformatted` | | lint only | off |
| `table-too-wide` | | lint only | on |

- `link-refactor` is on by default, but it acts only when the `link-refactor` setting has `enabled: true` (or the
  command line names it with `--enable`); switching the rule off stops the conversion.
- `paragraph-wrap` applies the `sentence` and `semantic` modes of `wrap`; `line-length` applies the `fill` behavior
  (see *Paragraph Wrapping*).
- An unknown rule name is an error.
- Naming the same rule twice, whether by identifier and alias or in different letter case, is an error. It is reported
  at the later entry and names the earlier one.
- The aliases of rules that LintMark no longer has are accepted and ignored: `MD002`, `MD006`, `MD008`, `MD025`,
  `MD033`, `MD041`, `MD043`, `MD044`, and `MD057`. A configuration that still contains them stays valid, but repeating
  one of them is still a duplicate.
- Rules that LintMark always applies cannot be switched off, so their identifiers and aliases (for example
  `line-ending`, `trailing-whitespace`, `MD009`, `bullet`, and `MD004`) are unknown names in `rules`. LintMark has 60
  rules: the 27 above can be switched, 26 formatting rules always apply (`line-ending` and the 25 rules of *Formatting
  Rules*), and 7 identifiers label failures (see *Usage*). A formatting rule is switched off for a passage with a
  suppression comment (see *Suppression*).

## Ignore Patterns

The `ignore` setting is a list of glob patterns for files that LintMark leaves alone:

```yaml
ignore:
  - "docs/generated/**"
  - "**/vendor/**"
```

- A file that matches a pattern is not read, not checked, not rewritten, and never reported, also not for read or
  parse problems.
- Patterns are relative to the directory of the configuration file that applies to the file (the current directory
  when no configuration file applies, as with `--no-config`). `*` matches within one path segment and `**` matches any
  number of directory levels, as on the command line; backslashes are accepted as separators. A file outside that
  directory is never ignored.
- The patterns replace the defaults; they do not extend them. The defaults are `.git/**`, `node_modules/**`,
  `**/.venv/**`, `**/generated/**`, `**/thirdparty/**`, and `.agent-logs/**`. To keep a default, list it again.
- Each item must be a non-blank, single-line piece of text. Backslashes are converted to forward slashes, and an exact
  duplicate of an earlier pattern is removed.
- `ignore: []` is valid and ignores nothing.
- **A file named on the command line is always processed**, even when an `ignore` pattern matches it. `ignore` applies
  only to files that were found through a directory, a glob, or the default selection. Only a `!` exclusion on the
  command line removes a file that was named explicitly.
- Patterns are judged with the configuration that applies to the file. A file in a folder with an invalid configuration
  has no usable patterns, so it is still read and checked, but never rewritten.

## Line Endings and .editorconfig

The line ending that a file is written with is decided per file, in this order:

1. The `end-of-line` setting in the configuration that applies to the file (`lf` or `crlf`).
2. The `end_of_line` property of the `.editorconfig` settings that apply to the file.
3. The file's own line endings: the one that occurs most often (LF when both occur equally often or the file has
   none).

The decision is applied: `--check` and `--diff` report a file whose line endings differ from it as `line-ending`, and
`--fix` rewrites every line ending of the file to it:

```text
docs/guide.md:1:1: line-ending Line endings are CRLF; expected LF
docs/notes.md:1:1: line-ending Mixed line endings (2 CRLF, 5 LF); expected LF
```

A file without any line ending, and a file that already uses only the chosen one, is compliant. A file that mixes CRLF
and LF is a violation of whichever ending is chosen for it, and its message names the mix. The `line-ending` rule
always applies: it is not configurable and cannot be suppressed, so it is not an entry of `rules`; choose the ending
with `end-of-line` or `.editorconfig` instead.

Details of the `.editorconfig` step:

- Only the values `lf` and `crlf`, in any letter case, are used. `cr`, `unset`, any other value, or no `end_of_line`
  property means "not specified" and moves on to the next step. It is never an error, because an `.editorconfig` file
  is shared with other kinds of files.
- The usual `.editorconfig` rules apply: the nearest file that sets the property wins for each Markdown file, a file
  with `root = true` ends the search, and a later section in a file overrides an earlier one.
- Section patterns such as `[*.md]` are case-sensitive on every platform, so `[*.MD]` does not match `a.md`.
- An `.editorconfig` that cannot be read is reported as a configuration error at the Markdown file it affects.
  Malformed content in an `.editorconfig` is ignored and is not an error.
- `.editorconfig` is consulted only when the configuration does not set `end-of-line`. `--config` does not disable it;
  `--no-config` does, together with `.lintmark.yaml`.

For example, with this `.editorconfig` next to `notes.md`, which uses LF line endings, and no `.lintmark.yaml`:

```ini
root = true

[*.md]
end_of_line = crlf
```

`lintmark notes.md` reports `notes.md:1:1: line-ending Line endings are LF; expected CRLF`, and `lintmark --fix
notes.md` converts the file to CRLF.

## Configuration Errors

Every configuration problem is reported on standard error in the same form as the other problems:

```text
path:line:col: config-error message
```

- The path is the configuration file, relative to the current directory with `/` separators. It can start with `../`
  when the file is outside the current directory. The line and column are 1-based positions in the configuration file.
  A problem that is not tied to a position, such as a file that is missing or cannot be read, is reported at `1:1`.
- The rule identifier is always `config-error`, and any configuration problem makes the exit code `1`.
- All problems in a file are reported in one run, ordered by position, so a configuration can be fixed in one pass.
  Syntax problems and problems with the structure of the document (such as a second document or a root that is not a
  mapping) end the checking of that file at that point.
- An invalid configuration is reported once, even when many Markdown files use it.
- A Markdown file with an invalid configuration is still read and checked, so its own read and parse problems are
  still reported, but it is never rewritten.

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

Syntax errors, a configuration file that does not exist, and a configuration file outside the current directory look
like this:

```text
../broken.yaml:3:1: config-error Invalid YAML: While parsing a flow sequence, did not find expected ',' or ']'.
missing.yaml:1:1: config-error Configuration file not found
```

## Sample Configuration

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

Most configuration files are much smaller. This one changes the line length and the bullet marker and keeps every other
default:

```yaml
line-length: 100
bullet: asterisk
```
