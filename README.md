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

LintMark currently provides the following (markdown linting and formatting rules are planned but not yet
implemented):

- **Markdown File Discovery**: Select files by path, directory, or glob pattern (with `!` exclusions);
  with no arguments every `**/*.md` file under the current directory is used
- **Markdown Parsing**: Every selected file is read as UTF-8 text with LF or CRLF line endings and parsed;
  files that cannot be read or parsed are reported as `path:line:col: rule-id message`
- **Deterministic Parallel Processing**: Files are processed in parallel and reported in a stable order, so
  output does not depend on scheduling
- **Check Mode**: `--check` states that files are only checked, never modified (currently the default and only
  behavior)
- **Configuration**: An optional `.lintmark.yaml` (nearest file wins, no merging) or an explicit `--config <file>`
  is found, read, and validated, and every problem is reported as `path:line:col: config-error message`;
  `--no-config` uses the built-in defaults; the line ending can also come from `.editorconfig`
- **Standardized Command-Line Interface**: Context class handling common arguments
  (`--version`, `--help`, `--silent`, `--validate`, `--results`, `--depth`, `--log`, `--check`, `--config`,
  `--no-config`)
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

# Report problems only; never modify a file (this is currently the only behavior)
lintmark --check docs

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
Markdown file and reports every problem in it; the validated settings are not applied yet, because no linting or
formatting rule runs in this release.

- **Discovery**: For each Markdown file the search starts in the file's directory and walks up. The nearest
  `.lintmark.yaml` wins and files are never merged. The search stops after the repository root (the first directory
  that contains `.git`, as a directory or a file); without a `.git` entry it continues to the file-system root.
- **`--config <file>`** uses exactly that file (relative to the current directory; the last one wins when repeated)
  and disables discovery. **`--no-config`** ignores both `.lintmark.yaml` and `.editorconfig` and uses the built-in
  defaults. The two options cannot be combined with each other, and neither can be combined with `--validate`.
- **Line endings**: `end-of-line` in the configuration wins, then the `end_of_line` property of `.editorconfig`
  (only `lf` and `crlf` are used; other values mean "not specified"), then the file's own line endings, which later
  releases apply. `--config` does not disable `.editorconfig`; `--no-config` does.
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
they are accepted and checked but not yet applied. See the [User Guide][link-guide] for the complete rule list and
every detail.

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

Nothing is written for files that are read and parsed successfully with a valid configuration. Each file that
cannot be processed, and each configuration problem, is reported on standard error, one line per problem, in the
form `path:line:col: rule-id message`. Paths are relative to the current directory and use `/` separators; lines
are ordered by path and then by position.

```text
docs/broken.md:2:3: file-encoding-error Invalid UTF-8 byte sequence
missing.md:1:1: file-read-error File not found
missing.yaml:1:1: config-error Configuration file not found
```

The exit code is `0` when every file was processed and `1` when any problem was reported (including any
configuration problem) or an argument was invalid. A pattern that matches no file is not an error.

## Error Handling

Unrecognized arguments cause the tool to print an error message to standard error and exit
with a non-zero exit code. For example:

```text
Error: Unsupported argument '--unknown'
```

`--validate` cannot be combined with paths, `--check`, `--config`, or `--no-config`, and `--config` cannot be
combined with `--no-config`:

```text
Error: --validate cannot be combined with paths, --check, --config, or --no-config (Parameter 'args')
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
[link-continuous-compliance]: https://github.com/demaconsulting/ContinuousCompliance
