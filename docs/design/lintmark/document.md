## Document

![Document Structure](DocumentView.svg)

### Overview

The `Document` subsystem turns the bytes of one Markdown file into a trustworthy in-memory model. It
decodes and normalizes the file (`SourceDocument`), parses it with a fixed Markdown pipeline into a
source-ordered block list with corrected source ranges, a suppression map and a link reference index
(`DocumentParser`), resolves suppression comments into offset ranges (`SuppressionMap`), and measures display
width (`TextWidth`). It does no file-system access and produces no output; reading files and reporting failures
belong to the `Engine` subsystem. Contained units: `SourceDocument`, `DocumentParser`, `SuppressionMap`, `TextWidth`.

### Interfaces

Provided (all in-process .NET members; the types are internal except `SourceDocumentException`):

- `SourceDocument.Create` and `FromText` build the immutable normalized document; `ToBytes` restores the
  original encoding, byte order mark and line endings on write. Failures are `SourceDocumentException`
  (`file-encoding-error`, `file-line-ending-error`).
- `DocumentParser.Parse` returns the `ParsedDocument`: source, Markdig syntax tree, `BlockInfo` list,
  `SuppressionMap`, `ReferenceIndex` and the optional grid table fallback location (`IsFormatUnsafe`).
  `GetLines`, `GetContinuationPrefix` and `IsLongerPrefix` describe the lines and container prefixes of leaf blocks.
- `SuppressionMap.IsSuppressed` and `IsSuppressedRange` answer whether a rule is suppressed at an offset or range.
- `TextWidth.Of` measures the terminal columns of a text.

Consumed: Markdig (see the OTS design *Markdig*), the `Linting` subsystem's `RuleCatalog` (rule id and alias
resolution for suppression comments), and the .NET base class library. Consumers are the `Engine` subsystem
(`LintMarkEngine`), the `Linting` rules, the `Formatting` subsystem (document and block formatters), and the
`Configuration` subsystem (`ConfigLoader` reuses `SourceDocument.Decode`).

### Design

The units form a short pipeline. `SourceDocument` is the foundation: every other unit reads its LF-normalized
text and uses its line table to convert offsets to lines and columns. `DocumentParser` parses that text once with
a pipeline that is built once per process and shared read-only. It flattens the syntax tree into the block list,
compensating for the parser's range quirks, and then builds the `SuppressionMap` and `ReferenceIndex` from the
same blocks and tree, so all views describe one parse. `TextWidth` is independent of the others and is used
by the formatting and table-layout code to count columns. Every collection that the results expose is immutable.

The block list is the subsystem's central guarantee: siblings do not overlap and every block lies inside its
parent. The parser does not guarantee this for grid tables, so the nesting is verified; when it fails, the
document is parsed again without grid tables and marked format-unsafe. The formatter never rewrites such a
document, and lint reports a lint-only `grid-table-invalid` finding. See *DocumentParser Design*.

Suppression comments are the other cross-cutting behavior. `DocumentParser` passes the `RuleCatalog` resolver to
`SuppressionMap`, so a comment may name a rule by its id or by a legacy rule-number alias. The `Linting` subsystem
also uses `Document` (its rules read the parsed document), so the two subsystems refer to each other. This is not a
cycle between units: `RuleCatalog` depends on no `Document` unit except the line-ending enumeration of
`SourceDocument`, which depends on nothing. Lint filters individual findings by offset; formatting
skips whole blocks that intersect a suppressed range. See *SuppressionMap Design*.

YAML front matter is parsed as its own block kind and treated as verbatim text: no formatter edits it and it is not
a paragraph, so the paragraph-based line-length checks never apply to it. The only state shared between threads is
the immutable pipeline; every other object is created per file and used by one worker, following the thread-safety
rules in the *LintMark* system design.
