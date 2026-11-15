## Formatting

![Formatting Structure](FormattingView.svg)

### Overview

The `Formatting` subsystem turns a parsed document into a proposal: the text edits and the line ending that would bring
the document into canonical form, plus the findings that applying the proposal would remove. It never writes a file and
never changes a document; it only describes the change. All text changes are edits against the LF-normalized text of
the document, so text that no edit touches is preserved by construction.

The subsystem has three units and two nested subsystems:

- `DocumentFormatter` orchestrates a run: it decides which lines are owned by which block, lets the passes and the block
  formatters propose edits, plans lists, refactors links and wraps paragraphs on the text the earlier steps produce,
  composes all edits, decides the line ending, and reports findings. Its supporting types are `IBlockFormatter`,
  `FormatContext`, `FormatResult`, `LineLayout`, `LineOwnership`, `RawTextElements`, and `FrontMatterGuard`.
- `WhitespacePasses` holds the rules that apply to everything no block formatter claims: trailing whitespace and tabs
  (`LeafWhitespace` with `InlineMap`), runs of blank lines (`BlankLinePass`), blank lines between blocks
  (`BlockSpacingPass`), and the end of the file (`TrailerPass`).
- `EditSet` is the conflict-checked collection of edits; its supporting types are `TextEdit`, `SourcePatcher`
  (applies edits in one pass) and `EditRebaser` (composes two stages of edits).
- `Blocks` is the nested subsystem of the block formatters (headings, code, thematic breaks, quotes, lists, tables) and
  of the paragraph formatter; see *Blocks Design*.
- `Inline` is the nested subsystem that normalizes inline syntax, tokenizes and wraps text, and converts long links to
  reference links; see *Inline Design*.

### Interfaces

**DocumentFormatter.Format**: Formats a parsed document.

- *Type*: In-process .NET instance method; provided to the `Engine` subsystem.
- *Contract*: takes a `ParsedDocument` and the effective `LintMarkConfig` and returns a `FormatResult`: the ordered,
  conflict-free edits to the LF-normalized text, the line ending to write, and the formatting findings (the
  `line-ending` finding, one finding per rule and line for each edit of an always-applied rule, `link-refactor`,
  `line-length` and `paragraph-wrap` findings, and the lint-only findings of paragraphs that cannot be wrapped).
  Pure: nothing is written and the document is not changed.
- *Constraints*: a format-unsafe document yields no edit and no finding and keeps the majority line ending of the
  file, so rewriting leaves it byte for byte as it was. Two conflicting edits throw `InvalidOperationException`, which
  is a defect of a formatter. One instance serves every worker thread.

**SourcePatcher.Apply**: Applies ordered edits to the text of a document in one pass; provided to `Engine` and used
inside `DocumentFormatter` to read the text that earlier steps produce.

**IBlockFormatter**: The extension point of the subsystem, consumed by `DocumentFormatter` and implemented in `Blocks`.

The subsystem consumes the `Document` subsystem (`ParsedDocument`, `BlockInfo`, `SourceDocument`, `DocumentParser`,
`TextWidth`), the `Configuration` subsystem (`LintMarkConfig`), the `Linting` subsystem (`RuleCatalog`, `Diagnostic`),
and Markdig (OTS) for the syntax tree.

### Design

**Pass order.** `DocumentFormatter.Format` works in stages, each reading the text the previous stages produce:

1. Build the line arithmetic and the line ownership map of the original text.
2. Leaf whitespace and inline normalization, then blank-line runs, block spacing, and the trailer (`WhitespacePasses`,
   `InlineNormalizer`); these only touch lines no block formatter claims.
3. Block formatters, from the last block to the first (headings, code, thematic breaks, quotes, tables).
4. Lists, planned on the text that stages 2 and 3 produce and composed with their edits.
5. Link refactoring on the text produced so far, followed by a new layout of the tables whose cells changed.
6. Paragraph wrapping on the final text.

**Line ownership.** Stages 2 and 3 must never edit the same characters, so `LineOwnership` is the agreement: each line
belongs to at most one leaf block, lines of code, HTML, math, front matter, grid tables, raw-text HTML elements, and
unrecognized blocks are protected, and container lines are marked so that a rule can tell indentation from content.

**Edit composition.** Stages 4 to 6 parse and measure the text that the earlier edits produce, but every result
must be expressed against the original text. `EditRebaser` composes two edit lists after one preparation pass over
the first list, with a binary search per edit of the second, so a document with many edits stays near linear; see
*EditSet Design*. The final list is conflict-free and independent of the order in which edits were added.

**Protection.** A format-unsafe document (grid table fallback) is never formatted. A block whose formatter has a
suppressed rule anywhere inside it stays as written. Every unit declines a change whose result could be read
differently by the parser. Correctness is not proven at run time; it is established by the unit tests and by the
development-time corpus harness.

**Findings.** Linting a file is formatting it and listing the differences, so every edit of an always-applied rule
becomes a finding at the position it changes; configurable rules report their own findings.
