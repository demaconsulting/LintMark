---
title: Tables and links
summary: How pipe tables are laid out, the table checks, and the link-refactor option.
aliases:
  - tables
  - links
  - link-refactor
  - grid-tables
tags:
  - reference
order: 6
see-also:
  - formatting
  - rules
  - configuration
examples:
  - name: table
    kind: source
    language: markdown
    save-as: table.markdown
    description: A pipe table without blanks around its cells.
    content: |
      # Table

      |a|b|
      |-|-|
      |1|2|
  - name: links
    kind: source
    language: markdown
    save-as: links.markdown
    description: An inline link that is wider than 60 columns.
    content: |
      # Links

      Read [the guide](https://example.com/lintmark/docs/user_guide/usage.md) first.
  - name: check
    kind: command
    language: shell
    description: Report the table layout. Exits 1.
    content: '{{tool}} table.markdown'
    exit: 1
    expect: 'table.markdown:3:1: table-column-style'
  - name: fix
    kind: command
    language: shell
    description: Write the table in the compact layout.
    content: '{{tool}} --fix table.markdown'
  - name: refactor
    kind: command
    language: shell
    description: Preview the conversion to a reference link. Exits 1.
    content: '{{tool}} --diff --enable link-refactor links.markdown'
    exit: 1
    expect: |
      links.markdown:3:6: link-refactor
      +<!-- Link References -->
---
**Tables.** {{tool}} writes a pipe table with a pipe at both ends of every
line (`table-pipe-style`) and a blank line before and after it
(`table-blank-lines`). The text of every cell is kept byte for byte. The
setting `tables` chooses the layout (`table-column-style`):

- `compact`, the default: one blank on each side of a cell and a short
  delimiter row, `| --- |`. A change to one row never changes another.
- `aligned`: every column padded to its widest cell. A table that would be
  wider than `line-length` that way is written compact.

The command that [[#check]] prints reports the table of
[[#table]], and the command from
[[#fix]] writes it as `| a | b |`. Alignment markers such
as `:---:` are kept. Four lint-only rules report what no rewrite fixes:

- `table-too-wide`: a table is wider than `line-length` even when compact.
- `table-column-count` (`MD056`): a row has another number of cells than
  the header.
- `grid-table-invalid`: a line looks like a grid table border but the
  table is not valid; the file is then never rewritten.
- `table-grid-unformatted`: a grid table is left as written. It is off
  by default.

Grid tables and tables in footnotes are left as written.

**Link refactoring.** The `link-refactor` setting converts inline links
whose source is wider than `threshold` (default 60) to reference links. It
is off until `link-refactor.enabled` is `true` in the configuration, or
`--enable link-refactor` is given for one run, as
[[#refactor]] shows.
Only the destination part is replaced, by a label of at most
`label-max-length` (default 40) characters made from the link text. The
definitions are sorted by label in one block at the end of the document,
below the `definitions-marker` comment (default
`<!-- Link References -->`). `in-tables` (default `true`) also converts
links in a table row that is wider than `line-length`. Existing
definitions are kept, and a second run converts nothing more.
