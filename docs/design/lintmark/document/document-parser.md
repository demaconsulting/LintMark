### DocumentParser

![Document Structure](DocumentView.svg)

#### Purpose

`DocumentParser` parses a `SourceDocument` with LintMark's fixed Markdown pipeline and turns Markdig's tree into a
flattened, source-ordered block list whose ranges can be trusted. It also owns the grid table fallback and builds the
`SuppressionMap` and the `ReferenceIndex` from the same parse. It is the only unit that touches Markdig's block model.

#### Data Model

- **ParsedDocument** (record): `Source`, `Ast` (Markdig `MarkdownDocument`), `Blocks` (`ImmutableArray<BlockInfo>`),
  `Suppressions`, `References` and `GridFallback` (`GridTableFallback?`). `IsFormatUnsafe` is true exactly when
  `GridFallback` is not null.
- **BlockInfo** (record): `Node` (Markdig block; null only for a footnote found by line scan), `Kind` (`BlockKind`),
  `Start` and `EndInclusive` (offsets; the end is the last visible character, never a line feed), `Depth` and `Parent`.
- **BlockKind**: Heading, Paragraph, FencedCode, UnclosedFence, IndentedCode, HtmlBlock, ThematicBreak, BlockQuote,
  List, ListItem, Table, GridTable, FrontMatter, LinkReferenceDefinition, Footnote, UnreferencedFootnote, MathBlock, Other.
- **GridTableFallback** (record struct): 1-based `Line` and `Column` of the first line that looks like a grid table
  border but is not part of a valid table (1:1 when it cannot be located).
- **LeafLine** (record): `Line`, `LineStart`, `ContentStart`, `ContentEnd`, `IsLazy` for one line of a leaf block.
- **ReferenceIndex** and **ReferenceDefinition**: `Definitions` in source order (duplicates included) and a
  first-wins lookup by normalized label. A definition has `Label`, `NormalizedLabel`, `Url`, `Title`, `Start`,
  `EndInclusive` and `Line`.

Invariants of `Blocks`: sorted by start (an enclosing block before its children), siblings do not overlap, each block
lies inside its parent. These hold by construction for computed ranges and by the nesting check for Markdig's ranges.

#### Key Methods

- `Parse(source)`: parses `source.Text` with the shared pipeline (never trivia tracking), flattens the tree, checks
  nesting, falls back if needed, then builds suppressions and references. Pipeline (a process constant, not
  configurable): precise source locations, YAML front matter, GFM pipe tables, grid tables, footnotes, task lists,
  auto links, strikethrough only, alert blocks and math. Auto identifiers, generic attributes, advanced extensions and
  pipe table column width inference are excluded because they inject nodes or break equivalence. YAML front matter
  (only at the start of the file) becomes a `FrontMatter` block and stays verbatim text.
- Flattener (private): walks the tree and drops synthetic shells (definition and footnote groups, empty-span nodes).
  It classifies each node, clamps ranges, and trims trailing line feeds. An unclosed fence extends to the end of its
  parent (or file). A footnote start moves back to its `[^label]:` marker. A definition whose title was rejected ends
  at its destination. Parents that end before their last child are extended (reverse sweep). Link reference
  definitions, which Markdig keeps at the root, get their parent from containment (sweep with a stack of open
  containers). A paragraph or setext heading that begins with definitions starts after them.
- Footnote scan (private): footnote definitions with no node (unreferenced, repeated label) are found by a regex
  line scan for `[^label]:` that lies in no leaf or footnote block. They become `UnreferencedFootnote`. The range ends
  before any block on the marker line, otherwise extends over following uncovered lines.
- `NestsCorrectly(blocks)`: sweeps with a stack of open blocks; the innermost open block must be the parent and contain
  the block.
- Grid table fallback: the grid table parser leaves damaged blocks for a table-looking line that is not a valid table.
  When nesting fails, `LocateOffendingGridLine` scans lines (skipping valid grid tables, code, HTML, math and front
  matter) for the shape of a grid border and records its position. The text is then parsed once more with an
  otherwise identical pipeline without grid tables. The result is marked format-unsafe, because valid grid tables may
  have turned into paragraphs. Linting uses the second reading; the formatter never rewrites it, and the engine
  reports a lint-only `grid-table-invalid` finding (unless disabled or suppressed). The fallback is attempted once.
- `GetLines(document, leaf)`: rebuilds the lines of a paragraph or heading (Markdig releases them). The first line
  starts at the block start. For a continuation line the content start is the first inline element on that line when
  only quote markers and blanks precede it, otherwise one quote marker per enclosing quote and blanks are stripped. A
  line is lazy when its prefix is shorter than `GetContinuationPrefix`.
- `GetContinuationPrefix` and `IsLongerPrefix`: the expected container prefix is the text before the block with all
  but `>` and blanks replaced by spaces; a footnote marker becomes a four-space indent. Widths expand tabs to
  multiples of four after dropping the optional space after each quote marker.
- `ReferenceIndex.Build` and `TryGet`: take definitions from `LinkReferenceDefinition` blocks (quotes and list items
  included, footnotes excluded). Labels are normalized by collapsing whitespace and lower-casing. Only definitions are
  indexed; reference usage is evaluated by lint rules through `TryGet` and `NormalizeLabel`.

#### Error Handling

Markdig exceptions are not caught; the engine reports them as parse failures. A null argument throws
`ArgumentNullException`, and a block that is not a paragraph or heading of the document is a precondition violation that
is not verified. Out-of-range or inverted Markdig spans are clamped or skipped. The only recovered condition is a
nesting failure, handled by the grid table fallback; a document that still does not nest is not detected further.

#### Dependencies

Markdig (see the OTS design *Markdig*), `SourceDocument` (text and line table), `SuppressionMap` (built here), and
`RuleCatalog` of the `Linting` subsystem (passed to `SuppressionMap` as the rule name resolver).

#### Callers

`LintMarkEngine` (`Parse`, `GridFallback` reporting), `DocumentFormatter` (`Parse`, `IsFormatUnsafe`, re-parsing after
edits), the `Linting` rules (`Blocks`, `Ast`, `References`), and the block and inline formatters (`GetLines`,
`GetContinuationPrefix`, `IsLongerPrefix`, `References`).
