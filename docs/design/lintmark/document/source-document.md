### SourceDocument

![Document Structure](DocumentView.svg)

#### Purpose

`SourceDocument` is an immutable, LF-normalized view of one Markdown file together with the facts needed to write
it back as it was found: encoding, byte order mark and line terminators. Its single responsibility is to turn bytes
into trustworthy normalized text and to answer position questions about that text. A file whose terminators are all
of one style is restored byte for byte; a mixed file is written in the majority style, because only the counts of the
two styles are kept (mixed endings are a violation that the formatter fixes).

#### Data Model

- **Path** (`string`): the path used verbatim for reporting.
- **Text** (`string`): the content with every `\r\n` replaced by `\n`; no byte order mark.
- **LineEndings** (`LineEndingInfo`): record struct `(LfCount, CrLfCount)` counting the original terminators;
  `IsMixed` is true when both are above zero.
- **HasBom** (`bool`) and **Encoding**: UTF-8, emitting a byte order mark only when the source had one.
- **MajorityLineEnding** (`LineEnding`, `Lf` or `CrLf`): CRLF only when CRLF terminators outnumber LF; ties and
  files without terminators give `Lf`.
- **LineStarts** (`IReadOnlyList<int>`, immutable array): offset of the first character of every line; a final newline
  yields a trailing empty line starting at `Text.Length`, and an empty text has one line.

Positions are 1-based; a column counts UTF-16 code units and a tab as one unit. Because a CR only ever precedes an
LF, a line and column in the normalized text equal the line and column in the original file; only absolute offsets
differ. No per-offset map to the original text is kept: the original terminators are restored on write from the counts.
The normalization exists because Markdig reads multi-line code spans differently for LF and CRLF input.

#### Key Methods

- `Create(path, bytes)`: strict decoding, then `Build`. A UTF-8 byte order mark (`EF BB BF`) is stripped and
  recorded. A UTF-16 mark (`FF FE`, `FE FF`; this also catches UTF-32 LE) or UTF-32 BE mark (`00 00 FE FF`) is rejected.
  The rest is decoded with a strict UTF-8 decoder that throws on invalid bytes instead of substituting characters.
- `Decode(bytes, out hasBom)`: the decoding step alone; `ConfigLoader` reuses it for configuration files. For invalid
  UTF-8 it counts line breaks (LF, CRLF, lone CR) before the failing byte index to report line and column.
- `FromText(path, text)`: the same normalization and validation for already decoded text (no BOM).
- `Build` (private): one pass over the text counts LF and CRLF terminators and rejects a bare CR. It replaces CRLF by LF
  only when CRLF occurs, then builds the `LineStarts` table (one entry per `\n`).
- `PositionOf(offset)`: binary search over `LineStarts`; offset must be in `[0, Text.Length]`.
- `OffsetOf(line, column)`: inverse; the column may address the line terminator but not beyond it.
- `Slice(start, length)`: a span over `Text`, no allocation.
- `ToDisk(text, target)` and `ToBytes(text, target)`: convert LF text to the requested terminator style and encode it,
  prepending the byte order mark when the source had one. With unchanged text and the original single style the
  result equals the original bytes.

#### Error Handling

Decoding and terminator problems throw the public `SourceDocumentException` with rule id `file-encoding-error` (bad
byte order mark or invalid UTF-8) or `file-line-ending-error` (bare carriage return), plus 1-based line and column of
the offending byte or character (1:1 for a byte order mark). The engine catches it and reports
`path:line:col: rule-id message`. Null arguments throw `ArgumentNullException`; offsets and positions outside the
document throw `ArgumentOutOfRangeException`. Nothing is repaired silently.

#### Dependencies

The .NET base class library only (`System.Text` encodings, `System.Collections.Immutable`). No other unit.

#### Callers

`LintMarkEngine` (`Create` to read a file, `FromText` for re-parsing rewritten text, `ToBytes` to write), `ConfigLoader`
(`Decode`), `DocumentParser` and every unit of the subsystem (text and line table), and the `Formatting` and `Linting`
subsystems through `ParsedDocument.Source` (`PositionOf`, `LineStarts`, `Text`, `Slice`).
