# Formatting Rules

LintMark checks and rewrites Markdown files with a fixed set of formatting rules. Each rule describes one way in
which a file differs from its canonical form, and every rule has a stable identifier. Most rules also have a legacy
number from markdownlint (for example `MD009`) that you can use in a suppression comment.

## How the Rules Work

- **Check**: `lintmark docs` reports every place that a rule would change, as `path:line:col: rule-id message`, and
  exits with `1` when it reports anything.
- **Fix**: `lintmark --fix docs` makes the changes, writes only files that pass every safety check (see *Safety
  Checks*), and reports what remains. A second run over the same files reports nothing and changes nothing.
- **Diff**: `lintmark --diff docs` first reports the findings, as in check mode, and then prints a unified diff of what
  `--fix` would change. It writes no file, and because it reports the findings, it exits with `1` when there are any.
- **Always applied**: A formatting rule cannot be switched off in the `rules` setting of the configuration: its
  identifier and its legacy number are unknown names there. Its *settings* (such as `code-fence`) choose the form that
  it writes. To leave a passage alone, use a suppression comment (see *Suppressing a Rule*).
- **Content is never changed**: Code (fenced, indented, and inline), HTML, math, front matter, and grid tables keep
  their content, including whitespace and blank lines. Text that LintMark cannot read with certainty is left as it is,
  and so is a rewrite that could change what the document means or how it renders: such a conversion is simply not
  made.
- **Containers**: Blocks inside a quote or list are changed in place, but no blank line is added or removed there,
  because that could change how a list renders.

In the examples below, a dot (`·`) stands for a space and an arrow (`→`) for a tab at the end of a line or inside text,
so that the whitespace is visible. Write the real characters in a file.

## Whitespace

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
the other yet.

### hard-tabs (MD010)

Replaces each tab in the text of a paragraph or heading by spaces up to the next tab stop of four columns. A tab in
the indentation of a line, in code (also inline code), in a table, in a link destination or title, at the end of a line
(the trailing whitespace rule owns that whitespace, and spaces there could turn into a line break), and in any block that
LintMark does not rewrite stays as it is.

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
the document renders. (The rules `MD018` and `MD020`, which report such text, are not part of LintMark yet.)

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

## Suppressing a Rule

A comment switches a rule off for a part of a file. The rules are named by identifier or by legacy number, in any letter
case, and both families of comments work: `lintmark-` and `markdownlint-`.

```markdown
<!-- lintmark-disable trailing-whitespace -->

Text that keeps its trailing whitespace.

<!-- lintmark-enable trailing-whitespace -->

<!-- markdownlint-disable-next-line MD022 -->
# A heading that touches the text after it
Text without a blank line before it.

<!-- lintmark-disable-file final-newline -->
```

- `disable` and `enable` mark a region, `disable-next-line` and `disable-line` mark one line, `disable-file` marks the
  whole file, and a comment without rule names applies to all rules.
- A block is left exactly as it is when any rule that applies to it is suppressed anywhere inside the block.
- `prettier-ignore` (and `prettier-ignore-start` with `prettier-ignore-end`) leaves the next block, or everything
  between the comments, as it is.
- A name that is not a rule suppresses nothing.

## Rules and Settings at a Glance

| Rule identifier       | Alias            | Setting (default)                        |
| --------------------- | ---------------- | ---------------------------------------- |
| `trailing-whitespace` | `MD009`          | `trailing-whitespace: strip`             |
| `hard-tabs`           | `MD010`          | none                                     |
| `max-blank-lines`     | `MD012`          | `max-blank-lines: 1`                     |
| `heading-style`       | `MD003`, `MD021` | `heading-style: atx`                     |
| `heading-space`       | `MD019`          | none                                     |
| `heading-start-left`  | `MD023`          | none                                     |
| `heading-blank-lines` | `MD022`          | none                                     |
| `fence-blank-lines`   | `MD031`          | none                                     |
| `list-blank-lines`    | `MD032`          | none                                     |
| `table-blank-lines`   | `MD058`          | none                                     |
| `thematic-break`      | `MD035`          | `thematic-break: dashes`                 |
| `code-block-style`    | `MD046`          | `code-block-style: fenced`               |
| `code-fence`          | `MD048`          | `code-fence: backtick`                   |
| `final-newline`       | `MD047`          | `final-newline: true`                    |

The line ending rule `line-ending` is described under *Line Endings and .editorconfig*; it has no legacy number and cannot
be suppressed. The rules that report problems without rewriting (for example `heading-increment`) are not implemented
yet.
