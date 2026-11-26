# ![LintMark: Markdown linting and formatting][hero]

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

The diagram below shows the workflow: you give LintMark Markdown files and optional settings, run it in one of three
modes, and use its findings, rewrites, and exit code.

![LintMark workflow in four steps. Prepare inputs: Markdown files, an optional .lintmark.yaml, and an .editorconfig for
line endings. Run LintMark with --check to report, or --fix or --diff to rewrite or preview, locally, in CI, or from
coding agents. Get results: findings as file:line:col: rule-id message, rewritten files or a unified diff, and an exit
code of 0 when clean or 1 for findings or errors. Use them as a CI quality gate, for auto-formatting, and for a
consistent style. Tool qualification evidence comes from lintmark --validate --results
validation-results.trx.][workflow]

## Features

LintMark provides:

- 🔧 **Auto-Fix Formatting**: Rewrites whitespace, headings, lists, code, and quotes canonically
- 📏 **Line-Length Reflow**: Re-wraps only the paragraphs that overflow the line length
- 📝 **Wrap Modes**: Chooses fill, sentence, or semantic line breaks
- 📊 **Table Formatting**: Writes pipe tables compact or column-aligned
- 🔎 **Lint-Only Checks**: Reports heading, link, code fence, and table problems
- 🔗 **Link Refactoring**: Turns long inline links into reference links on request
- 🗂️ **Flexible Configuration**: Reads `.lintmark.yaml` and the `.editorconfig` line ending
- 🔄 **markdownlint Compatible**: Accepts MDxxx rule IDs and `markdownlint-disable` comments
- 🙈 **Suppression Comments**: Leaves chosen passages or rules alone
- ⚡ **Parallel Processing**: Handles many files quickly with stable, ordered output
- ✅ **Self-Validation**: Built-in validation tests with TRX and JUnit output
- 💡 **Built-in Help**: `lintmark help` serves help topics with runnable examples
- 🖥️ **Multi-Platform Support**: Builds and runs on Windows, Linux, macOS
- ⚙️ **Multi-Runtime Support**: Targets .NET 10 and 11 runtimes
- 🔁 **Comprehensive CI/CD**: GitHub Actions workflows for quality checks, builds, tests
- 🛡️ **Continuous Compliance**: CI generates evidence using [Continuous Compliance][link-continuous-compliance]
- 📚 **Documentation Tooling**: Automates build notes, user guide, and compliance reports

## Installation

Install the tool globally using the .NET CLI:

```bash
dotnet tool install -g DemaConsulting.LintMark
```

## Getting Started

```bash
# Check every Markdown file under the current directory (nothing is modified)
lintmark

# Report problems in specific files or directories
lintmark --check README.md docs

# Rewrite files into canonical form, then report what remains
lintmark --fix

# Preview what --fix would change, as a unified diff
lintmark --diff docs/

# List the built-in help topics, then read one
lintmark help
lintmark help getting-started
```

A minimal `.lintmark.yaml`:

```yaml
line-length: 100
wrap: fill
tables: aligned
ignore:
  - docs/generated/**
```

The exit code is `0` when nothing is left to report and `1` otherwise, so `lintmark --check` works as a CI gate.

## Learn More

The [User Guide][link-guide] covers everything else:

- [Command-line options and exit codes][link-guide-usage]
- [Configuration][link-guide-configuration]
- [Formatting rules][link-guide-rules] and [lint rules][link-guide-lint-rules]
- [Paragraph wrapping][link-guide-wrapping] and [tables and links][link-guide-tables-links]
- [Suppression comments][link-guide-suppression]
- [Migrating from markdownlint][link-guide-migrating]

## Self-Validation

Run the built-in validation tests, and optionally save the results as TRX or JUnit. Besides the version and help
output, the tests check a compliant file and a file with deliberate violations, fix a file, preview a fix with
`--diff`, and apply a `.lintmark.yaml`, each in an isolated temporary folder, so a passing run is evidence that the
installed tool works:

```bash
lintmark --validate
lintmark --validate --results results.trx
```

## Documentation

Generated documentation includes:

- **Build Notes**: Release information and changes
- **User Guide**: Comprehensive usage documentation
- **Code Quality Report**: CodeQL and SonarCloud analysis results
- **Requirements**: Functional and non-functional requirements
- **Verification**: Requirement-to-test mapping and verification evidence design

## Contributing

See [CONTRIBUTING.md](https://github.com/demaconsulting/LintMark/blob/main/CONTRIBUTING.md) for
guidelines on reporting bugs, suggesting features, and submitting pull requests.

## License

Copyright (c) DEMA Consulting. Licensed under the MIT License. See [LICENSE][link-license] for details.

By contributing to this project, you agree that your contributions will be licensed under the MIT License.

<!-- Badge References -->
[hero]: https://raw.githubusercontent.com/demaconsulting/LintMark/main/docs/images/hero.png
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
[link-continuous-compliance]: https://github.com/demaconsulting/ContinuousCompliance
[link-guide]: https://github.com/demaconsulting/LintMark/blob/main/docs/user_guide/introduction.md
[link-guide-usage]: https://github.com/demaconsulting/LintMark/blob/main/docs/user_guide/usage.md
[link-guide-configuration]: https://github.com/demaconsulting/LintMark/blob/main/docs/user_guide/configuration.md
[link-guide-rules]: https://github.com/demaconsulting/LintMark/blob/main/docs/user_guide/formatting-rules.md
[link-guide-lint-rules]: https://github.com/demaconsulting/LintMark/blob/main/docs/user_guide/lint-rules.md
[link-guide-wrapping]: https://github.com/demaconsulting/LintMark/blob/main/docs/user_guide/wrapping.md
[link-guide-tables-links]: https://github.com/demaconsulting/LintMark/blob/main/docs/user_guide/tables-and-links.md
[link-guide-suppression]: https://github.com/demaconsulting/LintMark/blob/main/docs/user_guide/suppression.md
[link-guide-migrating]: https://github.com/demaconsulting/LintMark/blob/main/docs/user_guide/migrating-from-markdownlint.md
