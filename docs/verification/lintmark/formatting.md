## Formatting

### Verification Approach

The `Formatting` subsystem is verified by integration tests in `FormattingSubsystemTests.cs`. Each test parses a small
Markdown text with the real `DocumentParser`, runs the real `DocumentFormatter`, and applies the result with the real
`SourcePatcher`. The scenarios of the formatting rules build the `DocumentFormatter` as the default engine does, with the
five real block formatters of the tool (heading, code block, thematic break, list, and block quote) and the real
`InlineNormalizer` (the helper `BlockFormatterAssertions.ProductionFormatter()` creates it). The contract scenarios
build it with test doubles instead. Private helpers format a document and patch it in one step. The doubles exist only
in the test project, so that the subsystem tests can use rules that are deliberately simple or deliberately broken:

- `ReplacingFormatter` (from `TestBlockFormatters.cs`) is a simple and correct paragraph formatter that replaces one
  text by another and is registered under the `paragraph-wrap` rule, so suppression comments for that rule apply.
- `SubsystemHeadingUppercaseFormatter` (defined in the subsystem test file) writes headings in upper case and is
  registered under the `heading-style` rule. It lets a test combine formatters for different kinds of block.
- `OverlappingEditsFormatter` (from `TestBlockFormatters.cs`) is a deliberately broken formatter that proposes two
  edits of the same characters.
- `OutOfRangeEditFormatter` (from `TestBlockFormatters.cs`) is a deliberately broken formatter that proposes an edit
  beyond the end of the text.
- `ThrowingFormatter` (from `TestBlockFormatters.cs`) throws an `InvalidOperationException` when it is asked to format
  a paragraph.

The units are not replaced by doubles, so the tests show that the edit set, the formatter, and the patcher agree with
each other. The subsystem tests cover the behavior that crosses unit boundaries. The remaining branches of each
subsystem requirement (a document without line terminators, a tie between the line endings, a mixed file, a
`disable-next-line` comment, and every rejected argument) are verified by the unit tests and are described in the unit
documents.

The rules of the tool that change text (whitespace, blank lines, spacing between blocks, the final newline, headings,
code blocks, thematic breaks, lists, block quotes, and the delimiters of emphasis and the form of bare URLs) are verified
in this file with the shipped formatters, each with the small cases that show its behavior. The `Blocks` verification adds
the corpus of documents and the safety checks for the same rules, and the `Inline` verification adds the cases of the
inline rules.

### Test Environment

N/A - standard test environment. The tests use only in-memory text and bytes; no file is read or written.

### Acceptance Criteria

- All integration tests pass with zero failures.
- Edits that two formatters propose for different blocks are both applied, in document order, and the text that no
  edit covers is unchanged, also a character outside the basic plane and the text of a block that no formatter
  accepts.
- Without a block formatter the patched text is the very same string as the source text.
- Two edits of the same characters make formatting fail instead of choosing one of them, and an edit outside the text
  makes patching fail.
- An exception that a block formatter throws reaches the caller unchanged.
- A block in which the rule of its formatter is suppressed, also by a comment inside the block, stays as written, and
  other blocks are still formatted.
- A document that is flagged as unsafe to rewrite yields no edit and no problem.
- A file that uses CRLF is patched as LF text, the `line-ending` problem is reported, and the bytes that are written
  use the line ending that the configuration asks for.
- Without a configured line ending the majority ending of the file is written, and a file whose line endings all agree
  with the target has no `line-ending` problem.
- Trailing whitespace is removed from text, including a line of only whitespace, and kept in code; a hard break keeps
  its meaning, with two spaces or a backslash.
- A tab in plain text becomes spaces up to the next tab stop, and a tab in a code span, in indentation, or in a code
  block is kept.
- A run of blank lines is reduced to the configured maximum.
- A document ends with exactly one line ending when the configuration asks for a final newline, and with none when it
  does not.
- A heading, a fenced code block, a list, and a table that touch the text around them get a blank line, and no blank
  line is added or removed inside quotes and lists.
- A setext heading becomes an ATX heading where that keeps the meaning, closing number signs are removed, the blanks
  after the number signs become one space, and an indented heading starts at the left margin; a multi-line setext
  heading, text that ends in a number sign, and `#Title` stay as written.
- A thematic break and a code fence are written in the style that the configuration names, an indented code block
  becomes a fenced one, and a braced fence keeps its character.
- Code, HTML, math, front matter, and a fence that is never closed keep their content.
- A line that looks like the border of a grid table is not changed.
- No blank line is inserted next to a line that looks like the border of a grid table.
- A bullet marker is written with the configured character, an ordered list is numbered in the configured style from the
  number it starts with, the items of a list start in the same column with nested bullets indented as configured and one
  space after each marker, and one space follows each block quote marker; each change is reported under the rule of its
  kind (`bullet`, `ordered-style`, `list-indent`, `list-marker-space`, and `blockquote-space`).
- Two bullet lists that differ only by their marker stay two lists, a list stays one list, and the lines and blocks of a
  list item move with the item when its columns change.
- Emphasis and strong emphasis, also inside lists and quotes, are written with the configured delimiter and bare URLs are
  wrapped in angle brackets
  unless that is configured otherwise; each change is reported under its rule (`emphasis`, `strong`, and `bare-urls`).
- Each change is reported as a formatting problem of its rule at the place where the change starts.
- The canonical document, and the result of formatting a document, produce no edit and no problem, also for lists, block
  quotes, and inline text, and the same text gives the same result on many threads.

### Test Scenarios

#### Normal operation

**Edits of several formatters are all applied in order**: A document with a heading and a paragraph that contains a
double space is formatted by a `ReplacingFormatter` (double space to single space) and the heading formatter. The
result holds two edits, the heading edit (rule `heading-style`) first and the paragraph edit (rule `paragraph-wrap`)
second, and the patched text is `# TITLE`, a blank line, and `body text`.

**A document without formatters stays identical**: A clean document with a double space is formatted by a
`DocumentFormatter` that has no block formatters. The result has no edit, and the patched text is the same string
instance as the source text.

**Text that no edit covers is kept**: A document with a paragraph that starts with a character outside the basic
plane, a fenced code block that contains a double space, and a last paragraph that contains a double space is
formatted with a `ReplacingFormatter`. The result holds one edit, and the patched text differs from the source only
in the double space of the last paragraph.

**A suppressed block stays verbatim while other blocks are formatted**: A paragraph inside a region that disables
`paragraph-wrap`, and one paragraph after the region, are formatted with a `ReplacingFormatter`. Only the paragraph
after the region is changed, and the paragraph inside the region and both comments are unchanged.

**A suppression comment inside a block leaves the whole block verbatim**: A paragraph with an inline comment that
disables `paragraph-wrap` and an inline comment that enables it again in its middle, and a paragraph after it, are
formatted with a `ReplacingFormatter`. The first paragraph keeps its double spaces and only the second is changed.

**A CRLF file is patched as LF text and reported**: A file with CRLF line endings and two double spaces is parsed
from bytes and formatted with a configuration that asks for LF. The patched text has the double spaces removed and no
carriage return. The result holds one `line-ending` problem and the line ending LF, and the bytes produced from the
patched text with that line ending are the LF text.

**Without a configured line ending the majority is written**: A file with two CRLF terminators and one LF terminator
is formatted with a configuration that has no line ending. The line ending of the result is CRLF, one `line-ending`
problem is reported, and the bytes produced from the patched text with that line ending use CRLF throughout.

**A file whose line endings all agree has no problem**: A CRLF file is formatted with a configuration that asks for
CRLF. No problem is reported and the line ending of the result is CRLF.

**Trailing whitespace is removed from text only**: Text with trailing spaces, a line of only spaces, a line that ends in
a tab, and a fenced code block with trailing spaces are formatted. The whitespace of the text lines is gone, the line of
spaces is empty, and the code line is as written.

**Hard breaks keep their meaning**: Lines that end in two spaces, in one space, in a backslash, and in three spaces are
formatted. The two spaces and the backslash stay, the three spaces become two, and the single space is removed.

**Tabs are expanded only in plain text**: A tab between two words becomes spaces up to the next tab stop. A tab inside a
code span, a tab that indents a nested list item, and a tab inside a fenced code block are kept.

**A run of blank lines is reduced to the configured maximum**: Three blank lines between two paragraphs are formatted
with a maximum of one blank line, and again with a maximum of two. One blank line, and two blank lines, remain.

**A document ends with exactly one line ending**: A document without a final line ending, and one with several blank
lines at the end, are formatted with the default configuration. Both end with exactly one line ending.

**A document ends without a line ending when none is configured**: A document that ends with a line ending is formatted
with a configuration that turns the final newline off. The line ending is gone.

**A heading that touches text gets blank lines**: A heading directly before a paragraph, and a heading directly after
one, are formatted. A blank line separates them.

**A fence that touches text gets blank lines**: A fenced code block between two lines of text is formatted. A blank line
is inserted on both sides of the fence.

**A list that touches text gets a blank line**: A list directly after a line of text is formatted. A blank line
separates them.

**A table that touches text gets a blank line**: A table directly after a line of text is formatted. A blank line
separates them.

**A setext heading becomes an ATX heading where that is safe**: A one-line setext heading is formatted and becomes an
ATX heading. A multi-line setext heading, and one whose text ends in a number sign, stay as written.

**A closed heading loses its closing signs**: A heading with closing number signs is formatted, and the signs go. A
heading whose text ends in a number sign is as written, and no other problem is reported for it.

**The blanks after the number signs become one space**: A heading with four spaces after its number signs is formatted
and gets one space. `#Title`, which is a paragraph, is as written.

**An indented heading starts at the left margin**: A heading that is indented by three spaces is formatted and has no
indentation.

**Thematic breaks use the configured style**: A break written with asterisks is formatted with the default and becomes
three dashes. A break written with dashes is formatted with the asterisk style and becomes three asterisks. A break that
touches the text before it is as written.

**Code fences use the configured fence character**: A tilde fence with an info string is formatted with the default and
becomes a backtick fence. A backtick fence is formatted with the tilde style and becomes a tilde fence. A braced fence
is as written under the tilde style.

**An indented code block becomes a fenced one**: An indented code block after a heading is formatted. It becomes a
fenced code block with the same content, separated from the heading by a blank line.

**Bullet markers use the configured character**: Lists with asterisks, dashes, and a nested list are formatted with
`bullet: dash`, `asterisk`, and `plus`. Every marker, nested ones included, is the configured character, and each change
is reported under the rule `bullet`.

**Bullet lists are never merged or split**: Two bullet lists that differ only by their marker and a list with a nested
list are formatted with `bullet: dash`, `asterisk`, and `plus`. The three lists at the margin keep their different
markers, the nested list follows the configured character, and the four lists at the margin are still four lists with
the nested list inside the last item.

**Ordered numbers follow the configured style**: Lists numbered `1. 1.`, `5. 5.`, and `1. 2.` are formatted with the
default `incrementing` style and with `one`. The numbers count up from the first number or repeat it, and each change is
reported under the rule `ordered-style`.

**List columns and spacing are canonical**: Items indented by two columns, a nested bullet indented by four columns, a
nested bullet indented by two columns, and items with several spaces after the marker are formatted with `list-indent: 2`
and `4`. The items start in one column, the nested bullet is indented as configured, one space follows each marker, and
each change is reported under `list-indent` or `list-marker-space`.

**List items with content stay in their items**: Items with a continuation line, with a second paragraph, with a nested
list and fenced code, with a tenth number that gains a digit and a continuation line, and bullet items that start in
column two with continuation lines are formatted. Every continuation line, paragraph, and block moves with its item and
the number of items is the same afterward.

**Block quote markers are followed by one space**: A quote with two spaces after the marker, a heading in a quote with
three spaces, and nested quotes with extra spaces are formatted. One space follows each marker, and each change is
reported under the rule `blockquote-space`.

**Emphasis delimiters use the configured character**: `_a_ __b__` is formatted with the default configuration and
`*a* **b**` with `underscore` for both settings. The delimiters are asterisks and underscores respectively, and each
change is reported under `emphasis` or `strong`.

**Emphasis in lists and quotes uses the configured character**: Emphasis in a bullet item, in an ordered item, and in a
block quote is formatted with the default asterisk and with underscores configured. The delimiter is the configured one
and each change is reported under `emphasis`.

**Bare URLs are wrapped unless configured otherwise**: A sentence with a bare URL is formatted with the default
configuration and with `bare-urls: leave`. The URL is wrapped in angle brackets and reported under `bare-urls`, or the
text is unchanged and nothing is reported.

**Lists, quotes, and inline text are stable after one run**: A document with a list that has wrong markers, emphasis, and
a URL in its first item, a numbered list with repeated numbers, and a quote with a wide gap and strong emphasis is
formatted, and the result is formatted again. The first result is the canonical text, and the second run proposes no
edit.

**Code, HTML, math, front matter, and grid tables keep their content**: A document with front matter, a fenced code
block, an HTML block, a math block, and a grid table, each with trailing whitespace, tabs, and blank lines, is formatted.
No edit and no problem result.

**A line that looks like a grid table border is not changed**: Lines that start with a plus sign after a list marker or
a quote marker, with trailing whitespace, are formatted with the shipped formatters. No
edit and no problem result.

**A line that looks like a grid table border is not separated**: A line that starts with a plus sign after a list marker,
directly after text, is formatted with the same formatters. No blank line is inserted.

**Quotes and lists keep their blank lines**: A list item with a fence, a quote with a heading directly followed by text,
and a list with two blank lines between its items are formatted. No edit results.

**Violations are reported per rule at their place**: A heading with two spaces after its number sign that touches a
paragraph with trailing whitespace is formatted. Three formatting problems are reported in document order, one for the
space after the number sign, one for the missing blank line, and one for the trailing whitespace, each at the line
and column where its change starts.

**A canonical document and a rewritten document need nothing more**: A document that breaks several rules is formatted
once, and its result and a hand-written canonical document are formatted again. No edit and no problem result for
either.

#### Boundary conditions

**A fence that is never closed stays as written**: A document with a backtick fence that the document never closes,
followed by trailing whitespace and blank lines after its code, is formatted. A second document with a tilde fence
that is never closed is formatted too. The text of both is unchanged.

**A document that is unsafe to rewrite produces nothing**: A document with a line that looks like the border of a grid
table, but is not a valid grid table, is flagged as unsafe to rewrite. Formatting it with a `ReplacingFormatter`
yields no edit and no problem, and the patched text equals the source.

#### Error handling

**Overlapping edits are rejected**: A `DocumentFormatter` with an `OverlappingEditsFormatter` formats a one-paragraph
document. The call throws `InvalidOperationException` instead of choosing one of the two edits.

**An edit outside the text is rejected when it is applied**: A `DocumentFormatter` with an `OutOfRangeEditFormatter`
formats a one-paragraph document. The edit set accepts the edit and the result holds it, but `SourcePatcher.Apply`
throws `ArgumentException` because the edit lies beyond the end of the text.

**A failure of a block formatter reaches the caller**: A `DocumentFormatter` with a `ThrowingFormatter` formats a
one-paragraph document. The call throws the `InvalidOperationException` of the formatter with its own message.

#### Robustness

**The same lists and inline text give the same result on many threads**: One shipped formatter is shared by eight threads
that start together; each formats a document with a list, a quote, and inline text that need rewrites. Every result equals
the result of a single call.
