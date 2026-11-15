#### AtomTokenizer

##### Verification Approach

`AtomTokenizer` is verified with unit tests in `AtomTokenizerTests.cs`. The real parser, which supplies the inline spans
of the paragraph, is used; nothing is replaced by a double. Each test tokenizes the lines of a real paragraph and
asserts the atoms (their text and the segments they form) or the refusal.

##### Test Environment

N/A - the tests need only the standard test runner and in-memory text.

##### Acceptance Criteria

- All unit tests pass with zero failures.
- Plain words are separated at whitespace, and each construct that must not be split is one atom: links, images, code
  spans, angle-bracket links, bare URLs, inline HTML tags, inline math, entities, footnote references, and Pandoc
  constructs.
- Escaped characters stay in their words, and a word ending in a backslash or a task marker joins the next word.
- A hard break, with spaces or a backslash, ends a segment.
- Container prefixes are not part of the atoms.
- A multi-line code span or link text joins its lines where that is safe.
- Input that cannot be tokenized with certainty is refused: lines that do not fit the spans, padded multi-line code
  spans, and multi-line constructs that cannot be joined.

##### Test Scenarios

**Plain words are separated at whitespace**: A paragraph of words gives one atom per word.

**Constructs stay whole**: Links, images, code spans, angle-bracket links, bare URLs, inline HTML tags, inline math,
entities, footnote references, and Pandoc constructs are each one atom.

**Escapes and joins**: Escaped characters stay in their words, and a word ending in a backslash or a task marker is
joined with the next word.

**Hard breaks end segments**: A hard break with spaces and one with a backslash end a segment.

**Container prefixes are excluded**: Quote and list prefixes are not part of any atom.

**Multi-line constructs are joined**: A code span or link text across lines is joined into one atom.

**Unsafe multi-line input is refused**: A code span padded on both sides, with a blank or delimiter at the break, a
construct that cannot be joined, and lines that do not fit the spans are refused.
