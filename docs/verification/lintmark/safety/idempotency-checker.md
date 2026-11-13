### IdempotencyChecker

#### Verification Approach

`IdempotencyChecker` is verified with unit tests in `IdempotencyCheckerTests.cs`. The unit is a static class with no
state whose only input besides the text is a formatting delegate, so the delegate is the seam where the tests
substitute their doubles. Two kinds of delegate are used. The first kind is built from the real `DocumentParser`,
`DocumentFormatter`, `SourcePatcher`, and the default configuration, driven by the block formatters of the
`TestBlockFormatters` helper: `ReplacingFormatter` (a correct formatter that collapses double spaces),
`FlipFlopFormatter` (switches between two layouts on every pass), and `GrowingFormatter` (appends a space on every
pass). The second kind is a plain lambda that changes one line, appends a character, throws, or returns null, which
gives exact control over the line that the description must name and over the failure paths. The tests assert on the
verdict and, where the requirement is about it, on the exact description. The checker itself is never replaced.

#### Test Environment

N/A - standard test environment. The tests use only in-memory text.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- A formatting step that returns its input unchanged is idempotent and gives no description.
- A formatting step that flips between two layouts, that grows the text, or that changes any line is not idempotent.
- A formatting step that throws is not idempotent, and the description names the type of the exception.
- A formatting step that returns null is not idempotent.
- The description names the first line that changed, including the first line, the last line, and the single line of
  empty text, with excerpts of both versions.
- A null text or a null formatting step throws `ArgumentNullException`.

#### Test Scenarios

##### Normal operation

**A stable formatter is idempotent**: Text that the correct double-space formatter has already formatted is formatted
again; nothing changes, so the verdict is equivalent with no description.

**A formatter that flips between two layouts is not idempotent**: Text in the first of two layouts is formatted again
by the flipping formatter; the second pass changes the first line, so the verdict is not equivalent and the
description names line 1.

**A formatter that keeps growing the text is not idempotent**: A paragraph is formatted again by the growing
formatter; the second pass appends a space, so the verdict is not equivalent and the description contains the words
`changes line 1`.

**The first changed line is named**: A step that changes only the third of four lines gives a description that names
line 3.

##### Boundary conditions

**A difference on the first line is reported as line one**: A step that changes the first line gives a description
that starts with `Formatting the formatted text again changes line 1:`.

**A difference at the very end is reported on the last line**: A step that appends a character to text without a final
line ending gives `Formatting the formatted text again changes line 3: first "c" then "cd"`.

**Empty text that grows is reported on line one**: A step that adds a character to empty text gives the description
`Formatting the formatted text again changes line 1: first "" then "x"`.

##### Error handling

**A formatting step that throws is not idempotent**: A delegate that throws `InvalidOperationException` gives the
description `Formatting the formatted text again failed (InvalidOperationException)`, and the exception does not
escape the check.

**A formatting step that returns nothing is not idempotent**: A delegate that returns null gives the description
`Formatting the formatted text again returned no text`.

**Null arguments are rejected**: A null text and a null formatting step each throw `ArgumentNullException`.
