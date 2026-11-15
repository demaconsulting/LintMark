# Lint Rules

A lint rule finds a problem that formatting cannot fix. It only reports: it never changes a file, because every
repair would change what the document says or how it reads. This page describes the 24 lint-only rules. The 26 rules
that always rewrite are in *Formatting Rules*; the three configurable formatting rules (`line-length`,
`paragraph-wrap`, and `link-refactor`) are in *Wrapping Paragraphs* and *Tables and Links*.

## How Lint Rules Work

- **Output**: A finding is printed as `path:line:col: rule-id message`, in the order of line, column, rule, and
  message. LintMark exits with `1` when any finding is reported, also when `--fix` was used, because `--fix` does not
  repair a lint-only finding.
- **Switching on and off**: Every lint rule is in the `rules` setting of a configuration, by identifier or by legacy
  number, with `true` or `false`. The command line options `--enable` and `--disable` override the configuration for one
  run. Three rules are off by default: `descriptive-link-text`, `table-grid-unformatted`, and
  `dollar-sign-in-shell-code`.
- **Suppressing**: A suppression comment drops the findings that it covers (see *Suppression Comments*).
- **Names**: Each rule has an identifier. Most have a legacy markdownlint number (such as `MD013`) that works in the
  same places.

```yaml
rules:
  descriptive-link-text: true
  MD001: false
```

In the examples, a finding is shown as `--check` prints it, without the two banner lines (version and copyright) that
LintMark prints before the findings. Every example was run with the tool. The off-by-default rules were switched on with
`--enable`.

## All Lint-Only Rules

| Rule identifier | Alias | Default | What it reports |
| --- | --- | --- | --- |
| `heading-increment` | `MD001` | on | A heading level skips a level. |
| `heading-duplicate` | `MD024` | on | Two headings in the same document have the same text. |
| `heading-trailing-punctuation` | `MD026` | on | A heading ends with punctuation. |
| `emphasis-as-heading` | `MD036` | on | Emphasis is used where a heading is meant. |
| `heading-missing-space` | `MD018` | on | A line starts with number signs but has no space after them. |
| `heading-closed-missing-space` | `MD020` | on | A closed heading has no spaces inside its number signs. |
| `image-alt-text` | `MD045` | on | An image has no alternative text. |
| `link-empty` | `MD042` | on | A link has no destination. |
| `link-fragment` | `MD051` | on | A link fragment does not match any heading. |
| `link-reference-undefined` | `MD052` | on | A reference link uses a label that is not defined. |
| `link-definition-unused` | `MD053` | on | A link reference definition is never used. |
| `link-reversed` | `MD011` | on | A link is written with the brackets and parentheses reversed. |
| `link-space` | `MD039` | on | The text of a link starts or ends with a space. |
| `descriptive-link-text` | `MD059` | off | A link text is not descriptive. |
| `code-fence-language` | `MD040` | on | A fenced code block has no language. |
| `table-too-wide` | none | on | A table is wider than the configured line length. |
| `table-column-count` | `MD056` | on | A table row has a different number of cells than the header. |
| `table-grid-unformatted` | none | off | A grid table is left as written. |
| `fence-unclosed` | none | on | A fenced code block is never closed. |
| `grid-table-invalid` | none | on | A line looks like a grid table border but the table is not valid. |
| `footnote-unused` | none | on | A footnote definition is never referenced. |
| `blockquote-blank-line` | `MD028` | on | A blank line separates two block quotes. |
| `dollar-sign-in-shell-code` | `MD014` | off | A shell command in a code block starts with a dollar sign. |
| `emphasis-space` | `MD037` | on | Emphasis has a space inside one of its delimiters but not the other. |

## Headings

### heading-increment (MD001)

Reports a heading whose level is more than one deeper than the level of the heading before it. The first heading of a
file is never reported, and a heading may go any number of levels up. The finding is at the start of the heading.

- **Fix**: Use the next level, or add the heading in between.
- **Example**:

  ```text
  # Title

  ### Skipped a level
  ```

  ```text
  heading-increment.md:3:1: heading-increment Heading level increases from 1 to 3; expected at most 2
  ```

### heading-duplicate (MD024)

Reports a heading whose text is the text of an earlier heading of the same file. The text is compared with its blanks
collapsed and its case kept, and every heading counts, whatever its level. The finding is at the later heading.

- **Fix**: Give the heading a text of its own, or switch the rule off for a file that repeats headings on purpose.
- **Example**:

  ```text
  # Guide

  ## Setup

  ## Setup
  ```

  ```text
  heading-duplicate.md:5:1: heading-duplicate Another heading has the text 'Setup'
  ```

### heading-trailing-punctuation (MD026)

Reports a heading that ends with one of `.`, `,`, `;`, `:`, `!`, or their full-width forms. A question mark is allowed,
and a character reference such as `&copy;` is not punctuation.

- **Fix**: Remove the punctuation.
- **Example**:

  ```text
  # Getting started.

  ## Why use it?
  ```

  ```text
  heading-trailing-punctuation.md:1:1: heading-trailing-punctuation Heading ends with the punctuation '.'
  ```

### emphasis-as-heading (MD036)

Reports a paragraph at the top level of the document that is one line of emphasis and nothing else, and does not end
with punctuation, where a heading is probably meant. A paragraph with a link or code in it, one with more than one line,
and one inside a list or a quote is not reported.

- **Fix**: Write the line as a heading, or end it with punctuation if it is a sentence.
- **Example**:

  ```text
  # Guide

  **Installation**

  Run the installer.
  ```

  ```text
  emphasis-as-heading.md:3:1: emphasis-as-heading Emphasis 'Installation' is used instead of a heading
  ```

### heading-missing-space (MD018)

Reports the first line of a paragraph that starts, at the start of a line, with one to six number signs directly
followed by a letter. Such a line is a paragraph, not a heading, and the formatter does not add the space, because that
would change what the document shows. After a list marker or a quote marker the line is left alone (it can be an issue
number or a tag).

- **Fix**: Put a space after the number signs, or escape the first number sign with a backslash if the text is meant.
- **Example**:

  ```text
  # Guide

  ##Setup

  Text.
  ```

  ```text
  heading-missing-space.md:3:1: heading-missing-space Heading has no space after its number signs, so it is a paragraph
  ```

### heading-closed-missing-space (MD020)

Reports the same kind of line when it also ends with number signs that follow a non-blank character, like
`##Setup##`. This rule takes the place of `heading-missing-space` for such a line.

- **Fix**: Put a space after the opening and before the closing number signs, or write an ordinary heading.
- **Example**:

  ```text
  # Guide

  ##Setup##

  Text.
  ```

  ```text
  heading-closed-missing-space.md:3:1: heading-closed-missing-space Closed heading has no space inside its number
  signs, so it is a paragraph
  ```

  The finding is one line; it is wrapped here to fit the page.

## Links and Images

### image-alt-text (MD045)

Reports an image whose alternative text is empty or blank, and an HTML `img` tag without an `alt` attribute.

- **Fix**: Describe the image: `![The logo](logo.png)`.
- **Example**:

  ```text
  An image without text: ![](logo.png) and one with text: ![The logo](logo.png).

  <img src="logo.png">
  ```

  ```text
  image-alt-text.md:1:24: image-alt-text Image has no alternative text
  image-alt-text.md:3:1: image-alt-text Image has no alt attribute
  ```

### link-empty (MD042)

Reports a link whose destination is empty or only `#`.

- **Fix**: Add the destination, or remove the brackets until the target exists.
- **Example**:

  ```text
  A link without a destination: [the guide]() and [another](#).
  ```

  ```text
  link-empty.md:1:31: link-empty Link has no destination
  link-empty.md:1:49: link-empty Link has no destination
  ```

### link-fragment (MD051)

Reports a link to `#fragment` that no heading, explicit identifier (`{#id}`), or HTML `id` or `name` attribute of the
file provides. The headings are turned into fragments the way GitHub does it, with `-1` suffixes for repeats. The
fragments `#top` and line anchors such as `#L10` are always accepted, and the comparison ignores letter case.

- **Fix**: Correct the fragment, or add the heading or the anchor.
- **Example**:

  ```text
  # Install the Tool

  See [the setup](#setup) and [the installation](#install-the-tool).
  ```

  ```text
  link-fragment.md:3:5: link-fragment Link fragment '#setup' matches no heading or anchor
  ```

### link-reference-undefined (MD052)

Reports `[text][label]` and `[label][]` when the label has no definition. A shortcut reference (`[label]`) and a
footnote reference are not reported.

- **Fix**: Add the definition, or correct the label.
- **Example**:

  ```text
  See [the guide][guide] and [the other guide][other].

  [guide]: https://example.com/guide
  ```

  ```text
  link-reference-undefined.md:1:28: link-reference-undefined Reference link uses the label 'other', which has no definition
  ```

### link-definition-unused (MD053)

Reports a link reference definition that no link or image uses, and a label that is defined twice. The comment
convention `[//]: # (text)` is ignored. LintMark never deletes a definition.

- **Fix**: Remove the definition, or use it.
- **Example**:

  ```text
  See [the guide][guide].

  [guide]: https://example.com/guide
  [unused]: https://example.com/unused
  [guide]: https://example.com/other
  ```

  ```text
  link-definition-unused.md:4:1: link-definition-unused Link reference definition 'unused' is never used
  link-definition-unused.md:5:1: link-definition-unused Link reference definition 'guide' repeats the one at line 3
  ```

### link-reversed (MD011)

Reports `(text)[url]` where `[text](url)` was meant.

- **Fix**: Swap the brackets and the parentheses. The message shows the corrected link.
- **Example**:

  ```text
  Read (the guide)[https://example.com/guide] first.
  ```

  ```text
  link-reversed.md:1:6: link-reversed Link is written backwards; use [the guide](https://example.com/guide)
  ```

### link-space (MD039)

Reports an inline, reference, or collapsed link whose text starts or ends with a space or a tab. The message shows the
corrected text. An image, a shortcut reference, and a break at the edge of the text (a wrapped line) are not reported.
The formatter does not remove the space, because removing it would change the link text.

- **Fix**: Remove the space inside the brackets.
- **Example**:

  ```text
  Read [ the guide](https://example.com/guide) and [the tool ](https://example.com/tool).
  ```

  ```text
  link-space.md:1:6: link-space Link text has a space at its start; use [the guide] instead of [ the guide]
  link-space.md:1:50: link-space Link text has a space at its end; use [the tool] instead of [the tool ]
  ```

### descriptive-link-text (MD059)

Off by default. Reports a link whose text is only `click here`, `here`, `link`, or `more`, compared after lower-casing
and removing punctuation. Such a text says nothing about the destination, which is a problem for readers who scan the
links of a page.

- **Fix**: Write what the destination is: `[the user guide](...)`.
- **Example** (run with `--enable descriptive-link-text`):

  ```text
  Click [here](https://example.com/guide) or [more](https://example.com/more).
  ```

  ```text
  descriptive-link-text.md:1:7: descriptive-link-text Link text 'here' does not describe the destination
  descriptive-link-text.md:1:44: descriptive-link-text Link text 'more' does not describe the destination
  ```

## Code, Quotes, Emphasis, and Footnotes

### code-fence-language (MD040)

Reports a fenced code block without an info string.

- **Fix**: Name the language, or use `text` for output that is not code.
- **Example**:

  ````text
  ```
  echo hello
  ```
  ````

  ```text
  code-fence-language.md:1:1: code-fence-language Fenced code block has no language
  ```

### fence-unclosed

Reports a fenced code block or math block that is never closed. The parser then reads everything up to the end of the
container as code.

- **Fix**: Add the closing fence.
- **Example**:

  ````text
  ```bash
  echo hello

  Text that the parser reads as code.
  ````

  ```text
  fence-unclosed.md:1:1: fence-unclosed Fenced code block is never closed
  ```

### blockquote-blank-line (MD028)

Reports a blank line between two block quotes of the same container. A reader sees two quotes, and some renderers join
them into one.

- **Fix**: Write a `>` on the blank line to make one quote, or put text between the quotes.
- **Example**:

  ```text
  > The first quote.

  > The second quote.
  ```

  ```text
  blockquote-blank-line.md:2:1: blockquote-blank-line Blank line separates two block quotes
  ```

### dollar-sign-in-shell-code (MD014)

Off by default. Reports a code block in which every non-blank line starts with a dollar sign and a blank, because a
reader who copies the commands copies the prompt too. The finding is at the first line of the code.

- **Fix**: Remove the dollar signs, or add the output of the commands to the block.
- **Example** (run with `--enable dollar-sign-in-shell-code`):

  ````text
  ```bash
  $ dotnet build
  $ dotnet test
  ```
  ````

  ```text
  dollar-sign-in-shell-code.md:2:1: dollar-sign-in-shell-code Every command in the code block starts with a dollar sign
  ```

### emphasis-space (MD037)

Reports emphasis whose delimiters have a space inside on one side only, on one line: `*text *`, `* text*`, and the same
with `_`, `**`, and `__`. The rule reads the delimiters that the parser left as plain text, so a delimiter that is
escaped, in a code span, in a link destination, or in HTML is never reported. It does not report a space on both sides
(`* text *`) or a literal asterisk between words (`a * b * c`), and it needs a letter next to the delimiter on its
flanking side and a word character at the other end, which keeps wildcards and arithmetic out of the reports. The
formatter does not remove the space, because a literal `* text *` would become emphasis.

- **Fix**: Remove the space inside the delimiter. The message shows the corrected text.
- **Example**:

  ```text
  This is *wrong * here.

  This is (* wrong*) here.
  ```

  ```text
  emphasis.md:1:9: emphasis-space Emphasis has a space inside one of its delimiters; use *wrong* instead of *wrong *
  emphasis.md:3:10: emphasis-space Emphasis has a space inside one of its delimiters; use *wrong* instead of * wrong*
  ```

  The text `This is * wrong* here.` is not reported, because an asterisk with a space on both of its sides is read as a
  literal asterisk, not as the start of emphasis.

### footnote-unused

Reports a footnote definition that no reference uses. The parser drops such a definition from the tree, so no other rule
sees it. The finding is at the definition.

- **Fix**: Add the reference `[^2]` to the text, or remove the definition.
- **Example**:

  ```text
  A sentence with a note[^1] and a definition that is not used.

  [^1]: Used note.
  [^2]: Unused note.
  ```

  ```text
  footnote-unused.md:4:1: footnote-unused Footnote '[^2]' is defined but never referenced
  ```

## Tables

The rules for tables are described with the table formatting in *Tables and Links*. They are listed here with an
example finding each.

### table-too-wide

Reports the first line of a pipe table that is wider than `line-length` when the table is written compact, whichever
layout is configured. A line length of `0` switches the check off.

- **Fix**: Shorten the cells, or move the content out of the table.
- **Example** (`line-length: 40`):

  ```text
  table-too-wide.md:3:1: table-too-wide Table is 59 columns wide even when compact; the limit is 40
  ```

### table-column-count (MD056)

Reports every row whose number of cells differs from the number of cells of the header. A parser pads or ignores the
cells silently, and the formatter never repairs the row.

- **Fix**: Add or remove cells.
- **Example**:

  ```text
  | Name | Value |
  | --- | --- |
  | a | b |
  | c |
  | d | e | f |
  ```

  ```text
  table-column-count.md:4:1: table-column-count Table row has 1 cells; the header has 2
  table-column-count.md:5:1: table-column-count Table row has 3 cells; the header has 2
  ```

### table-grid-unformatted

Off by default. Reports every Pandoc grid table, which LintMark never changes. It is useful for a project that wants to
know where the formatter does not act.

- **Example** (run with `--enable table-grid-unformatted`):

  ```text
  +------+------+
  | a    | b    |
  +======+======+
  | 1    | 2    |
  +------+------+
  ```

  ```text
  table-grid-unformatted.md:1:1: table-grid-unformatted Grid table is left as written
  ```

### grid-table-invalid

Reports a line that looks like the border of a grid table when the table is not valid. The file is then read without
grid tables, and `--fix` never rewrites it, because that reading could turn a valid grid table into text.

- **Fix**: Correct the table, so that its borders have the same width.
- **Example**:

  ```text
  Some text.

  > +---+---+
  > | a | b |
  > +---+
  ```

  ```text
  grid.md:3:3: grid-table-invalid Line looks like a grid table border but the table is not valid; the file is never
  rewritten
  ```

  The finding is one line; it is wrapped here to fit the page.

## Failure Identifiers

The tool reports its own failures under identifiers that look like rules, but they are not rules: they cannot be
switched off, they have no legacy numbers, and a suppression comment cannot drop them. A failure is reported as a
finding, and the exit code is `1`.

| Identifier | Reported when |
| --- | --- |
| `file-read-error` | A file cannot be read. |
| `file-encoding-error` | A file is not valid UTF-8 or has an unsupported byte order mark. |
| `file-line-ending-error` | A file contains a bare carriage return. |
| `file-parse-error` | The Markdown parser failed, or inline markup is nested over 128 levels. |
| `file-format-error` | Formatting a file failed. |
| `file-write-error` | A rewritten file could not be written. |
| `config-error` | A configuration source has a problem. |
