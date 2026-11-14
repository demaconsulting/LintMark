# LintMark

[![GitHub forks][badge-forks]][link-forks]
[![GitHub stars][badge-stars]][link-stars]
[![GitHub contributors][badge-contributors]][link-contributors]
[![License][badge-license]][link-license]
[![Build][badge-build]][link-build]
[![Quality Gate][badge-quality]][link-quality]
[![Security][badge-security]][link-security]
[![NuGet][badge-nuget]][link-nuget]

LintMark is a .NET tool, distributed as a NuGet tool package, for linting and optionally reformatting markdown files
into a preferred format, with optional tweaks via a .lintmark.yaml configuration file.

The diagram below is a placeholder workflow diagram: replace its TODO boxes (in `docs/images/workflow.svg`,
then re-render `docs/images/workflow.png`) to show how users prepare inputs, run your tool, get its outputs,
and use them.

![Placeholder workflow diagram: replace the TODO boxes to show how users prepare inputs, run your tool, get
its outputs, and use them][workflow]

## Features

LintMark currently provides the following (the formatting rules for whitespace, blank lines, headings, code blocks,
thematic breaks, lists, block quotes, emphasis, bare URLs, the end of the file, and line endings are implemented; the
rules for paragraphs, tables, and links, and the lint-only rules, are planned but not yet implemented):

- **Markdown File Discovery**: Select files by path, directory, or glob pattern (with `!` exclusions);
  with no arguments every `**/*.md` file under the current directory is used
- **Markdown Parsing**: Every selected file is read as UTF-8 text with LF or CRLF line endings and parsed;
  files that cannot be read or parsed are reported as `path:line:col: rule-id message`
- **Deterministic Parallel Processing**: Files are processed in parallel and reported in a stable order, so
  output does not depend on scheduling
- **Check Mode**: Without `--fix`, files are only checked and never modified; `--check` states this explicitly
- **Fix Mode**: `--fix` rewrites each file into its canonical form (the formatting rules below) and reports what
  remains; a file that is already compliant is not touched
- **Diff Preview**: `--diff` reports the findings and prints a unified diff of what `--fix` would change; it writes no
  file
- **Formatting Rules**: Twenty-two always-applied rules, each with an identifier and a markdownlint number: trailing
  whitespace (`trailing-whitespace`, MD009), tabs (`hard-tabs`, MD010), runs of blank lines (`max-blank-lines`, MD012),
  heading style, spacing, and indentation (`heading-style`, `heading-space`, `heading-start-left`; MD003, MD019, MD021,
  MD023), blank lines around headings, fences, lists, and tables (`heading-blank-lines`, `fence-blank-lines`,
  `list-blank-lines`, `table-blank-lines`; MD022, MD031, MD032, MD058), thematic breaks (`thematic-break`, MD035),
  code blocks and fences (`code-block-style`, `code-fence`; MD046, MD048), the final newline (`final-newline`,
  MD047), list markers, numbering, indentation, and spacing (`bullet`, `ordered-style`, `list-indent`,
  `list-marker-space`; MD004, MD029, MD005, MD007, MD030), block quote spacing (`blockquote-space`, MD027), emphasis
  and strong emphasis delimiters (`emphasis`, `strong`; MD049, MD050), and bare URLs (`bare-urls`, MD034). Code, HTML,
  math, and front matter are never changed, and a conversion that could change the meaning or the rendering of a
  document is not made. MD018 and MD020 are not implemented, and MD037, MD038, and MD039 are not fixed: they are
  planned as lint-only checks. Each rule is documented, with examples, in the [User Guide][link-guide-rules]
- **Rule Suppression**: `lintmark-disable` and `markdownlint-disable` comments, by identifier or by legacy number, leave
  a passage as it is
- **Safety Checks**: A rewrite is written only when it renders to the same HTML, keeps the Pandoc structure
  (fences, grid table borders, and so on), and is stable when formatted again; this includes a rewrite that only
  changes the line endings, because the renderer shows some text differently for CRLF and LF; otherwise the file
  is refused
  (`safety-html-mismatch`, `safety-structure-mismatch`, `safety-not-idempotent`) and left as it was
- **Atomic Writes**: A rewrite replaces the file in one step, keeps its permissions, and never overwrites a file
  that changed after it was read
- **Line Endings**: The line ending of a file comes from `end-of-line`, else `.editorconfig`, else the file's own
  majority; a deviation is reported as `line-ending` and fixed by `--fix`
- **Grid Table Protection**: A line that looks like a grid table border but is not part of a valid table is
  reported as `grid-table-invalid` and the file is never rewritten
- **Ignore Patterns**: The `ignore` patterns of the configuration leave matching files alone (files named on the
  command line are always processed)
- **Configuration**: An optional `.lintmark.yaml` (nearest file wins, no merging) or an explicit `--config <file>`
  is found, read, and validated, and every problem is reported as `path:line:col: config-error message`;
  `--no-config` uses the built-in defaults; the line ending can also come from `.editorconfig`
- **Standardized Command-Line Interface**: Context class handling common arguments
  (`--version`, `--help`, `--silent`, `--validate`, `--results`, `--depth`, `--log`, `--check`, `--fix`, `--diff`,
  `--config`, `--no-config`)
- **Self-Validation**: Built-in validation tests with TRX/JUnit output
- **Multi-Platform Support**: Builds and runs on Windows, Linux, and macOS
- **Multi-Runtime Support**: Targets .NET 10 and 11
- **Comprehensive CI/CD**: GitHub Actions workflows with quality checks, builds, and
  integration tests
- **Linting Enforcement**: markdownlint, TextLint, and yamllint enforced on every CI run
- **Continuous Compliance**: Compliance evidence generated automatically on every CI run, following
  the [Continuous Compliance][link-continuous-compliance] methodology
- **SonarCloud Integration**: Quality gate and security analysis on every build
- **Documentation Generation**: Automated build notes, user guide, code quality reports,
  requirements, justifications, and trace matrix
- **Requirements Traceability**: Requirements linked to passing tests with auto-generated trace matrix

## Installation

Install the tool globally using the .NET CLI:

```bash
dotnet tool install -g DemaConsulting.LintMark
```

## Usage

```bash
# Default behavior (no arguments) — displays the banner, then reads and parses every **/*.md file
# under the current directory (node_modules and .git folders are skipped)
lintmark

# Process one file, a directory (every *.md file beneath it), or several of them
lintmark README.md
lintmark docs
lintmark README.md docs

# Process files selected by glob patterns (* and **); quote patterns so the shell does not expand them
lintmark "docs/**/*.md"

# Exclude files with a leading ! (quote it as well)
lintmark "docs/**/*.md" "!docs/generated/**"

# Report problems only; never modify a file (this is the default)
lintmark --check docs

# Rewrite files into canonical form when every safety check passes, then report what remains
lintmark --fix docs

# Preview what --fix would change as a unified diff; no file is written
lintmark --diff docs

# Use an explicit configuration file instead of discovering .lintmark.yaml
lintmark --config config/lintmark.yaml docs

# Ignore .lintmark.yaml and .editorconfig; use the built-in defaults
lintmark --no-config docs

# Display version
lintmark --version

# Display help
lintmark --help

# Run self-validation
lintmark --validate

# Save validation results (--result is an accepted alias for --results)
lintmark --validate --results results.trx
lintmark --validate --result results.xml

# Set heading depth for embedded validation output
lintmark --validate --depth 2

# Silent mode with logging
lintmark --silent --log output.log
```

## Command-Line Options

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
| `--results <file>`, `--result <file>` | Write results to `.trx` (TRX) or `.xml` (JUnit XML) file.    |
| `--depth <#>`                         | Set heading depth for markdown output (default: 1)           |
| `--log <file>`                        | Write output to log file                                     |

## Configuration

LintMark is configured with an optional `.lintmark.yaml` file. Every setting has a built-in default, so a file only
lists the settings it changes. This release finds, reads, and validates the configuration that applies to each
Markdown file and reports every problem in it. It applies `end-of-line`, `final-newline`, `max-blank-lines`,
`code-fence`, `thematic-break`, `heading-style`, `code-block-style`, `trailing-whitespace`, `bullet`, `list-indent`,
`ordered-style`, `emphasis`, `strong`, `bare-urls`, `ignore`, and the `grid-table-invalid` entry of `rules`; the other
settings are validated and stored, but no linting or formatting rule uses them yet.

- **Discovery**: For each Markdown file the search starts in the file's directory and walks up. The nearest
  `.lintmark.yaml` wins and files are never merged. The search stops after the repository root (the first directory
  that contains `.git`, as a directory or a file); without a `.git` entry it continues to the file-system root.
- **`--config <file>`** uses exactly that file (relative to the current directory; the last one wins when repeated)
  and disables discovery. **`--no-config`** ignores both `.lintmark.yaml` and `.editorconfig` and uses the built-in
  defaults. The two options cannot be combined with each other, and neither can be combined with `--validate`.
- **Line endings**: `end-of-line` in the configuration wins, then the `end_of_line` property of `.editorconfig`
  (only `lf` and `crlf` are used; other values mean "not specified"), then the file's own majority line ending
  (LF on a tie). The result is applied: `line-ending` is reported for a deviation, and `--fix` rewrites the file.
  `--config` does not disable `.editorconfig`; `--no-config` does.
- **Strict checking**: Setting names and word values are lower-case and case-sensitive; numbers and booleans are
  plain, unquoted values; unknown settings, duplicate settings, and settings with no value are errors.
- **Errors**: Problems are reported as `path:line:col: config-error message` with 1-based positions in the
  configuration file, all in one run, and make the exit code `1`.

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
| `rules`               | a mapping of rule names to `true` or `false`          | all on except three        |
| `ignore`              | a list of glob patterns                               | six built-in patterns      |

The `link-refactor` options are `enabled` (default `false`), `threshold` (`60`), `in-tables` (`true`),
`break-link-text` (`false`), `label-max-length` (`40`), and `definitions-marker` (`<!-- Link References -->`).
The `rules` setting switches rules on or off by identifier or markdownlint alias (such as `MD013`);
`descriptive-link-text`,
`table-grid-unformatted`, and `dollar-sign-in-shell-code` are off by default. The `ignore` patterns replace the
defaults (`.git/**`, `node_modules/**`, `**/.venv/**`, `**/generated/**`, `**/thirdparty/**`, `.agent-logs/**`);
they are relative to the directory of the configuration file, and a file named on the command line is always
processed. See the [User Guide][link-guide] for the complete rule list and every detail.

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

Nothing is written for files that are read and parsed successfully with a valid configuration and nothing to fix.
Each problem is reported on standard error, one line per problem, in the form `path:line:col: rule-id message`.
Paths are relative to the current directory and use `/` separators; lines are ordered by path and then by position.

```text
docs/broken.md:2:3: file-encoding-error Invalid UTF-8 byte sequence
docs/guide.md:1:1: line-ending Line endings are CRLF; expected LF
docs/start.md:9:1: heading-blank-lines A heading is not surrounded by blank lines.
a.md:3:1: grid-table-invalid Line looks like a grid table border but the table is not valid; the file is never rewritten
missing.md:1:1: file-read-error File not found
missing.yaml:1:1: config-error Configuration file not found
```

The reported rule identifiers are `file-read-error`, `file-encoding-error`, `file-line-ending-error`,
`file-parse-error`, `config-error`, `line-ending`, the 22 formatting rules (`trailing-whitespace`, `hard-tabs`,
`max-blank-lines`, `heading-style`, `heading-space`, `heading-start-left`, `heading-blank-lines`, `fence-blank-lines`,
`list-blank-lines`, `table-blank-lines`, `thematic-break`, `code-block-style`, `code-fence`, `final-newline`, `bullet`,
`ordered-style`, `list-indent`, `list-marker-space`, `blockquote-space`, `emphasis`, `strong`, and `bare-urls`, each
reported at the place that `--fix` would change), `grid-table-invalid`, `safety-html-mismatch`,
`safety-structure-mismatch`, `safety-not-idempotent`, `file-format-error`, and `file-write-error`. A `safety-*`
identifier means that a rewrite was refused because it could damage the document; the file is left as it was. A
grid table problem (`grid-table-invalid`) is never rewritten, and the formatting rules and `line-ending` are always
applied, so they are not configurable in `rules` (a comment switches a formatting rule off for a passage; `line-ending`
is not suppressible). `--diff` reports the findings on standard error, as in check mode, and writes its unified diff to
standard output.

The exit code is `0` when every file was processed and nothing is left to report, and `1` when any problem was
reported (including any configuration problem) or an argument was invalid. After `--fix`, the problems that
rewriting removed are not reported. A pattern that matches no file is not an error.

## Error Handling

Unrecognized arguments cause the tool to print an error message to standard error and exit
with a non-zero exit code. For example:

```text
Error: Unsupported argument '--unknown'
```

`--validate` cannot be combined with paths, `--check`, `--fix`, `--diff`, `--config`, or `--no-config`, `--fix`
cannot be combined with `--check` or `--diff`, and `--config` cannot be combined with `--no-config`:

```text
Error: --validate cannot be combined with paths, --check, --fix, --diff, --config, or --no-config (Parameter 'args')
Error: --fix cannot be combined with --check or --diff (Parameter 'args')
Error: --config cannot be combined with --no-config (Parameter 'args')
```

This behavior enables CI/CD pipelines to detect and surface misconfiguration failures
automatically.

## Self Validation

Running self-validation produces a report containing the following information:

```text
# DEMA Consulting LintMark

| Information         | Value                                              |
| :------------------ | :------------------------------------------------- |
| Tool Version        | <version>                                          |
| Machine Name        | <machine-name>                                     |
| OS Version          | <os-version>                                       |
| DotNet Runtime      | <dotnet-runtime-version>                           |
| Time Stamp          | <timestamp> UTC                                    |

✓ LintMark_VersionDisplay - Passed
✓ LintMark_HelpDisplay - Passed

Total Tests: 2
Passed: 2
Failed: 0
```

Each test in the report proves:

- **`LintMark_VersionDisplay`** - `--version` outputs a valid version string.
- **`LintMark_HelpDisplay`** - `--help` outputs usage and options information.

Use `--depth <#>` to control the heading level of the validation output (default: `1`).
This is useful when embedding validation output into a larger markdown document:

```bash
# Embed validation at heading level 2
lintmark --validate --depth 2
```

See the [User Guide][link-guide] for more details on the self-validation tests.

On validation failure the tool will exit with a non-zero exit code.

## Documentation

Generated documentation includes:

- **Build Notes**: Release information and changes
- **User Guide**: Comprehensive usage documentation
- **Code Quality Report**: CodeQL and SonarCloud analysis results
- **Requirements**: Functional and non-functional requirements
- **Requirements Justifications**: Detailed requirement rationale
- **Trace Matrix**: Requirements to test traceability

## Contributing

See [CONTRIBUTING.md](https://github.com/demaconsulting/LintMark/blob/main/CONTRIBUTING.md) for
guidelines on reporting bugs, suggesting features, and submitting pull requests.

## License

Copyright (c) DEMA Consulting. Licensed under the MIT License. See [LICENSE][link-license] for details.

By contributing to this project, you agree that your contributions will be licensed under the MIT License.

<!-- Badge References -->
[workflow]: https://raw.githubusercontent.com/demaconsulting/LintMark/main/docs/images/workflow.png
[badge-forks]: https://img.shields.io/github/forks/demaconsulting/LintMark?style=plastic
[badge-stars]: https://img.shields.io/github/stars/demaconsulting/LintMark?style=plastic
[badge-contributors]: https://img.shields.io/github/contributors/demaconsulting/LintMark?style=plastic
[badge-license]: https://img.shields.io/github/license/demaconsulting/LintMark?style=plastic
[badge-build]: https://img.shields.io/github/actions/workflow/status/demaconsulting/LintMark/build_on_push.yaml?style=plastic
[badge-quality]: https://sonarcloud.io/api/project_badges/measure?project=demaconsulting_LintMark&metric=alert_status
[badge-security]: https://sonarcloud.io/api/project_badges/measure?project=demaconsulting_LintMark&metric=security_rating
[badge-nuget]: https://img.shields.io/nuget/v/DemaConsulting.LintMark?style=plastic

<!-- Link References -->
[link-forks]: https://github.com/demaconsulting/LintMark/network/members
[link-stars]: https://github.com/demaconsulting/LintMark/stargazers
[link-contributors]: https://github.com/demaconsulting/LintMark/graphs/contributors
[link-license]: https://github.com/demaconsulting/LintMark/blob/main/LICENSE
[link-build]: https://github.com/demaconsulting/LintMark/actions/workflows/build_on_push.yaml
[link-quality]: https://sonarcloud.io/dashboard?id=demaconsulting_LintMark
[link-security]: https://sonarcloud.io/dashboard?id=demaconsulting_LintMark
[link-nuget]: https://www.nuget.org/packages/DemaConsulting.LintMark
[link-guide]: https://github.com/demaconsulting/LintMark/blob/main/docs/user_guide/introduction.md
[link-guide-rules]: https://github.com/demaconsulting/LintMark/blob/main/docs/user_guide/formatting-rules.md
[link-continuous-compliance]: https://github.com/demaconsulting/ContinuousCompliance
