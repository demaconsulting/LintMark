#### BlockQuoteFormatter

![Blocks Structure](BlocksView.svg)

##### Purpose

`BlockQuoteFormatter` proposes the edits that write exactly one space after each quote marker on the paragraph and ATX
heading lines of a block quote (`>  >  text` becomes `> > text`). It enforces `blockquote-space`. It accepts the
outermost quote of a nest and handles the leaves of nested quotes itself.

##### Data Model

`BlockQuoteFormatter` is an internal sealed class without fields. The constants `MinRun` (two) and `MaxRun` (four) are
the shortest and longest run of spaces after a marker that is collapsed.

- `RuleIds`: `blockquote-space`.
- *Accepted blocks*: a block of kind `BlockQuote` whose node is a `QuoteBlock` and whose parent is not a `QuoteBlock`.
- *Claims*: none. A quote is a container and `LineOwnership` claims leaf lines only, so the passes keep editing the
  text of the lines. The edits replace only the run of spaces after a marker.
- *Handled leaves*: a paragraph or an ATX heading whose every enclosing block up to the document is a block quote
  (`IsHandledLeaf`). Setext headings, tables, code, HTML, math, and anything inside a list item or footnote are never
  handled: there the spaces after a marker are content or structure.

Invariants: an edit replaces a run of two to four spaces that stands directly after a `>` in the container prefix of a
line with one space; no edit is an insertion; every edit carries the identifier `blockquote-space`.

##### Key Methods

**CanFormat**: Returns `true` for the outermost block quote of a nest.

**Format**: Adds the edits of one quote (`FormatContext context`, `BlockInfo block`, `EditSet edits`). The block must
be accepted by `CanFormat` (not verified). A canonical quote adds no edit.

1. When any line of the quote holds a footnote definition marker (`LineLayout.HasFootnoteDefinitionMarker`), the whole
   quote stays as written, because the parser reads such a body inconsistently.
2. The blocks of the document are in source order, so a bisection finds the first block that starts at the quote; each
   later block that ends inside the quote and is a handled leaf is formatted (`FormatLeaf`).
3. The depth of a leaf is the number of quotes around it; its lines come from `DocumentParser.GetLines`. A line is
   skipped when it has no content (the whitespace rule owns it), when its prefix holds a tab, or when its number of `>`
   characters differs from the depth, which is a lazy continuation line. The number of markers decides, not the width of
   the prefix, because the width depends on the spaces this rule removes.
4. Every `>` in the prefix followed by two to four spaces gets that run replaced by one space. Runs of five or more
   stay (the line would be indented code); runs of zero or one are canonical.

At most three spaces beyond the one the marker owns are removed and a block start needs fewer than four, so which
block the line starts cannot change. The decision uses only the markers and run lengths of the prefix, so a second run
finds nothing to change.

##### Error Handling

A null argument throws `ArgumentNullException`. A line that does not have the shape described produces no edit. An
edit that conflicts with another throws `InvalidOperationException` from `EditSet.Add`; that is a defect, and
`LintMarkEngine` reports it as a `file-format-error` for the file. Nothing is caught here.

##### Dependencies

- **IBlockFormatter**, **FormatContext**, and **LineLayout** (`DocumentFormatter` unit) - the contract, the document,
  and the footnote marker test and line arithmetic.
- **EditSet** and **TextEdit** (`EditSet` unit) - the edits.
- **BlockInfo**, **BlockKind**, **ParsedDocument**, and **DocumentParser** (`DocumentParser` unit) - the blocks and the
  source lines of a leaf (`GetLines`).
- **RuleCatalog** (`Linting` subsystem) - the rule identifier.
- **Markdig** (OTS) - `QuoteBlock`, `ParagraphBlock`, and `HeadingBlock`.

##### Callers

- **DocumentFormatter** - calls `CanFormat` for every block and `Format` for the accepted blocks that are not
  suppressed.
