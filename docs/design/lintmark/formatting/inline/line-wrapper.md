#### LineWrapper

![Inline Structure](InlineView.svg)

##### Purpose

`LineWrapper` fills lines with atoms: it puts as many atoms on a line as fit in the width, never splits an atom, and
never starts a line with an atom that would be read as block syntax. It works on atoms and widths only; it knows
nothing about paragraphs, prefixes, or hard breaks. Its supporting type `WrappedLines` (the result) has no unit of its
own and is described in this document.

##### Data Model

`LineWrapper` is an internal static class without state.

**WrappedLines**: A sealed record, immutable, with `Lines` (an immutable array of strings, in order, none empty, without
container prefix and without trailing whitespace) and `HasBreakableOverflow` (whether a line that holds more than one
atom is still wider than the width available to it, because the guard allowed no better break). The constructor
rejects a default or empty array and an empty line (`ArgumentException`) and a null line (`ArgumentNullException`).

**Width conventions**: Widths are display columns, measured with `TextWidth.Of` on each atom once. A line is its atoms
joined with one space, so its width is the sum of the widths of its atoms plus one column for each gap. The first line
has `firstWidth` columns available and every other line has `continuationWidth` columns; the caller has already
subtracted the width of the container prefix from the line length (at least 1). An atom that is wider than the width
stands alone on its line and that line is accepted over width: nothing is cut. A line that holds only one atom is never
a breakable overflow, so a long link or URL is not a finding.

**Guard**: The atom that would start a line is tested with `BlockSyntaxGuard.IsGuardedLineStart`. The wrapper never
escapes characters: a guarded atom is kept off the start of a line by moving the break, not by changing the text.

##### Key Methods

**Wrap**: Wraps a run of atoms into lines.

- *Parameters*: `ImmutableArray<string> atoms`, `int firstWidth`, `int continuationWidth`.
- *Returns*: a `WrappedLines`.
- *Preconditions*: the atoms are not a default or empty array and hold no null and no empty atom; each width is at least
  1.
- *Postconditions*: every atom appears exactly once, in order; the lines are the atoms joined with single spaces; no
  line after the first starts with a guarded atom; nothing is modified.
- *Errors*: `ArgumentException` for a default or empty array or an empty atom, `ArgumentNullException` for a null atom,
  `ArgumentOutOfRangeException` for a width below 1.

Algorithm (greedy fill):

1. Measure every atom. Start the first line with the first atom.
2. For each next atom, in order: when the atom fits on the current line (line width, one gap, and the atom width within
   the width available to the line), add it. The width available is `firstWidth` while the first line is being filled
   and `continuationWidth` afterwards.
3. Otherwise a break is needed before the atom. When the atom may start a line (the guard accepts it), the break goes
   there and the atom starts the next line. When the guard refuses it, look back from the atom for the *nearest earlier
   atom that may start a line* (not the first atom of the current line, which would leave an empty line). When one
   exists and the atoms from there up to and including the refused atom fit in the continuation width, the break goes
   before that earlier atom, so the new line starts with the earlier atom and the refused atom lands inside it. Only the
   nearest candidate is tried: a candidate further back only widens the new line.
4. When no such break exists, the refused atom stays on the current line over its width, and the filling continues (a
   later atom that the guard accepts can still start a line).
5. Build the lines and set `HasBreakableOverflow` when a line of two or more atoms is wider than the width available to
   it (which only the guard-forced overflow of step 4 can produce).

Example with a width of 16: for the words `alpha beta gamma - delta`, the first line fills with `alpha beta gamma` and
the dash does not fit, so a plain greedy fill would start the next line with `-`, a bullet marker. The wrapper moves the
break before `gamma` instead, and the lines are `alpha beta` and `gamma - delta`.

Properties: the result depends only on the atoms and the two widths, never on how the atoms were laid out before, and a
text that was wrapped this way, tokenized again, is wrapped to itself (a fixed point); wrapping is deterministic. The
wrapper is not aware of a guarded atom that follows a hard break; the caller checks the first atom of every segment
after the first.

##### Error Handling

`ArgumentException`, `ArgumentNullException`, and `ArgumentOutOfRangeException` for invalid arguments, as listed under
**Wrap**. Nothing is caught. A situation where no good wrap exists (a refused atom that cannot be moved, an atom wider
than the width) is a result, `HasBreakableOverflow` or a line over width, never an exception.

##### Dependencies

- **BlockSyntaxGuard** (this subsystem) - which atoms may not start a line.
- **TextWidth** (`Utilities` subsystem) - the display width of an atom.
- **WrappedLines** (this unit) - the result.
- **.NET BCL** - immutable arrays and string handling.

##### Callers

- **ParagraphFormatter** (`Blocks` subsystem) - wraps each segment of a paragraph that has a line wider than the line
  length.
