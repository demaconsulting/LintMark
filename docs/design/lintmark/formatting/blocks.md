### Blocks

![Blocks Structure](BlocksView.svg)

#### Overview

The `Blocks` subsystem is nested in the `Formatting` subsystem. It holds the formatters that know the shape of one
kind of block and propose the edits that bring blocks of that kind into canonical form. A formatter only proposes
edits: it never applies one, never writes a file, and never changes the content of code. The subsystem has seven units:

- `HeadingFormatter` - ATX headings (`heading-style`, `heading-space`, `heading-start-left`).
- `CodeBlockFormatter` - fenced code with the configured fence character (`code-fence`, `code-block-style`).
- `ThematicBreakFormatter` - thematic breaks of the configured style (`thematic-break`).
- `BlockQuoteFormatter` - one space after each quote marker (`blockquote-space`).
- `ListFormatter` - marker characters, numbering, columns, and marker spacing (`bullet`, `ordered-style`,
  `list-indent`, `list-marker-space`).
- `TableFormatter` - pipe tables in the compact or aligned layout (`table-pipe-style`, `table-column-style`).
- `ParagraphFormatter` - the overflow-only reflow of paragraphs (`line-length`) and the sentence and semantic
  layouts (`paragraph-wrap`).

The heading, code block, thematic break, and table formatters also enforce `trailing-whitespace` on the lines they
claim, and `HeadingFormatter` enforces `hard-tabs` in heading text. The units share no state and do not call each
other. They use the line arithmetic of `DocumentFormatter` (`LineLayout`, `LineOwnership`, and `FrontMatterGuard`) and
the whitespace helpers of `WhitespacePasses` (`LeafWhitespace`, `InlineMap`) so that the passes and the formatters
treat whitespace and tab stops in the same way.

#### Interfaces

**IBlockFormatter**: Implemented by `HeadingFormatter`, `CodeBlockFormatter`, `ThematicBreakFormatter`,
`BlockQuoteFormatter`, `ListFormatter`, and `TableFormatter`; provided to `DocumentFormatter`.

- *Contract*: `RuleIds` lists the rules the formatter enforces (the block is left as written when any of them is
  suppressed inside it), `CanFormat` accepts the blocks of the unit, and `Format` adds edits to an `EditSet`. Edits lie
  on the lines of the accepted block, never overlap, and each carries the identifier of the rule it enforces.
- *Constraints*: a null argument throws `ArgumentNullException`. The block passed to `Format` must be one that
  `CanFormat` accepted (not verified). Every formatter is stateless, so one instance serves every worker thread.

**ParagraphFormatter**: Not an `IBlockFormatter`; provided to `DocumentFormatter` for its final wrap stage.

- *Contract*: `Format(FormatContext, BlockInfo)` returns a `ParagraphOutcome` (an optional edit that rewrites the whole
  paragraph, the position of the first line that is too wide, and whether such a line stays too wide). The static
  `IsEnabled` and `IsLayoutEnforced` read the configuration; `RuleIds` lists the rules whose suppression keeps a
  paragraph as written.
- *Constraints*: pure and stateless; edits and offsets refer to the text the other rules produce.

The subsystem consumes the `Document` subsystem (`ParsedDocument`, `BlockInfo`, `DocumentParser`, and `TextWidth`),
the `Configuration` subsystem (`LintMarkConfig` and its settings), the `Linting` subsystem (`RuleCatalog`), the
`Inline` subsystem (`AtomTokenizer`, `LineWrapper`, `WrapGuard`, `BlockSyntaxGuard`), and Markdig (OTS). `PipeTable`
and `TableFormatter.PrefixWidth` are also used by `TableRules` of the `Linting` subsystem.

#### Design

**No run-time proof.** Each formatter decides from the source lines and the parser's tree whether the shape is exactly
what the parser promised and whether the new form reads the same; otherwise it proposes nothing. Edits are computed
from source lines, never from block spans (spans of headings, lists, and code blocks are inconsistent about trailing
whitespace). Correctness is not checked at run time. It is established by the unit tests and by the development-time
corpus harness. The run-time safeguards are deterministic: a document that is format-unsafe is not formatted at all,
the edits of all rules are composed without conflict, and the wrap guard keeps paragraph shapes whose meaning depends
on line breaks as written (see *Wrap Guard* in *LineWrapper Design*).

**Claiming.** `DocumentFormatter` gives each block to the first formatter whose `CanFormat` accepts it and keeps its
own passes away from the lines of an accepted leaf block, also when a rule of the formatter is suppressed there.
Headings, code blocks, thematic breaks, and tables are leaf blocks and are claimed. `ListFormatter` and
`BlockQuoteFormatter` accept the outermost container of a nest and handle everything inside it, but `LineOwnership`
claims leaf lines only, so the paragraphs, headings, and code inside a list or quote stay with the passes and the
formatters of those blocks. Their edits replace indentation, markers, and the blanks after markers only.

**Order of work.** `DocumentFormatter` runs the whitespace passes first and then the formatters from the last block to
the first. Lists are planned afterwards on the text those edits produce, tables are laid out again after links are
converted to reference links, and paragraphs are wrapped last on the final text (see *DocumentFormatter Design*).
The result never depends on the order in which edits are added.

**Left as written.** Every unit declines a conversion whose result could be read differently (a nested setext heading,
a braced fence, an indented code block in a container, a list with a lazy line or tab, a table that cannot be read with
certainty, a paragraph the wrap guard refuses); each unit design lists its own conditions.

**Thread safety.** The units are stateless. `ListFormatter` creates a `ListPlan` for one call and uses it on one
thread; `ParagraphFormatter` holds only an immutable rule list. The shared helpers are immutable per document.
