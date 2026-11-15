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

- **File Selection**: Select files by path, directory, or glob pattern with `!` exclusions; with no arguments every
  `**/*.md` file under the current directory is used
- **Check, Fix, and Diff**: `--check` (the default) reports problems, `--fix` rewrites each file into its canonical
  form, and `--diff` previews the rewrite as a unified diff
- **Formatting Rules**: 26 always-applied rules for whitespace, blank lines, headings, code blocks, thematic breaks,
  lists, block quotes, tables, emphasis, bare URLs, code spans, the end of the file, and line endings
- **Switchable Rules**: 27 rules that a configuration can switch on or off, 24 of them lint-only checks for headings,
  links, images, code fences, tables, footnotes, and block quotes
- **Paragraph Wrapping**: `fill` (only over-wide paragraphs), `sentence`, and `semantic` wrap modes at a configurable
  line length
- **Tables**: Tables are written compact or aligned, with pipes at both ends, and a table that cannot fit is reported
- **Link Refactoring**: Optional conversion of long inline links to reference links
- **Suppression**: `lintmark-disable` and `markdownlint-disable` comments leave a passage as it is
- **Configuration**: An optional `.lintmark.yaml` (the nearest file wins) and the `end_of_line` property of
  `.editorconfig`; every problem in a configuration is reported
- **Deterministic Parallel Output**: Files are processed in parallel and reported in a stable order
- **Atomic Writes**: A rewrite replaces the file in one step, keeps its permissions, and never overwrites a file that
  changed after it was read
- **Self-Validation**: Built-in validation tests with TRX and JUnit output
- **Multi-Platform**: Runs on Windows, Linux, and macOS with .NET 10 and 11
- **Continuous Compliance**: Compliance evidence is generated on every CI run, following the
  [Continuous Compliance][link-continuous-compliance] methodology

## Installation

Install the tool globally with the .NET CLI (.NET 10 or later is required):

```bash
dotnet tool install -g DemaConsulting.LintMark
```

## Usage

```bash
# Check every **/*.md file under the current directory (nothing is modified)
lintmark

# Check one file, a directory, or several of them
lintmark README.md docs

# Check files selected by glob patterns, leaving some out (quote the patterns)
lintmark "docs/**/*.md" "!docs/generated/**"

# Rewrite files into canonical form, then report what remains
lintmark --fix docs

# Preview what --fix would change as a unified diff; no file is written
lintmark --diff docs

# Use an explicit configuration file, or ignore all configuration
lintmark --config config/lintmark.yaml docs
lintmark --no-config docs

# Switch rules on or off for this run
lintmark --enable descriptive-link-text --disable MD013 docs
```

Problems are reported on standard error, one per line, as `path:line:col: rule-id message`:

```text
docs/start.md:3:10: trailing-whitespace A line ends with trailing whitespace, or a hard break is not exactly two spaces.
docs/start.md:4:1: bullet A bullet list item does not use the configured marker character.
docs/start.md:4:1: list-blank-lines A list is not surrounded by blank lines.
```

## Command-Line Options

```text
lintmark [options] [paths...]
```

| Option | Description |
| --- | --- |
| `paths...` | Files, directories, or globs to process; `!pattern` excludes |
| `--check` | Report problems without writing any file (the default) |
| `--fix` | Rewrite files into canonical form |
| `--diff` | Print a unified diff of what `--fix` would change |
| `--config <file>` | Use this configuration file instead of discovering `.lintmark.yaml` |
| `--no-config` | Ignore `.lintmark.yaml` and `.editorconfig`; use the built-in defaults |
| `--enable <ids>` | Force rules on (comma-separated ids or legacy rule numbers; repeatable) |
| `--disable <ids>` | Force rules off (same syntax as `--enable`) |
| `-v`, `--version` | Display version information |
| `-?`, `-h`, `--help` | Display help message |
| `--silent` | Suppress console output |
| `--validate` | Run self-validation |
| `--results <file>`, `--result <file>` | Write validation results to a `.trx` (TRX) or `.xml` (JUnit) file |
| `--depth <#>` | Set heading depth (1 to 6) for validation output (default: 1) |
| `--log <file>` | Write output to log file |

## Configuration

LintMark is configured with an optional `.lintmark.yaml` file. Every setting has a built-in default, so a file lists
only the settings it changes. For example:

```yaml
line-length: 100
bullet: asterisk
tables: aligned
rules:
  MD013: false
  descriptive-link-text: true
ignore:
  - "docs/generated/**"
```

The *Configuration* chapter of the User Guide lists every setting with its allowed values and default, and explains
how the file is discovered and how the line ending is chosen.

## Exit Codes

The exit code is `0` when every file was processed and nothing is left to report, and `1` when any problem was
reported (including a configuration problem), when an argument is invalid, or when self-validation fails.

## Self-Validation

`lintmark --validate` runs built-in tests and prints a report; add `--results results.trx` (or `results.xml`) to save
the results. Use `--depth <#>` to set the heading level of the report when you embed it in a larger document:

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

## Documentation

The [User Guide][link-guide] covers [usage][link-guide-usage], [configuration][link-guide-configuration],
the [formatting rules][link-guide-rules], the [lint rules][link-guide-lint-rules],
[paragraph wrapping][link-guide-wrapping], [tables and links][link-guide-tables-links],
[rule suppression][link-guide-suppression], and [migrating from markdownlint][link-guide-migrating].
CI also generates build notes, code quality reports, requirements, justifications, and a trace matrix.

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
[link-guide-usage]: https://github.com/demaconsulting/LintMark/blob/main/docs/user_guide/usage.md
[link-guide-configuration]: https://github.com/demaconsulting/LintMark/blob/main/docs/user_guide/configuration.md
[link-guide-rules]: https://github.com/demaconsulting/LintMark/blob/main/docs/user_guide/formatting-rules.md
[link-guide-lint-rules]: https://github.com/demaconsulting/LintMark/blob/main/docs/user_guide/lint-rules.md
[link-guide-wrapping]: https://github.com/demaconsulting/LintMark/blob/main/docs/user_guide/wrapping.md
[link-guide-tables-links]: https://github.com/demaconsulting/LintMark/blob/main/docs/user_guide/tables-and-links.md
[link-guide-suppression]: https://github.com/demaconsulting/LintMark/blob/main/docs/user_guide/suppression.md
[link-guide-migrating]: https://github.com/demaconsulting/LintMark/blob/main/docs/user_guide/migrating-from-markdownlint.md
[link-continuous-compliance]: https://github.com/demaconsulting/ContinuousCompliance
