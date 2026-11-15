# Formatting Rules

LintMark checks and rewrites Markdown files with a fixed set of formatting rules. Each rule describes one way in
which a file differs from its canonical form, and every rule has a stable identifier. Most rules also have a legacy
number from markdownlint (for example `MD009`) that you can use in a suppression comment.

There are 26 formatting rules that are always applied, and three formatting rules that a configuration can switch off
(`line-length`, `paragraph-wrap`, and `link-refactor`). The rules that only report a problem, without changing the
file, are described in *Lint Rules*. This page describes the rules that rewrite; the layout of paragraphs, tables, and
links has pages of its own: *Wrapping Paragraphs* and *Tables and Links*.

## How the Rules Work

- **Check**: `lintmark docs` reports every place that a rule would change, as `path:line:col: rule-id message`, and
  exits with `1` when it reports anything.
- **Fix**: `lintmark --fix docs` makes the changes, writes the files that change, and reports what remains. A second
  run over the same files reports nothing and changes nothing.
- **Diff**: `lintmark --diff docs` first reports the findings, as in check mode, and then prints a unified diff of what
  `--fix` would change. It writes no file, and because it reports the findings, it exits with `1` when there are any.
- **Always applied**: A formatting rule that is listed in the section *Rules and Settings at a Glance* cannot be
  switched off in the `rules` setting of the configuration: its identifier and its legacy number are unknown names
  there. Its *settings* (such as `code-fence`) choose the form that it writes. To leave a passage alone, use a
  suppression comment (see *Suppression Comments*). The exceptions are `line-length` (`MD013`), `paragraph-wrap`, and
  `link-refactor` (`MD054`), which `rules` and the options `--enable` and `--disable` can switch on and off.
- **Content is never changed**: Code (fenced, indented, and inline), HTML, math, front matter, and grid tables keep
  their content, including whitespace and blank lines. Links keep their text and destination. Text that LintMark
  cannot read with certainty is left as it is, and so is a rewrite that could change what the document means or how
  it renders: such a conversion is simply not made. LintMark renders the document before and after the change and
  leaves a place unchanged when the result would differ.
- **Containers**: Blocks inside a quote or list are changed in place, but no blank line is added or removed there,
  because that could change how a list renders. A list inside a quote changes only its marker character or number,
  never its columns.

In the examples below, a dot (`·`) stands for a space and an arrow (`→`) for a tab at the end of a line or inside text,
so that the whitespace is visible. Write the real characters in a file.

## Whitespace

### line-ending

Writes every line ending of a file in one style. A file that has only one kind of line ending keeps it, unless the
configuration asks for the other kind; a file with mixed line endings is written in the style that the configuration
names, or in the style that most of its lines have. A bare carriage return (a line ending that is neither LF nor CRLF)
is not rewritten: it is reported as the failure `file-line-ending-error`.

- **Setting**: `end-of-line`: `lf` or `crlf`. There is no default value. Without the setting, the `end_of_line` property
  of the `.editorconfig` files that apply to the file (`lf` or `crlf`) decides, and without that, the majority style of
  the file. `--no-config` also ignores `.editorconfig`.
- **Alias**: none. The rule cannot be suppressed by a comment, because a line ending belongs to the whole file.
- **Example** (`end-of-line: crlf`, in a file whose lines end with LF):

  ```text
  a.md:1:1: line-ending Line endings are LF; expected CRLF
  ```

  For a file with both endings, the message is `Mixed line endings (2 CRLF, 3 LF); expected LF`.

### trailing-whitespace (MD009)

Removes spaces and tabs at the end of a line of text. A hard line break (two spaces at the end of a line) is kept, and
three or more spaces become exactly two, because that is the same break. One space is not a break and is removed.
Whitespace inside code, inline code, inline HTML, and link destinations or titles is never changed.

- **Setting**: `trailing-whitespace: strip` (the only value, and the default).
- **Before**:

  ```text
  A line with a trailing space·
  A hard break······
  keeps exactly two spaces.
  ```

- **After**:

  ```text
  A line with a trailing space
  A hard break··
  keeps exactly two spaces.
  ```

Use a backslash at the end of a line if you prefer a hard break that does not depend on trailing spaces; LintMark
leaves it as it is. The setting `hard-break` (`spaces` or `backslash`) is validated but does not convert one form into
the other: a hard break keeps the form that it has.

### hard-tabs (MD010)

Replaces each tab in the text of a paragraph or heading by spaces up to the next tab stop of four columns. A tab in
the indentation of a line, in code (also inline code), in a table, in a link destination or title, at the end of a line
(the trailing whitespace rule owns that whitespace, and spaces there could turn into a line break), and in any block
that LintMark does not rewrite stays as it is.

- **Setting**: none; the tab stops are four columns wide.
- **Before**: `a→b` and `abc→d`
- **After**: `a···b` and `abc·d`

### max-blank-lines (MD012)

Reduces a run of blank lines between blocks to the maximum, and empties a line that holds only spaces or tabs. Blank
lines inside code and HTML blocks, and inside a quote or list, are kept (their whitespace is still removed).

- **Setting**: `max-blank-lines`, a whole number from `1` to `100000`. The default is `1`.
- **Before** (default setting):

  ```text
  First paragraph.



  Second paragraph.
  ```

- **After**:

  ```text
  First paragraph.

  Second paragraph.
  ```

### final-newline (MD047)

Ends the file with exactly one line ending. A missing final line ending is added, and extra blank lines (or lines that
hold only whitespace) at the end are removed. With `final-newline: false` the file ends with its last character instead.
A file without any content is left alone, and so is the end of a file whose last block keeps its trailing blank lines
as content (a fence that is never closed, or an HTML comment or script block that is never closed, for example). The end
of a file after an HTML comment or script block that is closed is treated like any other.

- **Setting**: `final-newline`, `true` or `false`. The default is `true`.
- **Before**: `The last line.` with no line ending, or with three blank lines after it.
- **After**: `The last line.` followed by exactly one line ending.

Files that end with an extra blank line are common in generated documentation. Put such files in the `ignore` setting
(for example `**/generated/**`, which is ignored by default) or change the generator.

## Headings

### heading-style (MD003, MD021)

Writes every heading as an ATX heading (`# Title`) without closing number signs. A setext heading (text with an
underline) becomes an ATX heading of the same level, and the closing number signs of `# Title #` are removed. A heading
is left alone when the conversion could change what it means: a setext heading of several lines, one inside a quote or
list, one whose text ends with a number sign or a backslash, and one whose text starts like a Pandoc construct.

- **Setting**: `heading-style: atx` (the only value, and the default).
- **Before**:

  ```text
  Title
  =====

  Section
  -------

  ### Closed ###
  ```

- **After**:

  ```text
  # Title

  ## Section

  ### Closed
  ```

### heading-space (MD019)

Puts exactly one space between the number signs of a heading and its text. Text that starts with number signs but has
no space after them (`#Title`) is a paragraph, not a heading, and is never changed: adding the space would change how
the document renders. The lint-only rules `heading-missing-space` (`MD018`) and `heading-closed-missing-space` (`MD020`)
report such text (see *Lint Rules*).

- **Setting**: none.
- **Before**: `##····Title`
- **After**: `## Title`

### heading-start-left (MD023)

Removes the indentation of a heading that is not inside a quote or list. A heading inside a container keeps the
indentation that belongs to the container.

- **Setting**: none.
- **Before**: `···## Title`
- **After**: `## Title`

### heading-blank-lines (MD022)

Puts a blank line between a heading and the blocks around it.

- **Setting**: none.
- **Before**:

  ```text
  Some text.
  ## Title
  More text.
  ```

- **After**:

  ```text
  Some text.

  ## Title

  More text.
  ```

## Blocks

### fence-blank-lines (MD031)

Puts a blank line between a fenced code block and the blocks around it.

- **Setting**: none.
- **Before**:

  ````text
  Text before.
  ```bash
  echo hello
  ```
  Text after.
  ````

- **After**:

  ````text
  Text before.

  ```bash
  echo hello
  ```

  Text after.
  ````

### list-blank-lines (MD032)

Puts a blank line between a list and the blocks around it. Text that only looks like the start of a list, and a line
that continues a list item without being indented, are not separated, because the Markdown parser reads them as part of
the same block.

- **Setting**: none.
- **Before**:

  ```text
  Some text.
  - first
  - second
  > A quote.
  ```

- **After**:

  ```text
  Some text.

  - first
  - second

  > A quote.
  ```

  A line of text directly after the last item, such as `More text.`, is a lazy continuation of that item, so it is
  part of the list and no blank line is added before it. Write the blank line yourself when the next paragraph should
  be separate.

### table-blank-lines (MD058)

Puts a blank line between a pipe table and the blocks around it. LintMark does not change the cells of a table.

- **Setting**: none.
- **Before**:

  ```text
  Text before.
  | a | b |
  |---|---|
  | 1 | 2 |
  # Heading
  ```

- **After**:

  ```text
  Text before.

  | a | b |
  |---|---|
  | 1 | 2 |

  # Heading
  ```

### thematic-break (MD035)

Writes every thematic break as three characters of the same kind. A break written as `***`, `___`, `- - -`, or
`-----` becomes `---` by default. A break is left as it is where the dashes form would change the meaning of the
document: directly under a line of text (it would underline the text and make a heading), on the first line of a file or
after a line of dashes that could start or close front matter, after a list marker of the same character, and after a
plus sign list marker (`+ ---` looks like the border of a grid table). Trailing
whitespace is removed in every case.

- **Setting**: `thematic-break`: `dashes` (`---`), `asterisks` (`***`), or `underscores` (`___`). The default is
  `dashes`.
- **Before**:

  ```text
  Text.

  ***

  More text.
  ```

- **After**:

  ```text
  Text.

  ---

  More text.
  ```

### code-block-style (MD046)

Writes a code block that is indented by four spaces as a fenced code block. The content moves four columns to the left
and is otherwise unchanged. An indented block inside a quote or list stays as it is, because its indentation also puts
it in the container.

- **Setting**: `code-block-style: fenced` (the only value, and the default).
- **Before**:

  ```text
  Run this command:

      dotnet build
  ```

- **After**:

  ````text
  Run this command:

  ```
  dotnet build
  ```
  ````

### code-fence (MD048)

Writes the fence of a fenced code block with the configured character: backticks (`` ``` ``) by default, or tildes
(`~~~`). The new fence is as long as the old one, or longer when a line of the code could close it. A fence is left as
it is when the conversion would change the block: a backtick fence cannot have a backtick in its info string, and a
fence whose info string starts with a brace (Pandoc attributes, such as `{=html}`) keeps its character.

- **Setting**: `code-fence`: `backtick` or `tilde`. The default is `backtick`.
- **Before** (default setting):

  ````text
  ~~~python
  print("hello")
  ~~~
  ````

- **After**:

  ````text
  ```python
  print("hello")
  ```
  ````

## Lists and Block Quotes

These rules change the markers, numbers, and spacing of lists and block quotes. They change nothing else in an item:
the text of an item or quote is only touched by the rules for inline text (see *Inline Text*) and by the rules of the
other sections. In every rule below the following places are left as they are:

- A lazy continuation line (a line of text that continues an item or quote without its indentation or marker).
- Whitespace that contains a tab: the marker character or number of such a line can change, its spacing does not.
- The columns of a list in a block quote: only the marker character or number of such a list changes. A list in a
  footnote definition is not rewritten.
- Code blocks, HTML, and math.

### bullet (MD004)

Writes the marker of every item of a bullet list with the configured character. All items of a list are converted
together or not at all. Two lists that are separated only by a blank line, because their marker characters differ,
stay two lists: a conversion that would merge them into one list is not made. A list is also left alone when the new
marker would turn one of its items into a thematic break (`+ - -` would become `- - -`).

- **Setting**: `bullet`: `dash` (`-`), `asterisk` (`*`), or `plus` (`+`). The default is `dash`.
- **Before** (default setting):

  ```text
  * Apples
  * Pears

  Some text.

  - Plums
  - Figs

  + Dates
  + Nuts
  ```

- **After**:

  ```text
  - Apples
  - Pears

  Some text.

  - Plums
  - Figs

  + Dates
  + Nuts
  ```

  The list `+ Dates` directly follows the list `- Plums`; with the same marker, the two would become one list, so it
  stays. Change such neighbors by hand, or separate them with a paragraph.

### ordered-style (MD029)

Numbers the items of an ordered list in the configured style. The first number of a list and its delimiter (`.` or
`)`) are kept, whatever they are.

- **Setting**: `ordered-style`: `incrementing` or `one`. The default is `incrementing`. With `incrementing` each item
  has the number of the item before it plus one. With `one` every item repeats the number of the first item, so a list
  that starts with `3)` has `3)` on every item.
- **Before** (default setting):

  ```text
  1. one
  1. two
  1. three

  3) three
  3) four
  7) five

  0. zero
  5. five
  ```

- **After**:

  ```text
  1. one
  2. two
  3. three

  3) three
  4) four
  5) five

  0. zero
  1. five
  ```

  With `ordered-style: one` the same lists keep `1. 1. 1.`, and become `3) 3) 3)` and `0. 0.`.

### list-indent (MD005, MD007)

Aligns the items of a list and sets the indentation of nested bullet lists. The items of one list start in the same
column; the items of a top-level list start at the left margin; a bullet list inside a bullet item starts the
configured number of columns to the right of the marker of its parent; and the content of an item that moves (its
continuation lines and nested blocks) moves with it. Any other nesting, such as a list in an ordered item or an
ordered list in a bullet item, keeps its offset from the content of its parent item.

- **Setting**: `list-indent`: `2` or `4`. The default is `2`.
- **Before** (default setting):

  ```text
  ·- first
  ·- second
  ····- nested four
  ····- nested four again

  - parent
  ···- nested three
  ·····with continuation
  ```

- **After**:

  ```text
  - first
  - second
  ··- nested four
  ··- nested four again

  - parent
  ··- nested three
  ····with continuation
  ```

### list-marker-space (MD030)

Puts exactly one space between a list marker and the text of the item. An item with five or more spaces after the
marker is left alone, because its text is an indented code block.

- **Setting**: none.
- **Before**:

  ```text
  -···wide item
  1.···numbered
  ```

- **After**:

  ```text
  - wide item
  1. numbered
  ```

### blockquote-space (MD027)

Puts exactly one space between the `>` of a block quote and its text. It applies to the paragraph and heading lines of
a quote. A line with five or more spaces after the marker is left alone, because the spaces indent a code block or
continue a paragraph.

- **Setting**: none.
- **Before**:

  ```text
  >···quoted text
  >···more quoted text
  ```

- **After**:

  ```text
  > quoted text
  > more quoted text
  ```

## Inline Text

These rules change the delimiters of emphasis and strong emphasis, wrap bare URLs, and remove needless spaces inside
code spans. They work on the text of a paragraph. The text of tables and headings is not normalized, and code spans,
link and image destinations, HTML, and math keep their content. Emphasis in link text and in an image description is
written like any other emphasis
(`[_a_](url)` becomes `[*a*](url)`). Emphasis or strong emphasis that ends the last paragraph of a footnote is left as
written; everything else in a footnote is normalized. A paragraph that holds a delimiter that LintMark cannot pair with
certainty is left whole: unpaired or mixed delimiters (`_one` and `*two_`) and nested emphasis
(`_outer *inner* outer_`) are examples.

### emphasis (MD049)

Writes emphasis with the configured delimiter. Emphasis inside a word is kept as it is when the configured delimiter
is the underscore, because underscores inside a word do not form emphasis (`snake*case*word` stays).

- **Setting**: `emphasis`: `asterisk` or `underscore`. The default is `asterisk`.
- **Before** (default setting):

  ```text
  This is _emphasis_ and __strong__ text.
  ```

- **After**:

  ```text
  This is *emphasis* and **strong** text.
  ```

  With `emphasis: underscore` and `strong: underscore`, the After text becomes the Before text again. A paragraph with
  literal underscores inside a word, such as `snake_case_word`, is left whole.

### strong (MD050)

Writes strong emphasis with the configured delimiter. The example of `emphasis` shows both rules, because its line holds
both kinds of emphasis. Strong emphasis follows the same restrictions as emphasis.

- **Setting**: `strong`: `asterisk` or `underscore`. The default is `asterisk`.
- **Before** (default setting): `__strong__`
- **After**: `**strong**`

### bare-urls (MD034)

Wraps a bare URL in angle brackets, which makes it an autolink. Only addresses that start with `http://`, `https://`,
or `ftp://` are changed. A period, comma, or closing parenthesis directly after the URL stays outside the brackets.
Addresses that start with `www.`, e-mail addresses, URLs in links, URLs already in angle brackets, and code are not
changed.

- **Setting**: `bare-urls`: `autolink` or `leave`. The default is `autolink`; `leave` switches the rule off.
- **Before** (default setting):

  ```text
  See https://example.com/page. Also (http://example.com/a), or ftp://files.example.com/x, but not
  www.example.com or user@example.com.
  ```

- **After**:

  ```text
  See <https://example.com/page>. Also (<http://example.com/a>), or <ftp://files.example.com/x>, but not
  www.example.com or user@example.com.
  ```

### code-span-space (MD038)

Removes the one space on each side of a code span that is padded with exactly one space on each side. The padding is
not part of the code, and a renderer removes it anyway, so removing it does not change how the text renders.

- **Setting**: none. The rule is always applied, and it is not listed in `rules`. A suppression comment switches it
  off for a passage (`code-span-space` or `MD038`).
- **Before**:

  ```text
  Use ` code ` and ` padded ` here, but `  two  ` and ` one` and `` ` tick `` and `   ` stay.
  ```

- **After**:

  ```text
  Use `code` and `padded` here, but `  two  ` and ` one` and `` ` tick `` and `   ` stay.
  ```

- **Reported**: As `code-span-space` at the opening backtick of the first padded code span of a line, with the message
  `A code span has a space after its opening backtick and before its closing backtick that it does not need.`

The padding is needed, and so it is kept, in these cases:

- The content of the code span starts or ends with a backtick (the third kept span of the example): the space
  separates the content from the delimiter.
- The padding is on one side only, or is wider than one space (the first two kept spans of the example): removing a
  space would change the content.
- The code span consists of spaces only (the last span of the example).
- The code span continues over more than one line, or it is in a heading or in a table cell. The text of headings and
  tables is not normalized.

### Checks that only report

Two more cases of text that looks wrong are not rewritten, because removing the spaces would change how the text
renders: spaces inside emphasis markers (a literal `* text *` would become emphasis) and spaces inside link text (the
link text would lose characters). The lint-only rules `emphasis-space` (`MD037`) and `link-space` (`MD039`) report
them (see *Lint Rules*).

## Tables

These rules put a blank line around a table and write it in the configured layout. Their details, the aligned layout,
and the width handling are in *Tables and Links*. `table-blank-lines` (`MD058`) is described in the section *Blocks*.

### table-pipe-style (MD055)

Writes a pipe at the start and at the end of every line of a table.

- **Setting**: none.
- **Before**: `a | b`, `-|-`, `1 | 2` on three lines.
- **After**: `| a | b |`, `| --- | --- |`, `| 1 | 2 |`.

### table-column-style (MD060)

Writes the cells of a table in the layout of the setting `tables`: the compact layout, with one blank on each side of a
cell and a short delimiter row, or the aligned layout, with every column padded to its widest cell. The aligned layout
is used only when the table fits the line length.

- **Setting**: `tables`: `compact` or `aligned`. The default is `compact`.
- **Before**: `|---|:-:|` as the delimiter row.
- **After** (default setting): `| --- | :---: |`.

## Links

### link-refactor (MD054)

Converts an inline link whose source is wider than a threshold to a reference link, and collects the definitions at the
end of the document. It does nothing until the option `link-refactor.enabled` is `true`. The options, the labels, and
an example are in *Tables and Links*.

- **Setting**: `link-refactor`, a group of options: `enabled` (default `false`), `threshold` (`60`), `in-tables`
  (`true`), `break-link-text` (`false`), `label-max-length` (`40`), and `definitions-marker`
  (`<!-- Link References -->`).
- **Switching it off**: `rules: link-refactor: false`, or `--disable link-refactor`.
- **Before** (with `link-refactor.enabled: true`):

  ```text
  See [the guide](https://example.com/lintmark/docs/user_guide/introduction.md "Guide") now.
  ```

- **After**:

  ```text
  See [the guide][the-guide] now.

  <!-- Link References -->
  [the-guide]: https://example.com/lintmark/docs/user_guide/introduction.md "Guide"
  ```

## Paragraphs

### line-length (MD013)

Wraps a paragraph that has a line wider than the limit. The rule fills only what overflows: a paragraph is rewrapped
only when at least one of its lines is wider than `line-length`, and a paragraph that fits is never touched, however
its lines are broken. The width is measured in display columns. The details, with examples, are in *Wrapping
Paragraphs*.

- **Settings**: `line-length` is the width in display columns; the default is `120`, and `0` means unlimited, which
  switches the rule off. `wrap` selects the method: `fill` (the default) re-wraps only over-wide paragraphs, `sentence`
  starts every sentence on a new line, and `semantic` does the same and breaks a sentence that is wider than
  `line-length` only at the latest clause break (`,` `;` `:` and the dash) that fits.
- **Switching it off**: Besides `line-length: 0`, the `rules` setting switches this rule off by its identifier or its
  legacy number (`line-length: false` or `MD013: false`). A suppression comment (`line-length` or `MD013`) leaves a
  passage alone.
- **Before** (`line-length: 50`):

  ```text
  LintMark rewraps a paragraph only when a line is wider than the limit, and it keeps the words in order.
  ```

- **After**:

  ```text
  LintMark rewraps a paragraph only when a line is
  wider than the limit, and it keeps the words in
  order.
  ```

### paragraph-wrap

Lays out every paragraph the way the `sentence` or `semantic` mode of `wrap` asks: one sentence per line. In the default
`fill` mode this rule reports nothing. It has no legacy number, and it can be switched off in `rules` like
`line-length`.

- **Setting**: `wrap`: `fill`, `sentence`, or `semantic`. The default is `fill`.
- **Before** (`wrap: sentence`):

  ```text
  LintMark is a formatter. It rewrites Markdown files, e.g. a README.
  ```

- **After**:

  ```text
  LintMark is a formatter.
  It rewrites Markdown files, e.g. a README.
  ```

## Suppressing a Rule

A comment switches a rule off for a part of a file. The rules are named by identifier or by legacy number, in any letter
case, and both families of comments work: `lintmark-` and `markdownlint-`.

```markdown
<!-- lintmark-disable trailing-whitespace -->

Text that keeps its trailing whitespace.

<!-- lintmark-enable trailing-whitespace -->

<!-- markdownlint-disable-next-line MD022 -->
## A heading that touches the text after it
Text without a blank line before it.

<!-- lintmark-disable-file final-newline -->
```

All the forms of comment, the scope of each, and what cannot be suppressed are described in *Suppression Comments*.

## Rules and Settings at a Glance

| Rule identifier | Alias | Switchable in `rules` | Setting (default) |
| --- | --- | --- | --- |
| `line-length` | `MD013` | yes | `line-length: 120` |
| `paragraph-wrap` | none | yes | `wrap: fill` |
| `link-refactor` | `MD054` | yes | `link-refactor.enabled: false` |
| `line-ending` | none | no | `end-of-line` (no default) |
| `trailing-whitespace` | `MD009` | no | `trailing-whitespace: strip` |
| `hard-tabs` | `MD010` | no | none |
| `max-blank-lines` | `MD012` | no | `max-blank-lines: 1` |
| `heading-style` | `MD003`, `MD021` | no | `heading-style: atx` |
| `heading-space` | `MD019` | no | none |
| `heading-start-left` | `MD023` | no | none |
| `heading-blank-lines` | `MD022` | no | none |
| `fence-blank-lines` | `MD031` | no | none |
| `list-blank-lines` | `MD032` | no | none |
| `table-blank-lines` | `MD058` | no | none |
| `table-pipe-style` | `MD055` | no | none |
| `table-column-style` | `MD060` | no | `tables: compact` |
| `thematic-break` | `MD035` | no | `thematic-break: dashes` |
| `code-block-style` | `MD046` | no | `code-block-style: fenced` |
| `code-fence` | `MD048` | no | `code-fence: backtick` |
| `final-newline` | `MD047` | no | `final-newline: true` |
| `bullet` | `MD004` | no | `bullet: dash` |
| `ordered-style` | `MD029` | no | `ordered-style: incrementing` |
| `list-indent` | `MD005`, `MD007` | no | `list-indent: 2` |
| `list-marker-space` | `MD030` | no | none |
| `blockquote-space` | `MD027` | no | none |
| `emphasis` | `MD049` | no | `emphasis: asterisk` |
| `strong` | `MD050` | no | `strong: asterisk` |
| `bare-urls` | `MD034` | no | `bare-urls: autolink` |
| `code-span-space` | `MD038` | no | none |

The table lists the 26 rules that are always applied, as well as the three rules that `rules` can switch off. A rule
that is not switchable is applied to every file; a setting, where one exists, chooses the form that the rule writes. A
suppression comment is the way to leave a passage alone. `line-length: 0` means that there is no limit. The setting
`hard-break` (`spaces` or `backslash`) is validated, but the rule `trailing-whitespace` does not convert one form into
the other.

The rules that report problems without rewriting (for example `heading-increment`) are described in *Lint Rules*, and
they can all be switched on and off in `rules`.
