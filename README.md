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
- **Standardized Command-Line Interface**: Context class handling common arguments
  (`--version`, `--help`, `--silent`, `--validate`, `--results`, `--depth`, `--log`, `--check`)
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
| `-v`, `--version`                     | Display version information                                  |
| `-?`, `-h`, `--help`                  | Display help message                                         |
| `--silent`                            | Suppress console output                                      |
| `--validate`                          | Run self-validation (cannot be combined with paths)          |
| `--results <file>`, `--result <file>` | Write results to `.trx` (TRX) or `.xml` (JUnit XML) file.    |
| `--depth <#>`                         | Set heading depth for markdown output (default: 1)           |
| `--log <file>`                        | Write output to log file                                     |

## Output and Exit Codes

Nothing is written for files that are read and parsed successfully. Each file that cannot be processed is
reported on standard error, one line per problem, in the form `path:line:col: rule-id message`. Paths are
relative to the current directory and use `/` separators; lines are ordered by path and then by position.

```text
docs/broken.md:2:3: file-encoding-error Invalid UTF-8 byte sequence
missing.md:1:1: file-read-error File not found
```

The exit code is `0` when every file was processed and `1` when any problem was reported or an argument
was invalid. A pattern that matches no file is not an error.

## Error Handling

Unrecognized arguments cause the tool to print an error message to standard error and exit
with a non-zero exit code. For example:

```text
Error: Unsupported argument '--unknown'
```

Combining `--validate` with paths or `--check` is also an error.

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
