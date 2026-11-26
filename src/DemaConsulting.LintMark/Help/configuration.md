---
title: Configuration file
summary: The .lintmark.yaml file, how it is found, and how the options override it.
aliases:
  - config
  - lintmark-yaml
  - settings
  - editorconfig
  - ignore
tags:
  - reference
order: 3
see-also:
  - formatting
  - rules
  - troubleshooting
examples:
  - name: notes
    kind: source
    language: markdown
    save-as: notes.markdown
    description: A list written with asterisk bullets.
    content: |
      # Notes

      * one
      * two
  - name: discovered
    kind: command
    language: shell
    description: The sample .lintmark.yaml asks for asterisk bullets.
    content: '{{tool}} notes.markdown'
  - name: no-config
    kind: command
    language: shell
    description: Ignore the file and use the defaults. Exits 1.
    content: '{{tool}} --no-config notes.markdown'
    exit: 1
    expect: 'notes.markdown:3:1: bullet'
  - name: missing
    kind: command
    language: shell
    description: A named file that does not exist. Exits 1.
    content: '{{tool}} --config missing.yaml notes.markdown'
    exit: 1
    expect: 'missing.yaml:1:1: config-error Configuration file not found'
---
Every setting has a default, so a `.lintmark.yaml` file lists only what it
changes. This sample lists every setting, with its allowed values; it sets
`line-length`, `end-of-line`, `bullet`, and `tables` away from their
defaults:

```yaml
---
# .lintmark.yaml: every setting, with its allowed values
line-length: 100              # 0 to 100000; 0 is unlimited
wrap: fill                    # fill, sentence, or semantic
end-of-line: lf               # lf or crlf
final-newline: true           # true or false
heading-style: atx            # atx only
bullet: asterisk              # dash, asterisk, or plus
list-indent: 2                # 2 or 4
ordered-style: incrementing   # incrementing or one
emphasis: asterisk            # asterisk or underscore
strong: asterisk              # asterisk or underscore
code-fence: backtick          # backtick or tilde
code-block-style: fenced      # fenced only
hard-break: spaces            # spaces or backslash
thematic-break: dashes        # dashes, asterisks, or underscores
bare-urls: autolink           # autolink or leave
tables: aligned               # compact or aligned
max-blank-lines: 1            # 1 to 100000
trailing-whitespace: strip    # strip only
link-refactor:
  enabled: false              # true or false
  threshold: 60               # 1 to 100000
  in-tables: true             # true or false
  break-link-text: false      # true or false
  label-max-length: 40        # 1 to 100000
  definitions-marker: "<!-- Link References -->"
rules:                        # rule id or MDxxx number: true or false
  heading-increment: true
  MD013: true
  descriptive-link-text: false
ignore:                       # replaces the built-in list
  - ".git/**"
  - "node_modules/**"
  - "**/.venv/**"
  - "**/generated/**"
  - "**/thirdparty/**"
  - ".agent-logs/**"
```

Every other value in the sample is its default, and `ignore` is the
built-in list; the four defaults are 120, no `end-of-line`, `dash`, and
`compact`. With this file next to the file of [[#notes]], the command that
[[#discovered]] prints reports nothing.

**Finding the file.** For each Markdown file, the tool looks for
`.lintmark.yaml` in its folder and then in the parent folders. The nearest
file wins and files are never merged. The search stops at the repository
root, the first folder with a `.git` entry.

**Overrides.** `--config <file>` uses one file for every Markdown file; the
last one given wins, and `.editorconfig` still applies. `--no-config`
ignores `.lintmark.yaml` and `.editorconfig`, as [[#no-config]] shows.

**Strict checking.** Names and words are lower case, numbers and booleans
are unquoted, and an unknown or repeated setting is an error. A problem is
reported as `path:line:col: config-error message`, at its position in the
configuration file, or at 1:1 when the file cannot be found or read
([[#missing]]). A file with an invalid configuration is never rewritten.

**Line endings.** `end-of-line` decides; without it, the `end_of_line` of
`.editorconfig` (`lf` or `crlf`); without that, the ending most lines of
the file already use.

**Ignore.** The patterns are relative to the folder of the configuration
file and replace the built-in list. They skip files that a folder, a glob,
or the default selection finds; a file named as an argument is still
processed.
