### EditSet

![Formatting Structure](FormattingView.svg)

#### Purpose

`EditSet` collects the edits that formatters propose for one document and rejects conflicting edits as they are
added, so the set that reaches the patcher is always conflict-free and ordered the same way whatever the order in
which formatters ran. Its supporting type `TextEdit` (one change to the text) is described in this document.

#### Data Model

**TextEdit**: A readonly record struct `(int Start, int Length, string Replacement, string RuleId)` with the derived
property `End` (`Start + Length`, exclusive). It replaces `Length` characters of the LF-normalized text starting at
`Start` with `Replacement`; a deletion has an empty replacement and an insertion has a length of zero. `RuleId` names
the rule that makes the change, for reporting. It is immutable with value equality.

Invariants of a `TextEdit` created with its constructor: `Start` and `Length` are not negative, their sum does not
overflow, the replacement is not null, and the rule identifier is neither null nor empty. A default instance (no
replacement, no rule) is not valid; `EditSet` and `SourcePatcher` reject it.

**EditSet**: A sealed class with two members of state: a `List<TextEdit>` that is always ordered by start offset and
then by length and never holds conflicting edits, and a static comparer, `PositionOrder`, that defines that order
(start offset first, then length). Properties: `Count` and `IsEmpty`.

**Conflict rule**: Two edits conflict when

- both are insertions at the same offset (their relative order would be arbitrary);
- an insertion lies strictly inside the range of a replacement (at the start or end of the range is allowed);
- both are replacements and their ranges share a character.

Edits that merely touch (one ends where the next starts) do not conflict. Identical ranges conflict, because they are
either two insertions at one offset or two replacements that share characters.

#### Key Methods

**Add**: Adds an edit.

- *Parameters*: `TextEdit edit`.
- *Preconditions*: the edit was created with its constructor.
- *Postconditions*: the edit is in the set at its ordered position, and the set is still conflict-free.

Algorithm: binary search for the insertion point with the `PositionOrder` comparer (start, then length); compare the
edit with its
neighbor before the point and its neighbor after it; insert. Only the two neighbors need to be compared, because the
set never holds conflicting edits, so an edit that conflicts with any member also conflicts with the member just before
or just after its position. An insertion sorts before a replacement that starts at the same offset because its
length is zero.

**ToImmutable**: Returns an immutable snapshot of the edits.

- *Returns*: `ImmutableArray<TextEdit>` ordered by start offset and then length; empty when none were added.
- *Postconditions*: later `Add` calls do not change an earlier snapshot, and the snapshot is identical for any order of
  `Add` calls.

**ThrowIfConflict** (private): Applies the conflict rule to two edits that are neighbors in the ordering, and throws
with a message that names both rules and both ranges.

#### Error Handling

A conflict is a defect of a formatter, never of the document, so it is not handled in this unit: `Add` throws
`InvalidOperationException` ("Edit of rule ... conflicts with edit of rule ...") and the exception propagates through
`DocumentFormatter` to `LintMarkEngine`, which reports it as a `file-format-error` for that file and leaves the file
untouched. A default `TextEdit` given to `Add` raises `ArgumentException`. The `TextEdit` constructor raises
`ArgumentNullException` for a null replacement or rule identifier, `ArgumentException` for an empty rule identifier,
and `ArgumentOutOfRangeException` for a negative start or length or a range whose end overflows.

#### Dependencies

- **.NET BCL** — `List<T>`, binary search, immutable arrays, and the standard exception types. No other tool units are
  used.

#### Callers

- **DocumentFormatter** — creates one `EditSet` per document and passes it to the block formatters.
- **IBlockFormatter** implementations — add their edits to it.
- **SourcePatcher** — consumes the snapshot (as an ordered array of `TextEdit`).
