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
  `BlockInfo` list, the `SuppressionMap`, the `ReferenceIndex`, and, when the document had to be read without grid
  tables, the `GridFallback` location that marks it as format-unsafe (`ParsedDocument.IsFormatUnsafe`).
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

The subsystem consumes the Markdig library (syntax tree and source ranges), the `Linting` subsystem (`RuleCatalog`, for
resolving the rule names of suppression comments), and the .NET base class
library. It is consumed by the `Engine` subsystem, the `Formatting` subsystem (the
document, its blocks, and its suppressions), and the `Safety` subsystem (the shared Markdown pipeline).

### Design

The units form a short pipeline. `SourceDocument` is the foundation: every other unit reads its
LF-normalized text and uses its line table to convert offsets to line and column. `DocumentParser`
parses the normalized text once with a pipeline that is built once per process and shared read-only
by all threads. It then flattens the syntax tree into the block list, compensating for the parser's
range quirks, and builds the `SuppressionMap` and `ReferenceIndex` from the same blocks and tree so
that all three views describe the same parse. Every collection that the result exposes (`SourceDocument.LineStarts`,
`ParsedDocument.Blocks`, `SuppressionMap.Ranges`, `RuleSet.Rules`, `ReferenceIndex.Definitions`) is immutable,
not merely read-only through an interface, so one stage or caller cannot change what the others see.

Block ranges produced by the parser are not trustworthy as they are, and `DocumentParser` compensates for the
behaviors listed by name in *DocumentParser Design* (*Parser Behaviors Compensated*): unclosed fences, trailing
line feeds, synthetic group nodes, definitions reported at the document root, dropped and misplaced footnote
definitions, containers that end before their last child, invalid grid tables, paragraphs that overlap
definitions, definitions whose range includes a rejected title, the difference between LF and CRLF in
multi-line code spans, lazy continuation lines, and released paragraph lines. The result is a block list in
which siblings never overlap and every block lies inside its parent. This holds by construction for the ranges
the unit computes itself, by validation (with the grid table fallback) for the parser's own ranges, and is
checked by a randomized adversarial test; it is not proven for arbitrary input. For the
repository corpus and the generated fixtures, no document text is additionally left unaccounted for outside the
block ranges other than whitespace and container prefixes (quote markers and list markers); this is not
claimed for arbitrary input (text that the parser swallows after a dropped footnote can lie outside every
block). The span-tiling tests of the subsystem check these properties, and a randomized adversarial test checks
the nesting properties.

One compensation is a deliberate behavior and not only a range correction: when the blocks do not nest, the
document is parsed once more without grid tables, so a document that contains both a valid and an invalid grid
table is read without grid tables. The fallback is not silent. The parser locates the first line that looks like a
grid table border but is not part of a valid table, records it as the `GridFallback` of the `ParsedDocument`, and
thereby marks the document as format-unsafe (`IsFormatUnsafe`): the second reading can turn valid grid tables into
paragraphs, which a comparison of rendered output cannot see, so a document that needed it is never rewritten. The
engine reports the recorded line as a `grid-table-invalid` finding unless the rule is switched off or suppressed there,
and refuses to rewrite the document either way. Linting continues to use the second reading. The fallback is a
response to any failure of the nesting check; the grid table parser is the only known cause.

The `SuppressionMap` and `ReferenceIndex` units consume the `BlockInfo` and `BlockKind` types that are
declared with `DocumentParser`, while the parser builds both of them. This is a cycle of types only: the
parser calls the two units and they never call back into it, so there is no cycle of behavior.

`DocumentParser` also passes a rule-name resolver built on the `RuleCatalog` of the `Linting` subsystem to the
`SuppressionMap`, so a suppression comment that names a rule by its legacy rule number suppresses the canonical rule.
The `Linting` subsystem does not depend on `Document`, so this adds no cycle.

Thread safety follows the tool-wide rules in the *LintMark* system design: the only state shared
between threads is the immutable Markdown pipeline. Every other object is created per file and
used by one worker; `SourceDocument`, `SuppressionMap`, and `ReferenceIndex` are immutable after
construction.

Supporting value types are documented with the unit that owns them: `LineEnding` and
`LineEndingInfo` with `SourceDocument`; `ParsedDocument`, `BlockInfo`, `BlockKind`, and `LeafLine`
with `DocumentParser`; `SuppressionRange`, `RuleSet`, and `RuleSetKind` with `SuppressionMap`; and
`ReferenceDefinition` with `ReferenceIndex`; and `GridTableFallback` with `DocumentParser`.
