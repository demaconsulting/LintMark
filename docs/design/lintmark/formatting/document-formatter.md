### DocumentFormatter

![Formatting Structure](FormattingView.svg)

#### Purpose

`DocumentFormatter` turns a parsed document into the edits and the line ending that bring it into canonical form,
without writing anything. It walks the blocks in source order, lets the first block formatter that accepts each block
propose edits, leaves blocks with suppressed rules alone, applies the rules that concern every document to everything
that no block formatter claims (trailing whitespace, tabs, blank lines, the blank lines between blocks, and the end of
the file), reports every edit of those rules as a problem, and decides the line ending of the file. Its supporting types
`IBlockFormatter`, `FormatContext`, and `FormatResult`, and the internal helpers that the passes and the block formatters
share, are described in this document.

#### Data Model

**IBlockFormatter**: The interface of a formatter for one kind of block, with three members:

- `ImmutableArray<string> RuleIds`: the rules the formatter enforces; a block is left verbatim when any of them is
  suppressed anywhere inside it.
- `bool CanFormat(BlockInfo block)`: whether the formatter formats the block.
- `void Format(FormatContext context, BlockInfo block, EditSet edits)`: adds the edits that bring an accepted block
  into canonical form.

An implementation must be a pure function of the block, the document, and the configuration (no state between calls
and no side effect other than adding edits) and must be safe for concurrent use, because one instance serves every
worker thread. The block formatters of the tool are in the `Blocks` subsystem (see *Blocks Design*). Because
`DocumentFormatter` does not consult the `rules` setting of the configuration, a formatter must check
`FormatContext.Config.Rules` for its own rules itself when a rule is configurable; the rules of the tool's formatters are
always applied and are switched off for a passage only, with suppression comments.

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
is `line-ending`. The production engine creates the formatter with the three block formatters of the `Blocks`
subsystem (heading, code block, thematic break).

**Rules that apply to every document**: These rules are registered in `RuleCatalog` as always-applied formatting rules
(not configurable, suppressible by comment) and are implemented by the passes below: `trailing-whitespace` (MD009),
`hard-tabs` (MD010), `max-blank-lines` (MD012), `heading-blank-lines` (MD022), `fence-blank-lines` (MD031),
`list-blank-lines` (MD032), `table-blank-lines` (MD058), and `final-newline` (MD047). The block formatters enforce
`heading-style` (MD003, MD021), `heading-space` (MD019), `heading-start-left` (MD023), `thematic-break` (MD035),
`code-block-style` (MD046), and `code-fence` (MD048), and the whitespace rules on their own lines. MD018 and MD020 are not
implemented: `#Title` is a paragraph to the parser, and adding the space would change how the document renders.

**Supporting types** (internal, no unit of their own; shared with the block formatters):

- `LineLayout`: line arithmetic over the LF-normalized text. Lines are numbered from zero, a line ends at its line feed,
  a text that ends with a line feed has no line after it, blank means empty or only spaces and tabs, a tab stop is the
  next multiple of four columns, and `LooksLikeGridBorder` recognizes a line that starts like the border of a grid table
  after quote and list markers.
- `LineOwnership`: which leaf block covers each line and who may edit it. A line of a block that a block formatter
  accepts is *claimed* (also when the block is suppressed): only that formatter edits it. A line of code, HTML, math,
  front matter, a grid table, an unreferenced footnote, a multi-line link reference definition, or a block the tool does
  not model is *protected*: nobody edits its content. Remaining lines belong to the passes. It also records which lines
  lie inside a container, which blocks keep their trailing blank lines (`KeepsTrailingBlankLines`: an unclosed fence, an
  unreferenced footnote, a block the tool does not model, and an HTML block of the types 1 to 5 — comment, script, pre or
  style, processing instruction, declaration, CDATA section — whose own end condition does not occur in the block, so
  that only the end of its container closes it; an HTML block that holds its end condition is closed and keeps nothing;
  a footnote body that is not a paragraph is handled by the end-of-file pass), and the top-level blocks. Every range is
  measured to the last line with content, not to the parser's span, so a line of spaces and an empty line never change
  container membership between runs; the exceptions are blocks whose trailing blank lines are content, and a top-level
  indented code block, whose last line is the last line that has content or that holds only whitespace but is wider than
  the four columns of indentation (the parser keeps what is left of such a line as a line of the code; inside a list item
  it drops the line, so there the measure stays at the last line with content). The parser's span of an unclosed fence
  (and its container) and of an unreferenced footnote stops early, so the lines up to the next top-level block are claimed
  and protected.
- `InlineMap`: per leaf block, the offsets that are plain text, the spans that must never be edited (code spans, inline
  HTML, math, autolink URLs, link destinations and titles, and the label of a link), and the line breaks (a break that
  is hard because of spaces is told apart from a backslash break). `Build` can move every span by a constant, for the
  one case where the parser's spans are known to be off by a fixed amount (an ATX heading with a tab after the number
  signs; see *HeadingFormatter Design*).
- `LeafWhitespace`: the trailing-whitespace and tab edits of the lines of one leaf block. Trailing whitespace goes, except
  that a hard break written with spaces keeps exactly two (a tab before the spaces is text and stays, and a run after a
  backslash is left alone so that a literal backslash does not become a break). Tabs become spaces up to the next tab stop
  of four columns, only after the first plain text of the line (unless the caller says that its stretch starts in the
  text, as the heading formatter does), so indentation and container prefixes never change. A tab in the whitespace at
  the end of a line is never expanded, whether or not `trailing-whitespace` is suppressed: that whitespace belongs to the
  trailing whitespace rule, and a tab turned into spaces could leave two spaces before the line end, which are a hard
  break. A line that looks like a grid table border is never edited, because whether the parser reads it as part of a
  table can depend on the neighboring lines that the same run may change.
- `FrontMatterGuard`: whether a new `---` line could close front matter opened by the first line of the document.
- `BlankLinePass`, `BlockSpacingPass`, and `TrailerPass`: the three document-level passes described under *Key Methods*.

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

Algorithm: (1) for a format-unsafe document, return an empty result with the majority line ending; (2) create a
`LineLayout` and the `LineOwnership` map (a block is claimed when some registered formatter's `CanFormat` accepts it);
(3) create an `EditSet` and a `FormatContext`; (4) for each block in source order, find the first formatter whose
`CanFormat` accepts it; when there is none, or when any of the formatter's `RuleIds` is suppressed anywhere inside the
block (`SuppressionMap.IsSuppressedRange` over the block's range), leave the block verbatim; otherwise call the
formatter's `Format`, which adds its edits; (5) run the whitespace edits of the unclaimed blocks and the three passes
(below); (6) resolve the target line ending; (7) check the line endings, convert the edits of the always-applied rules
into diagnostics, and return the snapshot of the edit set, the target, and the diagnostics.

**Whitespace of unclaimed blocks** (private `AddLeafEdits`): for each leaf block that no formatter claims and that is not
protected, a paragraph or heading goes through `LeafWhitespace` (trailing whitespace unless `trailing-whitespace` is
suppressed in the block, tabs unless `hard-tabs` is), and the lines of a table, a thematic break, or a single-line link
reference definition lose their trailing whitespace. Lines that no leaf block covers and that are not blank hold only
container markers (`>` or a list marker) and lose their trailing whitespace too.

**BlankLinePass**: A run is a maximal sequence of blank lines that no leaf block covers. A run longer than
`config.MaxBlankLines` loses its surplus lines (`max-blank-lines`), and the lines that stay are emptied
(`trailing-whitespace`, since a line of spaces is a blank line with trailing whitespace). Inside a list, list item, block
quote, or footnote the surplus lines are kept (removing a blank line can change whether a list is loose) but their
whitespace is still emptied. The run at the end of the file belongs to the trailer pass. A run that follows a block that
keeps its trailing blank lines, or that touches a line looking like a grid table border, is left alone, and a rule that
is suppressed over the run is skipped.

**BlockSpacingPass**: Two top-level blocks that follow each other without a blank line need one when either is a
heading, a fenced (or convertible indented) code block, a list, or a table. The decision uses the parsed blocks, never
the look of the lines: `text` followed by `2. item` is one paragraph, a line after a list item is a lazy continuation,
and a row after a table is a table row. Only the block kinds whose end the parser reports reliably are separated
(heading, paragraph, fenced code, indented code, HTML block, thematic break, block quote, list, table, link reference
definition, front matter, math block), never a footnote, an unreferenced footnote, a grid table, an unclosed fence, or a
block that the tool does not model. No blank line is inserted inside a container, before a block that the parser
reports after more than three columns or a tab on its first line (for instance a definition that follows other text on
its line; unless it is indented code, which is judged after its conversion to a fenced block), next to a grid-table-like
line, after a block that keeps its trailing blank lines, or between a next-line suppression directive and the first line
of the block it covers (a one-line suppression range that starts at the first line of the later block: the inserted blank
line would take the place of the covered line, the rule that the author switched off would fire in the next run, and the
result would not be stable; the guard also holds back a blank line before a block that carries a `disable-line`
directive on its first line, which only leaves a missing blank line as it was). An indented code block counts as a
fenced block for
`fence-blank-lines`, so that the result is stable after the block formatter converts it. One blank line is inserted per
boundary, as a zero-length edit at the start of the later block's first line, and attributed to the first applicable
rule that is not suppressed on either block, in the order `heading-blank-lines`, `fence-blank-lines`,
`list-blank-lines`, `table-blank-lines`; two rules that inserted at the same offset would conflict. Block formatters never
add a zero-length edit at a line start, so these insertions never collide with theirs.

**TrailerPass**: Everything after the last line with content is the trailer; a whitespace-only line that a block owns (the
rest of an indented code block) counts as content. With `final-newline` on (the default) the
trailer becomes a single line feed, which adds a missing one and removes extra blank lines and whitespace-only lines; with
it off the trailer is removed. A document without content is left alone, a suppression of `final-newline` over the trailer
is honored, and when the last content belongs to a block that keeps its trailing blank lines (an unclosed fence, an
unreferenced footnote, a block the tool does not model, an HTML block of the types 1 to 5 that no end condition closes)
the pass makes no edit at all. A closed HTML block (`text`, a blank line, `<!-- c -->` without a final line ending, or
`<script>` ... `</script>` followed by blank lines) has no trailing content, so its end of file is normalized like
that of any other block. A footnote body that is not a paragraph is also left alone.

**Edits to diagnostics** (private `ReportEdits`): linting a file is formatting it and listing what would change, so
every edit whose rule is an always-applied formatting rule of the catalog (kind `Formatting`, not configurable, and not
`line-ending`) becomes one `Diagnostic` of kind `Formatting`. Its position is the position of the start of the edit
(for an inserted blank line, the line before which it is inserted; for `final-newline`, the end of the last line with
content), its message is the description of the rule in `RuleCatalog`, and at most one diagnostic is created for each
rule and line. Edits of a configurable rule, for example those of a block formatter that a test installs, are not
reported here.

**Idempotency**: formatting the result of a formatting run proposes no edit. This holds because ranges never depend on
whether a line is empty or holds spaces, the guards of the block formatters depend only on kinds that the spacing pass
separates and never on blank lines inserted in the same run, edits of different rules never overlap, and the claimed lines
are edited by one author only. The `Engine` subsystem verifies it for every rewrite with the idempotency check.

**Line ending check** (private): Counts the terminators of the other style than the target in the source's
`LineEndingInfo`. When none, there is no diagnostic. Otherwise one `line-ending` diagnostic of kind `Formatting` is
created at line 1, column 1 with the message `Line endings are CRLF; expected LF` (or the reverse) for a file that
uses one style, or `Mixed line endings (n CRLF, m LF); expected LF` (or `CRLF`) for a mixed file. A file without
terminators never violates the rule.

**Suppression handling**: Suppression is honored at block granularity. A `disable` region, a `disable-next-line`
comment, a `prettier-ignore` comment, and the other directive families of the `SuppressionMap` all make a range
suppressed; the formatter leaves the whole block verbatim when any part of it is suppressed for any rule of its
formatter. The passes check the range of the block, run, boundary, or trailer that they would edit. Rule names in
suppression comments are resolved through the aliases of the rule catalog by `DocumentParser` (see *DocumentParser
Design*), so `markdownlint-disable MD009` and `lintmark-disable trailing-whitespace` suppress the same rule, and a
name that is not a registered rule suppresses nothing registered.

#### Error Handling

`ArgumentNullException` for a null argument and for a null formatter element. A formatter that proposes conflicting
edits makes `EditSet.Add` throw `InvalidOperationException`; any exception a formatter throws propagates unchanged.
Nothing is caught here: `LintMarkEngine` converts every exception of the formatting step into a `file-format-error`
diagnostic for that file and leaves the file untouched. An edit outside the text is not detected here but by
`SourcePatcher`. The passes are written so that their own edits cannot conflict with each other or with those of the
block formatters (see the idempotency paragraph above); a conflict would still surface as the exception above. A line
that does not have the shape the parser promised, and a document that the passes cannot judge safely, produce no edit
instead of an error. The constructors of `FormatContext` and `FormatResult` reject incomplete values (a null document or
configuration; a default array, a default edit, a null diagnostic, or an undefined line ending), so such a value can
never reach a block formatter or the engine.

#### Dependencies

- **ParsedDocument**, **BlockInfo**, **BlockKind**, **SourceDocument**, **LineEnding**, and **SuppressionMap**
  (`Document` subsystem) — the blocks, the majority line ending, the terminator counts, and the suppression queries.
- **LintMarkConfig** (`Configuration` subsystem) — the line ending, `max-blank-lines`, and `final-newline` settings and
  the configuration handed to formatters.
- **Diagnostic**, **DiagnosticKind**, and **RuleCatalog** (`Linting` subsystem) — the `line-ending` finding and the
  identifiers and descriptions of the always-applied rules.
- **EditSet** and **TextEdit** (this subsystem) — the proposed edits.
- **Markdig** (OTS) — the inline tree and block nodes that `InlineMap` and `LineOwnership` read.

#### Callers

- **LintMarkEngine** — calls `Format` for every file that may be formatted, and again for the formatting step of the
  idempotency check; a test seam of the engine installs block formatters (including broken ones).
- **HeadingFormatter**, **CodeBlockFormatter**, and **ThematicBreakFormatter** (`Blocks` subsystem) — use the supporting
  types `LineLayout`, `LineOwnership`, `InlineMap`, `LeafWhitespace`, and `FrontMatterGuard`.
