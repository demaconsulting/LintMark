### EditSet

#### Verification Approach

`EditSet` and its supporting types `TextEdit` and the composition of edit lists are verified with unit tests in
`EditSetTests.cs` (the set and the edit) and in a second test class of the unit (the composition of edit lists). All of
them depend only on the .NET base class library and on each other (the composition builds its result through a real
`EditSet`), so no test doubles are used.
The tests build edits with a small `Edit` helper that uses one rule identifier, add them to a real `EditSet`, and assert
on the exceptions, the snapshots, and the counts. The order-independence test uses a `Permutations` helper to add the
same five edits in all 120 orders. The test for immutable results uses the `ReadOnlyAssertions` helper, which tries
every change that a cast to a mutable interface would allow. The `TextEdit` tests call its constructor directly.

The tests of the composition use a real `SourceDocument` and a real `SourcePatcher` as the oracle: for each scenario
about the composition of two edit lists they apply the first list to the original text, apply the second list to that
intermediate text, and compare the result with applying the composed list to the original text. The scenarios assert the
exact composed edits as well. One scenario draws 6,000 random rounds from a seeded generator (a text of up to 24
characters from a small alphabet that includes the line feed, a random conflict-free first list, and a random
conflict-free second list for the intermediate text) and compares the two results in each round; a round that throws is
a failure, and the failures are reported together. The scenario about threads calls the composition from eight threads
with the same two lists and compares each result with that of a single call.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- An edit that shares a character with an edit in the set, and a second insertion at one offset, throws
  `InvalidOperationException` and leaves the set unchanged; the message names both rules and both ranges.
- Edits that only touch, including an insertion at the end of a replacement, are accepted.
- A default edit throws `ArgumentException`.
- The snapshot is in ascending order of start offset and then length, with an insertion before a replacement at the
  same start, and it is identical for every order of additions.
- A snapshot is never changed by later additions and cannot be modified, also not through a cast.
- The set reports its count and whether it is empty.
- A `TextEdit` rejects a negative start or length, a range that overflows, a null replacement, and a null or empty
  rule identifier; it exposes its values and the end of its range, and equal values give equal edits.
- Composing the edits of a second list, computed on the text that a first list produces, with the first list gives edits
  against the original text whose result equals applying the two lists one after the other; an edit of the first list
  inside a range that the second list replaces is absorbed, an edit of the second list that starts or ends inside text
  that the first list wrote is extended to all of that text, edits that only touch stay separate, an insertion that
  touches an insertion is merged with it, and two edits of the second list in one output of the first list are merged.
- An edit that replaces nothing with nothing is dropped, and when one of the two lists is empty the other is returned
  unchanged.
- A default list, a default edit, an unordered list, and a list with conflicting edits are rejected with an
  `ArgumentException` that names the list; a negative offset is rejected.
- An offset of the produced text maps back to the offset of the original text, an offset inside written text maps to the
  start of the range that the edit replaced, and an offset after a deletion maps behind the deleted characters.
- The composition of random edit lists equals applying the lists in sequence in every round, and the same lists give the
  same composition on many threads.

#### Test Scenarios

##### Normal operation

**Edits that touch are accepted**: A set holds an edit of characters 0 to 3. Edits that start where it ends, and two
more edits that touch each other (characters 10 to 12 and 12 to 14) are added. All four edits are in the set.

**An insertion at the end of a replacement is accepted**: A set holds a replacement of characters 5 to 8, and an
insertion at offset 8 is added. Both are in the snapshot, the replacement first.

**An insertion sorts before a replacement at the same start**: A set holds a replacement at offset 5 with length 3, and
an insertion at offset 5 is added. The snapshot lists the insertion (length 0) first and the replacement second.

**The snapshot does not depend on the order of additions**: Five edits that do not conflict, including an insertion
that touches two replacements, are added in each of the 120 possible orders. Every snapshot equals the edits ordered
by start offset and then length.

**A later addition does not change an earlier snapshot**: A snapshot is taken from a set with one edit, another edit is
added, and a second snapshot is taken. The first snapshot still holds one edit and the second holds two.

**A snapshot cannot be modified**: A snapshot of two edits is cast to the read-only list and collection views and to
the mutable list interface. The read-only helper confirms that no change is possible, the mutable interface reports
that it is read-only, an add and an element assignment throw `NotSupportedException`, and the snapshot still holds
two edits.

**The set reports its contents**: A new set has a count of 0 and is empty. After two edits are added the count is 2
and the set is not empty.

**A text edit exposes its values**: An edit is created with start 3, length 4, the replacement `new`, and the rule
`paragraph-wrap`. Each member returns the value that was passed in.

**The end of an edit is its start plus its length**: A replacement of characters 3 to 7 has the end 7, and an
insertion at offset 9 has the end 9.

**Edits with the same values are equal**: Two edits with the same values are equal and have the same hash code, and
an edit that differs in the rule identifier is not equal to them.

**Edits that lie outside each other are translated**: A first list replaces the first character of `abcdef` with three
characters, and a second list, computed on the lengthened text, replaces the character at its offset 5. The composition
holds both edits, the second moved to offset 3 of the original, and applying it gives the same text as applying the two
lists one after the other.

**An edit of the first list inside a replaced range is absorbed**: A first list replaces one character at offset 2 with
two characters, and a second list replaces a range of five characters from offset 1 with one character. The composition
is a single edit over the four original characters from offset 1, and the text equals the sequential result.

**An edit of the second list inside written text is extended to it**: A first list replaces the character at offset 2
with three characters, and a second list replaces the middle one of those three. The composition is one edit that
replaces the original character with the three characters in their changed form, so the text keeps its other characters.

**A deletion that touches a replaced range stays outside it**: A first list deletes the character at offset 2, and a
second list replaces the character that then stands at offset 2. The composition keeps both edits, the deletion at
offset 2 and the replacement at offset 3, and the text equals the sequential result.

**An insertion of the second list that touches an insertion of the first is merged with it**: A first list inserts two
characters at offset 2, and a second list inserts one character before them, inside them, or after them. In each of the
three cases the composition is one insertion at offset 2 that holds the combined text, with the new character first,
between, or last, and the text equals the sequential result.

**Two edits of the second list in one output of the first list are merged**: A first list replaces one character with
five, and a second list changes the second and the fourth of the five. The composition is one edit over the original
character with the five characters in their changed form.

**The composition of random edits equals applying the lists in sequence**: 6,000 rounds draw a random text and a random
list of edits for it, apply it, draw a random list for the result, and compose the two lists. In every round the
composition applied to the original text gives the sequential result, and none throws.

**Offsets of the output map back to the original**: A list replaces the third character of a text with three characters.
The offsets 0, 1, and 2 of the output map to themselves, the offsets 3 and 4 (inside the written text) map to offset 2,
the start of the replaced range, and the offsets 5, 6, and 8 map to 3, 4, and 6.

**An offset after a deletion maps behind the deleted characters**: A list deletes three characters from offset 2. The
offset 2 of the output maps to offset 5 of the original.

**The same lists give the same composition on many threads**: Two lists, one with a replacement and a deletion and one
with two replacements and an insertion, are composed once and then two hundred times on each of eight threads that
start together. Every composition equals the single-thread composition.

##### Boundary conditions

**One empty list needs no translation**: A list of two edits is composed with an empty list on either side, and two
empty lists are composed. The non-empty list is returned as it is, whichever side the empty list is on, and the result
of two empty lists is empty.

**An edit that changes nothing is dropped**: A first list holds an insertion of the empty text at offset 2, and a second
list inserts one character at offset 2. The composition holds only the insertion of the character, so the empty edit
does not stand in its way.

**A new set is empty**: A newly created set holds no edit; its `IsEmpty` value is true and its count is 0.

**An empty set gives an empty snapshot**: A snapshot of an empty set is initialized (not the default value of the
array type) and empty, so it can be used without a check.

**The largest representable range is accepted**: An edit with start 1 and length `int.MaxValue - 1` is created. Its
end is exactly `int.MaxValue`.

##### Error handling

**A missing, unordered, or conflicting list is rejected by the composition**: The composition is called with a default
array as the first list and as the second list, with a default edit in the first list, with an unordered second list,
and with a first list whose edits conflict. Each call throws `ArgumentException` that names `first` or `second` as
appropriate.

**An invalid argument of the offset mapping is rejected**: The mapping is called with a default list, which throws
`ArgumentException` that names `first`, and with the offset -1, which throws `ArgumentOutOfRangeException` that names
`offset`.

**An overlapping edit is rejected**: A set holds an edit of characters 0 to 5. An edit of characters 3 to 8 throws
`InvalidOperationException` whose message names both rules and both ranges (`Edit of rule 'paragraph-wrap' at 3..8
conflicts with edit of rule 'paragraph-wrap' at 0..5.`), and the set still holds one edit.

**An edit inside another edit is rejected**: A set holds an edit of characters 0 to 10. An edit of characters 2 to 5
throws `InvalidOperationException`, and the set still holds one edit.

**An edit that contains an existing edit is rejected**: A set holds an edit of characters 4 to 6. An edit of
characters 0 to 10 throws `InvalidOperationException`, and the set still holds one edit.

**Two insertions at one offset are rejected**: A set holds an insertion at offset 5. A second insertion at offset 5
throws `InvalidOperationException`, because their order would be arbitrary, and the set still holds one edit.

**An insertion strictly inside a replacement is rejected**: A set holds a replacement of characters 5 to 8. An
insertion at offset 6 throws `InvalidOperationException`, and the set still holds one edit.

**A replacement around an existing insertion is rejected**: A set holds an insertion at offset 6. A replacement of
characters 5 to 8 throws `InvalidOperationException`, and the set still holds one edit.

**Two edits of the same range are rejected**: A set holds an edit of characters 5 to 8. Another edit of the same
characters throws `InvalidOperationException`, and the set still holds only the first edit.

**A default edit is rejected**: A default edit (no replacement and no rule) is added to an empty set. The call throws
`ArgumentException`, and the set is still empty.

**A negative start or length is rejected**: A text edit is created with start -1, and then with length -1. Each call
throws `ArgumentOutOfRangeException`.

**A missing replacement is rejected**: A text edit is created with a null replacement. The call throws
`ArgumentNullException`.

**A missing or empty rule identifier is rejected**: A text edit is created with a null rule identifier, which throws
`ArgumentNullException`, and with an empty rule identifier, which throws `ArgumentException`.

**A range that overflows is rejected**: A text edit is created with start `int.MaxValue` and length 1, and with start
1 and length `int.MaxValue`. Each call throws `ArgumentOutOfRangeException`, because the end cannot be represented.
