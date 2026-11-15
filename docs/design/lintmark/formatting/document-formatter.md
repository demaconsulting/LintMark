### DocumentFormatter

![Formatting Structure](FormattingView.svg)

#### Purpose

`DocumentFormatter` turns a parsed document into the edits and the line ending that bring it into canonical form,
without writing anything. It owns the order of the stages (see *Formatting Design*), the claiming of blocks by block
formatters, the composition of the edits of all stages against the original text, the decision of the line ending, and
the findings of the formatting step. Its supporting types are described here: `IBlockFormatter`, `FormatContext`,
`FormatResult`, `LineLayout`, `LineOwnership`, `RawTextElements`, and `FrontMatterGuard`.

#### Data Model

**DocumentFormatter**: A sealed class with immutable state: `_formatters` (the registered `IBlockFormatter`s in priority
order, copied from the constructor), `_inline` (optional `InlineNormalizer`), `_paragraphs` (optional
`ParagraphFormatter`), and `_links` (optional `LinkRefactorer`); an absent optional collaborator switches its stage
off. The constant `LineEndingRuleId` is `line-ending`. `LintMarkEngine.CreateDefaultFormatter` registers heading, code
block, thematic break, list, block quote, and table formatters, in this order, plus one of each optional collaborator.

**IBlockFormatter**: `RuleIds` (rules enforced; a block is left as written when any is suppressed inside it),
`CanFormat(BlockInfo)`, and `Format(FormatContext, BlockInfo, EditSet)`. An implementation is a pure function of its
arguments and safe for concurrent use.

**FormatContext**: A sealed record `(Document, Config, Layout, Ownership)` shared by the formatters of one document.

**FormatResult**: A sealed record `(Edits, LineEnding, Diagnostics)`; all collections are immutable.

**LineLayout**: Line arithmetic over the LF-normalized text: `LineCount` (zero for an empty text; the empty line after a
final line feed is not counted), `LineStart`, `LineEnd` (the offset of the line feed), `LineOf(offset)`, `IsBlank`,
`LastContentLine`, `LooksLikeGridBorder`, `NextTabStop` with `TabWidth` of four. Every rule works on whole lines of the
original text because the parser's spans are not reliable enough to derive an edit from.

**LineOwnership**: Per line: the leaf block that covers it, whether it is protected, whether it lies in a container, and
whether it lies in a footnote. Built once per text by `Build`. Ranges run to the last line with content, not to the
parser's span (which may include whitespace-only lines), so a rule never depends on whether such a line is empty. The
exceptions are blocks whose trailing blank lines are content (unclosed fence, HTML blocks closed only by the end of the
file) and top-level indented code. Protected are fenced, unclosed, and indented code, HTML, math, front matter, grid
tables, multi-line link reference definitions, unreferenced footnotes, unmodeled blocks, and leaf blocks selected by
`RawTextElements`. `TopLevel` lists the blocks without parent; `IsContainer` is true for list, item, quote, footnote.

**RawTextElements**: Finds leaf blocks inside an open `pre`, `script`, `style`, or `textarea` element that the Markdown
text opens and does not close in the same block; whitespace there is visible in the page, so these blocks are
protected. **FrontMatterGuard**: `CouldOpenFrontMatter` is true when the first line is `---` and the document has no
front matter, so that a rewrite creating a later `---` line would swallow text into front matter.

#### Key Methods

**Format(ParsedDocument, LintMarkConfig)**: Returns the `FormatResult`.

1. A format-unsafe document returns no edit and no finding, with the majority line ending of the source.
2. Build `LineLayout` and `LineOwnership`. Create one `EditSet`.
3. `AddLeafEdits` visits every unclaimed, unprotected leaf block: paragraphs and headings get trailing-whitespace
   and tab edits through the inline structure and the inline normalizer; table rows, thematic breaks, and single-line
   definitions lose trailing whitespace; lines no leaf covers (container markers) lose trailing whitespace.
   Suppressed rules are skipped. Then `BlankLinePass`, `BlockSpacingPass`, and `TrailerPass` run.
4. Visit the blocks from last to first; the first formatter whose `CanFormat` accepts the block formats it unless a rule
   in `RuleIds` is suppressed in the block. Lists are skipped here.
5. `FormatLists`, `RefactorLinks`, and `WrapParagraphs` each re-parse the text produced by all edits so far (only when
   there are edits), run their step on that parse, and compose the result with `EditRebaser.Rebase`. A re-parse that
   is format-unsafe leaves the edits unchanged.
6. Collect findings: the `line-ending` finding, one finding per always-applied rule and line for each edit
   (`ReportEdits`), the `link-refactor` findings, and the `line-length` and `paragraph-wrap` findings.

**Line ending**: the target is `config.EndOfLine`, else the majority ending of the source. One `line-ending` finding at
1:1 is reported when any terminator differs from the target; the rule cannot be configured or suppressed. The line
ending is a result field because the LF-normalized text has no line ending to edit.

**FormatLists**: Lists are planned on the text of the other rules, because moving an item depends on the lines
around the list (whether the line after an item would become part of it) and those lines may change. The edits of the
lists are composed afterwards, so the result does not depend on rule order and a second run finds nothing to do.

**RefactorLinks**: When `link-refactor` is enabled, `LinkRefactorer.Plan` decides conversions on the produced text. If
a conversion changed a table cell, the tables of the result are laid out again and composed, because the layout of a
table depends on its cells and the second run must not change it.

**WrapParagraphs**: Runs only when wrapping is enabled and either the configured layout is enforced (sentence or
semantic) or some line is wider than the limit (`HasLineWiderThan`, a prefilter that skips ASCII lines no longer than
the limit). `ParagraphFormatter.Format` is called for every paragraph of the final text; each wrap edit is composed with
the other edits and every outcome is turned into findings by `AddLineLengthDiagnostics`: a fixable `line-length`
finding when the wrap is proposed, a lint-only finding for a line that has a break opportunity and stays too wide, a
fixable or lint-only `paragraph-wrap` finding when the paragraph is not in the sentence or semantic layout. Offsets of
the produced text are translated to the original with `EditRebaser.CreateOffsetMapper`.

**ReportEdits**: One finding per rule and line for each edit whose rule is an always-applied formatting rule; edits of
configurable rules are reported by their own formatters.

#### Error Handling

An edit conflict between formatters is a defect: `EditSet.Add` throws `InvalidOperationException`, which propagates to
`LintMarkEngine`, which reports it as a file failure and leaves the file unchanged. Null arguments throw
`ArgumentNullException`. Any other exception of a block formatter propagates unchanged. A document that cannot be
trusted (format-unsafe, or a re-parse that is) is left as written; this is not an error.

#### Dependencies

- **WhitespacePasses**, **EditSet** (both in this subsystem) and the **Blocks** and **Inline** subsystems.
- **Document** subsystem: `ParsedDocument`, `BlockInfo`, `SourceDocument`, `DocumentParser` (re-parse), `TextWidth`.
- **Configuration** subsystem: `LintMarkConfig`. **Linting** subsystem: `RuleCatalog`, `Diagnostic`.
- **Markdig** (OTS): the syntax tree reachable through the parsed blocks.

#### Callers

`LintMarkEngine` (the `Engine` subsystem) calls `Format` for every file; the `Blocks` and `Inline` units and the
passes use `LineLayout`, `LineOwnership`, `FormatContext`, and `FrontMatterGuard`; `TableRules` of the `Linting`
subsystem uses `LineLayout`.
