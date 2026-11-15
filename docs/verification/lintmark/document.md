## Document

### Verification Approach

The `Document` subsystem is verified by integration tests in `DocumentSubsystemTests.cs`. Each test starts from raw
bytes or text, as the engine does, and drives the real `SourceDocument`, `DocumentParser`, `SuppressionMap`, and
`TextWidth` together: decode and normalize the text, parse it into the block tree with its reference index and
suppression ranges, and measure display widths. The tests assert on the combined result, for example that the
source ranges of the blocks tile the normalized text and that a suppression comment found by the parser covers the
block it names. The Markdown library is the real one. No mocks are used. The support helper `SpanTilingAssertions`
checks that sibling ranges never overlap and that parents contain their children. Details of each unit are verified
in the unit tests.

### Test Environment

N/A - standard test environment. The tests use in-memory bytes and text only and no files.

### Acceptance Criteria

- All integration tests pass with zero failures.
- Bytes in UTF-8 with LF or CRLF line endings become a block tree whose ranges address the LF-normalized text, and
  positions map back to the original line and column.
- Undecodable input (invalid UTF-8, UTF-16, a bare carriage return) is refused with a positioned error and never
  yields a tree.
- Suppression comments found while parsing cover the blocks and findings in their range, for the rules they name
  by id or alias.
- The display width of mixed text is the sum of its characters' widths in terminal columns.

### Test Scenarios

#### Normal operation

**CRLF document is parsed against normalized text**: A CRLF document with headings, a list, and a table yields
blocks in source order whose ranges slice the normalized text exactly.

**Reference definitions are indexed with the tree**: Definitions in the top level and in nested containers are found
by label regardless of case.

**Suppression comment covers the named block**: A disable-next-line comment suppresses the following block for the
named rule only.

**Display width measures wide text**: A line of ASCII, CJK, and emoji text has the width that the unit rules give.

#### Boundary conditions

**Empty and unterminated documents parse**: An empty file has one empty line, a file without a final newline keeps
its last line, and an unclosed fence runs to the end of the document.

**Grid-table fallback is marked, not crashed on**: An invalid grid border marks the document format-unsafe at that
line while the blocks are still produced.

#### Error handling

**Invalid UTF-8, UTF-16, and bare carriage returns are refused with a position**: The error names the line and
column of the first fault.

**Unknown suppression names suppress nothing**: A comment that names no registered rule has no effect.
