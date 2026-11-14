#### BlockQuoteFormatter

![Blocks Structure](BlocksView.svg)

##### Purpose

`BlockQuoteFormatter` proposes the edits that write block quotes with exactly one space after each quote marker: two to
four spaces after a `>` become one, so `>   text` becomes `> text` and `>  >  text` becomes `> > text`. It enforces the
rule `blockquote-space` and nothing else; the trailing whitespace of the lines stays with the passes of
`DocumentFormatter`.

##### Data Model

`BlockQuoteFormatter` is an internal sealed class without instance data; the constants `MinRun` (two) and `MaxRun` (four)
are the shortest and the longest run of spaces that is collapsed.

- `RuleIds`: an immutable array that holds `blockquote-space`. `DocumentFormatter` leaves a whole quote as written when
  the rule is suppressed anywhere inside it.
- *Accepted blocks*: a block of kind `BlockQuote` whose node is a Markdig `QuoteBlock` and whose parent is not a
  `QuoteBlock`, the outermost quote of a nest. The formatter visits the leaves of the nested quotes itself.
- *Claims*: none. A quote is a container, and `LineOwnership` claims the lines of leaf blocks only, so the passes keep
  editing the text of the paragraphs and headings in the quote. The edits of this unit replace only the run of spaces
  after a marker.
- *Handled leaves*: a paragraph, or an ATX heading, whose every enclosing block up to the document is a block quote.
  Setext headings, tables, code, HTML, math, and anything inside a list item, footnote, or other container are never
  handled: after a marker the spaces are content or structure there (indentation of code, a list indentation, the
  parts of a table or a setext heading).

Invariants: an edit replaces a run of two to four spaces that stands directly after a `>` in the container prefix of a
line with one space; no edit is an insertion; every edit carries the identifier `blockquote-space`.

##### Key Methods

**CanFormat**: Accepts the outermost block quote of a nest.

- *Parameters*: `BlockInfo block`.
- *Returns*: `true` for a `BlockQuote` block with a `QuoteBlock` node whose parent is not a `QuoteBlock`.
- *Errors*: `ArgumentNullException` for a null block.

**Format**: Adds the edits of one quote.

- *Parameters*: `FormatContext context`, `BlockInfo block` (accepted by `CanFormat`), `EditSet edits`.
- *Preconditions*: no argument is null and the block is an outermost block quote.
- *Postconditions*: `edits` holds the edits that collapse the runs of spaces after quote markers on the lines of the
  handled leaves; a canonical quote adds none.

Algorithm: (1) when any line of the quote holds a footnote definition marker (`LineLayout.HasFootnoteDefinitionMarker`,
deliberately wider than the syntax), the whole quote stays as written, because the parser reads the body of a footnote
that stands in a quote inconsistently. (2) The blocks of the document are in source order, so a bisection finds the first
block that starts at the quote; every later block that ends inside the quote is looked at. (3) For each handled leaf the
depth is the number of quotes around it, and each of its lines is taken from `DocumentParser.GetLines`. (4) A line is
skipped when it has no content (the whitespace rule owns it), when its prefix holds a tab, or when the number of `>`
characters in its prefix differs from the depth, which is the case for a lazy continuation line: it has fewer markers
than quotes and no marker to collapse after. The number of markers decides, not the width of the prefix, because the
width depends on the spaces this rule removes. (5) For every `>` in the prefix followed by a run of two to four spaces,
that run is replaced by one space. A run of five or more is left alone (such a line would be indented code), and a run
of zero or one is canonical already.

Why this is safe: at most three spaces beyond the one that belongs to the marker are removed, and a block start needs
fewer than four columns of indentation, so which block the line starts cannot change. The lines of leaves that the
formatter does not handle are not touched at all. A lazy line keeps its text. Because the decision uses markers and run
lengths of the prefix only, a second run finds nothing to change (idempotency), and it does not depend on any edit of
another rule.

##### Error Handling

`ArgumentNullException` for a null argument and `ArgumentException` for a block that is not an outermost block quote. A
line that does not have the shape described above produces no edit. An edit that conflicts with another edit would make
`EditSet.Add` throw `InvalidOperationException`; that is a defect of a formatter and `LintMarkEngine` turns it into a
`file-format-error` for the file. Nothing is caught here.

##### Dependencies

- **IBlockFormatter**, **FormatContext**, **EditSet**, and **TextEdit** (`Formatting` subsystem) - the contract, the
  inputs, and the edits.
- **ParsedDocument**, **BlockInfo**, **BlockKind**, and **DocumentParser** (`Document` subsystem) - the blocks and the
  source lines of a leaf (`GetLines`).
- **RuleCatalog** (`Linting` subsystem) - the rule identifier of the edits.
- **Markdig** (OTS) - `QuoteBlock`, `ParagraphBlock`, and `HeadingBlock`.
- **LineLayout** (supporting type of `DocumentFormatter`, reached through `FormatContext`) - the footnote marker test
  and the line arithmetic.

##### Callers

- **DocumentFormatter** - calls `CanFormat` for every block and `Format` for the accepted blocks that are not
  suppressed.
- **LintMarkEngine** - creates the instance that the production `DocumentFormatter` is configured with.
