#### ListFormatter

![Blocks Structure](BlocksView.svg)

##### Purpose

`ListFormatter` proposes the edits that write each list in the canonical form: one marker character for every item of a
bullet list, numbers that count the configured way, items that start in the same column, nested bullet lists indented by
the configured amount, and exactly one space after each marker. It enforces the rules `bullet`, `ordered-style`,
`list-indent`, and `list-marker-space`. It never changes what a list means: a list that cannot be rewritten safely is
left exactly as written and reports nothing. Its supporting types `ListModel` (with the record `ListItemModel`) and
`ListPlan` have no unit of their own and are described in this document.

##### Data Model

`ListFormatter` is an internal sealed class without fields.

- `RuleIds`: an immutable array of `bullet`, `ordered-style`, `list-indent`, and `list-marker-space`, in this order.
  `DocumentFormatter` leaves a whole nest of lists as written when any of them is suppressed anywhere inside it.
- *Accepted blocks*: a block of kind `List` whose node is a Markdig `ListBlock` and whose parent is not a
  `ListItemBlock`, the outermost list of a nest. The formatter handles every list inside it itself, so a nested list is
  never visited twice. A list inside a block quote or a footnote has a parent that is not a list item and is therefore
  the outermost list of its own nest, also when that quote or footnote stands inside a list item. The edits for a list in
  a footnote are then dropped by the proof (see *Tier 1*).
- *Claims*: none. A list is a container, and `LineOwnership` claims the lines of leaf blocks only, so the passes of
  `DocumentFormatter` and the inline normalizer keep editing the text of the paragraphs, code, and other blocks inside
  the items. The edits of this unit replace only indentation, markers, and the blanks between a marker and its text.

**ListItemModel**: A sealed record, immutable, with these members: `Info` (the item block), `MarkerOffset`, `MarkerLine`,
`MarkerColumn` (the characters before the marker on its line), `MarkerLength` (one for a bullet, the digits plus the
delimiter for a number), `DigitsLength` (zero for a bullet), `SpacesAfter` (the blanks between the marker and the text
or the end of the line), `HasText`, `OldContentColumn` (where the content of the item starts, as the parser reads it:
marker column, marker length, and the spaces after the marker when there are one to four of them and text follows,
otherwise one), `FirstLine`, `LastLine` (the last line with content, from `LineOwnership.LinesOf`, never the parser's
span), `Lists` (the lists that are direct children of the item), and `IsMovable`.

**ListModel**: A sealed class, immutable after `Build`, with `Info`, `IsOrdered`, `Items` (an immutable array in source
order), `FirstLine`, `LastLine`, and `IsMovable`. It is built from the blocks the parser reported and from the text, and
checked against both.

- *Item hazards*: an item is **not movable** (all its lines cannot be shifted by a constant without risk) when any
  line of it, the lines of nested items included, holds one of these: a tab in the container prefix (among the blanks
  and quote markers before the text), a tab after the marker of an item that starts on the line, a lazy continuation (a
  non-blank line after the first whose indentation, counted in spaces, is below the content column of the item), a line
  with the exact shape of a grid table border (`LineLayout.LooksLikeGridBorder`), a footnote definition marker
  (`LineLayout.HasFootnoteDefinitionMarker`, deliberately wider than the syntax), or a whitespace-only line that is code
  or HTML (protected) and holds a tab. It is also not movable when its marker line does not start with spaces only, no
  text follows the marker, more than four spaces follow it (five or more start indented code), or a nested list starts
  on its marker line or leaves the item.
- *Movable list*: a list is movable when every item is. The outermost list also needs to be at the top level of the
  document (`Parent` is null) and to start in a column of at most three (a list that starts further right only exists
  because of what precedes it). A nested list is only ever moved together with its item, so its verdict concerns its own
  items alone.
- *Invariants*: the model is rejected (`Build` returns `null`, nothing is proposed for the nest) when a node is not a
  list or item as expected, a marker is not where the item block says it is (a bullet character, or one to nine digits
  and a delimiter, followed by a blank when text follows), the first and last lines of the items do not match those of the
  list, or the items overlap or leave content between them. Measuring ranges with `LineOwnership.LinesOf` keeps them
  independent of blank lines.

**ListPlan**: A sealed class created for one call (one `FormatContext` and one `EditSet`) and used by one thread. Its
state is the list of planned edits, which reach the `EditSet` only when the plan has been accepted. The constant
`ProofTailLines` is three and `MaxNumber` is 999,999,999.

##### Key Methods

**CanFormat**: Accepts the outermost list of a nest.

- *Parameters*: `BlockInfo block`.
- *Returns*: `true` for a `List` block with a `ListBlock` node whose parent is not a `ListItemBlock`.
- *Errors*: `ArgumentNullException` for a null block.

**Format**: Adds the edits of one nest of lists.

- *Parameters*: `FormatContext context`, `BlockInfo block` (accepted by `CanFormat`), `EditSet edits`.
- *Preconditions*: no argument is null and the block is an outermost list.
- *Postconditions*: `edits` holds the edits that make every list of the nest canonical, or none when the nest cannot be
  rewritten safely; a canonical nest adds none.

Algorithm: `ListModel.Build` models the nest, and when it returns a model `ListPlan.Apply` plans, proves, and adds the
edits. `ListPlan` works in two tiers.

- **Tier 1** changes no column and applies at any depth, also inside block quotes. It converts the marker character
  of a bullet list and the digits of an ordered list while the digit count stays. A list in the body of a footnote is
  accepted by `CanFormat` but left as written: the proof renders the lines of the nest by themselves, and there the
  four-space indent of a footnote body reads as indented code, so the proof cannot show that a rewrite keeps the
  rendering and the plan is dropped.
- **Tier 2** changes columns: the column of a marker, the blanks after it, and a number that gains or loses a digit. It
  applies only to a movable nest, and inside a nest it applies to a list only when every item of that list is movable,
  and, for a nested list, only while its offset to the content of its item is between zero and three before and after the
  move.

**Bullet conversion** (`bullet`, Tier 1, per list, all or nothing): the marker of every item becomes the configured
character (`-` by default). The list is left as written when every item already uses the character; when any item has
no text; when the new marker line would be a thematic break (the rest of the line holds only blanks and the new marker
character, and with the marker there are at least three of them); when the text after the marker is itself a thematic
break (the thematic break rule may rewrite it in the same run to the character of the marker, which would turn the whole
line into a break); when the text after the marker holds only the characters of a grid table border (dashes, equal signs,
colons, plus signs, and blanks, with at least one dash or equal sign), because a plus sign marker would then make the
line a border; or when the nearest sibling block before or after the list in the same container is also a bullet list,
which only differs from this one by its marker character and would merge with it.

**Numbering** (`ordered-style`): the first number and the delimiter (`.` or `)`) of an ordered list are never
changed, because the first number is the start number of the rendered list. Item *i* becomes `n + i` for
`incrementing` or `n` for `one` (the first number is repeated, not forced to 1), written without padding. The numbering
of a list is left as written when the first number has a leading zero and more than one digit, when a number would
exceed nine digits, or when a change of digit count is needed and the list cannot be shifted (then every number of the
list stays, also those that keep their width). A change that keeps the digit count is Tier 1; one that changes the
digit count moves the content column of the item and is part of Tier 2.

**Target columns** (`list-indent`, Tier 2), computed top down from the new columns of the item that holds the list:

- a top-level bullet list starts at column 0;
- a top-level ordered list keeps the column of its first item;
- a bullet list directly inside a bullet item starts at the new marker column of the item plus the `list-indent`
  setting (2 or 4);
- every other nested list (ordered, or bullet inside an ordered item) starts at the new content column of its item plus
  the offset that its first item had to the old content column;
- every item of a list gets the column of the list (the alignment of MD005).

The new content column of an item is the new marker column, the marker length (with the new number), and one space. The
**shift** of the item is the new content column minus the old one.

**Edits of an item** (Tier 2): when the marker column differs from the target, the indentation of the marker line is
replaced by that many spaces (`list-indent`). When the blanks after the marker are not exactly one, they are replaced by
one space (`list-marker-space`). When the shift is not zero, the leading whitespace of every other line of the item that
has content is replaced by a run that is `shift` columns wider or narrower, so the relative indentation of everything
inside the item (nested lists, indented and fenced code, HTML, further paragraphs) is exactly what it was. A
whitespace-only line changes only when it is code or HTML and reaches at least the old content column. The lines of a
nested list that moves on its own are skipped, and one that cannot move is shifted with the item. The shift edits are
attributed to `list-indent` when the marker moves, to `list-marker-space` when only the blanks after the marker change,
and otherwise to `ordered-style`. Every edit replaces existing characters and none inserts, so none can collide with the
blank line that the block spacing pass inserts at the start of a line, and a line receives at most one shift edit because
it belongs to its innermost item.

**Safety proof** (`ListPlan.Apply`): before anything reaches the `EditSet`, the plan is proved by rendering. The stretch
is the lines of the nest plus the next three non-blank lines after it, with any blank lines between them, grown to the end
of every edit of another formatter or pass that starts inside the stretch and ends beyond it. The stretch is rendered
with Markdig and the pipeline of `DocumentParser` twice: with only the edits of other formatters and passes (those that
start inside it, which the document formatter has already collected for the blocks after the list and for the passes),
and with those edits plus the planned edits. The plan stands only when the two HTML strings are equal. Overlapping edits
cannot be proved and fail the proof. The edits of other formatters are part of both texts, because they are part of the
result: a line after a list that another rule moves to the margin is no danger to the list, and one that it leaves
indented may become part of the last item once the item is shrunk. When the proof of the full plan fails, the plan is
made again with Tier 1 only and proved the same way; when that fails too the nest stays as written.

**Why a shift is safe.** A uniform shift of all lines of an item by the change of its content column keeps all relative
indentation, so everything that depends on columns is unchanged. The cases where it is not (lazy lines, tabs, a start of
indented code after the marker, containers whose prefix changes the meaning of columns, parser readings that depend on
neighboring lines) are exactly the hazards listed in the data model, and the proof covers what the hazards do not name.

**Idempotency**: after the edits every movable list satisfies its own triggers (columns equal the targets, one space,
numbers as configured), so a second run proposes nothing. A list that was not movable has the same hazards after a
uniform shift and stays unmovable. A decision never depends on text that another formatter or pass changes in the same
run, because `DocumentFormatter` runs the passes first and the block formatters from the last block to the first (see
*DocumentFormatter Design*) and the proof sees those edits. The engine verifies idempotency for every rewrite.

##### Error Handling

`ArgumentNullException` for a null argument and `ArgumentException` for a block that is not an outermost list. A nest
whose shape is not what the parser promised produces no edit, and so does a nest whose proof fails. A list that cannot
be rewritten without changing its meaning reports no violation: a violation that cannot be fixed is not a formatting
violation. An edit that conflicts with another edit makes `EditSet.Add` throw `InvalidOperationException` when the
accepted plan is added; that is a defect of a formatter and `LintMarkEngine` turns it into a `file-format-error` for the
file. Nothing is caught here.

##### Dependencies

- **IBlockFormatter**, **FormatContext**, **EditSet**, and **TextEdit** (`Formatting` subsystem) - the contract, the
  inputs, and the edits.
- **ParsedDocument**, **BlockInfo**, **BlockKind**, and **DocumentParser** (`Document` subsystem) - the blocks and the
  pipeline that the proof renders with.
- **LintMarkConfig**, **BulletMarker**, and **OrderedStyle** (`Configuration` subsystem) - the `bullet`, `ordered-style`,
  and `list-indent` settings.
- **RuleCatalog** (`Linting` subsystem) - the rule identifiers of the edits.
- **Markdig** (OTS) - `ListBlock`, `ListItemBlock`, and the HTML rendering of the proof.
- **LineLayout** and **LineOwnership** (supporting types of `DocumentFormatter`, reached through `FormatContext`) - line
  arithmetic, the exact grid border test, the footnote marker test, and which lines are protected.

##### Callers

- **DocumentFormatter** - calls `CanFormat` for every block and `Format` for the accepted blocks that are not
  suppressed.
- **LintMarkEngine** - creates the instance that the production `DocumentFormatter` is configured with.
