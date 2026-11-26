---
title: Troubleshooting
summary: Exit codes, the failure identifiers, and the usage errors and how to fix them.
aliases:
  - errors
  - exit-codes
  - problems
tags:
  - troubleshooting
order: 10
see-also:
  - command-line
  - configuration
  - rules
examples:
  - name: unknown-option
    kind: command
    language: shell
    description: An unknown option. Exits 1.
    content: '{{tool}} --unknown'
    exit: 1
    expect: "Error: Unsupported argument '--unknown'"
  - name: silent-error
    kind: command
    language: shell
    description: Argument errors are printed even with --silent. Exits 1.
    content: '{{tool}} --silent --unknown'
    exit: 1
    expect: "Error: Unsupported argument '--unknown'"
  - name: missing-file
    kind: command
    language: shell
    description: A named file that does not exist. Exits 1.
    content: '{{tool}} missing.md'
    exit: 1
    expect: 'missing.md:1:1: file-read-error File not found'
  - name: bad-depth
    kind: command
    language: shell
    description: An option value out of range. Exits 1.
    content: '{{tool}} --validate --depth 7'
    exit: 1
    expect: '--depth requires an integer between 1 and 6'
  - name: unknown-topic
    kind: command
    language: shell
    description: A help topic that does not exist. Exits 2.
    content: '{{tool}} help no-such-topic'
    exit: 2
    expect: "Unknown help card 'no-such-topic'"
---
**Exit codes.** 0 means every file was processed and nothing is left to
report. 1 means a problem was reported (a lint-only finding too), an
argument was invalid, or self-validation failed. `--silent` hides the
output but not the exit code. The `help` command exits 2 for an unknown
topic ([[#unknown-topic]]). `Unexpected error: ...` is a defect of the
tool; please report it.

**Failures.** These identifiers label problems with a file or a
configuration rather than with the Markdown. No setting or comment can
switch them off:

- `file-read-error`: the file does not exist or cannot be read
  ([[#missing-file]]).
- `file-encoding-error`: the file is not UTF-8, or has a UTF-16 or UTF-32
  byte order mark.
- `file-line-ending-error`: a carriage return is not part of a CRLF.
- `file-parse-error`: the Markdown parser failed, or inline markup is
  nested too deeply.
- `file-format-error`: formatting failed; this is a defect of the tool,
  please report it.
- `file-write-error`: `--fix` could not write the file, for example
  because it is read-only or changed after it was read. It stays as it
  was.
- `config-error`: a `.lintmark.yaml` or `.editorconfig` problem; see
  [[configuration]].

**Usage errors.** An unknown option ([[#unknown-option]]), a missing or
invalid option value ([[#bad-depth]]), or a rejected combination prints
`Error: ...` and processes nothing. These errors, and a log file that
cannot be opened, are found before `--silent` and `--log` take effect,
so they are always printed and never logged ([[#silent-error]]).

**Common causes.**

- A file name that starts with `-` is taken for an option; write
  `./-name.md`.
- `--log` replaces its file when the run starts, so never name one of
  your Markdown files as the log file.
- `--enable` and `--disable` accept only the 27 rules that can be
  switched; see [[rules]].
- A finding that `--fix` leaves is usually a lint-only rule: change the
  text, switch the rule off, or use a suppression comment.
