## Formatting

![Formatting Structure](FormattingView.svg)

### Overview

The `Formatting` subsystem turns a parsed document into a proposal: the text edits and the line ending that would
bring the document into canonical form. It never writes anything and never applies a change to a file; it only
describes the change, so that the `Safety` subsystem can verify the result before the `Engine` subsystem lets any
byte reach the disk. All text changes are expressed as edits against the LF-normalized text of the document, so text
that no edit touches is preserved by construction. The subsystem contains three units: `EditSet`, `SourcePatcher`,
and `DocumentFormatter`.

- `EditSet` is the per-file collection of proposed edits, checked for conflicts as they are added. Its supporting
  type `TextEdit` (one change) is described in *EditSet Design*.
- `SourcePatcher` applies an ordered set of edits to the text of a document in one pass.
- `DocumentFormatter` walks the blocks of a document, lets block formatters propose edits, honors suppression
  comments, decides the line ending, and returns a `FormatResult`. Its supporting types `IBlockFormatter`,
  `FormatContext`, and `FormatResult` are described in *DocumentFormatter Design*.

Block formatters (for headings, lists, paragraphs, tables, and so on) do not exist yet. The subsystem
defines the extension point and the machinery around it; with no block formatter registered, which is the production
setup, the only thing the subsystem decides is the line ending.

### Interfaces

**DocumentFormatter.Format**: Formats a parsed document.

- *Type*: In-process .NET instance method.
- *Role*: Provider.
- *Contract*: Takes a `ParsedDocument` and the effective `LintMarkConfig` of the file and returns a `FormatResult`:
  the ordered, conflict-free edits to the LF-normalized text, the line ending to write, and the formatting
  violations found (the `line-ending` diagnostic). Pure: nothing is written and the document is not changed.
- *Constraints*: Throws `ArgumentNullException` for a null argument and `InvalidOperationException` when two
  formatters propose conflicting edits; any other exception of a block formatter propagates unchanged. A format-unsafe
  document yields no edit and no diagnostic, and its returned line ending is `source.MajorityLineEnding` (not the
  configured one), so that rewriting leaves the file byte for byte as it was. Safe for concurrent use when the
  registered formatters are.

**SourcePatcher.Apply**: Applies edits to a document's text.

- *Type*: In-process .NET static method.
- *Role*: Provider.
- *Contract*: Takes a `SourceDocument` and the ordered edits and returns the patched LF-normalized text; the very
  same string instance when there are no edits.
- *Constraints*: Throws `ArgumentNullException` for a null source and `ArgumentException` for uninitialized edits, an
  edit outside the text, or edits that are unordered or conflict.

**EditSet.Add and EditSet.ToImmutable**: Collect and snapshot edits.

- *Type*: In-process .NET instance methods.
- *Role*: Provider.
- *Contract*: `Add` records one `TextEdit`; `ToImmutable` returns the edits ordered by start offset and then length,
  identical for any order of `Add` calls.
- *Constraints*: `Add` throws `InvalidOperationException` for an edit that conflicts with an edit already in the set
  and `ArgumentException` for a default edit. An `EditSet` is used by one thread.

**IBlockFormatter**: The extension point for block formatters.

- *Type*: In-process .NET interface.
- *Role*: Consumer (the subsystem calls implementations that later phases provide).
- *Contract*: `RuleIds` names the rules the formatter enforces, `CanFormat` accepts a block, and `Format` adds edits
  for an accepted block through the `EditSet` of the file.
- *Constraints*: Implementations must be pure and safe for concurrent use.

The subsystem consumes the `Document` subsystem (`ParsedDocument`, `BlockInfo`, `SourceDocument`, `LineEnding`, and
the `SuppressionMap`), the `Configuration` subsystem (`LintMarkConfig`), and the `Linting` subsystem (`Diagnostic` and
the `line-ending` identifier). It is consumed by the `Engine` subsystem.

### Design

The units form a short pipeline that the engine drives for each file. `DocumentFormatter` creates an `EditSet`, offers
every block to the registered formatters, and returns the snapshot of the set. The engine then calls `SourcePatcher`
with that snapshot to obtain the proposed text. The patcher does not trust the edits it is given: it checks each edit
against the text length and against the edit before it, because a wrong edit would silently corrupt a user's file and
the check costs little next to reading the file. `EditSet` rejects conflicts when edits are added; `SourcePatcher`
repeats the check when they are applied, so a defect in either is caught by the other. A conflict is a defect of a
formatter and not of the document, so it is reported by an exception and never resolved silently: dropping or merging
a change would make the output depend on the order in which formatters ran.

The line ending is not an edit. The text that every unit sees is LF-normalized, so there is no line ending in it to
edit. `DocumentFormatter` therefore returns the line ending to write beside the edits, and the engine applies it when
it converts the proposed text back to bytes. The line-ending rule is always applied: it cannot be switched off with the
`rules` setting and cannot be suppressed by a comment.

Safety of the whole subsystem rests on three properties: nothing is applied here, so the caller can still verify the
result; a document the parser could only read by dropping grid tables is never formatted; and a block with a
suppressed rule is left verbatim. Real block formatters arrive in later phases.

Constraint on block formatters: `DocumentFormatter` honors suppression comments but never applies the `rules` setting
of the configuration (the list of enabled and disabled rules). Every real block formatter must therefore check
`FormatContext.Config.Rules` for its own rules and propose no edit for a rule that the configuration disables. This
subsystem has no requirement for that check, because no real formatter exists yet; the requirement belongs to the
formatter that introduces the first rule.

Thread safety: `DocumentFormatter` holds an immutable list of formatters and keeps all state of a call in local
variables, so one instance serves every worker. `EditSet` is a per-file builder used by one thread; its snapshots,
`TextEdit`, `FormatContext`, and `FormatResult` are immutable. `SourcePatcher` is static and stateless.
