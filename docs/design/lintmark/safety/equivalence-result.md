### EquivalenceResult

![Safety Structure](SafetyView.svg)

#### Purpose

`EquivalenceResult` is the verdict of one safety check: either the check established that two texts are equivalent, or
it found a difference and describes it in one line of bounded length. It also provides the helpers that the checks use
to find and excerpt the first difference of two texts. One shared type for all checks keeps the report format of a
refusal the same whichever check refused.

#### Data Model

`EquivalenceResult` is an immutable record struct `(bool IsEquivalent, string? Description)`. The result is equivalent
only when the check positively established it. `Description` is null for an equivalent result and is a single line of
at most 240 characters plus an ellipsis for a different one. `EquivalenceResult.Equivalent` is the shared passing
instance. A default instance is not equivalent and has no description; it is never produced by the checks.

#### Key Methods

**Equivalent**: The shared passing result.

- *Returns*: an equivalent result with no description.

**Different**: Creates the result of a failed check.

- *Parameters*: `string description`.
- *Returns*: a result that is not equivalent.
- *Preconditions*: `description` is not null.
- *Postconditions*: the stored description has every line break (carriage return, line feed, and the Unicode line and
  paragraph separators) replaced by a space; a description longer than 240 characters is cut to 240 characters and
  ends with an ellipsis, so it is at most 243 characters in all; a shorter description is kept as it is.

**Excerpt**: Quotes a text for a description.

- *Parameters*: `string text`; `int start`.
- *Returns*: the text from the start offset on, on one line.
- *Postconditions*: the start is clamped into the text; at most 60 characters are returned; line feed, carriage
  return, and tab are shown as backslash escapes and any other control character as a question mark.

**FirstDifference**: Finds where two texts first differ.

- *Parameters*: `string first`; `string second`.
- *Returns*: the offset of the first differing character; the length of the shorter text when one is a prefix of the
  other; the length of the text when both are identical.

#### Error Handling

`Different` throws `ArgumentNullException` for a null description, so that a refusal without a reason can never be
created. The helpers accept every text, including the empty text and an offset outside it.

#### Dependencies

- **.NET BCL** — string handling and span search.

#### Callers

- **HtmlEquivalenceChecker**, **StructuralTokenChecker**, and **IdempotencyChecker** — each returns an
  `EquivalenceResult` and uses the helpers to describe the first difference.
- **LintMarkEngine** — reads the verdict of every check and uses `FirstDifference` to position the refusal at the
  first line where the proposed text departs from the file.
