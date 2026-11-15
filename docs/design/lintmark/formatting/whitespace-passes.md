### WhitespacePasses

![Formatting Structure](FormattingView.svg)

#### Purpose

`WhitespacePasses` holds the document-level rules that apply to everything no block formatter claims: trailing
whitespace and tabs inside text, runs of blank lines, the blank lines that separate headings, fenced code, lists and
tables from their neighbors, and the end of the file. The unit consists of `LeafWhitespace` with its helper `InlineMap`
(`trailing-whitespace`, `hard-tabs`), `BlankLinePass` (`max-blank-lines`), `BlockSpacingPass` (`heading-blank-lines`,
`fence-blank-lines`, `list-blank-lines`, `table-blank-lines`), and `TrailerPass` (`final-newline`). The passes never
touch protected lines and honor suppression comments at block, line, or range granularity.

#### Data Model

All passes are static and stateless; they read a `LineLayout`, a `LineOwnership`, the parsed document
(suppressions), and the configuration, and add `TextEdit`s to an `EditSet`, each tagged with its rule id.

**InlineMap**: Immutable per leaf block, built from the inline tree by `Build(ContainerInline?, shift)`. It stores
sorted spans of plain text, sorted spans that must never be edited, and the line breaks by the offset of their line
feed with a flag that tells a hard break written with spaces from a backslash break. Literal text and breaks are
editable; code, inline HTML, math, autolink URLs, link destinations, titles, footnote references, and task markers
are protected as a whole, and so is the label of a link that refers to a definition (the `[label]` of a reference
link; for a collapsed link the label is its text). The parser reports the bracketed text of an inline link as its
label; that span is not protected for an inline link, so its text is plain text like the text of a reference link,
and link refactoring does not change what is editable. Queries: `IsText`, `IsProtected`, `IsBreakAt`,
`IsSpaceBreakAt`. `Empty` has no text.

#### Key Methods

**LeafWhitespace.AddLineEdits / AddBlockEdits**: For each line of a paragraph or heading, find the trailing run of
blanks (never reaching before the block content on its first line). The run is removed only at a line end that the
parser treats as plain: before a line break or at the block end, and not inside a protected span. A hard break written
with spaces keeps exactly two spaces; a run after a backslash is kept, because removing it would turn a literal
backslash into a break. Lines that look like grid table borders are never edited. For a setext heading the underline
only loses trailing whitespace. Tabs are replaced by spaces up to the next tab stop of four columns (`CollectTabs`),
counted from the start of the line, only in plain text after the first text of the line, so indentation and
container prefixes never change; a tab that is kept still advances the column.

**LeafWhitespace.AddTrailingEdit**: Removes the trailing run of a line without inline structure (table rows, thematic
breaks, definitions, container marker lines); a preceding backslash keeps the run.

**BlankLinePass.Apply**: A run is a maximal sequence of blank lines that no leaf block covers. A run longer than
`MaxBlankLines` loses its surplus lines; the lines that stay are emptied. Runs are left alone when they end the file
(owned by the trailer rule), touch a grid-border-like line, follow a block that keeps its trailing blank lines (unclosed
fence), or follow an empty list item or an item with an empty marker line (the parser ends a list at two blank lines
there, so the count is structure). Inside a list, item, quote, or footnote the surplus is kept (removing a line can
make a list loose) but whitespace is still removed.

**BlockSpacingPass.Apply**: For adjacent top-level blocks that directly follow each other and where one is a heading, a
fenced or indented code block, a list, or a table, insert one blank line, attributed to the first applicable rule
that is not suppressed (heading, fence, list, table). It decides from parsed blocks, never from the look of lines, so a
lazy continuation or a table row is not a boundary. Only block kinds whose end the parser reports reliably are
separated; no line is inserted next to a grid-border-like line, before a block indented by more than three columns or a
tab, after an unclosed fence, between a next-line suppression directive and the line it covers, or where the blank line
would change how the first line of the block is read: before a setext heading that starts with a list marker and
directly follows a link reference definition (the open paragraph makes the marker text), before a list whose first line
is shaped like a thematic break (`- --` is an item after the block of an item, a thematic break after a blank line), and
before a table when the paragraph before it starts like a link reference definition (the parser keeps it as text right
before a table, and reads a definition after a blank line). Blocks inside
containers are neither fixed nor reported.

**TrailerPass.Apply**: Everything after the last line with content is the trailer. With `final-newline` it becomes one
line feed, otherwise it is removed. Left alone: documents without content, trailing blank lines that are block content,
trailers after a non-paragraph in a footnote, and a blank line after a border-shaped last line.

#### Error Handling

The passes detect no errors; any case they cannot judge safely produces no edit. Null arguments throw
`ArgumentNullException`. An edit conflict is a defect of the pass and is reported by `EditSet` as an
`InvalidOperationException`.

#### Dependencies

- **DocumentFormatter** (this subsystem): `LineLayout`, `LineOwnership`, `FormatContext` data.
- **EditSet** (this subsystem): `EditSet` and `TextEdit`.
- **Document** subsystem: `ParsedDocument`, `BlockInfo`, suppressions. **Configuration** subsystem: `LintMarkConfig`.
  **Linting** subsystem: `RuleCatalog` ids. **Markdig** (OTS): the inline tree behind `InlineMap`.

#### Callers

`DocumentFormatter` runs the passes and calls `LeafWhitespace` for unclaimed leaves. `HeadingFormatter` (the `Blocks`
subsystem) reuses `LeafWhitespace` and `InlineMap` so that trailing whitespace and tab stops are treated alike.
