#### BlockQuoteFormatter

##### Verification Approach

`BlockQuoteFormatter` is verified with unit tests in `BlockQuoteFormatterTests.cs`. The real parser, `LintMarkConfig`, and
`EditSet` are used; nothing is replaced by a double. `BlockFormatterAssertions` formats each input and asserts the exact
text and a stable second run. Tests of quoted blocks use real documents with code, HTML, math, tables, lists, and
footnotes inside the quote.

##### Test Environment

N/A - the tests need only the standard test runner and in-memory text.

##### Acceptance Criteria

- All unit tests pass with zero failures.
- Only outermost quotes are accepted.
- Every quote marker is followed by exactly one space, at any nesting depth, and alert and heading lines are normalized.
- Spaces that are content stay: code, HTML, math, tables, lists, footnotes, and setext headings in a quote are not
  changed, and neither is a quote in a list item.
- A lazy line, a tab in the prefix, and five spaces are left alone, and a marker-only line is left to the whitespace
  rules.
- A suppressed rule leaves the quote as written.

##### Test Scenarios

**Extra spaces after the marker become one**: A quote marker followed by several spaces is written with one, and the
edit is credited to the quote rule.

**Nested markers get one space each**: Markers such as `>>  >` are written with one space after each.

**Heading and alert lines are normalized**: A heading and an alert line in a quote are normalized.

**Spaces in quoted blocks stay**: Code, HTML, math, a table, a list, a footnote, and a setext heading in a quote keep
their spaces. A quote in a list item keeps its spaces.

**Unsafe prefixes are left alone**: A lazy line, a tab in the prefix, and five spaces after the marker stay as written.

**Marker-only line is left to the whitespace rules**: A line with only a marker is not edited here.

**Canonical quote gets no edit**: A canonical quote proposes nothing.

**Only outermost quotes are accepted**: Nested quotes and other blocks are not accepted, and the nested quote is handled
as part of its outermost quote.

**Suppressed rule leaves the quote**: A suppressed rule keeps the quote.
