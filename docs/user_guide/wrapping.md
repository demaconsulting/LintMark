# Wrapping Paragraphs

LintMark can keep the lines of a paragraph within a width, or lay a paragraph out one sentence per line. This page
describes the three wrap modes, the settings and rules that control them, what a wrap keeps whole, and what LintMark
reports but cannot fix. The paragraphs of a document are the only text that is wrapped: tables, headings, code, HTML,
math, and front matter keep their lines.

In the examples, a finding is shown as `--check` prints it, without the two banner lines (version and copyright) that
LintMark prints before the findings. Every example was run with the tool and the output is quoted as it was printed.

## Settings and Rules

| Setting | Values | Default |
| --- | --- | --- |
| `line-length` | a whole number from `0` to `100000`; `0` means unlimited | `120` |
| `wrap` | `fill`, `sentence`, or `semantic` | `fill` |
| `link-refactor.break-link-text` | `true` or `false` | `false` |

| Rule identifier | Alias | Default | What it reports |
| --- | --- | --- | --- |
| `line-length` | `MD013` | on | A paragraph with a line wider than `line-length`. |
| `paragraph-wrap` | none | on | A paragraph that is not laid out as the `sentence` or `semantic` mode asks. |

Both rules are formatting rules, so `--fix` repairs what they report. They are the only formatting rules, besides
`link-refactor`, that the `rules` setting of a configuration (and the `--enable` and `--disable` options) can switch on
or off. A suppression comment that names `line-length`, `MD013`, or `paragraph-wrap` leaves a passage alone (see
*What Is Exempt* and *Suppression Comments*).

A line length of `0` means no limit. The `fill` mode then never rewrites anything, and the `sentence` and `semantic`
modes write one sentence per line.

## The fill Mode

The default mode fills only what overflows: a paragraph is rewrapped only when at least one of its lines is wider than
`line-length`, and a paragraph that fits is never touched, however its lines are broken. The wrapped paragraph keeps the
same words in the same order, and every line that LintMark writes is as full as the limit allows. Paragraphs in list
items and in block quotes are wrapped too, and each continuation line gets the prefix that keeps it inside the item or
the quote. In the `fill` mode the rule `paragraph-wrap` reports nothing.

### How the width is measured

The width of a line is its number of display columns, not its number of characters. A letter, a digit, and a
punctuation mark take one column; a CJK character and most emoji take two columns; a combining mark takes none. The
prefix of a list item or a block quote (the marker and the space after it) belongs to the line and counts, and trailing
spaces do not.

### What is reported

A line of a paragraph that is wider than the limit, and that has at least one place at which it can be broken, is
reported as `line-length`, at the line and the first character that is beyond the limit. Only the first such line of a
paragraph is reported, with its width. The configuration of this example is `line-length: 50`:

```text
a.md:1:51: line-length Line is 103 columns wide; the limit is 50
```

A violation that `--fix` can repair is fixable. `--check` and `--diff` report it and write nothing, and `--fix` wraps
the paragraph and no longer reports it.

### What is fixed

- **Paragraphs**: A paragraph is filled greedily: as many words as fit go on each line, and a word that is wider than
  the limit stands alone on its line.
- **Prefixes**: The prefix of a list item or a block quote is kept, and it is added to every line that LintMark writes.
- **Hard line breaks**: A hard line break (two or more spaces, or a backslash, at the end of a line) stays where it is,
  and the text between two hard breaks is wrapped on its own.
- **Inline constructs**: A link, an image, a code span, inline HTML, a bare URL or autolink, a math expression, a
  footnote reference, and a Pandoc attribute or citation are never split. When one does not fit, it moves whole to the
  next line.
- **Block syntax**: No line that LintMark writes starts with text that a reader would take for another block: a list
  marker (`-`, `+`, `*`), a number marker (`1.`, `2026.`), a heading (`#`), a quote (`>`), a fence, a table, a
  definition, an HTML block, or a Pandoc construct. The word before it moves to the next line with it, so that the
  guarded word is never at the start.

A long paragraph, with `line-length: 50`:

- **Before**:

  ```text
  LintMark rewraps a paragraph only when a line is wider than the limit, and it keeps the words in order.
  ```

- **After**:

  ```text
  LintMark rewraps a paragraph only when a line is
  wider than the limit, and it keeps the words in
  order.
  ```

A list item keeps the indentation of its text:

- **Before**:

  ```text
  - A list item that is too long for the limit is wrapped, and its continuation lines line up with its text.
  - A short item.
  ```

- **After**:

  ```text
  - A list item that is too long for the limit is
    wrapped, and its continuation lines line up with
    its text.
  - A short item.
  ```

A block quote keeps its marker on every line:

- **Before**:

  ```text
  > A quoted paragraph that is too long for the limit keeps the quote marker on every line that it writes.
  ```

- **After**:

  ```text
  > A quoted paragraph that is too long for the
  > limit keeps the quote marker on every line that
  > it writes.
  ```

A link that does not fit moves whole to the next line. It is never split, so the line with the link stays wider than
the limit, and it is not reported again (see *Breaking the Text of a Link* for the one exception):

- **Before**:

  ```text
  Read the [LintMark user guide](https://example.com/lintmark/docs/user_guide/introduction.md) for the details.
  ```

- **After**:

  ```text
  Read the
  [LintMark user guide](https://example.com/lintmark/docs/user_guide/introduction.md)
  for the details.
  ```

A word that would be read as block syntax at the start of a line is kept with the word before it. In the first
paragraph, `2026.` would start a line (and look like an ordered list item); in the second, `-` would:

- **Before**:

  ```text
  The conference was held in the capital city in 2026. It was a great event for everyone who came.

  The new release removes the old option and adds it - a change that users asked for.
  ```

- **After**:

  ```text
  The conference was held in the capital city
  in 2026. It was a great event for everyone who
  came.

  The new release removes the old option and adds
  it - a change that users asked for.
  ```

The width is measured in display columns, so a line with CJK characters or emoji is wrapped earlier than the same
number of Latin letters. Each of the 15 emoji below takes two columns:

- **Before**:

  ```text
  日本語 のテキスト は一文字 が二桁 の幅に なります。 日本語 のテキスト は一文字 が二桁 の幅に なります。

  The party 🎉🎉🎉🎉🎉🎉🎉🎉🎉🎉🎉🎉🎉🎉🎉 started and everyone had a good time at it.
  ```

- **After**:

  ```text
  日本語 のテキスト は一文字 が二桁 の幅に
  なります。 日本語 のテキスト は一文字 が二桁
  の幅に なります。

  The party 🎉🎉🎉🎉🎉🎉🎉🎉🎉🎉🎉🎉🎉🎉🎉 started
  and everyone had a good time at it.
  ```

A paragraph that fits is not changed, even when its lines are short or uneven:

```text
A short paragraph that fits
is never touched, even when it
is wrapped in an odd way.
```

## Breaking the Text of a Link

By default the whole of an inline link, its text and its destination, is one unit that is never split. The setting
`link-refactor.break-link-text: true` lets a wrap break the text of an inline link at its blanks; only the destination
part `(...)` stays whole. With `line-length: 50`:

- **Before**:

  ```text
  Please read [the long user guide of the tool](https://example.com/guide.md) before you start.
  ```

- **After** (default):

  ```text
  Please read
  [the long user guide of the tool](https://example.com/guide.md)
  before you start.
  ```

- **After** (`break-link-text: true`):

  ```text
  Please read [the long user guide of the
  tool](https://example.com/guide.md) before you
  start.
  ```

The setting belongs to the `link-refactor` option group, but it also works when `link-refactor.enabled` is `false`
(see *Tables and Links*).

## The sentence Mode

With `wrap: sentence` every sentence of a paragraph starts a new line. A sentence that is wider than `line-length` is
filled onto several lines, as in the `fill` mode. One sentence per line gives small, readable differences in version
control, because a change to one sentence changes only its own lines.

Unlike the `fill` mode, this mode has one layout for every paragraph, so a paragraph that is not laid out that way is
reported as `paragraph-wrap`, whether or not a line overflows, and `--fix` writes the layout. A line that is too wide
and could be broken is still a finding of `line-length`. The configuration of this example is `wrap: sentence` and
`line-length: 50`:

- **Before**:

  ```text
  LintMark is a formatter. It rewrites Markdown files, e.g. a README. Dr. Smith wrote it in 2024! Is it fast? Yes, it is.

  The new release removes the old option, which nobody used, and adds a faster parser, which handles large files well.

  - A short item. Another sentence in the same item.
  ```

- **Findings**:

  ```text
  a.md:1:1: paragraph-wrap Paragraph is not wrapped per the configured sentence or semantic style
  a.md:1:51: line-length Line is 119 columns wide; the limit is 50
  a.md:3:1: paragraph-wrap Paragraph is not wrapped per the configured sentence or semantic style
  a.md:3:51: line-length Line is 116 columns wide; the limit is 50
  a.md:5:1: paragraph-wrap Paragraph is not wrapped per the configured sentence or semantic style
  ```

- **After**:

  ```text
  LintMark is a formatter.
  It rewrites Markdown files, e.g. a README.
  Dr. Smith wrote it in 2024!
  Is it fast?
  Yes, it is.

  The new release removes the old option, which
  nobody used, and adds a faster parser, which
  handles large files well.

  - A short item.
    Another sentence in the same item.
  ```

### Where a sentence ends

LintMark decides on whole words, so a sentence end inside a link, a code span, or an inline HTML tag is never seen.
A word ends a sentence when, after any closing quote, closing parenthesis, end of emphasis, or footnote reference, it
ends with a period, an exclamation mark, a question mark, or their East Asian forms, and the next word starts like a
sentence: with an upper-case letter, a digit, a letter without case, or a code span.

The rule is conservative on purpose, because a missed split only keeps two sentences on one line, while a wrong split
reads badly. There is no split:

- After an ellipsis.
- After a known abbreviation (`e.g.`, `Dr.`, `etc.`), an initial (`J.`), a dotted abbreviation (`U.S.`), or a number
  of one or two digits (`3.`).
- Before a word that starts in lower case, before an HTML tag, or before a symbol.

The list of abbreviations is built into the tool and cannot be configured. A sentence start that would be read as block
syntax at the start of a line is not a place for a line break: the sentence stays on the line of the one before it.

## The semantic Mode

With `wrap: semantic` every sentence starts a new line, like in the `sentence` mode, and a sentence that is wider than
the limit is broken at the latest clause break that fits the new line, instead of being filled to the limit. A word
ends a clause when it ends with a comma, a semicolon, a colon, or a dash; an emoji code such as `:smile:` and a scope
such as `std::` do not end a clause. When no clause break fits, the line is filled. With `line-length: 50`, the same
paragraphs as in the previous example become:

```text
LintMark is a formatter.
It rewrites Markdown files, e.g. a README.
Dr. Smith wrote it in 2024!
Is it fast?
Yes, it is.

The new release removes the old option,
which nobody used, and adds a faster parser,
which handles large files well.

- A short item.
  Another sentence in the same item.
```

Both modes depend only on the words and the width, never on how the paragraph was laid out before, so a second run
reports nothing and changes nothing.

## Choosing Which Rule Acts

The two rules are independent: either one is enough to rewrite a paragraph.

- With `paragraph-wrap` switched off, only a paragraph that has a line wider than the limit is rewritten, and it is
  written in the layout of the mode (one sentence per line for `sentence`). The paragraphs that fit are left alone.
- With `line-length` switched off (for example `--disable MD013`), `paragraph-wrap` still lays out every paragraph
  as its mode asks, and the line length still sets the width of the lines.

## What Is Reported but Not Fixed

LintMark reports some overflow that it cannot repair. The message then ends with `and the paragraph cannot be wrapped
automatically`, `--fix` leaves the paragraph as written, and the finding stays after the fix:

```text
lazy.md:1:51: line-length Line is 69 columns wide; the limit is 50 and the paragraph cannot be wrapped automatically
```

This happens when:

- A line of a list item is a lazy continuation line, one that continues the item without the indentation of its text.
  The paragraph is kept as written. In this example, the second line of the item is a lazy continuation line:

  ```text
  - A list item whose lines are too long for the limit of fifty columns
  continues here without the indentation of the item and is also too long.
  ```

- A line after a hard break would have to start with text that is read as block syntax.
- A guarded word cannot be moved without making the line wider than the limit.
- A wrap would be rejected by the checks that LintMark makes on its own result, for example because another rule would
  change the wrapped text again. The paragraph stays as written.

## What Is Exempt

LintMark neither reports nor changes the following, even where a line is wider than the limit:

- A line that has no place to break, because it holds a single link, URL, autolink, code span, image, or word.
- Code (fenced and indented), HTML, math, tables, headings, link reference definitions, and front matter.
- A paragraph in a footnote definition.
- A paragraph that holds a Pandoc structural line: a line that starts with `:::`, `$$`, `{=`, a backslash command, a
  `%` line, or the border of a grid table.
- A paragraph that holds a tab.
- A paragraph in which one of the rules that decide how its text is written is suppressed by a comment:
  `trailing-whitespace`, `hard-tabs`, `emphasis`, `strong`, `bare-urls`, `code-span-space`, `list-indent`,
  `list-marker-space`, or `blockquote-space` (or the legacy numbers of them).
- A paragraph in which the rule that would wrap it is suppressed: a `line-length` or `MD013` comment keeps a paragraph
  as it is in the `fill` mode, and in the `sentence` and `semantic` modes a paragraph is kept by comments for both
  `line-length` and `paragraph-wrap`.

## Safety

The wrap is the last step of a fix, after the other rules have written their changes. A paragraph is wrapped only
when the wrapped text renders to the same HTML as before, and a paragraph for which that cannot be proved is left as
it was.
