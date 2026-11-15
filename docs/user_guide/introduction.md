# Introduction

The workflow below shows how you prepare inputs, run LintMark locally, in CI, or from a coding agent, read its
results, and use them.

![LintMark workflow in four steps. Prepare inputs: Markdown files, an optional .lintmark.yaml, and an .editorconfig for
line endings. Run LintMark with --check to report, or --fix or --diff to rewrite or preview, locally, in CI, or from
coding agents. Get results: findings as file:line:col: rule-id message, rewritten files or a unified diff, and an exit
code of 0 when clean or 1 for findings or errors. Use them as a CI quality gate, for auto-formatting, and for a
consistent style. Tool qualification evidence comes from lintmark --validate --results
validation-results.trx.](workflow.svg)

## Purpose

LintMark is a .NET tool, distributed as a NuGet tool package, for linting and optionally reformatting
markdown files into a preferred format, with optional tweaks via a `.lintmark.yaml` configuration file.

It selects Markdown files by path, directory, or glob pattern, reads and parses each one, and reports the problems it
finds as `path:line:col: rule-id message`. With `--fix` it rewrites each file into its canonical form, and `--diff`
previews that rewrite as a unified diff. The checks are made by 60 registered rules:

- 26 formatting rules that are always applied: whitespace, blank lines, headings, code blocks, thematic breaks, lists,
  block quotes, tables, emphasis, bare URLs, code spans, the end of the file, and line endings.
- 27 rules that a configuration can switch on or off: 24 lint-only checks (headings, links, images, code fences,
  tables, footnotes, and block quotes) and the three configurable formatting rules `line-length`, `paragraph-wrap`, and
  `link-refactor`.
- 7 identifiers under which LintMark reports its own failures (file, formatting, and configuration problems).

LintMark also provides a standard command-line interface and self-validation, so that its own correct operation can
be shown as evidence in a regulated process.

## Scope

This user guide covers:

- Installation, the selection of files, the modes `--check`, `--fix`, and `--diff`, all command-line options, the
  output format, and the exit codes (see *Usage*)
- The `.lintmark.yaml` configuration file, every setting with its default, and the line ending that comes from
  `.editorconfig` (see *Configuration*)
- Every formatting rule that rewrites a file, with an example (see *Formatting Rules*)
- Every rule that only reports a problem (see *Lint Rules*)
- How paragraphs are wrapped (see *Paragraph Wrapping*), how tables are written and links are converted (see *Tables
  and Links*), and how a rule is suppressed for a passage (see *Suppression*)
- How to move from markdownlint to LintMark (see *Migrating from markdownlint*)

The guide describes the released behavior of the tool only. The design and verification of LintMark are documented
in separate documents.

## Continuous Compliance

LintMark follows the
[Continuous Compliance](https://github.com/demaconsulting/ContinuousCompliance) methodology, which ensures
compliance evidence is generated automatically on every CI run: requirements are linked to passing tests, a trace
matrix is generated on each release, and markdownlint, TextLint, and yamllint are enforced before any build proceeds.

## References

N/A
