---
title: Suppression comments
summary: HTML comments that switch rules off for a part of a file.
aliases:
  - suppress
  - lintmark-disable
  - markdownlint-disable
  - prettier-ignore
  - comments
tags:
  - reference
order: 8
see-also:
  - rules
  - formatting
examples:
  - name: kept
    kind: source
    language: markdown
    save-as: kept.markdown
    description: A deep heading and an asterisk list that are kept.
    content: |
      # Kept

      <!-- lintmark-disable heading-increment -->

      ### Deep heading

      <!-- lintmark-enable heading-increment -->

      <!-- markdownlint-disable MD004 -->

      * kept as written
      * with asterisks

      <!-- markdownlint-enable MD004 -->
  - name: check
    kind: command
    language: shell
    description: The suppressed passages report nothing.
    content: '{{tool}} kept.markdown'
  - name: fix
    kind: command
    language: shell
    description: The list keeps its asterisks.
    content: '{{tool}} --fix kept.markdown'
---
A suppression comment is an HTML comment that switches rules off for a
part of one file. For a whole project, use the `rules` setting or
`--disable` instead; see [[rules]].

Two families behave the same: `lintmark-` and `markdownlint-`, so a file
written for markdownlint keeps its comments. The keyword is lower case:

- `<!-- lintmark-disable [rules] -->` from here to the matching `enable`,
  or to the end of the file; `<!-- lintmark-enable [rules] -->` ends it.
- `<!-- lintmark-disable-line [rules] -->` the line of the comment.
- `<!-- lintmark-disable-next-line [rules] -->` the line after it.
- `<!-- lintmark-disable-file [rules] -->` the whole file.
- `<!-- lintmark-capture -->` and `<!-- lintmark-restore -->` remember
  and restore which rules are off.
- `<!-- prettier-ignore -->` the next block, for all rules;
  `<!-- prettier-ignore-start -->` to `<!-- prettier-ignore-end -->`
  everything between them.

Rules are named by identifier or `MDxxx` number, separated by blanks, in
any letter case. A comment without names applies to every rule, and a
name that is not a rule suppresses nothing. The file of [[#kept]] reports
nothing ([[#check]]), and `--fix` keeps its list as written
([[#fix]]).

**What a comment covers.** A lint-only finding is dropped when a comment
covers its position. A rule that rewrites a block, such as a list or a
paragraph, is suppressed for the whole block when a comment covers any
part of it. A suppressed rule is silent in `--check` and `--diff` too.

**What cannot be suppressed.** `line-ending`, because a line ending
belongs to the whole file, and the identifiers that label failures, such
as `file-read-error` and `config-error`.
