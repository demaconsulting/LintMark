#### CodeBlockFormatter

##### Verification Approach

`CodeBlockFormatter` is verified with unit tests in `CodeBlockFormatterTests.cs`. The real parser, `LintMarkConfig`, and
`EditSet` are used; nothing is replaced by a double. `BlockFormatterAssertions` formats each input and asserts the exact
text, that the result renders the same, and that a second run is stable. Tests of the content check that code text is
never changed.

##### Test Environment

N/A - the tests need only the standard test runner and in-memory text.

##### Acceptance Criteria

- All unit tests pass with zero failures.
- Closed fenced and indented code blocks are accepted, and unclosed fences are never edited.
- Indented code becomes a fenced block with the configured fence character, and a fence is lengthened when the content
  holds a fence-like line.
- A fence of the other character is converted, except when the info string would make that unsafe.
- The content of a code block is never changed, and trailing whitespace is removed only from fence lines.
- Indented code in a list item stays indented.
- A suppressed rule leaves the block as written.

##### Test Scenarios

**Indented code becomes fenced**: Indented code is written as a fence of the configured character, and a short blank
line keeps an empty line.

**Fence character is converted**: Tilde fences become backtick fences, and with the tilde setting backtick fences
become tilde fences, in place inside containers.

**Fence is lengthened around lookalikes**: Content that holds a fence-like line makes the fence longer, for fenced and
indented input.

**Braced or backtick info string keeps the fence**: An info string in braces keeps its fence character, and an info
string with a backtick keeps a tilde fence.

**Content is never changed**: Content, blank lines, and wide whitespace lines at the end of indented code stay, and
trailing blank lines stay outside the fence.

**Trailing whitespace only on fence lines**: Whitespace is removed from fence lines and nowhere else.

**Indented code stays indented where unsafe**: Indented code in a list item, and dashes right after first-line dashes,
stay as written. Dashes after front matter are converted.

**Unclosed fence is left alone**: An unclosed fence proposes nothing.

**Each edit is credited to its rule**: Edits of a non-canonical fence are reported under their own rules.

**Suppressed rule leaves the block**: A suppressed rule keeps the block.
