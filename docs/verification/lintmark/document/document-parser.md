### DocumentParser

#### Verification Approach

`DocumentParser` is verified with unit tests in `DocumentParserTests.cs`. The real Markdown library and the
real `SourceDocument` are used, because the unit exists to compensate for the library's behavior; mocking
the parser would remove what is being tested. Most tests build a `SourceDocument` from a small text with
`FromText` and assert on the block list: kind, effective range (checked as the exact source text of the
range), depth, and parent. There is one fixture for each behavior listed in *Parser Behaviors Compensated* of
the `DocumentParser` design, plus fixtures for the dialect and for the classification of each construct. The
`SuppressionMap` and `ReferenceIndex` that `Parse` builds are only checked for being populated here; their own
tests verify their content. Some assertions also run the span-tiling check of the subsystem tests
(`SpanTilingAssertions`, test support code described in the `Document` subsystem verification) on a small
fixture. The structure over a large corpus is verified separately at subsystem level.

The scenarios about the grid table fallback check the two members that a fallback adds to the result,
`GridFallback` (the 1-based line and column of the offending line, or null) and `IsFormatUnsafe` (true exactly
when the location is present). They use small documents in which the offending line is known by construction, and
assert on the exact line and column. One scenario parses the generated adversarial documents of the test support
class `RandomDocuments` (fixed seed 20260511, 500 documents per family) and checks every reported location with a
private helper: it must be line 1, column 1 (damage known but not located), or the position of the first plus sign
of a border-shaped line, preceded only by blanks and quote markers and followed only by border characters; the
scenario also requires that at least one generated document takes the fallback, so that it cannot pass vacuously.
No double is used.

The scenarios for `GetLines` and `GetContinuationPrefix` are supporting verification: the line geometry has no
requirement and no production caller yet, and its tests protect the behavior until the first consumer exists.

Line-ending style and the byte order mark are handled by `SourceDocument`, not by the parser, so the tests that
need bytes (the same blocks for LF and CRLF text, and offsets that ignore a byte order mark) are in the `Document`
subsystem and `SourceDocument` verification. The parser is given only LF-normalized text.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Blocks are in source order with enclosing blocks first and no group shells or empty-range nodes.
- Each fixture for a parser behavior compensated by the unit shows the corrected range, classification,
  depth, or parent.
- Every footnote definition is reported as a block that starts at its marker, whether or not it is referenced
  and wherever it stands, on any line of the document (the line-start anchor of the marker expression applies
  to each line); footnote-like text in code is not reported.
- A link reference definition reports its enclosing container, and the paragraph or setext heading that follows
  a definition does not overlap it.
- A footnote whose body ends with an indented HTML block ends with that block, so no block lies outside its parent.
- A dropped footnote ends before a block that starts on its marker line (a definition or another footnote
  marker) and before a definition that follows it behind a container prefix, so no two blocks overlap; a
  referenced footnote keeps such a definition as a child.
- A definition whose next-line title is rejected ends at its destination and the paragraph with the text does
  not overlap it; a valid title (including an empty one) is kept.
- A valid grid table next to such a footnote stays a grid table (no spurious fallback).
- A line that looks like a grid table row but is not a valid grid table leaves blocks that do not overlap and
  lie inside their parents; a valid grid table is still a grid table; a document with a valid and an invalid
  grid table is read without grid tables (the deliberate fallback of the design).
- The fallback is never silent: a document that took it reports the line and column of the first border-shaped
  line that is not part of a valid table (also behind quote markers, list markers, and indentation, with or without
  alignment colons and a trailing plus sign), never the line of a valid table beside it, and is format-unsafe; the
  location is line 1, column 1 when the damage is known but no line can be named; border-looking lines inside fenced
  code, indented code, and HTML blocks, valid grid tables, plain documents, and the empty document leave the document
  not format-unsafe. Locations compare by value, keep the line and column they were created with, and are rejected
  when either is below one.
- Every location that the adversarial documents produce is line 1, column 1 or a border character on a
  border-shaped line.
- A very large document is parsed within a generous time bound (a check against quadratic behavior).
- Concurrent calls give identical results.
- Invalid arguments are rejected with the documented exception.

#### Test Scenarios

##### Normal operation

**Block list cannot be changed through a cast**: `ParsedDocument.Blocks` is an immutable array; viewed as
`IList<BlockInfo>`, every attempted change throws `NotSupportedException`.

**Unreferenced footnotes are found on any line**: Documents whose only footnote definition is unreferenced and
stands on the first line, after a paragraph, directly after text, indented by three spaces, or far down the
document report it with its exact range each time.

**Blocks of a simple document are listed in source order**: A document with a heading, paragraphs, and a list
returns its blocks ordered by position, enclosing blocks before the blocks they contain.

**Every block construct of the dialect is classified with its exact range**: A document holding an alert, a math
block, a closed fenced code block, an HTML block, and a task list returns the expected kind and the exact source
text for each, and no unclosed fence is reported.

**Pipe table is a table whose cell keeps a pipe inside a code span**: A pipe table is a `Table`, and a code span
that contains an escaped pipe stays in its cell.

**Grid table is classified as a grid table**: A table written with `+` borders is `GridTable`, not `Table`.

**Front matter is one verbatim block**: A leading YAML section is a single `FrontMatter` block.

**Inline constructs are recognized**: A task list item, a bare-URL automatic link, strikethrough, and inline
math are found in the syntax tree with exact spans.

**Headings carry no generated identifiers**: Headings get no generated attributes, and no synthetic link
reference definitions are added.

**Nested containers report depth and parents**: A quote that contains a list whose item holds a paragraph
reports depths 0, 1, 2, and 3, and the parent of each block is the node of the block that encloses it.

**Reference definition inside a block quote reports its container**: A definition in a quote has the quote as
its parent and the next depth, although the parser keeps it at the document root; a definition outside any
container has no parent.

**Reference definition inside a list item reports its container**: A definition in a list item has the item as
its parent and the next depth.

**Reference definition in nested containers reports the innermost container**: Definitions in a quote that
contains a list whose item holds a definition report the innermost enclosing block.

**Definitions and footnotes appear in source order with their own ranges**: A document that mixes link
reference definitions, referenced footnotes, and unreferenced footnotes lists them in source order, each with
its own exact range and no group shell.

**Referenced footnote range includes its label marker**: Footnotes written as `[^n]: Note`, with the body on the
next line, with up to three spaces of indentation, and with several paragraphs each start at the `[` of the
marker and end on the last visible character.

**Footnote definition in a list item reports its container**: A footnote definition inside a list item is
reported with that item as its parent and the next depth.

**Footnote definition inside a block quote or list item is a footnote**: Such a definition is classified
`UnreferencedFootnote`, keeps the parser's definition node, reports the enclosing container, and is not a
`LinkReferenceDefinition`, so the reference index does not list it.

**Paragraph after a definition does not overlap it**: A paragraph that directly follows a definition starts at its
own text, and the definition keeps its own range.

**Paragraph after a definition in a block quote does not overlap it**: A paragraph that directly follows a
definition in a quote starts at its own text, excluding the quote marker, and nests next to the definition.

**Paragraph after stacked definitions in a list item does not overlap them**: A paragraph that follows two
stacked definitions in a list item starts at its own text, excluding the item's indentation, and nests next to
the definitions.

**Setext heading after a definition does not overlap it**: A setext heading that directly follows a definition
starts at its own first line, the definition keeps its own range, and the heading's lines are rebuilt as the text
line and the underline, numbered from the heading's first line.

**Setext heading after a definition in a container does not overlap it**: The same for a definition in a quote
and in a list item; the heading nests next to the definition and its lines exclude the container prefix.

**Unreferenced footnote is reported with its range**: A footnote definition that nothing refers to is an
`UnreferencedFootnote` block with a null node, starting at its marker.

**Repeated footnote label is reported**: When two definitions use the same label, or labels that differ only in
letter case, the second one is also reported as an `UnreferencedFootnote` block.

**Adjacent unreferenced footnotes are both reported**: Two definitions with the same label on consecutive lines
give two blocks, each with its own range.

**Footnote that ends with an indented HTML block contains it**: A referenced footnote whose body is followed by an
indented HTML block (alone, followed by a top-level paragraph, and after a nested list) ends on the last visible
character of the block, the block is a child at the next depth, and the span-tiling check reports no violation.

**Parse populates suppressions and references**: A document with a suppression comment and a link reference
definition produces a suppression map and a reference index that contain them.

**A suppression comment names a rule by its legacy number**: Documents with a comment that disables `MD013` and one that
disables `trailing-whitespace` (an identifier) are parsed. The paragraph after each is suppressed for the canonical rule
(`line-length` and `trailing-whitespace`), and an unrelated rule is not suppressed.

**A disable-next-line comment with a legacy number suppresses the next line only**: A document with a
`markdownlint-disable-next-line MD009` comment is parsed. The next line is suppressed for `trailing-whitespace` and the
line after it is not.

**A name that is not a registered rule suppresses nothing registered**: A document with a comment that disables
`MD999` is parsed. The name as written is suppressed, and `trailing-whitespace` is not.

**Dropped footnote followed by a definition in a container does not overlap it**: A footnote that nothing refers
to, followed on the next line by a definition behind a list marker, a quote marker, or an indent (including a
definition after a nested list that the footnote swallows), gives two `UnreferencedFootnote` blocks, each with
its own range, and the nesting check reports no violation.

**Dropped footnote with a definition on its marker line ends before it**: A footnote that nothing refers to (or
that repeats a label), with a definition behind its marker on the same line (also after a tab, with no gap,
behind an indent, with the destination on the next line, and in several footnotes in a row), gives the footnote
as its marker only and the definition as a following block with its own range; a marker followed only by another
marker without a destination stays one block. The same documents as CRLF bytes give the same blocks.

**Dropped footnote before another footnote marker gives two blocks**: `[^m]: [^n]: x` gives two top-level
`UnreferencedFootnote` blocks, `[^m]:` and `[^n]: x`, and the nesting check reports no violation. When a
paragraph refers to the outer label, the inner definition is a child of the footnote one level deeper.

**Definition with a rejected next-line title ends at its destination**: A definition followed by a line that
starts like a title and has text after it (at top level, in a quote, in a list item, and for a footnote-shaped
definition) ends at its destination, the text is a paragraph that does not overlap it, and a valid or empty
title is kept in the definition.

**Valid grid table beside such a footnote is kept**: A valid grid table followed by a dropped footnote with a
definition on its marker line gives a `GridTable`, the footnote, and the definition.

**Fallback document with such a footnote nests**: A document with a valid and an invalid grid table (read
without grid tables) and a dropped footnote with a definition on its marker line gives the footnote as its
marker, the definition after it, and the nesting check reports no violation.

**Invalid grid table leaves paragraphs and headings that do not overlap**: A grid-table-looking line directly
after a paragraph, before a heading, inside a block quote, and inside a list item is read as ordinary text; the
exact blocks are listed and the nesting check reports no violation.

**Valid grid table is still a grid table**: A document with only a valid grid table has exactly one `GridTable`
block (a regression guard for the fallback).

**Document with a valid and an invalid grid table is read without grid tables**: A valid grid table followed
by a grid-table-looking line that is not valid reports no `GridTable` block, and the blocks nest. This
documents the deliberate cost of the fallback; the scenarios below verify that it is reported.

**Invalid grid-table-looking line marks the document format-unsafe at that line**: Documents with a border-shaped
line that is not a valid grid table are parsed, and the result is format-unsafe with the location of the line and the
column of its first plus sign: after a paragraph (2:1), after a valid table (5:1), alone after text (3:1), indented
by two spaces (2:3, and 3:3 after a blank line), inside a block quote (2:3) and directly after a quote without the
quote marker (2:1), between list items (2:1), after a heading and a paragraph (4:1), with alignment colons (2:1), and
inside a list item after a valid table in the same item (6:3).

**The location is the invalid line and not a line of a valid table**: A valid grid table (also with a header
separator made of equal signs) followed by a paragraph and an invalid border gives the line of the invalid
border (5, 6, and 8), column 1.

**Valid grid table only is not marked**: A document that holds only a valid grid table has one `GridTable` block,
no location, and is not format-unsafe.

**Documents without grid-table-looking lines are not marked**: Plain text, a document with headings, lists, a quote,
and a pipe table, and the empty document are not format-unsafe and have no location; the block list is empty exactly
when the text is empty.

**A fallback document still produces nested blocks**: A document with a heading, a paragraph that holds an invalid
border, and a list gives the kinds heading, paragraph, list, list item, paragraph in that order, the paragraph text is
`para` and the border line, the document is format-unsafe, and the span-tiling check reports no violation.

**Grid table locations compare by value**: Two locations with equal line and column are equal and have equal hash
codes; a different line or column makes them different.

**Concurrent calls give identical results**: Several different documents (with lists, quotes, footnotes,
definitions in containers, an unclosed fence, tables, a document that takes the grid table fallback, and a valid
grid table next to a dropped footnote that holds a definition) are parsed on many threads; each result equals the
sequential result of its document.

##### Boundary conditions

**Border-looking line inside code or HTML is ordinary text and does not mark the document**: A line `+---+---+` inside
a backtick fence (also after a paragraph), inside a tilde fence, inside indented code, and inside an HTML block is
not located: the document is not format-unsafe, has no location, and the literal block is a block of its own.

**Border without a trailing plus sign is located like any other border**: `+---` and `+---+---` after a paragraph are
located at 2:1 and the document is format-unsafe.

**An offending line that cannot be located is reported at line 1, column 1**: A grid table opener without a closing
border, followed by text, is read by the first reading as a grid table block, so no remaining line can be named; the
document is format-unsafe and the location is 1:1.

**Locations of the adversarial documents are plausible**: Every generated adversarial document that takes the
fallback reports 1:1 or the first plus sign of a border-shaped line, preceded only by blanks and quote markers; at
least one document takes the fallback, and the failure message lists the first escaped documents.

**Empty document has no blocks**: An empty document parses to an empty block list.

**Unclosed fence extends to the end of the document**: A fence without a closing fence is `UnclosedFence`, and
its range runs to the end of the text.

**Unclosed fence in a block quote ends with the quote**: The range ends where the quote ends, not at the end of
the file.

**Unclosed fence in a list item ends with the item**: The range ends where the list item ends.

**Unclosed math block extends to the end of the document**: A math block without a closing delimiter is
`UnclosedFence`, and its range runs to the end of the text.

**Unclosed math block in a list item ends with the item**: The range ends where the list item ends.

**List followed by a blank line ends on its last visible character**: The range of a list and of its item ends
on the last character of text, not on the line feed that the parser includes.

**Indented code followed by a blank line ends on its last visible character**: The same for an indented code
block.

**Footnote-like text in code is not reported**: A line that looks like a footnote definition inside a code block
is not a block of its own.

**Indented footnote-like line inside a footnote is content**: An indented line that begins like a definition and
follows a blank line inside a footnote is part of that footnote and is not reported as a new definition.

**Greater-than sign that is paragraph text after a definition is kept**: An indented line after a definition that
begins with `>` and is not in a block quote starts the paragraph at the `>`.

**Tab-indented list, code, and text have exact ranges**: A list with a tab-indented nested item, a paragraph that
contains a tab, and a tab-indented code block report the exact kind, depth, and source text, using a tab stop of
four.

**Large document is parsed within a time bound**: A generated document of 20000 groups (a definition with text, an
unreferenced footnote, a quote, and a list item, plus a referenced footnote in every fiftieth group; 161200
blocks, about 0.8 MB) is parsed; every construct is found, the blocks are in source order, and parsing takes under
ten seconds. The time is the supporting check for the linear-time design (see *Complexity* in the design); a scan of
all blocks for each definition or footnote needs about a minute for this input. No requirement states a time, so
this scenario has no requirement link.

##### Error handling

**Null source is rejected**: `Parse` with a null source throws `ArgumentNullException`.

**A grid table location keeps a valid position**: The smallest position (line 1, column 1) and another position keep
the line and column they were created with.

**A grid table location below line one or column one is rejected**: A line of zero or a negative line, and a column
of zero or a negative column, each throw `ArgumentOutOfRangeException`.

##### Supporting scenarios for line geometry

These scenarios exercise `GetLines` and `GetContinuationPrefix`, which have no requirement yet.

**Lazy continuation lines are flagged and explicit prefixes are not**: In quotes and lists a continuation line
without the container prefix has `IsLazy` set, and lines with the prefix do not.

**Setext underline lookalike in a lazy line is paragraph text**: A lazy line that looks like a setext underline is
a lazy paragraph line, not an underline.

**Paragraph lines have exact content ranges**: The lines of a paragraph are rebuilt from the source with the
expected content start and end.

**Continuation content in nested containers excludes the prefix**: The content of a continuation line starts
after all quote and list prefixes.

**Continuation inside a code span excludes the quote marker**: A line that begins inside a multi-line code span
has its quote marker stripped.

**Tab-indented continuation starts after the tab**: The content of a tab-indented continuation line starts after
the tab.

**Continuation prefix blanks markers and keeps quote markers**: For `-`, `1.`, `10.`, quoted lists, quotes, and
plain text, the prefix has list markers and numbers replaced by spaces and keeps `>`.

**Setext heading yields text and underline lines**: A setext heading has two lines.

**Indented continuation of a footnote paragraph is not lazy**: A line indented by four spaces under a footnote's
first line is not lazy.

**Unindented continuation of a footnote paragraph is lazy**: A line without the indent is flagged lazy.

**Second paragraph of a footnote excludes its indent**: The content of its lines starts after the four-space
indent, and the lines are not lazy.

**Continuation prefix of a footnote paragraph is four spaces**: The prefix is the four-space body indent whatever
the label length, the indentation of the marker, or the spaces after it.

**Paragraph after a definition returns only its own lines**: The lines start at the paragraph's text, not at the
definitions before it.

**Line helpers reject a block that is not a paragraph or heading**: `GetLines` and `GetContinuationPrefix` throw
`ArgumentException` for a block of any other kind.

**Line helpers reject a block outside the document**: A block whose range lies outside the text makes `GetLines`
and `GetContinuationPrefix` throw `ArgumentException`.

**Line helpers reject null arguments**: A null document or block throws `ArgumentNullException`.
