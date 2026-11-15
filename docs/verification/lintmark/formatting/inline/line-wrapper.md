#### LineWrapper

##### Verification Approach

`LineWrapper` (with `SentenceSplitter`, `WrapGuard`, and `BlockSyntaxGuard`) is verified with unit tests in
`LineWrapperTests.cs` and `BlockSyntaxGuardTests.cs`. The units are pure functions over atoms and text, so the tests use
real objects: they build atoms directly, call the wrapper with the width and mode, and assert the exact lines. The guard
tests classify real line starts. Nothing is replaced by a double.

##### Test Environment

N/A - the tests need only the standard test runner and in-memory text.

##### Acceptance Criteria

- All unit tests pass with zero failures.
- Fill mode puts as many atoms on a line as fit the display width and moves the rest to the next line. An atom wider
  than the width stands alone and is reported, and a line exactly as wide as the width stays.
- The first line may have its own width, and an unlimited width writes one sentence per line.
- Sentence mode starts each sentence on its own line, and a sentence that is too wide is filled. Uncertain places are
  not split.
- Semantic mode breaks a too-wide sentence at the latest fitting clause, keeps an atom with an inner comma whole, and
  falls back to fill when there is no clause start.
- A line never starts with block syntax: bullets, numbers, quotes, headings, fences, definitions, HTML block starts,
  table rows, thematic runs, and Pandoc constructs are guarded, and links, code spans, task markers, and heading
  lookalikes are not.
- Atoms that cannot be moved keep the line and the overflow is reported.
- Wide and combining characters are counted in display columns, and every mode is idempotent.

##### Test Scenarios

**Atoms that do not fit go to the next line**: A run of atoms is filled to the width, and a line exactly as wide stays.

**Wide atom stands alone**: An atom wider than the width keeps its own line, and when no break is available it stays
over width and is reported.

**First line has its own width**: A smaller first width applies to the first line only.

**Sentences get their own lines**: In sentence mode every sentence starts a line, a too-wide sentence is filled, and
with unlimited width every sentence is one line.

**Sentences are found safely**: Terminators with closing marks and capitals with opening marks end and start sentences,
uncertain places are not split, and a single atom or empty text has no start.

**Clauses are found safely**: Clause punctuation gives a start and non-clause punctuation does not.

**Semantic mode breaks at clauses**: A too-wide sentence breaks at the latest fitting clause, a guarded clause start is
skipped, a clause beyond the width or none is filled, and an atom with an inner comma is not split.

**Block syntax is kept off the line start**: A break before guarded syntax moves earlier. A sentence that starts with
block syntax stays on the line before, and guarded atoms in a row keep the line and report the overflow.

**Guard classifies line starts**: Bullets, numbers, quote, heading, fence, definition, HTML, table, thematic, setext,
and Pandoc starts are guarded. Code spans, strikethrough, links, task markers, heading lookalikes, inline HTML, numbers
that are not markers, and ordinary text are not. A null line to the Pandoc check throws.

**Width is measured in columns**: Wide and combining characters are counted by display width in every mode.

**Every mode is idempotent**: Wrapping the wrapped text again gives the same lines.
