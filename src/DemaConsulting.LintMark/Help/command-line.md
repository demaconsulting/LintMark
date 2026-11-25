---
title: Command-line options
summary: Every option and path form, and the order in which a run handles them.
aliases:
  - options
  - flags
  - usage
  - arguments
  - paths
tags:
  - reference
order: 2
see-also:
  - configuration
  - rules
  - self-validation
examples:
  - name: guide
    kind: source
    language: markdown
    save-as: docs/guide.markdown
    description: A compliant Markdown file.
    content: |
      # Guide

      Clean text.
  - name: api
    kind: source
    language: markdown
    save-as: docs/generated/api.markdown
    description: A generated file with asterisk bullets.
    content: |
      # API

      * item
  - name: usage
    kind: command
    language: shell
    description: Print the short usage text. It ends with a pointer to this help.
    content: '{{tool}} --help'
    expect: "Run '{{tool}} help' for topics and examples."
  - name: glob
    kind: command
    language: shell
    description: Check the files that a glob finds; generated folders are ignored.
    content: '{{tool}} "docs/**/*.markdown"'
  - name: named
    kind: command
    language: shell
    description: A file named as an argument is always processed. Exits 1.
    content: '{{tool}} docs/generated/api.markdown'
    exit: 1
    expect: 'docs/generated/api.markdown:3:1: bullet'
  - name: silent-log
    kind: command
    language: shell
    description: Hide the console output and keep it in a log file.
    content: '{{tool}} --silent --log output.log --version'
  - name: unknown-option
    kind: command
    language: shell
    description: An unknown argument is an error, printed even with --silent. Exits 1.
    content: '{{tool}} --silent --unknown'
    exit: 1
    expect: "Error: Unsupported argument '--unknown'"
  - name: conflict
    kind: command
    language: shell
    description: Some options cannot be combined. Exits 1.
    content: '{{tool}} --fix --diff'
    exit: 1
    expect: 'Error: --fix cannot be combined with --check or --diff'
---
Usage: `{{tool}} [options] [paths...]`. Options start with `-` and may be
given in any order; every other argument is a path.

**Paths.** A path is a file, a folder (every `*.md` file below it), or a
glob such as `"docs/**/*.md"`; quote globs. A pattern that starts with `!`
excludes files, also files named explicitly. Without paths, the tool
checks `**/*.md` below the current folder. Files in folders that the
`ignore` setting lists, by default `generated` and `node_modules` among
others, are skipped ([[#glob]]), but a file named as an argument is always
processed, whatever its extension ([[#named]]).

| Option | Meaning |
| --- | --- |
| `--check` | Report problems and write nothing (the default). |
| `--fix` | Rewrite files into canonical form; report what remains. |
| `--diff` | Report problems and print the diff that `--fix` would apply. |
| `--config <file>` | Use this file instead of finding `.lintmark.yaml`. |
| `--no-config` | Ignore `.lintmark.yaml` and `.editorconfig`. |
| `--enable <ids>` | Force rules on, comma-separated; see [[rules]]. |
| `--disable <ids>` | Force rules off; both options are repeatable. |
| `-v`, `--version` | Print only the version. |
| `-?`, `-h`, `--help` | Print the short usage text ([[#usage]]). |
| `--silent` | Hide console output. Problems still set exit code 1. |
| `--log <file>` | Also write the output to a file, replacing it. |
| `--validate` | Run the self-tests; see [[self-validation]]. |
| `--results <file>` | Write the self-test results; `--result` also works. |
| `--depth <n>` | First heading level of the self-test report, 1 to 6. |

**Order.** `--version`, when all arguments are valid, prints only the
version and skips everything else. Without it, the tool prints a banner,
then handles the first of `--help`, `--validate`, or the check of the
selected files. The command that [[#silent-log]] prints writes the
version to the log file only.

**Errors.** An unknown argument, an option without its value, or a
rejected combination prints `Error: ...` and exits 1, as
[[#unknown-option]] and
[[#conflict]] show. `--fix` cannot be combined
with `--check` or `--diff`, `--config` cannot be combined with
`--no-config`, and `--validate` cannot be combined with paths or with
the options that check files. These errors are found before `--silent`
and `--log` take effect, so they are always printed and never logged.

**Help.** `help` is a command, not an option, and must be the first
argument. `--silent` and `--log` do not apply to it, and an unknown topic
exits 2. To check a file named `help`, write `./help`.
