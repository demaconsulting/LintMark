## Safety

![Safety Structure](SafetyView.svg)

### Overview

The `Safety` subsystem is the safety net that decides whether a proposed rewrite of a file may be written. A rewrite
may reach the disk only when three independent checks all pass: the rewritten text renders to the same HTML as the
original (`HtmlEquivalenceChecker`), it keeps the Pandoc structure that Markdown rendering cannot show
(`StructuralTokenChecker`), and formatting it again changes nothing (`IdempotencyChecker`). Every check fails closed:
a check that cannot be completed reports a difference, so an undecided check refuses the rewrite. The subsystem does
no file-system access and writes no output. It contains four units: `HtmlEquivalenceChecker`,
`StructuralTokenChecker`, `IdempotencyChecker`, and `EquivalenceResult`.

The subsystem also defines `SafetyStatus`, a supporting type that is shared by the engine and the checks: the outcome
of the safety checks for one proposed rewrite. `EquivalenceResult` is the verdict of one check and is documented as a
unit.

### Interfaces

**HtmlEquivalenceChecker.Check**: Compares the rendered HTML of two texts.

- *Type*: In-process .NET static method.
- *Role*: Provider.
- *Contract*: Takes the original and the rewritten LF-normalized text and returns an `EquivalenceResult`: equivalent
  when the normalized HTML is identical, otherwise a result that names the first difference with some context (or
  states that the check could not be completed).
- *Constraints*: Throws `ArgumentNullException` for a null text and `ArgumentException` for a text with a carriage
  return. Safe for concurrent use.

**HtmlEquivalenceChecker.CheckOnDisk**: Compares the rendered HTML of two texts with the line endings they have on disk.

- *Type*: In-process .NET static method.
- *Role*: Provider.
- *Contract*: As `HtmlEquivalenceChecker.Check`, but the texts may contain carriage returns and are rendered as they are,
  so that a difference that only the line endings cause is reported.
- *Constraints*: Throws `ArgumentNullException` for a null text. Fails closed and is safe for concurrent use.

**StructuralTokenChecker.Check**: Compares the Pandoc structural tokens of two texts.

- *Type*: In-process .NET static method.
- *Role*: Provider.
- *Contract*: Takes the same two texts and returns an `EquivalenceResult`: equivalent when the sequence of structural
  lines and the occurrence counts of attribute groups and citations are equal, otherwise a result that names the first token
  that differs.
- *Constraints*: As `HtmlEquivalenceChecker.Check`.

**IdempotencyChecker.Check**: Checks that formatting is a fixed point.

- *Type*: In-process .NET static method.
- *Role*: Provider.
- *Contract*: Takes the formatted LF-normalized text and a delegate that formats LF-normalized text, applies the
  delegate to the text, and returns an `EquivalenceResult`: equivalent when nothing changes, otherwise a result that
  names the first line that changes.
- *Constraints*: Throws `ArgumentNullException` for a null argument. A delegate that throws or returns null yields a
  result that is not equivalent.

**EquivalenceResult** and **SafetyStatus**: The shared types.

- *Type*: In-process .NET readonly record struct and enumeration.
- *Role*: Provider.
- *Contract*: `EquivalenceResult` is the verdict of one check, documented as a unit; `SafetyStatus` is described in
  the Design section.
- *Constraints*: Immutable.

The subsystem consumes the Markdig library (rendering to HTML) and the `Document` subsystem (the shared Markdown
pipeline of `DocumentParser`). It is consumed by the `Engine` subsystem, which runs the checks, adds the check that
the rewritten text is still read with grid tables, and records the outcome as a `SafetyStatus` in the `FileResult`.

### Design

The three checkers are independent of each other and of the formatter: each takes plain text and returns an
`EquivalenceResult`, so any of them can be tested with broken inputs and none of them depends on how a document is
parsed or formatted. They are complementary. The HTML check sees any change in how the page renders, but is blind to
Pandoc constructs that Markdown rendering shows as ordinary text, such as fenced divs, raw LaTeX commands, display
math fences, attributes, and citations; the structural check sees exactly those. The idempotency check sees a
formatter whose output is not stable, which rendering cannot show at all. Formatters are expected to leave Pandoc
structure alone, so the structural check is a backstop: it is deliberately strict, so it can refuse a harmless
rewrite but never accept a harmful one that changes these tokens.

**EquivalenceResult**: The verdict of one check, described in the design of the unit `EquivalenceResult`: equivalent
only when the check positively established it, and otherwise a description of the first difference in one line of
bounded length.

**SafetyStatus**: An enumeration with the members `Ok` (every check passed), `HtmlMismatch` (also when only the line
endings would change the page), `StructureMismatch` (which also covers a rewrite that the parser would read
differently), and `NotIdempotent`. Everything other than `Ok` means the file must not be written.

**Fail closed**: Each checker catches every exception of its own work and returns a result that is not equivalent
(for example `The rendered output could not be compared (ExceptionType)`). The engine wraps each check once more, so
an exception that escapes a checker is also a failed check. No path of the subsystem lets an undecided check pass.
The renderer of the HTML check and the token collection of the structural check can be replaced through an internal
overload of `Check`; production code always uses the real ones, and tests use the overload to make the work fail.

**Rendering on LF-normalized text**: The checks of the rewritten text itself accept only LF-normalized text and reject a
carriage return with `ArgumentException`, because the Markdown parser renders a multi-line code span differently for
CRLF input. The engine passes the normalized text of the document to them. Because of that very difference, a
rewrite that only converts the line endings can change how the page renders, so the HTML check has a second entry
point, `HtmlEquivalenceChecker.CheckOnDisk`, that renders texts with the line endings they really have; the engine runs
it on the bytes it would replace and the bytes it would write, for every rewrite that changes bytes, also when only the
line endings change.

Thread safety: all three checkers are static and stateless apart from the immutable shared pipeline and compiled
regular expressions, so they are safe for concurrent use. Every regular expression has a match timeout; a timeout is
a failed check.
