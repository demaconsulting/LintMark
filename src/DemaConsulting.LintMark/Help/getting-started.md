---
title: Getting started
summary: Install the tool, check a Markdown file, preview the fixes, and apply them.
aliases:
  - start
  - quickstart
  - examples
  - install
tags:
  - tutorial
order: 1
see-also:
  - command-line
  - configuration
  - troubleshooting
examples:
  - name: install-local
    kind: snippet
    language: shell
    save-as: install-local.sh
    description: Add the tool to the tool manifest of a repository.
    content: |
      dotnet new tool-manifest
      dotnet tool install --local DemaConsulting.LintMark
  - name: run-in-ci
    kind: snippet
    language: shell
    save-as: run-in-ci.sh
    description: Restore the manifest tools in CI, then run the tool.
    content: |
      dotnet tool restore
      dotnet {{tool}} --version
  - name: notes
    kind: source
    language: markdown
    save-as: notes.markdown
    description: A Markdown file with a list that needs fixing.
    content: |
      # Notes

      Some text
      * one
      * two
  - name: version
    kind: command
    language: shell
    description: Print the version and exit.
    content: '{{tool}} --version'
  - name: check
    kind: command
    language: shell
    description: Report what differs from the canonical form. Exits 1.
    content: '{{tool}} notes.markdown'
    exit: 1
    expect: |
      notes.markdown:4:1: bullet
      notes.markdown:4:1: list-blank-lines
  - name: diff
    kind: command
    language: shell
    description: Preview the fixes as a unified diff. Exits 1.
    content: '{{tool}} --diff notes.markdown'
    exit: 1
    expect: '+- one'
  - name: fix
    kind: command
    language: shell
    description: Rewrite the file. Exits 0 when nothing remains.
    content: '{{tool}} --fix notes.markdown'
  - name: topic
    kind: command
    language: shell
    description: Print a help topic, here the list of options.
    content: '{{tool}} help command-line'
    expect: '--fix'
---
{{tool}} {{version}} is a .NET tool.
It checks Markdown files against a canonical form and rewrites them on request.

1. **Install it.** The tool needs the .NET 10 SDK or later. In CI, set it
   up first when the runner has an older .NET, for example with
   `actions/setup-dotnet` and `dotnet-version: 10.x`.
   For a repository or CI, install it as a local tool. Run the commands
   that [[#install-local]] prints once, without
   `dotnet new tool-manifest` when `.config/dotnet-tools.json` exists, and
   commit that file. In CI, run `dotnet tool restore` first, then run the
   tool as `dotnet {{tool}}`, as in the commands that
   [[#run-in-ci]] prints. For one machine, install it globally and
   run it as `{{tool}}`:

   ```bash
   dotnet tool install -g DemaConsulting.LintMark
   ```

2. **Check it.** The command that [[#version]] prints shows the version.
3. **Check a file.** Save the file that [[#notes]] prints as
   `notes.markdown`. The command from [[#check]] reports each place that
   differs, as `path:line:col: rule-id message`, and exits 1. Without
   paths, the tool checks every `*.md` file below the current folder; a
   file that you name is checked whatever its extension.
4. **Preview and fix.** The command from [[#diff]] prints what `--fix`
   would change and writes nothing. The command from [[#fix]] rewrites
   the file, reports what remains, and exits 0 when nothing does.

[[command-line]] lists every option, and
[[configuration]] shows the `.lintmark.yaml` file that changes the
settings. Print any help topic with `{{tool}} help <topic>`, as in the
command that [[#topic]] prints. Add `--search <words>` to find a topic,
and `--json` to get any help answer as JSON.
