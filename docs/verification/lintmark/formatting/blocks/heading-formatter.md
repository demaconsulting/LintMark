#### HeadingFormatter

##### Verification Approach

`HeadingFormatter` is verified with unit tests in `HeadingFormatterTests.cs`. The real parser, `LintMarkConfig`,
`EditSet`, and `RuleCatalog` are used; nothing is replaced by a double. `BlockFormatterAssertions` formats each input
and asserts the exact text, that the result renders the same, and that a second run is stable. Edits are checked by the
rule they are credited to.

##### Test Environment

N/A - the tests need only the standard test runner and in-memory text.

##### Acceptance Criteria

- All unit tests pass with zero failures.
- ATX and setext headings are accepted and other blocks are not.
- A heading is written as `#` marker, one space, and the text, with no indentation, closing sequence, or trailing
  whitespace; an empty heading keeps its marker only.
- A setext heading becomes ATX where that is safe and stays setext in containers, when its text could be read as
  structure, or when spans do not fit its lines.
- Tabs are expanded outside code spans and protected spans, and the result is stable on the second run.
- A heading with a suppressed rule is left as written.

##### Test Scenarios

**Canonical heading gets no edit**: A heading that is already canonical produces no edit.

**Blanks after the marker become one space**: Several blanks after the marker become one.

**Closing sequence is removed**: A closed ATX heading loses its closing sequence, but number signs in text stay, and an
empty closed heading keeps its marker only.

**Indentation and trailing whitespace are removed**: An indented heading and a heading with trailing whitespace are
written without them.

**Setext heading becomes ATX**: A setext heading converts, also after a definition and with a tab in its text.

**Setext heading stays where conversion is unsafe**: A multi-line setext heading, one in a container, and one whose text
ends with a backslash and spaces, ends with the marker, or starts like structure stay setext.

**Tabs are expanded safely**: A tab in text expands to the tab stop. A tab in a code span or protected span stays, and
the result is stable after a paragraph without a blank line.

**Spans that do not fit leave the heading alone**: A heading whose spans do not fit the line is not edited.

**Each edit is credited to its rule**: Each edit of a non-canonical heading is reported under its own rule.

**Suppressed rule leaves the heading**: A suppressed rule keeps the heading, and a heading after the region is
formatted.
