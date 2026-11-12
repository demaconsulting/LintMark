### SourceDocument

#### Verification Approach

`SourceDocument` is verified with unit tests in `SourceDocumentTests.cs`. The class depends only on the .NET
base class library, so no mocking is needed. Tests build documents from byte arrays through `Create` and
from text through `FromText`, and assert on the normalized text, the terminator counts, the preserved byte
order mark, the typed failures (rule identifier, line, and column), the position arithmetic, and the bytes
produced by `ToBytes`.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- LF, CRLF, and mixed files normalize to LF text with correct terminator counts.
- Invalid UTF-8, a UTF-16 byte order mark, and a bare carriage return are rejected with the documented rule
  identifier and position.
- A UTF-8 byte order mark is preserved and restored; a file without one gets none.
- Unchanged text written back in the original terminator style reproduces the original bytes.
- Positions are 1-based, count UTF-16 code units, and convert back to the same offsets.
- The line table cannot be modified by a caller that casts it.

#### Test Scenarios

##### Normal operation

**Line table cannot be changed through a cast**: `LineStarts` is not a `List<int>` or an array, every attempted
change through `IList<int>` throws `NotSupportedException`, and positions are still computed from the
original table.

**LF file keeps its text and counts LF terminators**: A file with LF terminators keeps its text and counts only
LF terminators.

**CRLF file is normalized to LF**: A CRLF file gets LF text and counts CRLF terminators.

**Mixed terminators are detected and the majority is resolved**: A file with both terminators reports `IsMixed`
and resolves the majority style.

**Final newline is detected**: `HasFinalNewline` is true only when the text ends with a terminator.

**UTF-8 byte order mark is stripped and remembered**: The mark is removed from the text, `HasBom` is true, and
the encoding emits it again.

**File without a byte order mark gets none**: Writing a file that had no mark adds none.

**FromText normalizes like Create**: Text given to `FromText` is normalized and validated exactly as bytes given
to `Create`.

**Line table records every line**: `LineStarts` holds the start of every line, including the empty line after a
final newline.

**Offsets and positions convert both ways**: `OffsetOf` is the inverse of `PositionOf` for every offset.

**Slice returns the requested characters**: `Slice` returns the characters of the requested range of the
normalized text.

**LF file is restored byte for byte**: An LF file written back with LF reproduces its bytes.

**CRLF file is restored byte for byte**: A CRLF file written back with CRLF reproduces its bytes.

**File with byte order mark and CRLF is restored byte for byte**: Both the mark and the terminators are
reproduced.

**Normalized text is converted to the requested terminator style**: The static `ToDisk` produces LF or CRLF text
as requested.

##### Boundary conditions

**Equal LF and CRLF counts resolve to LF**: A tie gives `Lf` as the majority style.

**File without terminators resolves to LF**: The majority style of a file with no terminator is `Lf`.

**Empty file has a single empty line**: An empty file is a valid document with one empty line and no final
newline.

**Columns count UTF-16 code units and a tab counts as one**: A surrogate pair counts as two columns and a tab as
one; positions are 1-based.

**Carriage return at the very end of the text is a bare carriage return**: `FromText` rejects it as a line-ending
failure.

##### Error handling

**UTF-16 byte order mark is an encoding failure**: A UTF-16 mark raises `file-encoding-error` at line 1,
column 1.

**Invalid UTF-8 is reported at the first bad byte**: The failure is `file-encoding-error` with the line and column
of the byte.

**Bare carriage return is reported with its position**: The failure is `file-line-ending-error` at the line and
column of the carriage return.

**Null path is rejected**: `Create` and `FromText` throw `ArgumentNullException` for null arguments.

**Positions outside the document are rejected**: Offsets, lines, and columns outside the document raise
`ArgumentOutOfRangeException`.
