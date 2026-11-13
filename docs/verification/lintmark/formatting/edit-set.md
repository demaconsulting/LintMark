### EditSet

#### Verification Approach

`EditSet` and its supporting type `TextEdit` are verified with unit tests in `EditSetTests.cs`. Both depend only on
the .NET base class library, so no test doubles are used. The tests build edits with a small `Edit` helper that uses
one rule identifier, add them to a real `EditSet`, and assert on the exceptions, the snapshots, and the counts. The
order-independence test uses a `Permutations` helper to add the same five edits in all 120 orders. The test for
immutable results uses the `ReadOnlyAssertions` helper, which tries every change that a cast to a mutable interface
would allow. The `TextEdit` tests call its constructor directly.

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

##### Boundary conditions

**A new set is empty**: A newly created set holds no edit; its `IsEmpty` value is true and its count is 0.

**An empty set gives an empty snapshot**: A snapshot of an empty set is initialized (not the default value of the
array type) and empty, so it can be used without a check.

**The largest representable range is accepted**: An edit with start 1 and length `int.MaxValue - 1` is created. Its
end is exactly `int.MaxValue`.

##### Error handling

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
