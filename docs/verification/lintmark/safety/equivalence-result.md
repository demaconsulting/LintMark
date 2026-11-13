### EquivalenceResult

#### Verification Approach

`EquivalenceResult` is verified with unit tests in `EquivalenceResultTests.cs`. The unit is a small immutable type with
no dependency, so the tests use it directly: they read the shared result of a passed check, call the factory that
creates the result of a failed check with descriptions of controlled length and content, and call the helpers that
excerpt a text and find the first difference of two texts. The texts are small fixed strings, except for the
descriptions that are built to be shorter than, equal to, and longer than the limit. No test double is used, because
the behavior under test is a pure function of its arguments.

#### Test Environment

N/A - standard test environment. The tests use only in-memory text.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- A passed check has no description.
- A failed check keeps a short description as it is, and a description of up to 240 characters is kept; a longer
  description is cut to 240 characters plus an ellipsis, without splitting a surrogate pair.
- Every kind of line break in a description (carriage return, line feed, form feed, next line, line separator, and
  paragraph separator) becomes a space, and a null description is rejected.
- An excerpt escapes control characters and the Unicode line and paragraph separators, shows at most 60 characters of
  the text (up to 120 after escaping) without splitting a surrogate pair, starts at the given offset, and clamps a
  start outside the text.
- The first difference is the offset of the first differing character, or the length of the shorter text when one is
  a prefix of the other, or the length of the text when both are identical.

#### Test Scenarios

##### Normal operation

**A passed check has no description**: The shared passing result is equivalent and has no description.

**A short description is kept as it is**: A failed result that is created with a short description is not equivalent
and keeps the description unchanged.

**An excerpt starts at the given offset**: An excerpt of a six-character text taken from offset three holds the rest of
the text.

**The first difference is the offset of the first differing character**: Two texts that differ at offset two give two.

##### Boundary conditions

**A long description is cut to one bounded line**: A description of a thousand characters followed by a line break
and more text is cut to 240 characters followed by an ellipsis and has no line break.

**A description is cut only above the limit**: Descriptions of 239 and 240 characters are kept in full, and one of
241 characters is cut to 240 characters plus an ellipsis, which is 243 characters in all.

**A cut inside a surrogate pair drops the pair**: A description whose 240-character limit falls between the two
halves of an emoji is cut before the emoji, so no lone surrogate remains.

**Every kind of line break becomes a space**: A description with a carriage return and line feed pair, a line feed, a
carriage return, a form feed, a next line character, a line separator, and a paragraph separator is stored with one
space for each line break.

**An excerpt is cut at the excerpt length**: A text longer than the excerpt length gives an excerpt of exactly that
length.

**Escapes may double the excerpt length**: A text of line feeds longer than the excerpt length gives an excerpt of
twice the excerpt length, because each line feed is shown as a two-character escape.

**A surrogate pair is never split in an excerpt**: An emoji that straddles the end of the window is dropped as a
whole, and a start offset inside an emoji moves back to the beginning of the emoji.

**A start offset outside the text is clamped**: An excerpt starting before the text shows the whole text, and one
starting after the end is empty.

**A text that is a prefix of the other gives the shorter length**: The first difference of a text and an extension of
it is the length of the shorter text, in either order; identical texts give their length.

##### Error handling

**A null description is rejected**: Creating a failed result with a null description throws
`ArgumentNullException`.

**Control characters in an excerpt are escaped**: An excerpt of text with a line feed, a carriage return, a tab, and
another control character shows the first three as backslash escapes and the last as a question mark, so the
description stays on one line.

**Line and paragraph separators in an excerpt are escaped**: An excerpt of text with a line separator and a
paragraph separator shows both as question marks, so the description stays on one line.
