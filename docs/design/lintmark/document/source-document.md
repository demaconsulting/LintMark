### SourceDocument

![Document Structure](DocumentView.svg)

#### Purpose

`SourceDocument` is an immutable, LF-normalized view of one Markdown file together with the facts
needed to write the file back as it was found: the encoding, the byte order mark, and the
line terminators. A file whose terminators are all of one style is restored byte for byte; a file with
mixed terminators is not (only the counts of the two styles are kept, by design: mixed endings are a
violation that the formatter fixes by writing the majority style). Its single responsibility is to turn bytes into
trustworthy normalized text and to answer position questions about that text.

#### Data Model

**Path**: `string` — The path the document was read from, kept verbatim for reporting.

**Text**: `string` — The document text with every line terminator normalized to `\n`.

**LineEndings**: `LineEndingInfo` — A `readonly record struct (int LfCount, int CrLfCount)` with an
`IsMixed` property (true when both counts are above zero). It counts the original terminators.

**HasFinalNewline**: `bool` — `true` when `Text` ends with `\n`.

**HasBom**: `bool` — `true` when the original bytes started with the UTF-8 byte order mark.

**Encoding**: `System.Text.Encoding` — UTF-8 that emits a byte order mark only when `HasBom` is true.

**LineStarts**: `IReadOnlyList<int>` — The offset of the first character of every line. A final newline
produces a trailing empty line that starts at `Text.Length`; an empty file has one empty line. Backed by an
immutable array, so a caller cannot change it even by casting the list.

**MajorityLineEnding**: `LineEnding` — `CrLf` when CRLF terminators outnumber LF terminators, otherwise
`Lf` (a tie, or a file without terminators, is `Lf`). `LineEnding` is an enum with the values `Lf` and
`CrLf`.

`Text` is LF-normalized for a reason beyond convenience: the Markdown parser renders multi-line code spans
differently for LF and CRLF input (the *CRLF code-span difference* in *DocumentParser Design*), so every
parse and every offset works on the normalized text, and the original terminators are only counted so that
they can be restored on write.

Invariants: the instance is fully immutable; `Text` contains no `\r`; `LineStarts` is strictly increasing
and starts with 0.

**SourceDocumentException**: A public sealed exception carrying `RuleId` (`file-encoding-error` or
`file-line-ending-error`), `Line`, and `Column`. It is public because it crosses the engine boundary
as the typed failure of this unit.

Positions are 1-based. A column counts UTF-16 code units and a tab counts as one unit; display width
(East-Asian width, tab stops) is deliberately not modeled.

#### Key Methods

**Create**: Builds a document from the bytes of a file.

- *Parameters*: `string path` — path for reporting; `ReadOnlySpan<byte> bytes` — complete file content.
- *Returns*: `SourceDocument`.
- *Preconditions*: `path` is not null.
- *Postconditions*: `Text` is LF-normalized and the terminator counts reflect the original file.

Steps: (1) detect and strip the UTF-8 byte order mark (EF BB BF), recording `HasBom`; a UTF-16 or UTF-32
byte order mark is an encoding failure at 1:1; (2) decode strictly as UTF-8 — a decoder failure becomes an
encoding failure at the line and column of the first bad byte (the column counts UTF-16 units of the valid
text before the byte on that line); (3) scan the characters counting LF and CRLF terminators; a carriage
return that is not followed by a line feed is a line-ending failure at its own line and column; (4) replace
CRLF by LF only when CRLF terminators exist; (5) build the line table.

**FromText**: Builds a document from already decoded text (a test and tooling seam).

- *Parameters*: `string path`, `string text`.
- *Returns*: `SourceDocument` without a byte order mark.
- *Preconditions*: neither argument is null.
- *Postconditions*: Same normalization and validation as `Create`.

**PositionOf**: Converts an offset to a 1-based line and column.

- *Parameters*: `int offset` — in `[0, Text.Length]`; `Text.Length` addresses the end of the document.
- *Returns*: `(int Line, int Column)`.

A binary search over `LineStarts` finds the line; the column is the distance from the line start plus one.

**OffsetOf**: Converts a 1-based line and column back to an offset (the inverse of `PositionOf`; the column
may address the line terminator).

- *Parameters*: `int line`, `int column`.
- *Returns*: `int`.

**Slice**: Returns a `ReadOnlySpan<char>` over a range of `Text` without allocating.

**ToDisk**: Static. Converts LF-normalized text to a target `LineEnding` by replacing `\n` with `\r\n` for
`CrLf`. It is static because it needs no document state; it is the inverse of the normalization on read.

**ToBytes**: Instance method. Applies `ToDisk`, encodes with `Encoding`, and prepends the preamble when the
source had a byte order mark. For an unchanged text and the original terminator style, the bytes equal the
original file; for mixed files, writing the majority style restores a consistent file (the original bytes
cannot be restored, because only the counts of the terminators are kept; see the *line-ending* violation in the
system design).

Preconditions for the position methods: arguments inside the document; otherwise
`ArgumentOutOfRangeException` is thrown.

#### Error Handling

Content problems are reported through the typed `SourceDocumentException`: `file-encoding-error` ("Invalid
UTF-8 byte sequence" or "Unsupported byte order mark (only UTF-8 is supported)") and
`file-line-ending-error` ("Bare carriage return (only LF and CRLF line endings are supported)"). A file with
a bare carriage return is reported rather than silently rewritten. Programming errors raise
`ArgumentNullException` and `ArgumentOutOfRangeException`. No input is ever "repaired".

#### Dependencies

- **.NET BCL** — `System.Text` encodings and collections. No other tool units are used.

#### Callers

- **LintMarkEngine** — creates a document for every file it reads and converts the typed failure into a
  diagnostic; builds the bytes to write with `ToBytes`, creates documents from proposed text with `FromText`, and
  converts positions with `PositionOf` and `OffsetOf`.
- **DocumentFormatter** and **SourcePatcher** — read the LF-normalized text, the terminator counts, and the majority
  line ending.
- **DocumentParser** — parses `Text` and uses the line table.
- **SuppressionMap** and **ReferenceIndex** — convert offsets to lines.
