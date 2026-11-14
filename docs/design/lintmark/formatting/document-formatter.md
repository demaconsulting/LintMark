### DocumentFormatter

![Formatting Structure](FormattingView.svg)

#### Purpose

`DocumentFormatter` turns a parsed document into the edits and the line ending that bring it into canonical form,
without writing anything. It applies the rules that concern every document to everything that no block formatter claims
(trailing whitespace, tabs, blank lines, the blank lines between blocks, and the end of the file) and gives each
unclaimed paragraph to the inline normalizer (emphasis, strong emphasis, bare URLs, and code span padding). It then lets
the first block formatter that accepts each block propose edits, from the last block to the first, and leaves blocks
with suppressed rules alone. In a second stage it wraps the paragraphs that are still wider than the line length, on the
text that all of those rules produce, and composes the wraps with their edits. It reports every edit of an
always-applied rule and every wrap as a problem and decides the line ending of the file. Its supporting types
`IBlockFormatter`, `FormatContext`, and `FormatResult`, and the internal helpers that the passes and the block
formatters share, are described in this document. The supporting type `EditRebaser`, which composes the two stages, is
described in *EditSet Design*.

#### Data Model

**IBlockFormatter**: The interface of a formatter for one kind of block, with three members:

- `ImmutableArray<string> RuleIds`: the rules the formatter enforces; a block is left verbatim when any of them is
  suppressed anywhere inside it.
- `bool CanFormat(BlockInfo block)`: whether the formatter formats the block.
- `void Format(FormatContext context, BlockInfo block, EditSet edits)`: adds the edits that bring an accepted block
  into canonical form.

An implementation must be a pure function of the block, the document, and the configuration (no state between calls and
no side effect other than adding edits) and must be safe for concurrent use, because one instance serves every worker
thread. The block formatters of the tool are in the `Blocks` subsystem (see *Blocks Design*). Because
`DocumentFormatter` does not consult the `rules` setting of the configuration, a formatter must check
`FormatContext.Config.Rules` for its own rules itself when a rule is configurable; the rules of the tool's formatters
are always applied and are switched off for a passage only, with suppression comments.

**FormatContext**: A sealed record, immutable and shared by every formatter of one document, with the members `Document`
(the document the block belongs to, never format-unsafe), `Config` (the effective configuration of the file), `Layout`
(the `LineLayout` of the document), and `Ownership` (the `LineOwnership` map that the document formatter computed, so a
formatter of a container knows which lines hold code or other protected content and which are claimed). The constructor
`(ParsedDocument document, LintMarkConfig config, LineLayout layout, LineOwnership ownership)` is the one that
`DocumentFormatter` uses. The constructor `(ParsedDocument document, LintMarkConfig config)` is kept for callers that
have no document formatter around them: it creates its own `LineLayout` and a `LineOwnership` in which no block
formatter has claimed a line. Every constructor rejects a null argument. The second stage builds a context over the text
that the other rules produce, with an ownership map in which no formatter has claimed a line, and gives it to the
paragraph formatter of the `Blocks` subsystem. Later phases add the data that table formatting needs.

**FormatResult**: A sealed record with the constructor `(ImmutableArray<TextEdit> edits, LineEnding lineEnding,
ImmutableArray<Diagnostic> diagnostics)`, which rejects a default array, a default edit, a null diagnostic, and a
line ending that is not defined. It holds the ordered, conflict-free edits to the LF-normalized text, the line ending
the file is written with, and the findings of the formatting step: the formatting violations (the findings that applying
the result removes) and the lint-only `line-length` findings of paragraphs that could not be wrapped.
Immutable, including its collections.

**DocumentFormatter**: A sealed class with three fields: `_formatters`, an immutable array of the registered
`IBlockFormatter` instances in priority order, copied from the constructor argument, `_inline`, the optional
`InlineNormalizer` (`null` for none, in which case no inline rule is applied), and `_paragraphs`, the optional
`ParagraphFormatter` (`null` for none, in which case nothing is wrapped and no `line-length` finding is reported). The
constant `LineEndingRuleId` is `line-ending`. The production engine creates the formatter with the five block formatters
of the `Blocks` subsystem (heading, code block, thematic break, list, block quote, in this order), a new
`InlineNormalizer`, and a new `ParagraphFormatter`.

**Rules that apply to every document**: These rules are registered in `RuleCatalog` as always-applied formatting rules
(not configurable, suppressible by comment) and are implemented by the passes below: `trailing-whitespace` (MD009),
`hard-tabs` (MD010), `max-blank-lines` (MD012), `heading-blank-lines` (MD022), `fence-blank-lines` (MD031),
`list-blank-lines` (MD032), `table-blank-lines` (MD058), and `final-newline` (MD047). The block formatters enforce
`heading-style` (MD003, MD021), `heading-space` (MD019), `heading-start-left` (MD023), `thematic-break` (MD035),
`code-block-style` (MD046), `code-fence` (MD048), `bullet` (MD004), `ordered-style` (MD029), `list-indent` (MD005,
MD007), `list-marker-space` (MD030), and `blockquote-space` (MD027), and the whitespace rules on their own lines. The
inline normalizer enforces `emphasis` (MD049), `strong` (MD050), `bare-urls` (MD034), and `code-span-space` (MD038). The
configurable rule `line-length` (MD013) is the exception: it is enforced by the second stage through the paragraph
formatter, and it is applied only when the settings ask for it (`wrap` is `fill`, `line-length` is not 0, and the rule
is switched on in `rules`). MD018 and MD020 are not implemented: `#Title` is a paragraph to the parser, and adding the
space would change how the document renders. MD037 and MD039 are not implemented either: removing the spaces they
describe changes how the text renders.

**Supporting types** (internal, no unit of their own; shared with the block formatters):

- `LineLayout`: line arithmetic over the LF-normalized text. Lines are numbered from zero, a line ends at its line feed,
  a text that ends with a line feed has no line after it, blank means empty or only spaces and tabs, and a tab stop is
  the next multiple of four columns. Three members answer questions about plus signs and footnotes.
  `LooksLikeGridBorder` recognizes the exact shape of the border of a grid table: a plus sign after any quote markers,
  list markers, and indentation, followed by nothing but dashes, equal signs, colons, plus signs, and blanks, with at
  least one dash or equal sign (a line that merely starts with a plus sign, such as an item of a plus sign list, is not
  a border). `StartsWithPlusSign` is the broad question that the thematic break rule needs (does the line start with a
  plus sign after those prefixes, whatever follows). The static `HasFootnoteDefinitionMarker` finds `[^label]:` anywhere
  in a line, deliberately wider than the syntax, so that the formatters that change columns (lists and quotes) leave
  blocks alone that the parser reads inconsistently.
- `LineOwnership`: which leaf block covers each line and who may edit it. A line of a block that a block formatter
  accepts is *claimed* (also when the block is suppressed): only that formatter edits it. A line of code, HTML, math,
  front matter, a grid table, an unreferenced footnote, a multi-line link reference definition, or a block the tool does
  not model is *protected*: nobody edits its content. Remaining lines belong to the passes. It also records which lines
  lie inside a container, which blocks keep their trailing blank lines (`KeepsTrailingBlankLines`: an unclosed fence, an
  unreferenced footnote, a block the tool does not model, and an HTML block of the types 1 to 5 — comment, script, pre
  or style, processing instruction, declaration, CDATA section — whose own end condition does not occur in the block, so
  that only the end of its container closes it; an HTML block that holds its end condition is closed and keeps nothing;
  a footnote body that is not a paragraph is handled by the end-of-file pass), and the top-level blocks. Every range is
  measured to the last line with content, not to the parser's span, so a line of spaces and an empty line never change
  container membership between runs; the exceptions are blocks whose trailing blank lines are content, and a top-level
  indented code block, whose last line is the last line that has content or that holds only whitespace but is wider than
  the four columns of indentation (the parser keeps what is left of such a line as a line of the code; inside a list
  item it drops the line, so there the measure stays at the last line with content). The parser's span of an unclosed
  fence (and its container) and of an unreferenced footnote stops early, so the lines up to the next top-level block are
  claimed and protected.
- `InlineMap`: per leaf block, the offsets that are plain text, the spans that must never be edited (code spans, inline
  HTML, math, autolink URLs, link destinations and titles, and the label of a link), and the line breaks (a break that
  is hard because of spaces is told apart from a backslash break). `Build` can move every span by a constant, for the
  one case where the parser's spans are known to be off by a fixed amount (an ATX heading with a tab after the number
  signs; see *HeadingFormatter Design*).
- `LeafWhitespace`: the trailing-whitespace and tab edits of the lines of one leaf block. Trailing whitespace goes,
  except that a hard break written with spaces keeps exactly two (a tab before the spaces is text and stays, and a run
  after a backslash is left alone so that a literal backslash does not become a break). Tabs become spaces up to the
  next tab stop of four columns, only after the first plain text of the line (unless the caller says that its stretch
  starts in the text, as the heading formatter does), so indentation and container prefixes never change. A tab in the
  whitespace at the end of a line is never expanded, whether or not `trailing-whitespace` is suppressed: that whitespace
  belongs to the trailing whitespace rule, and a tab turned into spaces could leave two spaces before the line end,
  which are a hard break. A line that has the exact shape of a grid table border (`LooksLikeGridBorder`) is never
  edited, because whether the parser reads it as part of a table can depend on the neighboring lines that the same run
  may change.
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

**Constructor**: Registers the block formatters, the inline normalizer, and the paragraph formatter.

- *Parameters*: `IReadOnlyList<IBlockFormatter>? formatters` (default `null`, meaning none), `InlineNormalizer? inline`
  (default `null`, meaning no inline rule is applied), and `ParagraphFormatter? paragraphs` (default `null`, meaning
  nothing is wrapped and no `line-length` finding is reported).
- *Postconditions*: the list is copied into an immutable array, so a caller cannot change it afterwards.
- *Errors*: a null element of the list is rejected up front with `ArgumentNullException`.

**Format**: Formats a document.

- *Parameters*: `ParsedDocument document`; `LintMarkConfig config`.
- *Returns*: `FormatResult`.
- *Preconditions*: neither argument is null.
- *Postconditions*: nothing is written or changed; the edits are not applied; the edits are ordered and conflict-free.

Algorithm: (1) for a format-unsafe document, return an empty result with the majority line ending; (2) create a
`LineLayout` and the `LineOwnership` map (a block is claimed when some registered formatter's `CanFormat` accepts it);
(3) create an `EditSet` and run the passes first: the whitespace edits and the inline edits of the unclaimed blocks,
then the three passes (below); (4) create a `FormatContext` with the layout and the ownership map; (5) visit the blocks
in **reverse source order**, from the last block to the first: for each block, find the first formatter whose
`CanFormat` accepts it; when there is none, or when any of the formatter's `RuleIds` is suppressed anywhere inside the
block (`SuppressionMap.IsSuppressedRange` over the block's range), leave the block verbatim; otherwise call the
formatter's `Format`, which adds its edits; (6) resolve the target line ending, snapshot the edits (the **first-stage
edits**), and convert them into diagnostics; (7) run the second stage on the text that the first-stage edits produce
(below), which returns the final edits and the `line-length` diagnostics; (8) check the line endings and return the
final edits, the target, and the diagnostics of the line ending, of the first-stage edits, and of the second stage, in
this order.

*Order and the idempotency principle*: the order of the calls never changes the result, because the snapshot orders the
edits by position. It decides what a formatter can see. The passes run before every block formatter, and the blocks are
visited from the end of the document to its start (the blocks nested in a container follow it in the list, so they are
visited before it), so a formatter sees the edits proposed for the blocks after its own and by the passes. A formatter
that needs to judge the lines after its block (a list that must not move over a line that would then
belong to it) judges the text as it will be after this run. The rule behind it: a decision must not depend on text that
another formatter or pass changes in the same run, otherwise the next run would decide differently and the result would
not be idempotent.

**Whitespace and inline text of unclaimed blocks** (private `AddLeafEdits`): for each leaf block that no formatter
claims and that is not protected, a paragraph or heading goes through `LeafWhitespace` (trailing whitespace unless
`trailing-whitespace` is suppressed in the block, tabs unless `hard-tabs` is), and the lines of a table, a thematic
break, or a single-line link reference definition lose their trailing whitespace. A paragraph (also one inside a list, a
quote, or a footnote) is then given to `InlineNormalizer.AddBlockEdits` when a normalizer is registered (the normalizer
keeps emphasis that ends the last paragraph of a footnote as written); the normalizer checks the suppression of its four
rules itself, and a heading or a table cell never reaches it. Lines that no leaf block covers and that are not blank
hold only container markers (`>` or a list marker) and lose their trailing whitespace too.

**BlankLinePass**: A run is a maximal sequence of blank lines that no leaf block covers. A run longer than
`config.MaxBlankLines` loses its surplus lines (`max-blank-lines`), and the lines that stay are emptied
(`trailing-whitespace`, since a line of spaces is a blank line with trailing whitespace). Inside a list, list item,
block quote, or footnote the surplus lines are kept (removing a blank line can change whether a list is loose) but their
whitespace is still emptied. The run at the end of the file belongs to the trailer pass. A run that follows a block that
keeps its trailing blank lines, or that touches a line looking like a grid table border, is left alone, and a rule that
is suppressed over the run is skipped.

**BlockSpacingPass**: Two top-level blocks that follow each other without a blank line need one when either is a
heading, a fenced (or convertible indented) code block, a list, or a table. The decision uses the parsed blocks, never
the look of the lines: `text` followed by `2. item` is one paragraph, a line after a list item is a lazy continuation,
and a row after a table is a table row. Only the block kinds whose end the parser reports reliably are separated
(heading, paragraph, fenced code, indented code, HTML block, thematic break, block quote, list, table, link reference
definition, front matter, math block), never a footnote, an unreferenced footnote, a grid table, an unclosed fence, or a
block that the tool does not model. No blank line is inserted inside a container, before a block that the parser reports
after more than three columns or a tab on its first line (for instance a definition that follows other text on its line;
unless it is indented code, which is judged after its conversion to a fenced block), next to a grid-table-like line,
after a block that keeps its trailing blank lines, or between a next-line suppression directive and the first line of
the block it covers (a one-line suppression range that starts at the first line of the later block: the inserted blank
line would take the place of the covered line, the rule that the author switched off would fire in the next run, and the
result would not be stable; the guard also holds back a blank line before a block that carries a `disable-line`
directive on its first line, which only leaves a missing blank line as it was). An indented code block counts as a
fenced block for `fence-blank-lines`, so that the result is stable after the block formatter converts it. One blank line
is inserted per boundary, as a zero-length edit at the start of the later block's first line, and attributed to the
first applicable rule that is not suppressed on either block, in the order `heading-blank-lines`, `fence-blank-lines`,
`list-blank-lines`, `table-blank-lines`; two rules that inserted at the same offset would conflict. Block formatters
never add a zero-length edit at a line start, so these insertions never collide with theirs.

**TrailerPass**: Everything after the last line with content is the trailer; a whitespace-only line that a block owns
(the rest of an indented code block) counts as content. With `final-newline` on (the default) the trailer becomes a
single line feed, which adds a missing one and removes extra blank lines and whitespace-only lines; with it off the
trailer is removed. A document without content is left alone, a suppression of `final-newline` over the trailer is
honored, and when the last content belongs to a block that keeps its trailing blank lines (an unclosed fence, an
unreferenced footnote, a block the tool does not model, an HTML block of the types 1 to 5 that no end condition closes)
the pass makes no edit at all. A closed HTML block (`text`, a blank line, `<!-- c -->` without a final line ending, or
`<script>` ... `</script>` followed by blank lines) has no trailing content, so its end of file is normalized like that
of any other block. A footnote body that is not a paragraph is also left alone.

**Second stage: wrapping paragraphs** (private `WrapParagraphs`, `SettleWraps`, `WrapIsStable`, and `HasLineWiderThan`):
the first-stage rules change the lines that a wrap reads (a list that moves, a quote that loses a space, emphasis that
changes its delimiter) and their edits overlap the lines that a wrap rewrites. The wrap therefore reads the text that
the first-stage edits produce, `T1`, and its edits are composed with the first-stage edits afterwards, so the result is
the same as running the rules one after the other and the edits stay relative to the original text. A wrap that is
stable in `T1` is a function of canonical text, which is what makes the result stable when the formatted file is
formatted again.

1. *Gates*: the stage runs only when a paragraph formatter is registered and `ParagraphFormatter.IsEnabled` is true for
   the configuration (`wrap` is `fill`, `line-length` is not 0, the rule `line-length` is on). Otherwise the first-stage
   edits are the result and nothing is reported.
2. *Prefilter*: `T1` is built with `SourcePatcher.Apply`. When no line of it, without trailing blanks, is wider than the
   line length, the stage ends without a parse (the common case). A line of printable ASCII is as wide as it is long,
   and a line of at most half the limit cannot be too wide, because no character is wider than two columns, so most
   lines are not measured with `TextWidth`.
3. *Parse*: `T1` is parsed again, except that the original `ParsedDocument` and `LineLayout` are reused when no
   first-stage edit exists. A `T1` that is format-unsafe skips the stage. A `FormatContext` is built over `T1` with an
   ownership map in which nothing is claimed.
4. *Paragraphs*: every paragraph block of `T1` is given to `ParagraphFormatter.Format`; the outcomes that have an
   overflow offset (a line that is too wide and could be broken) are kept, in document order.
5. *Settle*: the wraps are checked against the other rules (below), and a wrap that would change again is withdrawn.
6. *Composition*: the edits of the wraps that stand are collected in an `EditSet` and composed with the first-stage
   edits by `EditRebaser.Rebase`: a first-stage edit whose text lies inside a rewritten range is **absorbed** (the wrap
   already contains its effect), and the result is again ordered, conflict-free, and relative to the original text.
7. *Diagnostics*: every outcome is reported (see below).

**Settling the wraps** (private `SettleWraps`): wrapping moves words to the start and the end of lines, and a rule may
treat a word differently there; the inline normalizer decides about a bare URL by the character before it, which is the
quote marker at the start of a line and a blank elsewhere. A wrap after which the inline normalizer or the paragraph
formatter would propose an edit is not a result that a second run could leave alone, so it is not made. The text with
every wrap applied is
parsed once more; each paragraph that was wrapped is located by its start offset (shifted by the wraps before it) and
given to the inline normalizer (`AddBlockEdits` into a scratch `EditSet`) and to the paragraph formatter once more. When
either proposes an edit, or the paragraph cannot be found, its wrap is **withdrawn**: the paragraph stays as written and
its outcome becomes a residual overflow without an edit, so it is reported lint-only. When the wrapped text is
format-unsafe, every wrap is withdrawn. The wraps do not depend on each other, so withdrawing one does not change
another.

**Wrap diagnostics** (private `AddLineLengthDiagnostics`): for each outcome the position is the offset of the first
character beyond the line length, mapped from `T1` to the original text with `EditRebaser.MapOffset` (an offset inside
text that a first-stage edit wrote maps to the start of the range that edit replaced) and then to a line and a 1-based
column of the original document (columns count UTF-16 code units). When the outcome has an edit, a `Formatting`
diagnostic of the rule `line-length` is created with the message `Line is N columns wide; the limit is L` (N is the
display width of that line in `T1`); it is fixable, so `--fix` removes it by writing the wrap. When a line that could be
broken stays too wide (the outcome has a residual overflow, also when the wrap was withdrawn or refused), a `LintOnly`
diagnostic of the same rule and position is created with the same message extended by the words `and the paragraph
cannot be wrapped automatically`; rewriting cannot fix it, so it stays after `--fix`. A paragraph whose only too-wide
lines are
single atoms yields nothing. The diagnostics of the first-stage edits are computed **before** the second stage, so the
edits that a wrap absorbs are still reported at the place they changed.

**Edits to diagnostics** (private `ReportEdits`): linting a file is formatting it and listing what would change, so
every edit whose rule is an always-applied formatting rule of the catalog (kind `Formatting`, not configurable, and not
`line-ending`) becomes one `Diagnostic` of kind `Formatting`. Its position is the position of the start of the edit
(for an inserted blank line, the line before which it is inserted; for `final-newline`, the end of the last line with
content), its message is the description of the rule in `RuleCatalog`, and at most one diagnostic is created for each
rule and line. Edits of a configurable rule, for example those of a block formatter that a test installs, are not
reported here.

**Idempotency**: formatting the result of a formatting run proposes no edit. This holds because ranges never depend on
whether a line is empty or holds spaces, the guards of the block formatters depend only on kinds that the spacing pass
separates and never on blank lines inserted in the same run, edits of different rules never overlap, the claimed lines
are edited by one author only, and a decision of a formatter never depends on text that another formatter or pass
changes in the same run (see *Order and the idempotency principle* under **Format**). The second stage keeps the
property in two ways: it reads the text that the first stage produces, so the trigger and the result of a wrap are
functions of canonical text, and it withdraws a wrap after which the inline normalizer or the paragraph formatter would
still propose an edit (see *Settling the wraps*). The `Engine` subsystem verifies it for every rewrite with the
idempotency check.

**Line ending check** (private): Counts the terminators of the other style than the target in the source's
`LineEndingInfo`. When none, there is no diagnostic. Otherwise one `line-ending` diagnostic of kind `Formatting` is
created at line 1, column 1 with the message `Line endings are CRLF; expected LF` (or the reverse) for a file that
uses one style, or `Mixed line endings (n CRLF, m LF); expected LF` (or `CRLF`) for a mixed file. A file without
terminators never violates the rule.

**Suppression handling**: Suppression is honored at block granularity. A `disable` region, a `disable-next-line`
comment, a `prettier-ignore` comment, and the other directive families of the `SuppressionMap` all make a range
suppressed; the formatter leaves the whole block verbatim when any part of it is suppressed for any rule of its
formatter. The passes check the range of the block, run, boundary, or trailer that they would edit, and the inline
normalizer checks each of its four rules over the range of the paragraph, so a suppressed rule leaves the paragraph
without that rule's edits only. The paragraph formatter is stricter, because a wrap rewrites the whitespace and the
prefix of every line of a paragraph: it keeps a paragraph as written when any of the ten rules of its `RuleIds` (its
own, the whitespace, inline, list, and quote rules that edit paragraph text) is suppressed anywhere in it. Rule names in
suppression comments are resolved through the aliases of the rule catalog by `DocumentParser` (see *DocumentParser
Design*), so `markdownlint-disable MD009` and `lintmark-disable trailing-whitespace` suppress the same rule, and a
name that is not a registered rule suppresses nothing registered.

#### Error Handling

`ArgumentNullException` for a null argument and for a null formatter element. A formatter that proposes conflicting
edits makes `EditSet.Add` throw `InvalidOperationException`; any exception a formatter throws propagates unchanged.
Nothing is caught here: `LintMarkEngine` converts every exception of the formatting step into a `file-format-error`
diagnostic for that file and leaves the file untouched. An edit outside the text is not detected here but by
`SourcePatcher`. The passes are written so that their own edits cannot conflict with each other or with those of the
block formatters (see the idempotency paragraph above); a conflict would still surface as the exception above. The
composition of the second stage is built through `EditSet`, so a conflict there surfaces as the same exception, and
`EditRebaser` rejects first-stage edits that are not ordered or conflict with `ArgumentException`; both are defects, not
document problems. A paragraph that cannot be wrapped safely, a wrap that is withdrawn, and a text that is format-unsafe
after the wraps are results (a lint-only finding, or the unwrapped paragraph), not errors. A line
that does not have the shape the parser promised, and a document that the passes cannot judge safely, produce no edit
instead of an error. The constructors of `FormatContext` and `FormatResult` reject incomplete values (a null document,
configuration, line arithmetic, or ownership map; a default array, a default edit, a null diagnostic, or an undefined
line ending), so such a value can never reach a block formatter or the engine.

#### Dependencies

- **ParsedDocument**, **BlockInfo**, **BlockKind**, **SourceDocument**, **LineEnding**, **DocumentParser**, and
  **SuppressionMap** (`Document` subsystem) — the blocks, the majority line ending, the terminator counts, the
  suppression queries, and the parse of the text of the second stage.
- **LintMarkConfig** (`Configuration` subsystem) — the line ending, `max-blank-lines`, `final-newline`, `line-length`,
  and `wrap` settings and the configuration handed to formatters and to the inline normalizer.
- **Diagnostic**, **DiagnosticKind**, and **RuleCatalog** (`Linting` subsystem) — the `line-ending` and `line-length`
  findings and the identifiers and descriptions of the always-applied rules.
- **EditSet**, **TextEdit**, **EditRebaser**, and **SourcePatcher** (this subsystem) — the proposed edits, their
  composition, and the text that the first-stage edits produce.
- **InlineNormalizer** (`Inline` subsystem) — the edits of emphasis, strong emphasis, bare URLs, and code span padding
  in paragraphs, and the check of a wrap.
- **ParagraphFormatter** and **ParagraphOutcome** (`Blocks` subsystem) — the wrap of each paragraph with a line that is
  too wide.
- **TextWidth** (`Utilities` subsystem) — the width of a line in the prefilter.
- **Markdig** (OTS) — the inline tree and block nodes that `InlineMap` and `LineOwnership` read.

#### Callers

- **LintMarkEngine** — calls `Format` for every file that may be formatted, and again for the formatting step of the
  idempotency check; a test seam of the engine installs block formatters (including broken ones).
- **HeadingFormatter**, **CodeBlockFormatter**, **ThematicBreakFormatter**, **ListFormatter**, and
  **BlockQuoteFormatter** (`Blocks` subsystem) — use the supporting types `LineLayout`, `LineOwnership`, `InlineMap`,
  `LeafWhitespace`, and `FrontMatterGuard` (the two container formatters reach `LineLayout` and `LineOwnership` through
  `FormatContext`).
- **ParagraphFormatter** (`Blocks` subsystem) — reads `FormatContext`, `LineLayout`, and `LineOwnership` (the footnote
  and protected lines) of the text of the second stage.
