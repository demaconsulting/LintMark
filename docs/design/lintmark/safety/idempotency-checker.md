### IdempotencyChecker

![Safety Structure](SafetyView.svg)

#### Purpose

`IdempotencyChecker` decides whether formatting already formatted text changes it again. It is the third safety
check. A formatter whose output is not a fixed point of itself (it flips between two layouts, or keeps growing the
text) makes every run produce a different file, which no one can review or trust, and rendering cannot show it.

#### Data Model

`IdempotencyChecker` is a static class with no state. Its input is the formatted LF-normalized text and a delegate
`Func<string, string>` that formats LF-normalized text and returns the formatted LF-normalized text. The formatting
step is passed in as a delegate so the check does not depend on how a document is parsed and formatted: the engine
supplies a delegate that parses the text, formats it with the same configuration, and applies the edits.

#### Key Methods

**Check**: Checks that formatting the formatted text leaves it unchanged.

- *Parameters*: `string formattedLf`; `Func<string, string> reformat`.
- *Returns*: `EquivalenceResult`.
- *Preconditions*: neither argument is null.
- *Postconditions*: the result is equivalent exactly when `reformat(formattedLf)` returns a string equal to
  `formattedLf` (ordinal comparison).

Algorithm: (1) validate; (2) call the delegate on the formatted text inside a `try` block; (3) a thrown exception
yields `Formatting the formatted text again failed (ExceptionType)` and a null result yields `Formatting the formatted
text again returned no text`, both not equivalent; (4) equal text is equivalent; (5) otherwise find the offset of the
first difference, count the line feeds before it to get the line number, and describe it as `Formatting the formatted
text again changes line N: first "…" then "…"` with excerpts that start at the beginning of that line.

#### Error Handling

`ArgumentNullException` for a null text or delegate. Everything else fails closed: a delegate that throws, or returns
null, is reported as not idempotent, so a check that cannot be completed refuses the rewrite. Nothing is propagated
from the delegate.

#### Dependencies

- **EquivalenceResult** (`Safety` subsystem, a unit) — the verdict and its description helpers.
- **.NET BCL** — span search and string handling.

#### Callers

- **LintMarkEngine** — runs the check last, with a delegate that reformats LF-normalized text with the file's
  configuration, on the proposed text of a file whose text would change.
