#### ListFormatter

![Blocks Structure](BlocksView.svg)

##### Purpose

`ListFormatter` proposes the edits that write each list in canonical form: one marker character for every item of a
bullet list, numbers that count the configured way, items that start in the same column, nested bullet lists indented
by the configured amount, and one space after each marker. It enforces `bullet`, `ordered-style`, `list-indent`, and
`list-marker-space`. Lists are never merged or split, and the first number and the delimiter of an ordered list are
never changed, because they decide how the list reads. A list that cannot be changed without changing its meaning is
left as written and reports nothing. The supporting types `ListModel`, `ListItemModel`, and `ListPlan` are described
in this document.

##### Data Model

`ListFormatter` is an internal sealed class without fields.

- `RuleIds`: `bullet`, `ordered-style`, `list-indent`, and `list-marker-space`.
- *Accepted blocks*: a block of kind `List` whose node is a `ListBlock` and whose parent is not a `ListItemBlock`: the
  outermost list of a nest. A list inside a quote or footnote is the outermost list of its own nest.
- *Claims*: none. `LineOwnership` claims leaf lines only, so the paragraphs and code inside items stay with the passes.
  The edits replace only indentation, markers, digits, and the blanks after markers.

**ListItemModel** (sealed record) holds one item: its block `Info`, `MarkerOffset`, `MarkerLine`, `MarkerColumn`,
`MarkerLength`, `DigitsLength` (zero for a bullet), `SpacesAfter`, `HasText`, `OldContentColumn` (where the parser
reads the content to start), `FirstLine`, `LastLine` (the last line with content, from `LineOwnership.LinesOf`, never
the parser's span), `Lists` (the lists directly inside the item), and `IsMovable`.

**ListModel** (sealed class, immutable after `Build`) holds one list: `Info`, `IsOrdered`, `Items`, `FirstLine`,
`LastLine`, and `IsMovable`. `Build` returns `null`, and nothing is proposed for the nest, when a marker is not where
the item block says it is, the first and last lines of the items do not match the list, or the items overlap or leave
content between them. An item is **not movable** when a line of it (nested items included) holds a tab before its
text or after a marker, a lazy continuation line (indented less than the content column), a grid table border, a
footnote definition marker, or a whitespace-only protected line with a tab; or when its marker line does not start with
spaces only, has no text after the marker, has more than four spaces after the marker (`MaxSpacesAfterMarker`), or
starts a nested list on the marker line. A list is **movable** when every item is, and, for the outermost list, when
the nest is at the top level of the document and starts at column `MaxIndent` (three) or less. A nested list is only
moved together with its item. Markers have at most `MaxDigits` (nine) digits.

**ListPlan** (sealed class, one per call, one thread) keeps the planned edits in a list that reaches the `EditSet` only
when the plan is accepted, the items whose content column shrinks, and a flag for lines whose meaning depends on their
indentation.

##### Key Methods

**CanFormat**: Returns `true` for the outermost list of a nest.

**Format**: Adds the edits of one nest (`FormatContext context`, `BlockInfo block`, `EditSet edits`). The block must be
accepted by `CanFormat` (not verified). It builds the `ListModel` and, when it is not `null`, calls
`new ListPlan(context, edits).Apply(model)`. `DocumentFormatter` calls it on the text that all other rules produce,
so the lines around a list are final, and composes the edits afterwards (see *DocumentFormatter Design*).

**ListPlan.Apply**: Plans the nest in two parts. Marker edits that keep every column (a new bullet character, digits
of the same width) apply at any depth, also inside quotes. Column edits (the marker column, the blanks after a marker,
a number that gains or loses a digit, and the shift of the other lines of the item) apply only to movable lists. A nest
that starts in a footnote is left as written (F-1). `ProcessList` visits the lists top down; decisions are all or
nothing per list.

- *Bullets* (`ShouldConvertBullet`): every marker becomes the configured character, unless the list already uses it,
  an item has no text, a new marker line would be a thematic break, an item's text is a thematic break (the thematic
  break rule may rewrite it in the same run), a plus sign marker (written or replaced) would make a grid table border
  lookalike, the next content line is rule-shaped, or the previous or next sibling is also a bullet list (the two
  would merge).
- *Numbering* (`NewNumbers`): item *i* becomes `n + i` for `incrementing` or `n` for `one`, where `n` is the first
  number as written. Numbering stays when the first number has leading zeros, a number would exceed nine digits, or a
  digit count must change but the list cannot be shifted.
- *Target column* (`TargetColumn`): a top-level bullet list starts at column 0, a top-level ordered list keeps the
  column of its first item, a bullet list inside a bullet item starts at the new marker column of the item plus
  `list-indent`, and any other nested list keeps its offset to the content column of its item (zero to three, before
  and after). Every item of a list gets the column of the list.
- *Item edits* (`MoveItem`): the marker line indentation (`list-indent`), the blanks after the marker
  (`list-marker-space`), and the leading blanks of every other content line, shifted by the change of the content
  column (`ShiftLines`), so the relative indentation of everything inside the item is unchanged. Lists that move on
  their own are skipped. Whitespace-only lines change only inside code or HTML. Every edit replaces characters, so a
  line gets at most one shift edit and none collides with a blank line insertion.

*Guards on the whole plan* (`IsSafe`). The column plan is replaced by the marker-only plan (which is dropped too when
its edits overlap) when: an item whose content column shrinks would take over the next non-blank
line (R-1, `LineAfterStaysOut`); a fenced code, indented code, HTML, or math line is not indented the way the shift
assumes, or a paragraph continuation line starts with block syntax only because of its indentation (R-2 and G-2,
`DependsOnIndentation`); a changed line would be a thematic break or setext underline by itself (G-1,
`ChangesLineIntoBreak`); two edits overlap (R-3, `HasOverlap`); a moved item that starts with a thematic break has more
lines (G-3); or a moved nested item has a block three or more columns past its content column (G-4).

Idempotence: after the edits every movable list meets its own conditions, and a list that was not movable stays so, so
a second run proposes nothing.

##### Error Handling

A null argument throws `ArgumentNullException`. A nest whose model cannot be built, and a nest whose plan is refused,
produce no edit and no report. An edit that conflicts with another throws `InvalidOperationException` when the
accepted plan is added to the `EditSet`; that is a defect, and `LintMarkEngine` reports it as a `file-format-error`.
Nothing is caught here.

##### Dependencies

- **IBlockFormatter**, **FormatContext**, **LineLayout**, and **LineOwnership** (`DocumentFormatter` unit) - the
  contract, the settings, line arithmetic, grid border and footnote marker tests, and which lines are protected.
- **EditSet** and **TextEdit** (`EditSet` unit) - the edits and `FindStartingAt` for the line after a list.
- **WrapGuard** (`LineWrapper` unit) - `IsInterruptingBlockStart` for continuation lines (G-2).
- **BlockInfo**, **BlockKind**, and **ParsedDocument** (`DocumentParser` unit) - the blocks of the nest.
- **LintMarkConfig**, **BulletMarker**, and **OrderedStyle** (`LintMarkConfig` unit) - `bullet`, `ordered-style`, and
  `list-indent`.
- **RuleCatalog** (`Linting` subsystem) - the rule identifiers.
- **Markdig** (OTS) - `ListBlock` and `ListItemBlock`.

##### Callers

- **DocumentFormatter** - accepts the list through `CanFormat` and calls `Format` in its list stage, on the text the
  other rules produce, for lists that are not suppressed.
