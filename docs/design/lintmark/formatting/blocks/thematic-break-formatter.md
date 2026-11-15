#### ThematicBreakFormatter

![Blocks Structure](BlocksView.svg)

##### Purpose

`ThematicBreakFormatter` proposes the edits that write each thematic break as three characters of the configured style
(`---`, `***`, or `___`), with no spaces between them and no trailing whitespace. It enforces `thematic-break` and
`trailing-whitespace` on the line of the break. Indentation and container prefix stay where they are.

##### Data Model

`ThematicBreakFormatter` is an internal sealed class without fields.

- `RuleIds`: `thematic-break` and `trailing-whitespace`.
- *Accepted blocks*: a block of kind `ThematicBreak` whose node is a Markdig `ThematicBreakBlock`.

Invariants: every edit lies on the line of the break; the break edit and the trailing whitespace edit are disjoint;
every edit carries the identifier of the rule it enforces.

##### Key Methods

**CanFormat**: Returns `true` for a thematic break and `false` for every other block.

**Format**: Adds the edits of one break (`FormatContext context`, `BlockInfo block`, `EditSet edits`). The block must
be accepted by `CanFormat` (not verified). The break characters run from the start of the block to the last character
equal to the first; a line with any other non-blank character produces no edit. The target is read from
`LintMarkConfig.ThematicBreak`. The break is replaced by the target unless it already equals it or `IsGuarded` says
the new form would be read differently. Trailing whitespace is removed in every case, guarded or not.

`IsGuarded` keeps the break as written when:

- the line holds a list marker of the target character before the break (`- ***` would become `- ---`, a break of its
  own);
- the target is `---` and any of these holds: the line starts with a plus sign (`+ ---` looks like a grid table
  border); the nearest non-blank line above looks like a grid table border and is a paragraph line; the break is the
  first line of the document, or the document could open front matter (`FrontMatterGuard.CouldOpenFrontMatter`); or
  text may stand directly above the break, so that the dashes would underline it.

A break that directly follows a heading, code block, another break, list, quote, or table is never guarded for the last
reason, because the blank-line rules put a blank line there anyway. Every other block above it (paragraph, HTML, link
reference definition, front matter, footnote, math, grid table) counts as text, so the guard is conservative. The
previous sibling is found in the flattened block list (`PreviousSibling`), because the parser's tree groups link
reference definitions at the document root. The decision depends only on the source, never on edits of other rules in
the same run, so a second run decides the same.

##### Error Handling

A null argument throws `ArgumentNullException`. A line that does not have the shape described produces no edit. An
edit that conflicts with another throws `InvalidOperationException` from `EditSet.Add`; that is a defect, and
`LintMarkEngine` reports it as a `file-format-error` for the file. Nothing is caught here.

##### Dependencies

- **IBlockFormatter**, **FormatContext**, **LineLayout**, and **FrontMatterGuard** (`DocumentFormatter` unit) - the
  contract, the settings, line arithmetic (including the grid border and plus sign tests), and the front matter guard.
- **EditSet** and **TextEdit** (`EditSet` unit) - the edits.
- **BlockInfo**, **BlockKind**, and **ParsedDocument** (`DocumentParser` unit) - the break, the block list, and the
  source.
- **LintMarkConfig** and **ThematicBreakStyle** (`LintMarkConfig` unit) - the `thematic-break` setting.
- **RuleCatalog** (`Linting` subsystem) - the rule identifiers.
- **Markdig** (OTS) - `ThematicBreakBlock`.

##### Callers

- **DocumentFormatter** - calls `CanFormat` for every block and `Format` for the accepted blocks that are not
  suppressed.
