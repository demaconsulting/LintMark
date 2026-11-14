#### ThematicBreakFormatter

![Blocks Structure](BlocksView.svg)

##### Purpose

`ThematicBreakFormatter` proposes the edits that write each thematic break in the canonical form: three characters of
the style of the configuration (dashes by default), with no spaces between them and no trailing whitespace. It enforces
the rule `thematic-break`, and the rule `trailing-whitespace` on the line of the break.

##### Data Model

`ThematicBreakFormatter` is an internal sealed class without fields.

- `RuleIds`: an immutable array of `thematic-break` and `trailing-whitespace`.
- *Accepted blocks*: a block of kind `ThematicBreak` whose node is a Markdig `ThematicBreakBlock`.
- *Target*: `---` for the style `dashes`, `***` for `asterisks`, and `___` for `underscores`.

Invariants: only the characters of the break (and the spaces between them) and the trailing whitespace are edited; the
indentation and any quote or list prefix stay; every edit carries the identifier of the rule that it enforces.

##### Key Methods

**CanFormat**: Accepts a thematic break.

- *Parameters*: `BlockInfo block`.
- *Returns*: `true` for a thematic break, `false` for every other block.
- *Errors*: `ArgumentNullException` for a null block.

**Format**: Adds the edits of one thematic break.

- *Parameters*: `FormatContext context`, `BlockInfo block` (accepted by `CanFormat`), `EditSet edits`.
- *Preconditions*: no argument is null and the block is a thematic break.
- *Postconditions*: `edits` holds the edits that make the break canonical; a canonical break adds none.

Algorithm: the break characters start at the block start and may be separated by blanks; the last one is followed only
by blanks, and any other character on the line means that the line is not what the parser promised and the break is left
alone. The characters up to the last break character are replaced by the target (`thematic-break`), unless a guard
applies, and the blanks after them are removed (`trailing-whitespace`), which also happens when a guard keeps the
characters.

Guards (`IsGuarded`) keep the new form from being read differently:

- *List marker guard*: when the text before the break on the line holds the first character of the target, as in
  `- ***` becoming `- ---`, the line would become a thematic break of its own and the list would be lost.
- *Grid border guard*: a line that starts with a plus sign after any quote and list markers (`+ ***`, a plus sign list
  marker) is left as written when the target is dashes, because `+ ---` looks like the border of a grid table and
  Pandoc may read the line as part of a table. This guard asks the broad question `LineLayout.StartsWithPlusSign`
  (whatever follows the plus sign), not `LineLayout.LooksLikeGridBorder`, which recognizes only the exact shape of a
  border as the line is written now: the line becomes a border because of the new dashes, so its present text says
  nothing. Asterisks and underscores after a plus sign are converted, because `+ ___` is no border.
- *First line guard*: a dashes break at the start of the first line of the file could open front matter. It is a check
  of its own because the front matter guard below judges a document whose first line is already dashes, while this one
  is for a break that would become that line.
- *Front matter guard*: a dashes break could close front matter that the first line opens (`FrontMatterGuard`).
- *Setext guard*: a dashes break directly under a line of text underlines it. A break is guarded unless it is the first
  block of its container, follows a blank line, or follows a block that is complete in itself (a heading, code block,
  break, list, quote, or table). Every other block above the break (a paragraph, an HTML block, a link reference
  definition, front matter, a footnote, math, a grid table) counts as text, which is conservative: the break stays as
  written. The previous block is taken from the flattened block list, found by position (a binary search, then the
  nearest earlier block with the same parent), because the parser's tree groups link reference definitions at the root.
  The set of blocks matches the blocks that the block spacing pass
  separates, so that the decision does not depend on blank lines inserted in the same run, which keeps formatting
  idempotent.

The guards other than the list marker guard apply to the dash form only, because asterisks and underscores cannot
create a heading, front matter, or a grid border. When any rule of the formatter is suppressed inside the break
(`thematic-break` or `trailing-whitespace`, by identifier or alias), `DocumentFormatter` leaves the whole block as
written, trailing whitespace included.

##### Error Handling

`ArgumentNullException` for a null argument and `ArgumentException` for a block that is not a thematic break. A line
that does not have the shape the parser promised produces no edit at all. An edit that conflicts with another edit would
make `EditSet.Add` throw `InvalidOperationException`; that is a defect of a formatter and `LintMarkEngine` turns it into
a `file-format-error` for the file. Nothing is caught here.

##### Dependencies

- **IBlockFormatter**, **FormatContext**, **EditSet**, and **TextEdit** (`Formatting` subsystem) - the contract, the
  inputs, and the edits.
- **ParsedDocument**, **BlockInfo**, **BlockKind**, and **SourceDocument** (`Document` subsystem) - the break, its
  previous sibling, and its source text.
- **LintMarkConfig** and **ThematicBreakStyle** (`Configuration` subsystem) - the `thematic-break` setting.
- **RuleCatalog** (`Linting` subsystem) - the rule identifiers of the edits.
- **Markdig** (OTS) - `ThematicBreakBlock`.
- **LineLayout** and **FrontMatterGuard** (supporting types of `DocumentFormatter`) - line arithmetic (including
  `StartsWithPlusSign`) and the front
  matter guard.

##### Callers

- **DocumentFormatter** - calls `CanFormat` for every block and `Format` for the accepted blocks that are not
  suppressed.
- **LintMarkEngine** - creates the instance that the production `DocumentFormatter` is configured with.
