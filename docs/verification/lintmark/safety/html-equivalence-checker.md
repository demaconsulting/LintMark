### HtmlEquivalenceChecker

#### Verification Approach

`HtmlEquivalenceChecker` is verified with unit tests in `HtmlEquivalenceCheckerTests.cs`. The unit is a static class
with no state, so the tests call `Check` directly with small fixed texts and assert on the verdict and on the
description. The real Markdig library and the real fixed pipeline of `DocumentParser` render the texts; nothing is
replaced by a double, because the unit's behavior is the comparison of that rendering. A theory holds a fixed set of
reference cases, each with the text before, the text after, and the expected verdict;
every failed verdict must have a description and every passing verdict none. The remaining tests each pin down one
rule of the normalization: a layout change that must pass, a meaning change that must be found, code and raw HTML
that must be compared exactly, the description of the first difference, the run time for hostile input, concurrent
use, and the rejection of invalid arguments. The run-time tests build text of twenty thousand unclosed `pre` or `code`
tags, of two hundred thousand unclosed `p`, `div`, or `h1` tags, of two hundred thousand tags with attributes or with a
quote or comment that never ends, and of fifty thousand closed code spans or
preformatted elements and measure the elapsed time with a stopwatch against a
generous bound, so they detect a loss of linear behavior without depending on the speed of the machine. The failure
tests call the overload of `Check` that takes the function that renders a text, and pass a renderer that throws, to
show that a comparison that cannot be completed is reported as a difference; this overload is a seam for tests, and
production code uses the real renderer. The same overload, with a renderer that returns its input, lets the tests
give HTML directly where the real renderer would not produce it (tabs, form feeds, tags with attributes, and comments).
The concurrency test calls `Check` from up to sixteen threads and compares
every result with the single-threaded result.

#### Test Environment

N/A - standard test environment. The tests use only in-memory text. The run-time test needs no more than the normal
test machine, because its bound is ten seconds for input that is checked in a small fraction of that.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Every reference case gets its expected verdict.
- Texts that differ only in line breaks inside a paragraph, bullet character, table spacing, heading syntax, tabs or
  form feeds against spaces, whitespace runs in text, whitespace between a block-level tag and text, or trailing
  whitespace are equivalent, and identical texts are equivalent without rendering. A document with titled links,
  images, aligned tables, task lists, footnotes, math, and alerts that is rewritten only in layout is equivalent.
- Texts that differ in a hard break, a list or heading created by a line break, a link destination, emphasis, glued
  words, a no-break space, a lost space between inline elements, or a lost space next to a tag that only looks like a
  block-level tag are different.
- Texts that differ in any attribute are different: a heading identifier that is changed (with double or single
  quotes, after another attribute, or in a tag with a capitalized name), added, or removed; a `href`, `title`, `alt`,
  or `class` value that is changed; an attribute that is added or removed; a quote style that is changed; and any
  whitespace (space, tab, line feed) inside an attribute value or between the attributes of a tag. A comment that
  differs in whitespace, and text after a tag that never ends, are different as well.
- Whitespace changes in code spans, fenced code, and the content of `pre`, `script`, `style`, and `textarea`
  elements are different, also when the element is written in capital letters or is never closed, and so is a changed
  or added or removed attribute (such as a heading identifier) inside such an element.
- A failed verdict describes the offset and the text around it for both renderings, on one line of bounded length.
- Input with twenty thousand unclosed `pre` or `code` tags, input with two hundred thousand unclosed `p`, `div`, or
  `h1` tags, input with fifty thousand code spans or preformatted elements, input with two hundred thousand tags with
  attributes, and input with two hundred thousand tags whose quote or comment never ends, is checked within the time
  bound and its difference is found.
- A renderer that throws gives a difference that names the exception type, and identical texts are still equivalent.
- Concurrent calls give the same results as sequential calls.
- A carriage return in either text throws `ArgumentException` when normalized text is compared, and a null text or a
  null renderer throws `ArgumentNullException`.
- When texts are compared with the line endings they have on disk, carriage returns are accepted, a conversion of the
  line endings of a code span framed by line breaks is a difference, a conversion of an ordinary document is
  equivalent, a failing renderer gives a difference, and a null text or renderer throws `ArgumentNullException`.

#### Test Scenarios

##### Normal operation

**The reference cases get their expected verdicts**: Twenty-nine pairs are compared. They cover
soft breaks, spacing, block quote and list layout, emphasis characters, bullet characters, heading syntax, table
spacing, and trailing spaces (all equivalent), and also hard breaks, constructs that a line break turns into a list,
heading, block quote, numbered list, or setext underline, changed code, changed or removed link parts, heading level
changes, lost emphasis, glued words, changed table cells, lost or loosened list items, and changed raw HTML links
(all different). A code span that wraps over two lines is equivalent to one with a space. The verdict matches the
expected one in every case, and the description is present exactly when the verdict is not equivalent.

**A soft break and a space are equivalent**: `alpha beta` is compared with the same words on two lines; the verdict
is equivalent with no description.

**A bullet character and table spacing are equivalent**: A list with an asterisk and a compact table are compared
with a list with a hyphen and a table with padded cells; the verdict is equivalent.

**A setext heading and an ATX heading are equivalent**: The same title written with an underline and with a hash
mark gives the same page.

**Trailing whitespace and trailing blank lines are equivalent**: A paragraph with trailing spaces, and a paragraph
followed by several blank lines, are each compared with the plain paragraph; both are equivalent.

**Heading identifiers are compared**: Two raw HTML headings that differ only in their `id` attribute are different,
with the real renderer and with HTML given directly, also when the identifiers are written in single quotes, after
another attribute, or in a tag with a capitalized name. A heading with an identifier compared with the same heading
without one, in both directions, is different. The same identifier with different whitespace around the heading is
equivalent.

**Attributes are compared**: Pairs of tags with a changed `href`, `title`, `alt`, or `class` value, a changed quote
style, an added or removed attribute, are each different, and the description gives the offset.

**Whitespace inside tags is compared**: A `title`, `alt`, `href`, `class`, or `id` value with two spaces, a tab, or a
line feed, compared with the same value with one space, is different. So are a Markdown link title and an image
alternative text that differ in a double space (with the real renderer), different spacing between the attributes of a
tag, and a quoted value that contains a greater-than sign followed by a changed run of spaces.

**Comments and unterminated tags are compared exactly**: A comment with two spaces compared with one space, and a tag
whose quote is never closed followed by text with a changed run of spaces, are each different.

**Text and block tag whitespace is equivalent**: Whitespace runs in text, line feeds between block tags, spaces
inside a block element at its edges, and a block tag in capital letters, are equivalent to the compact form. A space
removed next to an inline tag, next to `param`, or next to `p-x` is different.

**Harmless rewrites of rendered attributes are equivalent**: A document with a titled link, an image, an aligned table,
a task list, a footnote, math, and an alert is compared with a version with different spacing, wrapping, bullet
character, and padding; the verdict is equivalent.

**Tabs and form feeds collapse like spaces**: HTML given directly with a tab, a form feed, or a mixed run of
whitespace between two words is equivalent to the same HTML with one space.

**Identical text is equivalent without rendering**: Covered under error handling, with a renderer that always throws.

**Concurrent calls give the same result**: One equivalent and one different pair are checked sequentially, then 64
times from up to sixteen threads; every concurrent result equals the sequential one.

**The first difference is described with context**: `alpha` is compared with `omega`; the description contains the
word offset and both rendered excerpts, `<p>alpha</p>` as before and `<p>omega</p>` as after.

##### Boundary conditions

**A lost hard break is a difference**: A line with two trailing spaces before a line break, compared with the same
text without the spaces, is different, and the description shows the `<br` tag that was lost.

**A line break that creates a list is a difference**: `a - b` compared with `a` and `- b` on two lines is different
and the description shows the `<li>` element.

**A line break that creates a heading is a difference**: `a # b` compared with `a` and `# b` on two lines is different
and the description shows the `<h1>` element.

**A changed link destination is a difference**: Two links with different addresses are different, and the
description shows the new address.

**Lost spacing between emphasized words is a difference**: A strong word and an emphasized word separated by a space,
compared with the same words without the space, are different.

**Glued words are a difference**: `a b` compared with `ab` is different.

**A lost space between inline elements is a difference**: Two `span` elements with a space between them, compared with
the same elements without the space, are different.

**A changed no-break space is a difference**: `a`, a no-break space, and `b` compared with the same text using an
ordinary space is different, because only HTML whitespace is collapsed.

**Code is compared exactly**: Two spaces collapsed to one inside a code span, and inside a fenced code block, are
each a difference.

**Preformatted raw HTML is compared exactly**: Two spaces collapsed to one inside a raw `pre` element are a
difference, also when the element name is written in capital letters, and also when the opening tag is never closed
and the element runs to the end of the text. The same holds for the content of raw `script`, `style`, and `textarea`
elements, for the content of an inline `code` tag that is never closed, and for a tag with an attribute (a heading
identifier in a `pre` element, a link title in a `script` element) inside such an element, which is a difference when
the attribute is changed, added, or removed.

**The description of a difference is one bounded line**: Two texts of two thousand lines that differ from the first
word on give a description without line break or carriage return and with at most 243 characters.

**Very many unclosed tags are checked in linear time**: Twenty thousand repeated opening `pre` tags, and the same for
`code` tags, and two hundred thousand repeated opening `p`, `div`, or `h1` tags, are each compared with the same text
with one more character at the end; the check finishes within ten seconds and reports the difference.

**Very many tags with attributes are checked in linear time**: Two hundred thousand `a` tags with two quoted
attributes each (one value contains a greater-than sign), with text between them, are compared with the same text with
one more word at the end; the check finishes within ten seconds and reports the difference.

**Very many tags that never end are checked in linear time**: Two hundred thousand repeated `<a "`, `<a title='`,
`<!--`, and `<?x` texts, and one hundred thousand repeated `<pre title="` texts, are each compared with the same text
with one more character at the end; the check finishes within ten seconds and reports the difference.

**Many code spans and preformatted elements are checked in linear time**: Fifty thousand closed `code` elements, and
the same for `pre` elements, are each compared with the same text with one more word at the end; the check finishes
within ten seconds and reports the difference.

##### Error handling

**A renderer that throws is reported as a difference**: Two different texts are compared with a renderer that throws
`InvalidOperationException`; the verdict is not equivalent, and the description says that the output could not be
compared and names the exception type.

**Identical text never uses the renderer**: Two identical texts, and two identical texts that are only a lone `pre`
tag, are compared with a renderer that always throws; the verdict is equivalent with no description.

**A missing renderer is rejected**: A null renderer throws `ArgumentNullException`.

**A carriage return in the original text is rejected**: A text with a carriage return and line feed pair as the
first argument throws `ArgumentException`.

**A carriage return in the rewritten text is rejected**: The same text as the second argument throws
`ArgumentException`.

**Null texts are rejected**: A null first text and a null second text each throw `ArgumentNullException`.

**A conversion of the line endings of a code span is a difference**: A code span framed by line breaks is compared with
its CRLF form and, the other way round, its CRLF form with its LF form; with the line endings kept, the verdict is not
equivalent and the description gives the offset.

**A conversion of the line endings of an ordinary document is equivalent**: A document with a heading, a one-line code
span, a soft break, a hard break, a list, a fenced block, and a raw `pre` block is compared with its CRLF form in both
directions; the verdict is equivalent.

**A text change with CRLF line endings is still a difference**: Two CRLF texts that differ in a hard break give a
verdict that is not equivalent.

**Identical text with line endings never uses the renderer**: Two identical texts that contain a carriage return are
compared with a renderer that always throws; the verdict is equivalent with no description.

**A renderer that throws is a difference when line endings are compared**: Two different texts with a carriage return
are compared with a renderer that throws `InvalidOperationException`; the verdict is not equivalent and names the
exception type.

**Null texts and a null renderer are rejected when line endings are compared**: A null first text, a null second text,
and a null renderer each throw `ArgumentNullException`.
