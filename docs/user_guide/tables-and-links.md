# Tables and Links

This page describes how LintMark writes pipe tables and how it can turn long inline links into reference links. The
rules for lists, headings, and the other blocks are in *Formatting Rules*, and the rules that only report problems are
in *Lint Rules*.

In the examples, a finding is shown as `--check` prints it, without the two banner lines (version and copyright) that
LintMark prints before the findings. Every example was run with the tool and the output is quoted as it was printed.

## Tables

LintMark reads GitHub-flavored pipe tables. It changes only the blanks around the cells, the delimiter row, and the
pipes at the ends of the lines. The text of every cell is written back exactly as it is, so escapes such as `\|`, code
spans, and inline HTML survive byte for byte. The prefix of every line (block quote markers, list markers, and
indentation) is never touched, so a table in a list item or a block quote keeps its place.

### Rules and Settings of Tables

| Rule identifier | Alias | Kind | Default | Setting |
| --- | --- | --- | --- | --- |
| `table-blank-lines` | `MD058` | fixable | always applied | none |
| `table-pipe-style` | `MD055` | fixable | always applied | none |
| `table-column-style` | `MD060` | fixable | always applied | `tables: compact` |
| `table-too-wide` | none | lint-only | on | `line-length: 120` |
| `table-column-count` | `MD056` | lint-only | on | none |
| `table-grid-unformatted` | none | lint-only | off | none |
| `grid-table-invalid` | none | lint-only | on | none |

The fixable rules are always applied: the `rules` setting does not know them, and a suppression comment is the way to
leave a table alone. The lint-only rules can be switched on and off in `rules`, and with `--enable` and `--disable`.
The setting `tables` takes `compact` or `aligned`; the default is `compact`.

### Pipes at the Ends of a Line

The rule `table-pipe-style` writes a pipe at the start and at the end of every line of a table. The table of the
example has no pipes at the ends of its lines. The configuration is the default one:

- **Before**:

  ```text
  a | b
  -|-
  1 | 2
  ```

- **After**:

  ```text
  | a | b |
  | --- | --- |
  | 1 | 2 |
  ```

The findings are one per line (`table-pipe-style A line of a table lacks the pipe at its start or at its end.`), and
`--fix` writes the three lines. Text that does not form a table, for example a lone line with a pipe in it, is a
paragraph and is not changed.

### Compact and Aligned Layouts

The rule `table-column-style` writes the cells of a table in the layout that the setting `tables` asks for.

- **compact** (the default): every cell has one blank on each side, and the delimiter row is as short as possible
  (`| --- | --- |`). The layout of a row depends only on the cells of that row, so a change to one row never changes
  another row. This gives small differences in version control and few merge conflicts.
- **aligned**: every column is padded to the width of its widest cell, so the pipes line up. A change to the widest cell
  of a column changes every line of the table.

The alignment markers of the delimiter row (`:---`, `:---:`, `---:`) are kept in both layouts. The markers change how a
renderer shows a column; they do not change where LintMark puts the text of a cell, which starts at the left of the
padded cell.

The table of the first example is written as follows. The compact layout is the default:

- **Before**:

  ```text
  | Name | Qty | Note |
  |---|:-:|--:|
  | apple | 3 | red |
  | banana | 12 | yellow and long |
  ```

- **After** (compact):

  ```text
  | Name | Qty | Note |
  | --- | :---: | ---: |
  | apple | 3 | red |
  | banana | 12 | yellow and long |
  ```

- **After** (`tables: aligned`):

  ```text
  | Name   | Qty | Note            |
  | ------ | :-: | --------------: |
  | apple  | 3   | red             |
  | banana | 12  | yellow and long |
  ```

With the aligned layout, `--check` reports a finding for each line that differs from the aligned form, for example
`a.md:2:1: table-column-style A table is not written in the configured compact or aligned layout.` for the delimiter
row. A line that is already right is not reported.

### Width Handling

The width of a cell is its number of display columns, as for paragraphs (see *Wrapping Paragraphs*): a CJK character
and most emoji take two columns, so the pipes of an aligned table line up on the screen. With `tables: aligned`:

- **Before**:

  ```text
  | Name | Value |
  |:--|--:|
  | 日本語 | `a \| b` |
  | pipe \| escaped | x |
  ```

- **After**:

  ```text
  | Name            | Value    |
  | :-------------- | -------: |
  | 日本語          | `a \| b` |
  | pipe \| escaped | x        |
  ```

The aligned layout is used for a table only when every line of it, with its prefix, fits in `line-length`. When it does
not fit, the table is written compact, so that the setting `tables: aligned` never makes a line too wide. A table that
is wider than `line-length` even in the compact layout is reported by `table-too-wide`, because a pipe table row is one
line by specification and no rewrite can make it narrower (a line length of `0` switches the check off).

### Blank Lines Around a Table

The rule `table-blank-lines` puts a blank line between a table and the blocks around it. Both rules are applied
together:

- **Before**:

  ```text
  Text before.
  | a | b |
  |---|---|
  | 1 | 2 |
  | 3 |
  # Heading
  ```

- **After**:

  ```text
  Text before.

  | a | b |
  | --- | --- |
  | 1 | 2 |
  | 3 |

  # Heading
  ```

The row `| 3 |` has fewer cells than the header, and it is written with the cell that it has: a parser pads or ignores
cells without a message, and LintMark does not invent or drop content. The rule `table-column-count` reports such a row.

### What Is Left as Written

LintMark leaves a table as it is when it cannot read the table with certainty, and in these cases:

- A Pandoc grid table (a table drawn with `+`, `-`, `=`, and `|`) keeps its content, including its whitespace. The
  rule `table-grid-unformatted` can report it (it is off by default).
- A table in a footnote definition is not rewritten.
- A line of a table is split at every pipe that is not escaped, also inside a code span, a link, or an HTML attribute,
  exactly as the Markdown parser splits it, so a pipe in a code span has to be written as `\|`.

### Findings About Tables

The lint-only rules report these problems. They never change a file:

```text
table-too-wide.md:3:1: table-too-wide Table is 59 columns wide even when compact; the limit is 40
table-column-count.md:4:1: table-column-count Table row has 1 cells; the header has 2
table-grid-unformatted.md:1:1: table-grid-unformatted Grid table is left as written
grid.md:3:3: grid-table-invalid Line looks like a grid table border but the table is not valid; the file is never
rewritten
```

The last finding is one line; it is wrapped here to fit the page.

- `table-too-wide` is reported at the first line that is too wide. The width is that of the line with its prefix in the
  compact layout, whatever layout the table has now. To fix it, shorten the cells or move the content out of the table.
- `table-column-count` is reported at every row whose number of cells differs from the number of cells of the header.
  To fix it, add or remove the cells.
- `table-grid-unformatted` is reported at a grid table. It is meant for projects that want to know where the tool does
  not format.
- `grid-table-invalid` is reported at a line that looks like the border of a grid table when the table is not valid. The
  file is then read without grid tables and is never rewritten by `--fix`, because that reading could turn a valid grid
  table into text. Fix the table, or put a suppression comment for `grid-table-invalid` at the line.

## Link Refactoring

A long inline link makes a paragraph hard to read and to review. With the setting `link-refactor`, LintMark converts the
long inline links of a document to reference links and collects the definitions in one block at the end of the
document. The feature is opt-in: it does nothing until `link-refactor.enabled` is `true`.

### The Rule and Its Options

The rule is `link-refactor` (alias `MD054`, which covers the same ground in part). It is a formatting rule, so `--fix`
writes what it reports. It is on by default but acts only when the setting is enabled, and `rules: link-refactor: false`
(or `--disable link-refactor`) switches it off even then.

| Option | Values | Default | Meaning |
| --- | --- | --- | --- |
| `enabled` | `true` or `false` | `false` | Whether inline links are converted. |
| `threshold` | a whole number from `1` to `100000` | `60` | The width, in display columns, that a link has to exceed. |
| `in-tables` | `true` or `false` | `true` | Whether links in tables are converted. |
| `break-link-text` | `true` or `false` | `false` | Whether a wrap may break the text of an inline link. |
| `label-max-length` | a whole number from `1` to `100000` | `40` | The longest label that LintMark makes. |
| `definitions-marker` | an HTML comment on one line | `<!-- Link References -->` | The comment above the definitions. |

A link is converted when the width of its source is wider than `threshold`. The option `break-link-text` does not
convert anything; it lets a wrap break the text of a link (see *Wrapping Paragraphs*). The marker has to be exactly one
HTML comment on one line: it starts with `<!--`, ends with `-->`, and holds no line break.

A configuration that enables the conversion looks like this:

```yaml
link-refactor:
  enabled: true
  threshold: 40
  in-tables: true
  label-max-length: 40
  definitions-marker: "<!-- Link References -->"
```

### Which Links Are Converted

- A link in a paragraph is converted when it is written inline (`[text](destination "title")`), its whole source is
  wider than `threshold`, and its destination part is longer than the reference that replaces it. A short link such as
  `[home](https://x.io)` stays inline.
- A link in a table cell is converted only when `in-tables` is on and its row, in the compact layout, is wider than
  `line-length`, and only when the conversion of every convertible link of the row makes the row fit. Each row is judged
  on its own.
- These are left as they are: reference links, links in angle brackets, bare URLs, links in headings, code, HTML, and
  footnotes, links whose destination part is not on one line or uses a backslash or an entity, links in an image
  description, and links in a block where the rule is suppressed.

The decision depends only on the text of the link and of its row, never on the labels that happen to be free, so a
second run converts nothing more.

### What Is Written

Only the destination part `(destination "title")` is replaced, by `[label]`. The text of the link, with its brackets,
emphasis, and code, stays as it is. The definitions have one line each, sorted by label, below the marker comment, in
one block at the end of the document after one blank line. When the document already has the marker, the new
definitions are added after the last definition that follows it. Other definition blocks (such as a block of badge
definitions) and every existing definition stay as they are, and no definition is ever removed.

The configuration of this example is `link-refactor.enabled: true` and `link-refactor.threshold: 40`:

- **Before**:

  ```text
  # Links

  Read the [LintMark user guide](https://example.com/lintmark/docs/user_guide/introduction.md "Guide") first.

  See [home](https://x.io) and [the specification of CommonMark](https://spec.commonmark.org/0.31.2/#links) too.

  The [guide again](https://example.com/lintmark/docs/user_guide/introduction.md "Guide") uses the same label.
  ```

- **Findings**:

  ```text
  guide.md:3:10: link-refactor Inline link is 91 columns wide (threshold 40); use a reference link
  guide.md:5:30: link-refactor Inline link is 76 columns wide (threshold 40); use a reference link
  guide.md:7:5: link-refactor Inline link is 83 columns wide (threshold 40); use a reference link
  ```

- **After**:

  ```text
  # Links

  Read the [LintMark user guide][lintmark-user-guide] first.

  See [home](https://x.io) and [the specification of CommonMark][the-specification-of-commonmark] too.

  The [guide again][lintmark-user-guide] uses the same label.

  <!-- Link References -->
  [lintmark-user-guide]: https://example.com/lintmark/docs/user_guide/introduction.md "Guide"
  [the-specification-of-commonmark]: https://spec.commonmark.org/0.31.2/#links
  ```

### Labels

A label is the link text in lower case, with every run of characters that are not letters or digits replaced by one
hyphen, and cut to `label-max-length` at a hyphen. When nothing is left, the host and the first path segment of the
destination are used in the same way, and `link` when even that is empty. A label that is taken gets `-2`, `-3`, and so
on, the first that is free. A label counts as taken when the document has a definition with it (in any letter case) or
the text `[label]` anywhere, because a definition would turn such text into a link.

A definition that already exists is reused for a link with the same destination and title, when its label is made of
letters, digits, blanks, and simple punctuation. In the example above, the first and the third link share the label
`lintmark-user-guide`, because their destination and title are the same.

### Tables and a Custom Marker

With `line-length: 70`, `label-max-length: 12`, and `definitions-marker: "<!-- links -->"`, the row of a table that
is too wide is converted, and the label is cut at a hyphen:

- **Before**:

  ```text
  | Name | Link |
  |---|---|
  | spec | [the specification of CommonMark](https://spec.commonmark.org/0.31.2/#links) |
  | home | [home](https://x.io) |

  Read the [LintMark user guide](https://example.com/lintmark/docs/user_guide/introduction.md) first.
  ```

- **After**:

  ```text
  | Name | Link |
  | --- | --- |
  | spec | [the specification of CommonMark][the] |
  | home | [home](https://x.io) |

  Read the [LintMark user guide][lintmark] first.

  <!-- links -->
  [lintmark]: https://example.com/lintmark/docs/user_guide/introduction.md
  [the]: https://spec.commonmark.org/0.31.2/#links
  ```

The findings of this run are:

```text
b.md:2:1: table-column-style A table is not written in the configured compact or aligned layout.
b.md:3:1: table-too-wide Table is 87 columns wide even when compact; the limit is 70
b.md:3:10: link-refactor Table row is 87 columns wide (limit 70); use reference links
b.md:6:10: link-refactor Inline link is 83 columns wide (threshold 40); use a reference link
```

### Safety

The labels never collide with a label or with bracketed text that the parser does not read as a link, and the
definitions are written only where a block can be added safely. When that is not possible, nothing is converted.
LintMark renders the document before and after the change like for every other rule, and a change that would alter
the result is not made.
