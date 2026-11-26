---
title: Formatting rules
summary: The 26 rules that always apply, the settings that choose their style, and --fix.
aliases:
  - format
  - fix
  - formatting-rules
  - canonical-form
  - style
tags:
  - reference
order: 4
see-also:
  - wrapping
  - tables-and-links
  - suppression
examples:
  - name: style
    kind: source
    language: markdown
    save-as: style.markdown
    description: Emphasis, a break, and a fence in non-default styles.
    content: |
      # Style

      This is _emphasis_ and __strong__ text.

      ***

      ~~~text
      code
      ~~~
  - name: check
    kind: command
    language: shell
    description: Report each place that --fix would change. Exits 1.
    content: '{{tool}} style.markdown'
    exit: 1
    expect: |
      style.markdown:3:9: emphasis
      style.markdown:3:24: strong
      style.markdown:5:1: thematic-break
      style.markdown:7:1: code-fence
  - name: fix
    kind: command
    language: shell
    description: Rewrite the file into canonical form.
    content: '{{tool}} --fix style.markdown'
---
A formatting rule describes one way in which a file differs from its
canonical form. A check reports each place that `--fix` would change, as
`path:line:col: rule-id description`, as [[#check]] shows. `--fix`
rewrites it, as [[#fix]] shows; with the default settings the file of
[[#style]] gets `*emphasis*`, `**strong**`, `---`, and backtick fences.

These 26 rules always apply: the `rules` setting and `--enable` and
`--disable` cannot switch them. Where a setting chooses the style that a
rule writes, it is named in parentheses; see [[configuration]].

- **Whitespace:** `line-ending` (`end-of-line`), `trailing-whitespace`,
  `hard-tabs`, `max-blank-lines` (`max-blank-lines`), `final-newline`
  (`final-newline`).
- **Headings:** `heading-style`, `heading-space`, `heading-start-left`,
  `heading-blank-lines`.
- **Blocks:** `fence-blank-lines`, `list-blank-lines`,
  `table-blank-lines`, `thematic-break` (`thematic-break`),
  `code-block-style`, `code-fence` (`code-fence`).
- **Lists and quotes:** `bullet` (`bullet`), `ordered-style`
  (`ordered-style`), `list-indent` (`list-indent`), `list-marker-space`,
  `blockquote-space`.
- **Inline text:** `emphasis` (`emphasis`), `strong` (`strong`),
  `bare-urls` (`bare-urls`), `code-span-space`.
- **Tables:** `table-pipe-style`, `table-column-style` (`tables`); see
  [[tables-and-links]].

Three more formatting rules can be switched: `line-length` and
`paragraph-wrap` (see [[wrapping]]) and `link-refactor`.

**What --fix writes.** Only files that change are written, atomically,
and a byte order mark is kept. Code, HTML, math, front matter, and grid
tables keep their content, and a change that would alter how the
document renders is not made. A file is never rewritten when its
configuration is invalid, or when a line looks like a grid table border
but the table is not valid.

**Leaving a passage alone.** Only a suppression comment switches one of
these rules off, for a part of a file; see [[suppression]].
`line-ending` cannot be suppressed.
