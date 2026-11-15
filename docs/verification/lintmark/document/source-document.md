### SourceDocument

#### Verification Approach

`SourceDocument` is verified with unit tests in `SourceDocumentTests.cs`. The unit decodes bytes, normalizes line
endings, and maps positions, and it depends on nothing, so the tests give it byte arrays and text and assert on the
normalized text, the recorded line endings, the byte order mark, and the positions. No mocks, files, or process-wide
state are used.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- LF, CRLF, and mixed documents are normalized to LF with the counts and the majority ending recorded; a tie and a
  document without terminators resolve to LF.
- A UTF-8 byte order mark is stripped and restored on writing; the original bytes can be reproduced.
- Invalid UTF-8, a UTF-16 mark, and a bare carriage return are refused with an exception that carries a position.
- Positions count UTF-16 units, a tab counts as one column, and offsets and positions round-trip.

#### Test Scenarios

##### Normal operation

**CRLF text is normalized and counted**: The text becomes LF and the CRLF count is recorded.

**Mixed endings resolve to the majority**: The majority ending is chosen and the document is marked mixed.

**UTF-8 byte order mark is stripped and preserved**: The mark is not part of the text and is written back.

**Writing restores the original bytes**: A BOM and CRLF document converts back to the original bytes; a mixed
file is written in the majority style; a target ending converts on request.

**Positions and offsets round-trip**: Tabs and surrogate pairs count UTF-16 units with a tab as one column.

**Slices return the requested characters**: A range returns exactly its text.

##### Boundary conditions

**Empty file has one empty line**: The text is empty and the line count is one.

**Tie and no terminators resolve to LF**: Equal counts and a file without a line ending give LF.

**Out-of-range positions and offsets throw**: Offsets and positions beyond the text are refused.

##### Error handling

**UTF-16 mark is refused**: An encoding error is thrown.

**Invalid UTF-8 is refused with a position**: The exception names the line and column of the first bad byte.

**A bare carriage return is refused with a position**: A lone CR, also at the end of text created from a string,
throws a line-ending error.
