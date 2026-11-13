### DocumentFormatter

#### Verification Approach

`DocumentFormatter` is verified with unit tests in `DocumentFormatterTests.cs` (the contract of the formatter),
`DocumentFormatterPassTests.cs` (the rules that apply to every document and the reporting of their edits), and
`DocumentFormatterSupportTests.cs` (the internal helper types). The real `DocumentParser` and real
`SourceDocument` produce the documents, the real `LintMarkConfig` supplies the settings, and the real `SourcePatcher`
applies the edits so that a test can assert on the patched text. In `DocumentFormatterTests.cs` only the block
formatters are test doubles, and they come from `TestBlockFormatters.cs`:

- `ReplacingFormatter` is a simple, correct paragraph formatter registered under the `paragraph-wrap` rule. A private
  helper builds a `DocumentFormatter` with one that replaces a double space by a single space; other tests use
  two of them with different replacements to prove which one is asked.
- `OverlappingEditsFormatter` is a deliberately broken formatter that proposes two edits of the same characters.
- `ThrowingFormatter` is a deliberately broken formatter that throws `InvalidOperationException`.

`DocumentFormatterPassTests.cs` formats documents with the document formatter alone (`new DocumentFormatter()`, so the
passes see every block as unclaimed) and with the `ReplacingFormatter` double, so that the claim of a block by a
formatter can be observed. Every pass scenario that checks the patched text goes through `AssertSafeAndStable` of the
test helper `BlockFormatterAssertions` (a collaborator of the tests, described with the `Blocks` verification), which in
addition to the text asserts that the result renders to the same HTML, keeps its Pandoc structure, is stable when formatted
again, and that the edits, the problems, and the thread-independence of the result agree. The doubles of the unit are
`ReplacingFormatter` and `OverlappingEditsFormatter`, which the contract tests use, and `ThrowingFormatter`, which proves
that an exception propagates. Each scenario asserts on the patched text, and where reporting matters on the rule, line,
and column of the problems that the formatter returns. The tests of the helper types call them directly with documents
from the real parser; a few of them build a block by hand (`with`) to reach branches that the parser cannot produce.

Two parse helpers are used: one parses LF-normalized text, and one parses the bytes of a file so that its original
line endings are counted. Suppression is driven by real suppression comments in the Markdown text. The test for
parallel calls shares one formatter and one document among 64 calls that run with up to 16 threads, and compares
every result with the result of a single call.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Without block formatters, or for a block that no formatter accepts, no edit is proposed beyond the edits of the rules
  that apply to every document, so a document that those rules leave alone gets no edit.
- The edits of an accepting formatter are collected and ordered, and only the first accepting formatter is used for a
  block.
- A block is left verbatim when its formatter's rule is disabled by a region, when all rules are disabled, when a
  prettier-ignore comment precedes it, when a disable-next-line comment precedes it, or when a suppression comment lies
  inside the block; other blocks are still formatted, and the suppression of another rule has no effect.
- A document that is unsafe to rewrite yields no edit and no problem, even when its line endings differ from the
  configuration, and its line ending is the majority ending of the file.
- The configured line ending wins; without one the majority ending of the file is the target, and LF is the target
  when the endings are equally frequent or the file has no terminator.
- Exactly one `line-ending` formatting problem at line 1 and column 1 is reported when any line terminator differs
  from the target, with a message that names the endings found and the one expected; nothing is reported otherwise.
- Trailing whitespace and tabs in text are removed or expanded without changing a hard line break, a tab in the whitespace
  at the end of a line is never expanded, and whitespace in literal text, code, HTML, math, front matter, and grid tables
  is never touched.
- Runs of blank lines between blocks are reduced to the configured maximum, and blank lines inside blocks and
  containers are kept.
- Blank lines are inserted between top-level blocks that need them, once per boundary, and never inside containers, for
  text that is not another block, next to a fence that is never closed, or between a next-line suppression directive and
  the block it covers.
- The end of the file is exactly one line ending, or none when configured, also after an HTML block that its end
  condition closes.
- The end of the file is left alone after a fence that is never closed and after an HTML block that its end condition
  never closes.
- A whitespace-only line wider than the indentation at the end of a top-level indented code block is code and is kept,
  and a narrower one is emptied.
- A line that looks like the border of a grid table is neither edited nor separated from its neighbors by a blank line.
- A suppressed rule, named by its identifier or by an alias, is not applied where the suppression takes effect, and the
  lines of a block that a block formatter accepts are never edited by the passes.
- Each edit of an always-applied rule is reported once per rule and line at its place, with the description of the rule.
- Conflicting edits of a formatter make formatting throw `InvalidOperationException`.
- An exception that a block formatter throws reaches the caller with its type and message unchanged.
- Null arguments are rejected, a null list means no formatters, and later changes of the caller's list are ignored.
- A format context without a document or configuration, and a format result with a default list, a default edit, a
  null finding, or an undefined line ending, are rejected; valid ones keep their values.
- Parallel calls give the same result as a single call.

#### Test Scenarios

##### Normal operation

**A formatter without any block formatters changes nothing**: A document with a double space inside a paragraph is
formatted by a `DocumentFormatter` that has no block formatters. The result has no edit and no problem, and the line
ending is LF.

**The edits of an accepting formatter are collected**: A paragraph `a  b  c` is formatted with the double-space
formatter. The result holds two edits at offsets 1 and 4, ordered, both under the rule `paragraph-wrap`, and the
patched text is `a b c`.

**Only the first accepting formatter is used**: Two `ReplacingFormatter` doubles that both accept the paragraph, one
replacing the double space by an underscore and one by a single space, are registered in that order. The result holds
two edits, and the patched text is `a_b_c`, so the second formatter was never asked.

**A block with a suppressed rule is left verbatim**: Three paragraphs are formatted, of which the middle one lies in a
region that disables `paragraph-wrap`. The middle paragraph keeps its double space, and the first and last
paragraphs are formatted.

**A region that disables all rules leaves its block verbatim**: A paragraph inside a region that disables every rule,
and one after the region, are formatted. Only the paragraph after the region is changed.

**A prettier-ignore comment leaves the next block verbatim**: A prettier-ignore comment precedes the first of two
paragraphs. The first keeps its double space and the second is formatted.

**A suppression comment inside a block leaves the whole block verbatim**: A paragraph with three double spaces holds
an inline comment that disables `paragraph-wrap` after its first part and an inline comment that enables it again
before its last part. A paragraph follows it. The first paragraph keeps all its double spaces, and the later
paragraph is formatted.

**A disable-next-line comment leaves the block on the next line verbatim**: A comment that disables `paragraph-wrap`
for the next line directly precedes the first of two paragraphs. The first keeps its double space and the second is
formatted.

**Suppressing one block does not stop other blocks**: A prettier-ignore comment lies between two paragraphs, and a
paragraph comes before and after it. The result holds exactly two edits, for the first and the last paragraph, and the
ignored paragraph is unchanged.

**Suppressing another rule does not stop the formatter**: A paragraph lies inside a region that disables
`line-length` only. The paragraph is formatted.

**A block that no formatter accepts is left verbatim**: A heading and a paragraph are formatted by a formatter that
accepts paragraphs only. The heading keeps its double space and the paragraph is formatted.

**The configured line ending wins over the majority**: A file with two CRLF terminators and one LF terminator is
formatted with a configuration that asks for LF. The line ending of the result is LF.

**Without a configured line ending the majority is the target**: The same file is formatted with a configuration that
has no line ending. The line ending of the result is CRLF, and one problem reads `Mixed line endings (2 CRLF, 1 LF);
expected CRLF`.

**A CRLF file with a LF configuration is reported once at the first position**: A CRLF file is formatted with a
configuration that asks for LF. One problem is returned with the rule `line-ending`, the formatting kind, the path
`a.md`, line 1, column 1, and the message `Line endings are CRLF; expected LF`. The line ending of the result is LF.

**A mixed file is reported with the counts of each ending**: A file with two CRLF terminators and one LF terminator is
formatted with a configuration that asks for LF. The one problem has the message `Mixed line endings (2 CRLF, 1 LF);
expected LF`.

**A LF file with a CRLF configuration is reported**: A LF file is formatted with a configuration that asks for CRLF.
The one problem has the message `Line endings are LF; expected CRLF`, and the line ending of the result is CRLF.

**A file that already has the configured line ending has no problem**: A CRLF file is formatted with a configuration
that asks for CRLF. No problem is reported and the line ending of the result is CRLF.

**Parallel calls give the same result as a single call**: One formatter and one CRLF document with three double
spaces are shared by 64 calls on up to 16 threads, with a configuration that asks for LF. The single-call result
holds three edits, and every parallel result has the same edits, the same problems, and the same line ending.

##### Rules that apply to every document

**Trailing whitespace of text is removed**: A paragraph whose lines end with spaces and tabs is formatted. Every
trailing run is removed.

**A hard break written with spaces keeps exactly two spaces**: A line that ends with three spaces before the next line
of the paragraph is formatted. The three spaces become two, and a line that ends with one space has it removed.

**A backslash break and a break after a tab are kept**: A paragraph with a backslash break, a backslash followed by a
space at the end of a line, and a tab followed by two spaces at the end of a line is formatted. Each is unchanged,
because a backslash break takes no spaces, a space after a backslash is text, and a tab before the spaces is text.

**A tab at the end of a line is kept where the trailing whitespace rule is disabled**: A paragraph with a tab at the end
of a line, followed by a line or at the end of the file, in a region that disables `trailing-whitespace` is formatted.
The tab stays and no `hard-tabs` problem is reported; expanding it could leave two spaces, which are a hard break.

**Whitespace in inline literal text is kept**: A paragraph with a line break inside a code span, inside inline HTML, and
inside inline math is formatted, and so is one with a line break inside a link title. The whitespace inside each is
unchanged.

**Table rows, definitions, and breaks lose trailing whitespace**: A table, a single-line link reference definition, and
a thematic break that no block formatter accepts, each with trailing whitespace, are formatted. The trailing whitespace
is removed from each.

**Marker lines lose trailing whitespace**: A quote line that holds only a marker and spaces is formatted. The spaces are
removed.

**A heading that no block formatter claims loses trailing whitespace**: An ATX heading and a setext heading with
trailing spaces are formatted by a document formatter without block formatters. The spaces are removed.

**Protected blocks keep their whitespace**: Fenced code with a tab and trailing spaces, tilde-fenced code, indented
code, an HTML block, and a math block are formatted, and so are front matter and a grid table with trailing spaces.
Nothing in them changes.

**A wide whitespace-only line that ends indented code is code**: Top-level indented code that ends with a line of six
spaces, five spaces, or a tab and two spaces, followed by a blank line and text or by nothing, is formatted. The line
stays.

**A whitespace-only line that is not wider than the indentation after indented code is emptied**: A line of exactly four
spaces, or one after a wider line that stays, after top-level indented code is formatted. It is a blank line and is
normalized with the end of the file. The same wide line inside a list item is emptied as well, because the parser drops
it there.

**A line that looks like a grid table border is not edited**: Lines that start with a plus sign after a list marker or a
quote marker, with trailing whitespace, are formatted. Nothing changes.

**A line that looks like a grid table border is not separated**: A line that starts with a plus sign after a list marker,
directly after text, is formatted. No blank line is inserted.

**A tab in text becomes spaces up to the next tab stop**: Paragraphs with a tab after one, two, and four characters,
with two tabs in a row, and inside a list item and a later line of a paragraph are formatted. Each tab becomes the
number of spaces that reaches the next multiple of four columns.

**Tabs that must stay are kept**: A tab in the indentation of a continuation line, in a code span, in a link
destination, in a table row, in a line that looks like a grid table border, and in fenced code is formatted. Each is
unchanged.

**A run of blank lines collapses to the configured maximum**: A document with three blank lines between two paragraphs
is formatted with the maximum 1, 2, and 3. One and two blank lines remain, and with 3 nothing changes; a run of five and
a run of leading blank lines are reduced as well.

**A line of whitespace between blocks becomes empty**: A line of spaces, a line of spaces and tabs, and a line of one
tab between two paragraphs are formatted. Each becomes an empty line.

**Blank lines inside blocks are kept**: Runs of blank lines inside a fenced code block, an HTML comment block, a math
block, and front matter are formatted. They are unchanged.

**Blank runs in a list or quote are kept, without their whitespace**: Runs of blank lines between list items are
formatted, one run made of lines that hold spaces. The number of lines is unchanged and the whitespace is removed.

**Blank lines after a fence that is never closed are kept**: A document that ends in an unclosed fence followed by
blank lines is formatted. Nothing changes.

**A heading gets blank lines around it**: A heading that touches a paragraph, a heading, front matter, a list, or an
HTML comment before or after it is formatted. A blank line is inserted at each boundary.

**A code block gets blank lines around it**: A fenced block that touches paragraphs, a list item, or a thematic break,
and an indented block that touches a paragraph, are formatted. A blank line is inserted at each boundary.

**A list gets blank lines around it**: A bullet list and an ordered list that touch a paragraph, and a list that touches
a quote, are formatted. A blank line is inserted at each boundary.

**A table gets blank lines around it**: A pipe table that touches a paragraph, a heading, or a thematic break is
formatted. A blank line is inserted at each boundary.

**Text that is not another block is not separated**: A line that looks like an ordered list item after a paragraph and
an indented line after a paragraph, a lazy continuation of a list item, and a line without pipes after a table are
formatted. No blank line is inserted.

**A block that the parser reports after a prefix on its line is not separated (defensive, no requirement)**: A heading
followed by a definition that starts after other text on its line, and a list followed by a list marker after a tab, are
formatted. No blank line is inserted.

**A next-line directive keeps its place above its block**: A `disable-next-line` comment of the `markdownlint` or the
`lintmark` family stands directly above a heading, a list, a fenced block, a table, or indented code that touches it.
No blank line is inserted between them, the result renders the same, and it is stable. A comment that is not a next-line
directive, a region `disable`, or a next-line directive that is followed by a blank line does not hold back the blank
line.

**Blocks inside containers are not separated**: A fence in a list item, a heading in a quote, a heading in a list item,
and a list in a quote that touch other text are formatted. No blank line is inserted.

**A fence that is never closed is not separated from its neighbors**: An unclosed fence that follows a paragraph is
formatted. No blank line is inserted.

**One blank line is inserted where two rules apply**: A heading directly followed by a fenced code block is formatted.
One blank line is inserted, the one edit is credited to `heading-blank-lines`, and one problem is reported at line 2,
column 1.

**The next rule inserts the blank line when the first is suppressed**: The same document with a comment that disables
`MD022` is formatted. The blank line is inserted and credited to `fence-blank-lines`.

**The end of the file gets exactly one line ending**: Documents without a final line ending, with blank lines at the
end, with whitespace-only lines at the end, and ending in a list, a fenced block, or an indented block with blank lines
are formatted. Each ends with exactly one line feed.

**No final line ending is wanted when configured**: Documents that end with one or several line endings, or with none,
are formatted with `final-newline` off. Each ends with its last character, and trailing whitespace of the last line is
removed.

**A document without content is left alone**: An empty document, one with only blank lines, one with only whitespace
lines, and one with a single tab are formatted. No edit and no problem result.

**The end of the file is left alone after a fence that is never closed**: Documents that end in an unclosed fence, with
or without blank lines, are formatted. Nothing changes.

**The end of the file is left alone after an HTML block that its end condition never closes**: Documents that end in an
HTML comment, script, or CDATA block that its end condition never closes, with or without blank lines, are formatted.
Nothing changes.

**The end of the file is normalized after an HTML block that its end condition closes**: Documents that end in a closed
comment without a final line ending or with blank lines after it, in a closed script, pre, or style block, a processing
instruction, a declaration, or a CDATA section followed by blank lines, and a closed comment followed by blank lines and
text are formatted. Each ends with exactly one line feed.

**A suppressed rule, named by its identifier, leaves its part alone**: Trailing whitespace, tabs, and the end of a
document are each covered by a comment that disables the rule by its identifier. The text is left as written, no edit and
no problem results, and the same content without the comment is changed (a control).

**A line-scoped suppression on the last line keeps the end of the document**: A document whose last content line carries
a comment that disables `final-newline` for that line, with and without trailing blank lines and by identifier or alias,
is left as written, with no edit and no problem.

**A suppressed rule, named by an alias, leaves its part alone**: Trailing whitespace, tabs, and the end of a document are
each covered by a comment that disables the rule by its legacy number. The text is left as written, no edit and no
problem results, and the same content without the comment is changed (a control).

**A blank-line rule is not applied where a region disables it**: A run of blank lines in a region that disables
`max-blank-lines` is formatted. The run is unchanged. A run in a region that disables `trailing-whitespace` loses its
surplus lines and keeps the whitespace of the line that stays.

**Each edit is reported once per rule and line at its place**: A document `a<tab>b<tab>c`, `d` followed by a space, and
`e` without a final line ending is formatted. Three problems are reported in order of position: `hard-tabs` at line 1,
column 2 (one problem for the two tabs), `trailing-whitespace` at line 2, column 2, and `final-newline` at line 3,
column 2. Each is of kind formatting, has the path of the file, and has the description of the rule as its message. A
document that ends in a closed comment without a final line ending is formatted as well: the line ending is added, and one
`final-newline` problem is reported on the last line.

**An edit of a configurable rule is not reported (defensive, no requirement)**: A paragraph is formatted by the replacing
test double, whose rule is configurable. The text changes and no problem is reported.

**A claimed block is edited by its formatter only**: A paragraph with a double space and trailing spaces, accepted by
the test double, is formatted. The replacements of the double formatter are applied, and the trailing whitespace is not
removed by a pass, because the formatter claimed the lines.

**A claimed block stays claimed where the rule is suppressed**: The same paragraph in a region that disables the
formatter's rule is formatted. It is unchanged, including its trailing spaces.

**The helper types answer for their inputs**: `LineLayout` is asked for line counts, extents (including the line after
the last, which exists only after a final line feed), blank lines, tab stops,
and grid-border lookalikes; `InlineMap` for the plain-text, protected, and break offsets of a paragraph with code
and breaks, of a link, of no inline content, and of a tree built with a shift; `LeafWhitespace` for the tabs of a text
(also from a stretch that starts in text, where a tab after a code span counts) and for lines that must stay;
`LineOwnership` for the classes and the last content line of the lines of a mixed document, an indented block that is
followed by whitespace lines of several widths (also inside a list), an unclosed fence in a list, and for every kind
of HTML block whether its trailing blank lines are content; `FrontMatterGuard` for documents that could and could
not open front matter. Each answer is the documented one, and null arguments are rejected.

##### Boundary conditions

**A document without line terminators never violates the line ending rule**: A file `abc` without a terminator is
formatted once with a configuration that asks for LF and once with one that asks for CRLF. Neither result has a
problem.

**Equally frequent line endings target LF**: A file with one CRLF terminator and one LF terminator is formatted with a
configuration that has no line ending. The line ending of the result is LF, and one problem reads `Mixed line endings
(1 CRLF, 1 LF); expected LF`.

**A file without terminators and without a configured line ending targets LF**: The file `abc` is formatted with a
configuration that has no line ending. The line ending of the result is LF and no problem is reported.

**A document that is unsafe to rewrite is not touched**: A CRLF file with a line that looks like the border of an
invalid grid table is flagged as unsafe to rewrite. It is formatted with a configuration that asks for LF. The result
has no edit (one test) and no problem (another test), and its line ending is the majority ending of the file, which is
CRLF.

**A formatter list of null means no formatters**: A `DocumentFormatter` is created with a null list and formats a
document that has a double space. The call works and proposes no edit.

##### Error handling

**Conflicting edits of a formatter are refused**: A formatter with an `OverlappingEditsFormatter` formats a document.
The call throws `InvalidOperationException`, so the defect is not resolved silently.

**A null element in the formatter list is rejected**: A `DocumentFormatter` is created with a list that holds a null
element. The constructor throws `ArgumentNullException`.

**An exception of a block formatter reaches the caller unchanged**: A `DocumentFormatter` with a formatter that
throws `InvalidOperationException` (message `The formatter failed on purpose`) formats a one-paragraph document. The
call throws that same exception type with that same message, so nothing is caught or wrapped.

**Later changes of the caller's list are ignored**: A formatter is built from a list with one `ReplacingFormatter`
that writes a single space. The caller then clears the list and adds a formatter that writes an underscore. The
result of formatting still uses the original formatter, so `a  b` becomes `a b`.

**A missing document or configuration is rejected**: `Format` is called with a null document, and then with a null
configuration. Each call throws `ArgumentNullException`.

**A format context without a document or configuration is rejected**: A null document and a null configuration each
throw `ArgumentNullException`. A valid context keeps both values.

**A format result with invalid parts is rejected**: A default list of edits, a list that holds a default edit, a default
list of findings, a null finding, and a line ending that is not defined are each rejected, by a test of its own: the
first three throw `ArgumentException`, a null finding `ArgumentNullException`, and the line ending
`ArgumentOutOfRangeException`. A valid result keeps its edits, line ending, and findings.
