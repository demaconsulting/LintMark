# Suppression Comments

A suppression comment is an HTML comment that switches rules off for a part of a file. Use it where one passage has to
stay as it is, for example a table that is laid out by hand, a heading that has to touch the text after it, or a link
that is meant to point nowhere yet. The comments are invisible in the rendered document.

A suppression comment applies to one file. To switch a rule off for a whole project or for a directory, use the `rules`
setting of the configuration instead, or `--disable` on the command line (see *Configuration* and *Usage*).

## Comment Forms

Two families of comments work, and they behave the same: `lintmark-` and `markdownlint-`. The second family lets a
file that was written for markdownlint keep its comments. The keyword that follows the family is written in lower case.

| Comment | Applies to |
| --- | --- |
| `<!-- lintmark-disable [rules] -->` | The text from the comment to the matching `enable` or to the end of the file. |
| `<!-- lintmark-enable [rules] -->` | Ends a `disable`, for the named rules or for all of them. |
| `<!-- lintmark-disable-line [rules] -->` | The line or lines that the comment occupies. |
| `<!-- lintmark-disable-next-line [rules] -->` | The line after the last line of the comment. |
| `<!-- lintmark-disable-file [rules] -->` | The whole file, wherever the comment is. |
| `<!-- lintmark-capture -->` | Remembers which rules are switched off at this point. |
| `<!-- lintmark-restore -->` | Returns to the state of the last `capture`. |
| `<!-- prettier-ignore -->` | The next block, for all rules. |
| `<!-- prettier-ignore-start -->` and `<!-- prettier-ignore-end -->` | Everything between the two comments. |

Each of the first seven forms exists with the prefix `markdownlint-` too, for example
`<!-- markdownlint-disable-next-line MD022 -->`. The rules follow the keyword, separated by blanks. A comment without
rule names applies to all rules. A keyword written in upper case, such as `LINTMARK-DISABLE`, is not a directive.

- **Regions**: `disable` and `enable` change the set of switched-off rules from the start of their comment on. A
  `disable` without names switches everything off, and a later `enable` with names switches only those rules on again;
  an `enable` without names switches everything on. A region that is never ended runs to the end of the file.
- **Capture and restore**: `capture` remembers the state of the regions, and `restore` returns to it. Pairs can be
  nested. A `restore` without a `capture` does nothing.
- **Prettier comments**: `prettier-ignore` is accepted where a comment stands alone as a block. It covers the next
  block, not the next line. A `prettier-ignore-start` without an end runs to the end of the file, and an end without a
  start is ignored. The comments of this family cannot name rules: they always apply to all of them.
- **Where a comment can stand**: A comment is read where the Markdown parser finds an HTML comment: on a line of its
  own (a block comment), or inside a paragraph (an inline comment). A comment inside a code span, a code block, or the
  front matter is text, not a directive.

## Naming Rules

A rule is named by its identifier (such as `trailing-whitespace`) or by its legacy markdownlint number (such as
`MD009`), in any letter case. Both name the same rule, so `<!-- markdownlint-disable md009 -->` and
`<!-- lintmark-disable trailing-whitespace -->` are the same comment. A rule can have more than one number: `MD003`
and `MD021` name `heading-style`, and `MD005` and `MD007` name `list-indent`. *Formatting Rules* and *Lint Rules* list
the identifiers and the numbers of every rule.

A name that is not a rule suppresses nothing, and it is not an error. This holds for a misspelled name and for the
number of a rule that LintMark does not have (`MD002`, `MD006`, `MD008`, `MD025`, `MD033`, `MD041`, `MD043`, `MD044`,
and `MD057`).

## What a Suppression Covers

The rules do not all work on the same unit of text, and a suppression follows the unit of the rule:

- **Lint-only rules** report a finding at one position. A finding is dropped when a comment covers its position. This
  works for every lint-only rule.
- **Rules that rewrite a block** (a paragraph, a heading, a list, a table, a code block, a quote, and so on) are
  suppressed for the whole block. A block is left exactly as it is when any rule that applies to it is suppressed
  anywhere inside the block: a comment on one line of a paragraph keeps the whole paragraph, and a comment on one item
  of a list keeps the whole list.
- **Rules about the space between blocks** (for example `max-blank-lines`) are suppressed for the part of the file that
  the comment covers.
- **The end of the file** (`final-newline`) is suppressed when a comment covers the last line of text, which is the
  case for `disable-file`.
- **Wrapping** has its own rules about what a comment keeps (see *What Is Exempt* in *Wrapping Paragraphs*): in the
  `fill` mode a `line-length` comment keeps a paragraph, and in the `sentence` and `semantic` modes a paragraph is kept
  by comments for both `line-length` and `paragraph-wrap`.

A suppressed rule is also silent in `--check` and `--diff`: nothing is reported and nothing is written for the passage.

## What Cannot Be Suppressed

- The rule `line-ending` has no number and ignores every comment: a line ending belongs to the whole file, not to a
  passage. Put the file in `ignore`, or set `end-of-line`, if the endings must stay as they are.
- The identifiers under which LintMark reports a failure are not rules of a document, so no comment can suppress them:
  `file-read-error`, `file-encoding-error`, `file-line-ending-error`, `file-parse-error`, `file-format-error`,
  `file-write-error`, and `config-error`.

## Examples

The following file shows several forms together. It was checked with the default configuration; the findings and the
diff are what `--diff` printed:

```markdown
# Suppression

<!-- lintmark-disable trailing-whitespace -->

Text that keeps its trailing whitespace·

<!-- lintmark-enable trailing-whitespace -->

Text that loses it.·

<!-- markdownlint-disable-next-line MD022 -->
## A heading that touches the text after it
Text without a blank line before it.

* Item written with an asterisk <!-- lintmark-disable-line bullet -->
* Another item

<!-- prettier-ignore -->
| a | b |
|---|---|
| 1 | 2 |

<!-- lintmark-capture -->
<!-- lintmark-disable emphasis -->
This stays _as written_.
<!-- lintmark-restore -->

This becomes _emphasis_.

<!-- lintmark-disable-file link-fragment -->

See [nothing](#missing).
```

In this listing, a dot (`·`) stands for a space at the end of a line. The findings are:

```text
a.md:9:20: trailing-whitespace A line ends with trailing whitespace, or a hard break is not exactly two spaces.
a.md:28:14: emphasis Emphasis is not written with the configured delimiter.
```

Only two places are reported, and `--fix` changes only those (it removes the space at the end of line 9, and writes
`*emphasis*` in line 28). The suppressed trailing space, the heading, the list, the table, and the emphasis in
the captured region are untouched, and the link to `#missing` is not reported, because the file switches `link-fragment`
off.

A comment that applies to a lint-only rule works the same way. Without the first comment below, this paragraph would be
reported by `link-fragment`:

```markdown
<!-- lintmark-disable-next-line link-fragment -->
See [the setup](#setup).
```
