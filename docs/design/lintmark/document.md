## Document

![Document Structure](DocumentView.svg)

### Overview

The `Document` subsystem turns the bytes of one Markdown file into a trustworthy in-memory model. It
decodes and normalizes the file (`SourceDocument`), parses it with a fixed Markdown pipeline into a
source-ordered list of blocks with corrected source ranges (`DocumentParser`), resolves the
suppression comments that disable rules (`SuppressionMap`), and indexes the link reference
definitions (`ReferenceIndex`). It does no file-system access and writes no output; reading the file
and reporting failures belong to the `Engine` subsystem. The subsystem contains four units:
`SourceDocument`, `DocumentParser`, `SuppressionMap`, and `ReferenceIndex`.

### Interfaces

**SourceDocument.Create**: Builds a document from raw bytes.

- *Type*: In-process .NET static method.
- *Role*: Provider.
- *Contract*: Strict UTF-8 decoding with a preserved byte order mark and LF-normalized text. Returns
  an immutable `SourceDocument`.
- *Constraints*: Throws the public `SourceDocumentException` (rule `file-encoding-error` or
  `file-line-ending-error`, with line and column) for content it cannot represent faithfully.

**DocumentParser.Parse**: Parses a `SourceDocument`.

- *Type*: In-process .NET static method.
- *Role*: Provider.
- *Contract*: Returns a `ParsedDocument` holding the source, the Markdig syntax tree, the flattened
  `BlockInfo` list, the `SuppressionMap`, and the `ReferenceIndex`.
- *Constraints*: Exceptions raised by Markdig propagate to the caller. Safe to call concurrently for
  different documents.

**DocumentParser.GetLines** and **DocumentParser.GetContinuationPrefix**: Describe the lines of a
paragraph or heading and the container prefix of its continuation lines.

- *Type*: In-process .NET static methods.
- *Role*: Provider.
- *Contract*: See *DocumentParser Design*. Both are static and have no production caller yet.
- *Constraints*: Arguments must not be null; the block must be a paragraph or heading whose range lies inside
  the document text (otherwise `ArgumentException`). That the block belongs to the document is not verified.

**SuppressionMap.IsSuppressed** and **SuppressionMap.IsSuppressedRange**: Answer whether a rule is
suppressed at an offset or inside a range.

- *Type*: In-process .NET instance methods.
- *Role*: Provider.
- *Contract*: See *SuppressionMap Design*.
- *Constraints*: Immutable after construction; safe for concurrent reads.

**ReferenceIndex.TryGet**: Finds a link reference definition by label.

- *Type*: In-process .NET instance method.
- *Role*: Provider.
- *Contract*: See *ReferenceIndex Design*.
- *Constraints*: Immutable after construction; safe for concurrent reads.

The subsystem consumes the Markdig library (syntax tree and source ranges) and the .NET base class
library; it uses no other subsystem.

### Design

The units form a short pipeline. `SourceDocument` is the foundation: every other unit reads its
LF-normalized text and uses its line table to convert offsets to line and column. `DocumentParser`
parses the normalized text once with a pipeline that is built once per process and shared read-only
by all threads. It then flattens the syntax tree into the block list, compensating for the parser's
range quirks, and builds the `SuppressionMap` and `ReferenceIndex` from the same blocks and tree so
that all three views describe the same parse.

Block ranges produced by the parser are not trustworthy as they are, and `DocumentParser` compensates for the
behaviors listed by name in *DocumentParser Design* (*Parser Behaviors Compensated*): unclosed fences, trailing
line feeds, synthetic group nodes, definitions reported at the document root, dropped and misplaced footnote
definitions, paragraphs that overlap definitions, the difference between LF and CRLF in multi-line code spans,
lazy continuation lines, and released paragraph lines. The result is a block list in which siblings never
overlap, every block lies inside its parent, and no document text is left unaccounted for outside the block
ranges other than whitespace and container prefixes (quote markers and list markers). The span-tiling tests of
the subsystem check exactly these properties.

The `SuppressionMap` and `ReferenceIndex` units consume the `BlockInfo` and `BlockKind` types that are
declared with `DocumentParser`, while the parser builds both of them. This is a cycle of types only: the
parser calls the two units and they never call back into it, so there is no cycle of behavior.

Thread safety follows the tool-wide rules in the *LintMark* system design: the only state shared
between threads is the immutable Markdown pipeline. Every other object is created per file and
used by one worker; `SourceDocument`, `SuppressionMap`, and `ReferenceIndex` are immutable after
construction.

Supporting value types are documented with the unit that owns them: `LineEnding` and
`LineEndingInfo` with `SourceDocument`; `ParsedDocument`, `BlockInfo`, `BlockKind`, and `LeafLine`
with `DocumentParser`; `SuppressionRange`, `RuleSet`, and `RuleSetKind` with `SuppressionMap`; and
`ReferenceDefinition` with `ReferenceIndex`.
