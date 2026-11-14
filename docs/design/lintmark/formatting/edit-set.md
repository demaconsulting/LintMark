### EditSet

![Formatting Structure](FormattingView.svg)

#### Purpose

`EditSet` collects the edits that formatters propose for one document and rejects conflicting edits as they are
added, so the set that reaches the patcher is always conflict-free and ordered the same way whatever the order in
which formatters ran. Its supporting type `TextEdit` (one change to the text) is described in this document, and so is
`EditRebaser`, a static supporting type that composes two lists of edits, where the second was computed on the text that
the first produces, into one list against the original text (the document formatter wraps paragraphs on the text that
the other rules produce, and the edits of a document are always relative to the original text).

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

**EditRebaser**: A static class without state, used by `DocumentFormatter` and by the tests. For edits `A` against the
original text `T0` and edits `B` against `T1 = Apply(T0, A)`, it computes edits `C` against `T0` with
`Apply(T0, C) == Apply(T1, B)`. The terms of the composition:

- The *output* of an edit of `A` is the text it writes in `T1`. The outputs of the edits of `A` are found by walking the
  edits in order and summing the change in length of the edits before.
- A `B` edit whose start or end lies *strictly inside* an output is extended to the ends of that output (the extension
  is text that `A` wrote, known without the original). An insertion of `B` that touches an insertion of `A` is merged
  with it, because the order of two insertions at one offset is not defined.
- `B` edits whose extended ranges overlap are merged into one edit; the text between them is text that `A` wrote.
- An edit of `A` whose output lies inside a merged range is *absorbed*: the replacement already contains its effect, so
  the edit is dropped. A deletion of `A` at a position strictly inside a merged range is absorbed too.
- An edit of `A` that only touches the boundary of a `B` edit (a deletion at the boundary, or text written just before
  or after it) stays.
- An edit that replaces nothing with nothing (an empty range and an empty replacement) is dropped from both lists first;
  when `B` is empty the result is `A`, and when `A` is empty it is `B`.
- The rule identifier of a merged edit is that of its first `B` edit.

The result is built through an `EditSet`, so it is ordered by start and length and a conflict cannot go unnoticed.
`EditRebaser` is stateless and does not modify its arguments, so it is safe for concurrent use.

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

**EditRebaser.Rebase**: Composes two lists of edits into one list against the original text.

- *Parameters*: `ImmutableArray<TextEdit> first` (against the original text), `ImmutableArray<TextEdit> second` (against
  the text that `first` produces).
- *Returns*: the edits against the original text, ordered by start and then length.
- *Preconditions*: each array is not a default array, holds no default edit, is ordered by start and then length, and
  has no conflicts.
- *Postconditions*: applying the result to the original text gives the same text as applying `second` to the text that
  `first` produces, for every pair of valid lists.

Algorithm: validate both lists (each edit is added in the given order to a scratch `EditSet`, which also detects a
default edit and conflicts); drop the edits that replace nothing with nothing; return the other list when one is empty;
compute the outputs of `first`; extend every edit of `second` outward to whole outputs and merge those that overlap into
*groups*, each a range of the produced text and the text that replaces it; mark every edit of `first` whose output lies
inside a group as absorbed; translate each group to the original text (its start with the edits that end at it before
it, its end with the edits that start at it after it); and add the edits of `first` that were not absorbed and the
groups to a new `EditSet`.

**EditRebaser.MapOffset**: Maps an offset of the produced text back to the original text.

- *Parameters*: `ImmutableArray<TextEdit> first`, `int offset` (in the produced text).
- *Returns*: the offset of the same character in the original text when no edit wrote it; for an offset strictly inside
  text that an edit wrote, the start of the range that edit replaced.
- *Errors*: `ArgumentException` as for `Rebase`; `ArgumentOutOfRangeException` for a negative offset.

#### Error Handling

A conflict is a defect of a formatter, never of the document, so it is not handled in this unit: `Add` throws
`InvalidOperationException` ("Edit of rule ... conflicts with edit of rule ...") and the exception propagates through
`DocumentFormatter` to `LintMarkEngine`, which reports it as a `file-format-error` for that file and leaves the file
untouched. A default `TextEdit` given to `Add` raises `ArgumentException`. The `TextEdit` constructor raises
`ArgumentNullException` for a null replacement or rule identifier, `ArgumentException` for an empty rule identifier,
and `ArgumentOutOfRangeException` for a negative start or length or a range whose end overflows. `EditRebaser` raises
`ArgumentException` for an array that is a default array, holds a default edit, or whose edits are unordered or
conflict (an `InvalidOperationException` from the scratch set is wrapped), `ArgumentOutOfRangeException` for a negative
offset, and `InvalidOperationException` when the composition itself produces conflicting edits or finds text that no
edit wrote inside an overlap; all of these are defects of the caller's edits and propagate like a conflict.

#### Dependencies

- **TextEdit** and **EditSet** (this unit) — the edits, and the ordered conflict-free result of the composition.
- **.NET BCL** — `List<T>`, binary search, immutable arrays, `StringBuilder`, and the standard exception types. No other
  tool units are used.

#### Callers

- **DocumentFormatter** — creates one `EditSet` per document and passes it to the block formatters; composes the
  first-stage edits with the wraps through `EditRebaser.Rebase` and maps the position of a finding with
  `EditRebaser.MapOffset`.
- **IBlockFormatter** implementations — add their edits to it.
- **SourcePatcher** — consumes the snapshot (as an ordered array of `TextEdit`).
