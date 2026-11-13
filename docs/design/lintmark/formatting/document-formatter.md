### DocumentFormatter

![Formatting Structure](FormattingView.svg)

#### Purpose

`DocumentFormatter` turns a parsed document into the edits and the line ending that bring it into canonical form,
without writing anything. It walks the blocks in source order, lets the first block formatter that accepts each block
propose edits, leaves blocks with suppressed rules alone, and decides the line ending of the file. Its supporting types
`IBlockFormatter`, `FormatContext`, and `FormatResult` are described in this document.

#### Data Model

**IBlockFormatter**: The interface of a formatter for one kind of block, with three members:

- `ImmutableArray<string> RuleIds`: the rules the formatter enforces; a block is left verbatim when any of them is
  suppressed anywhere inside it.
- `bool CanFormat(BlockInfo block)`: whether the formatter formats the block.
- `void Format(FormatContext context, BlockInfo block, EditSet edits)`: adds the edits that bring an accepted block
  into canonical form.

An implementation must be a pure function of the block, the document, and the configuration (no state between calls
and no side effect other than adding edits) and must be safe for concurrent use, because one instance serves every
worker thread. Real block formatters arrive in later phases. Because `DocumentFormatter` does not consult the `rules`
setting of the configuration, a formatter must check `FormatContext.Config.Rules` for its own rules itself.

**FormatContext**: A sealed record with the constructor `(ParsedDocument document, LintMarkConfig config)`, which
rejects a null argument. `Document` is the document the block belongs to (never format-unsafe) and `Config` is the
effective configuration of the file. Immutable and shared by every formatter of one document. Later phases add the
data that wrapping and table formatting need.

**FormatResult**: A sealed record with the constructor `(ImmutableArray<TextEdit> edits, LineEnding lineEnding,
ImmutableArray<Diagnostic> diagnostics)`, which rejects a default array, a default edit, a null diagnostic, and a
line ending that is not defined. It holds the ordered, conflict-free edits to the LF-normalized text, the line ending
the file is written with, and the formatting violations found (the findings that applying the result removes).
Immutable, including its collections.

**DocumentFormatter**: A sealed class with one field, `_formatters`, an immutable array of the registered
`IBlockFormatter` instances in priority order, copied from the constructor argument. The constant `LineEndingRuleId`
is `line-ending`. The production engine creates the formatter without block formatters.

**Line-ending rule**: The target line ending is the `EndOfLine` of the configuration, or, when the configuration gives
none, the majority line ending of the file (CRLF when CRLF terminators outnumber LF terminators, otherwise LF; a tie or
a file without terminators is LF). The rule is violated when the file has at least one terminator that differs from
the target. It is always applied: it is not in the `rules` setting and no suppression comment affects it, because a
whole-file property has no partial form.

**Format-unsafe documents**: A document for which `ParsedDocument.IsFormatUnsafe` is true is never formatted. The
parser had to read it without grid tables, so its blocks no longer describe the file and no edit based on them can be
trusted. The result has no edit and no diagnostic, not even for line endings, and its line ending is the majority
ending of the file, so that rewriting leaves the file byte for byte as it was.

#### Key Methods

**Constructor**: Registers the block formatters.

- *Parameters*: `IReadOnlyList<IBlockFormatter>? formatters` (default `null`, meaning none).
- *Postconditions*: the list is copied into an immutable array, so a caller cannot change it afterwards.
- *Errors*: a null element of the list is rejected up front with `ArgumentNullException`.

**Format**: Formats a document.

- *Parameters*: `ParsedDocument document`; `LintMarkConfig config`.
- *Returns*: `FormatResult`.
- *Preconditions*: neither argument is null.
- *Postconditions*: nothing is written or changed; the edits are not applied; the edits are ordered and conflict-free.

Algorithm: (1) for a format-unsafe document, return an empty result with the majority line ending; (2) create an
`EditSet` and a `FormatContext`; (3) for each block in source order, find the first formatter whose `CanFormat`
accepts it; when there is none, or when any of the formatter's `RuleIds` is suppressed anywhere inside the block
(`SuppressionMap.IsSuppressedRange` over the block's range), leave the block verbatim; otherwise call the
formatter's `Format`, which adds its edits; (4) resolve the target line ending; (5) check the line endings and return
the snapshot of the edit set, the target, and the diagnostics.

**Line ending check** (private): Counts the terminators of the other style than the target in the source's
`LineEndingInfo`. When none, there is no diagnostic. Otherwise one `line-ending` diagnostic of kind `Formatting` is
created at line 1, column 1 with the message `Line endings are CRLF; expected LF` (or the reverse) for a file that
uses one style, or `Mixed line endings (n CRLF, m LF); expected LF` (or `CRLF`) for a mixed file. A file without
terminators never violates the rule.

**Suppression handling**: Suppression is honored at block granularity. A `disable` region, a `disable-next-line`
comment, a `prettier-ignore` comment, and the other directive families of the `SuppressionMap` all make a range
suppressed; the formatter leaves the whole block verbatim when any part of it is suppressed for any rule of its
formatter. Suppression comment rule names are not yet resolved through the aliases of the rule catalog (see
*DocumentParser Design*); until that wiring exists only a canonical rule identifier or a directive without rule names
suppresses a block.

#### Error Handling

`ArgumentNullException` for a null argument and for a null formatter element. A formatter that proposes conflicting
edits makes `EditSet.Add` throw `InvalidOperationException`; any exception a formatter throws propagates unchanged.
Nothing is caught here: `LintMarkEngine` converts every exception of the formatting step into a `file-format-error`
diagnostic for that file and leaves the file untouched. An edit outside the text is not detected here but by
`SourcePatcher`. The constructors of `FormatContext` and `FormatResult` reject incomplete values (a null document or
configuration; a default array, a default edit, a null diagnostic, or an undefined line ending), so such a value can
never reach a block formatter or the engine.

#### Dependencies

- **ParsedDocument**, **BlockInfo**, **SourceDocument**, **LineEnding**, and **SuppressionMap** (`Document` subsystem)
  — the blocks, the majority line ending, the terminator counts, and the suppression queries.
- **LintMarkConfig** (`Configuration` subsystem) — the line ending setting and the configuration handed to
  formatters.
- **Diagnostic**, **DiagnosticKind**, and **RuleCatalog** (`Linting` subsystem) — the `line-ending` finding.
- **EditSet** and **TextEdit** (this subsystem) — the proposed edits.

#### Callers

- **LintMarkEngine** — calls `Format` for every file that may be formatted, and again for the formatting step of the
  idempotency check; a test seam of the engine installs block formatters (including broken ones).
