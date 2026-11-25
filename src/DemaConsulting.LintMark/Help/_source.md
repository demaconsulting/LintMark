---
name: lintmark
schema: 2
title: LintMark
summary: Built-in help of LintMark, the Markdown linter and formatter of DEMA Consulting.
---
{{tool}} checks Markdown files against a canonical form, reports what differs, and rewrites the files with
`--fix`. Options and paths drive it, and an optional `.lintmark.yaml` file changes the settings.

The only command is `{{tool}} help`, which must be the first argument and takes topic names, such as
`{{tool}} help command-line`.
