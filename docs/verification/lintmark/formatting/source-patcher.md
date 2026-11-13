### SourcePatcher

#### Verification Approach

`SourcePatcher` is verified with unit tests in `SourcePatcherTests.cs`. The patcher is a static, pure function over a
`SourceDocument` and an array of edits, so no test doubles are used: the tests create a real document from text
with `SourceDocument.FromText`, build the edits with a small `Edit` helper, usually through a real `EditSet` so that
the array has the order that production code gives, and call `Apply`. Tests that must refuse an invalid array build
the `ImmutableArray` directly, because an `EditSet` would not let such a list exist. One test compares the one-pass
result with a naive reference that applies the same edits one by one from the end of the text. It uses a small
deterministic pseudo-random sequence with a fixed seed, so a failure can always be reproduced, and the sequence
produces random texts, random edits that do not conflict, and a random order of addition.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Replacements, deletions, and insertions are applied at the positions given, including at the end of the text, and
  every other character is unchanged.
- Positions count UTF-16 code units, so text with a surrogate pair is patched correctly.
- The result equals that of a naive reference implementation for many random edit lists.
- An empty edit list returns the same string instance as the source text.
- An edit that reaches beyond the end of the text, and an insertion beyond the end, throws `ArgumentException`.
- A list that is not in ascending order, overlapping edits, and two insertions at one offset throw
  `ArgumentException`.
- A null document throws `ArgumentNullException`, and a default list or a default edit throws `ArgumentException`.

#### Test Scenarios

##### Normal operation

**Replacement, deletion, and insertion are applied together**: The text `hello world` followed by a line feed is
patched with a replacement of the first five characters by `HELLO`, a deletion of the following space, and an
insertion of `!` at the end of the line. The result is `HELLOworld!` followed by a line feed.

**Edits at the end of the text are applied**: The text `abc` is patched with a replacement of its last character by
`C` and an insertion of a line feed at offset 3. The result is `abC` followed by a line feed.

**Positions count UTF-16 code units**: The text holds an accented letter, an emoji that takes two UTF-16 units, and the
letter `b`. Edits replace the accented letter by `e`, the whole emoji by `<smile>`, and `b` by `B`. The result is
`e<smile>B` followed by a line feed, which shows that the pair is replaced as one unit and its neighbors are
patched correctly.

**Many random edits match a naive reference**: For 40 rounds, a random text of 50 to 449 characters and a list of
random replacements, deletions, and insertions (some of which touch) are created. The edits are added to a set in
a shuffled order and the snapshot is patched. The result equals the text that is obtained by applying the same edits
one by one from the end of the text.

##### Boundary conditions

**An empty list returns the same text**: A document is patched with an empty edit list. The result is the same string
instance as the text of the document, with no copy.

##### Error handling

**An edit beyond the text is rejected**: The text `abc` and an edit of characters 2 to 7 are given. The call throws
`ArgumentException`.

**An insertion beyond the end is rejected**: The text `abc` and an insertion at offset 4 are given. The call throws
`ArgumentException`, because offset 3 (the end) would be valid but offset 4 does not exist.

**A list that is not in ascending order is rejected**: Two edits in descending order are given. The call throws
`ArgumentException`, because the patcher does not sort its input.

**Overlapping edits are rejected**: Two edits of characters 0 to 5 and 3 to 5 are given in a list. The call throws
`ArgumentException`.

**Two insertions at one offset are rejected**: Two insertions at offset 2 are given. The call throws
`ArgumentException`, because their order would be arbitrary.

**A list that was never initialized is rejected**: The default value of the immutable array is given. The call throws
`ArgumentException`.

**An edit that was never initialized is rejected**: A list that holds a default edit is given. The call throws
`ArgumentException`, because the default edit has no replacement and no rule.

**A missing document is rejected**: A null document and an empty list are given. The call throws
`ArgumentNullException`.
