## Safety

### Verification Approach

The `Safety` subsystem is verified by integration tests in `SafetySubsystemTests.cs`. The tests run the three real
checkers (`HtmlEquivalenceChecker`, `StructuralTokenChecker`, and `IdempotencyChecker`) on the same texts, to show
that each check sees damage that the others cannot see and that a correct rewrite passes all three. The texts are
small, fixed strings. For the idempotency scenarios the formatting step is built from the real `DocumentParser`,
`DocumentFormatter`, and `SourcePatcher` driven by the deliberately simple or deliberately broken block formatters of
the `TestBlockFormatters` helper: `ReplacingFormatter` (a correct formatter that collapses double spaces),
`FlipFlopFormatter` (switches between two layouts on every pass), and `GrowingFormatter` (appends a space on every
pass). Their output renders to the same page every time, so only the idempotency check can refuse them. No mocking
framework is used, and the subsystem has no file-system access that would need a stand-in.

The reporting of a difference is verified at the subsystem level by running the three real checkers on pairs that
each fail, with texts long enough that the description has to be cut, and on a pair that all of them accept. The
shared `EquivalenceResult` verdict type, which is a unit of this subsystem, and the detail of each checker are
verified by the unit tests of those units, described in their own verification documents.

### Test Environment

N/A - standard test environment. The tests use only in-memory text and need no files, directories, or settings.

### Acceptance Criteria

- All integration tests pass with zero failures.
- A reflow that does not change the page passes the HTML check, and a change of the page is refused with a
  description.
- A conversion of the line endings that changes how the page renders is refused by the HTML check of the text with its
  line endings, and a conversion of an ordinary document is accepted.
- Pandoc structure that was joined into prose is refused by the structure check although the HTML check accepts the
  same pair.
- A formatter that flips between two layouts or keeps growing the text is refused by the idempotency check although the
  rendered page is the same each time.
- A correct formatter changes the text and passes all three checks.
- Every failed check describes its first difference in one line of bounded length, and a passed check has no
  description.

### Test Scenarios

#### Normal operation

**A benign reflow passes the HTML check**: A paragraph that is wrapped at different places is compared with the
original; the verdict is equivalent.

**A semantic change fails the HTML check**: A paragraph with a hard break is compared with the same text without the
hard break; the verdict is not equivalent and a description is given.

**A conversion of the line endings of a code span fails the HTML check of the text with its line endings**: A code span
framed by line breaks is compared with its CRLF form with the line endings kept; the verdict is not equivalent and a
description is given.

**A conversion of the line endings of an ordinary document passes**: A paragraph and a list are compared with their CRLF
form with the line endings kept; the verdict is equivalent.

**A joined fence fails the structure check**: A fenced div of three lines is compared with the same words joined into
one line; the verdict is not equivalent.

**The HTML check cannot see a joined fence but the structure check can**: The same pair of texts is given to both
checks; the HTML check says equivalent because the renderer shows both as one paragraph, and the structure check says
not equivalent.

**A correct formatter passes every check**: A document with double spaces and a fenced div is formatted by the
correct formatter. The text changes as expected (the double spaces become single spaces), and the HTML check, the
structure check, and the idempotency check all pass.

**Passed checks have no description**: A rewrite that collapses a double space is accepted by the HTML check, the
structure check, and the idempotency check, and none of them has a description.

#### Boundary conditions

**Failed checks describe the first difference in one bounded line**: A pair that fails the HTML check, a pair that
fails the structure check, and a text that fails the idempotency check each have a description. The texts carry five
hundred further lines, yet every description has no line break or carriage return and has at most 243 characters.

#### Error handling

**A formatter that flips between two layouts is refused**: Text that is the result of the first pass of the flipping
formatter is checked by formatting it again. The second pass changes the layout again, so the verdict is not
equivalent, although the HTML check accepts the first pass as the same page.

**A formatter that keeps growing the text is refused**: Text that is the result of the first pass of the growing
formatter is checked by formatting it again. The text grows once more, so the verdict is not equivalent, although the
HTML check accepts the first pass as the same page.
