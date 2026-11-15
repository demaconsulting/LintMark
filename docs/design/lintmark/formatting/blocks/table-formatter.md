#### TableFormatter

![Blocks Structure](BlocksView.svg)

##### Purpose

`TableFormatter` proposes the edits that write each GFM pipe table in canonical layout: a pipe at both ends of every
line, one blank inside each pipe, and a minimal delimiter row (`compact`, the default), or every column padded to its
widest cell (`aligned`, opt-in). Only the blanks around cells, the delimiter row, and the end pipes change; the text of
every cell is written back byte for byte. It enforces `table-pipe-style`, `table-column-style`, and
`trailing-whitespace`. The supporting types `PipeTable`, `PipeTableRow`, and `TableAlignment` are described here.

##### Data Model

`TableFormatter` is an internal sealed class without fields.

- `RuleIds`: `table-pipe-style`, `table-column-style`, and `trailing-whitespace`.
- *Accepted blocks*: a block of kind `Table` whose node is a Markdig `Table`. A Pandoc grid table has kind `GridTable`
  and is never accepted.

**PipeTable** (sealed class, immutable) is the table read from the source: `Rows` (an array of `PipeTableRow`; index 0
is the header, index 1 the delimiter row), `Alignments` (one `TableAlignment` per column of the delimiter row), and
`ColumnCount` (the cells of the header). `TryParse(LineLayout, BlockInfo)` builds it; `ComposeCompact()` and
`ComposeAligned()` write the two layouts as the content of every line without its prefix.

**PipeTableRow** (sealed record) holds `Line`, `Prefix` (quote markers, list markers, indentation, never touched),
`ContentStart`, `ContentEnd`, `LineEnd`, `Cells` (trimmed text without the delimiting pipes), `HasLeadingPipe`, and
`HasTrailingPipe`; `HasNoPipe` is true for a body line of text with no pipe, which the parser reads as a one-cell row.
**TableAlignment** is `None`, `Left`, `Center`, or `Right`, as the colons of the delimiter row say.

##### Key Methods

**CanFormat**: Returns `true` for a pipe table and `false` for a grid table and every other block.

**Format**: Adds one edit for every line that changes (`FormatContext context`, `BlockInfo block`, `EditSet edits`).
The block must be accepted by `CanFormat` (not verified).

1. The table is left as written when its first line is in a footnote or protected, or `PipeTable.TryParse` returns
   `null`.
2. The layout is `ComposeCompact()`. When `LintMarkConfig.Tables` is `TableStyle.Aligned`, `ComposeAligned()` is used
   instead if every line, with its prefix width (`PrefixWidth`, tab-aware) and `TextWidth.Of`, fits the line length, or
   when the line length is 0. A table too wide to align falls back to compact; it stays too wide only if even compact
   is too wide, which is the lint-only rule `table-too-wide` of the `Linting` subsystem. The formatter never shortens a
   row beyond the compact layout.
3. For each row whose content (from `ContentStart` to `LineEnd`, so trailing blanks go too) differs from the layout, one
   `TextEdit` replaces it. The rule is `trailing-whitespace` when only the end of the line changes, `table-pipe-style`
   when the line lacks its leading or trailing pipe, and `table-column-style` otherwise. Edits are added after all
   are proposed.

**TryParse** returns `null` (so the table is left as written) when the block is not a pipe table, it has fewer than two
lines, a line cannot be read, the delimiter row is not dashes with optional colons, its cell count differs
from the header, or the reading disagrees with the parser (`AgreesWithParser`: column count, alignment, row count, and
the text of every non-empty cell the parser holds). A line is split at every pipe not escaped by a backslash, including
pipes in code spans, as GFM does. A pipe after two backslashes (parsers disagree) and a tab inside a cell make the line
unreadable. The first and last pipe of the content are delimiters, not cell boundaries.

**Layouts.** Compact joins cells with one blank inside each pipe (a single blank for an empty cell) and writes the
delimiter cells with three dashes and the colons of the alignment, so a row depends on its own cells only and an edit
to one row never changes another. Aligned pads each column to its widest cell, at least three columns, measured in
terminal columns on the text as written (`\|` counts as two). Rows with more or fewer cells than the header are written
with the cells they have; inventing or dropping content is not the formatter's business (the lint-only
`table-column-count` rule reports them). A body line without a pipe is written as it is. Both layouts are fixed points:
a second run reads the same cells and writes the same lines.

##### Error Handling

A null argument throws `ArgumentNullException`. A table that cannot be read with certainty, or that lies in a
footnote, produces no edit and no report. An edit that conflicts with another throws `InvalidOperationException` from
`EditSet.Add`; that is a defect, and `LintMarkEngine` reports it as a `file-format-error`. Nothing is caught here.

##### Dependencies

- **IBlockFormatter**, **FormatContext**, **LineLayout**, and **LineOwnership** (`DocumentFormatter` unit) - the
  contract, the settings, line arithmetic, and the footnote and protected lines.
- **EditSet** and **TextEdit** (`EditSet` unit) - the edits.
- **BlockInfo**, **BlockKind**, and **ParsedDocument** (`DocumentParser` unit) - the table block.
- **TextWidth** (`TextWidth` unit, `Document` subsystem) - display width of cells and lines.
- **LintMarkConfig** and **TableStyle** (`LintMarkConfig` unit) - the `tables` and `line-length` settings.
- **RuleCatalog** (`Linting` subsystem) - the rule identifiers.
- **Markdig** (OTS) - `Table`, `TableRow`, and `TableCell` for the agreement check.

##### Callers

- **DocumentFormatter** - calls `CanFormat` for every block and `Format` for tables that are not suppressed, and again
  for the tables whose cells changed when links are converted to reference links.
- **LintRules** (`Linting` subsystem) - `TableRules` uses `PipeTable.TryParse`, `ComposeCompact`, and
  `TableFormatter.PrefixWidth` for `table-column-count` and `table-too-wide`.
