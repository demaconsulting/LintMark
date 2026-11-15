### EditSet

![Formatting Structure](FormattingView.svg)

#### Purpose

`EditSet` is the vocabulary of change of the `Formatting` subsystem. It collects the edits that rules propose for one
document, rejects conflicting edits as they are added, and keeps them in one deterministic order. The unit also owns its
supporting types: `TextEdit` (one change), `SourcePatcher` (applies edits to the text in one pass), and `EditRebaser`
(composes two stages of edits and maps offsets back to the original text).

#### Data Model

**TextEdit**: A readonly record struct `(Start, Length, Replacement, RuleId)` with the derived `End` (exclusive). It
replaces `Length` characters of the LF-normalized text at `Start` by `Replacement`; a deletion has an empty replacement
and an insertion has a length of zero. `RuleId` names the rule for reporting. Value equality; immutable.

**EditSet**: A sealed class holding a `List<TextEdit>` that is always ordered by start offset, then length, and never
holds conflicting edits. `IsEmpty` tells whether it holds an edit. Not safe for concurrent use: one set belongs to one
thread and one document; snapshots (`ToImmutable`) are immutable.

**Conflict rule**: Two edits conflict when both are insertions at one offset (their order would be arbitrary), when an
insertion lies strictly inside a replaced range, or when two replacements share a character. Edits that only touch
do not conflict. A conflict is a defect of a formatter and is never resolved silently, because merging or dropping a
change would make the output depend on the order in which formatters ran.

**Outputs** (private to `EditRebaser`): for the first list of edits, the start and end of each edit's replacement text
in the produced text and the cumulative change in length before each edit, all prepared once as arrays.

#### Key Methods

**EditSet.Add(TextEdit)**: Binary search for the position by `(Start, Length)` and compare with the edit before and the
edit after only: the set holds no conflicts, so any conflict is with a neighbor. Throws `InvalidOperationException` that
names both rules and ranges. An insertion sorts before a replacement that starts at the same offset.

**EditSet.ToImmutable / FindStartingAt**: `ToImmutable` returns the snapshot, identical for any order of `Add` calls.
`FindStartingAt(start, accept)` returns the first accepted edit that starts at an offset (binary search), for formatters
that look at edits already proposed.

**SourcePatcher.Apply(SourceDocument, edits)**: Builds the new text in one ascending pass: copy the untouched text,
append each replacement. Cost is linear in the text length plus replacement lengths. It returns the same string
instance when there are no edits and throws `ArgumentException` for an edit outside the text or one that starts inside
the edit before it.

**EditRebaser.Rebase(first, second)**: For edits `A` against text `T0` and edits `B` against `T1 = Apply(T0, A)`,
returns edits `C` against `T0` with `Apply(T0, C) == Apply(T1, B)`. Algorithm:

1. Drop edits that replace nothing with nothing; return the other list when one is empty.
2. Prepare the output ranges of `A` in `T1`.
3. Extend each edit of `B` that starts or ends strictly inside text that an edit of `A` wrote to the ends of that
   text (the text is known from `A`, not from `T0`). An insertion of `B` that touches an insertion of `A` joins it,
   since the order of two insertions at one offset is undefined.
4. Merge edits of `B` whose extended ranges overlap into one group; the text between them is text written by `A`.
5. Mark every edit of `A` whose output lies inside a group as absorbed (its effect is in the group's replacement). An
   edit of `A` that only touches a group boundary stays.
6. Translate each group's range to `T0` using the prepared offsets and add the remaining `A` edits and the groups to a
   new `EditSet`, so a conflict cannot go unnoticed.

The rule id of a merged edit is that of its first `B` edit. Each step is a binary search over prepared arrays, so a
document with thousands of edits is composed without quadratic work.

**EditRebaser.CreateOffsetMapper(first)**: Returns a function from an offset in the produced text to the original text:
the offset of the same character when no edit wrote it, otherwise the start of the range the edit replaced. Used to
report findings at positions of the original file.

#### Error Handling

`EditSet.Add` throws `InvalidOperationException` for a conflict; `SourcePatcher.Apply` throws `ArgumentException` for an
invalid edit list; `EditRebaser` throws `InvalidOperationException` when a composition produces conflicting edits or
finds an overlap of text that no edit wrote. All of these are defects of the producer of the edits, not of the document;
none is handled here. They propagate through `DocumentFormatter` to `LintMarkEngine`, which reports a failure for the
file and leaves it untouched.

#### Dependencies

- **SourceDocument** (`Document` subsystem): the text that `SourcePatcher` patches.
- No OTS items; the .NET base class library supplies lists, binary search, immutable arrays, and `StringBuilder`.

#### Callers

- **DocumentFormatter** creates the sets, composes stages with `EditRebaser`, and patches the text between stages.
- **Block formatters, whitespace passes, and `LinkRefactorer`** add their edits to a set.
- **LintMarkEngine** applies the final edits with `SourcePatcher.Apply`.
