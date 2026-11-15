### EditSet

#### Verification Approach

`EditSet` (with `TextEdit`, `SourcePatcher`, and `EditRebaser`) is verified with unit tests in `EditSetTests.cs` and
`EditRebaserTests.cs`. The units are pure data and text transformations with no collaborators, so the tests use real
objects and build edits directly. The rebase tests compare the result of rebasing with the result of applying the two
edit lists in sequence, including a randomized comparison with a fixed seed, and one test checks that the time grows in
proportion to the number of edits. The helper `EditSetTestExtensions.cs` supplies the construction shortcuts.

#### Test Environment

N/A - the tests need only the standard test runner and in-memory text.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- An edit set accepts adjacent edits and an insertion at the start or the end of a replacement, and rejects overlapping
  edits, contained edits, and two insertions at one offset.
- The ordered result does not depend on the order in which edits were added.
- `SourcePatcher` produces the expected text for replacements, deletions, and insertions, by UTF-16 offsets, and rejects
  overlapping edits and edits beyond the text.
- Rebasing a second edit list over a first one gives the same text as applying both in sequence, in time proportional to
  the number of edits.

#### Test Scenarios

**Adjacent edits are accepted**: Two edits that touch are added. Both are kept.

**Overlapping or contained edit is rejected**: An edit that overlaps or lies inside another throws.

**Two insertions at one offset are rejected**: The second insertion at the same offset throws.

**Insertion order at one start**: An insertion before a replacement at the same start is ordered first, and an insertion
at the end of a replacement is accepted.

**Add order does not matter**: The same edits added in any order give the same ordered result.

**Edit can be found by offset**: An accepted edit is found by its start offset.

**Patch applies replace, delete, and insert**: A mix of edits produces the expected text, including an edit at the end of
the text and Unicode text patched by UTF-16 offsets.

**Patch rejects bad edits**: Overlapping edits and an edit beyond the text throw.

**Rebase translates edits outside each other**: Edits that do not meet are moved by the length changes of the first list.

**Rebase merges edits that meet**: A second edit inside the output of a first edit extends to its ends, two second edits
in one output merge, and touching insertions merge.

**Rebase absorbs and drops**: An edit inside a replaced range is absorbed, and an edit that changes nothing is dropped.

**Rebase equals sequential application**: Random edit lists give the same text rebased as applied in sequence.

**Rebase takes linear time**: Very many edits take time in proportion to their number.

**Offsets map between output and original**: The offset mapper maps output offsets back to the original and positions
after a deletion behind the deleted characters.
