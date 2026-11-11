### DocumentParser

![Document Structure](DocumentView.svg)

#### Purpose

`DocumentParser` parses a `SourceDocument` with LintMark's fixed Markdown pipeline and turns the
parser's syntax tree into a flattened, filtered, source-ordered block list whose ranges compensate for
the parser's known quirks. It also assembles the `ParsedDocument` that bundles the tree, the blocks,
the `SuppressionMap`, and the `ReferenceIndex`.

#### Data Model

`DocumentParser` is a static class. Its only state is the pipeline, built once and shared read-only:

| Pipeline setting | Reason |
| --- | --- |
| Precise source locations | Exact block and inline ranges are the basis of every position |
| YAML front matter | Front matter is a distinct block that must not be parsed as Markdown |
| Pipe tables with GFM rules | A pipe inside a code span stays in its cell |
| Grid tables | Pandoc-style tables are recognized as tables |
| Footnotes | Footnote definitions and references are blocks and inlines |
| Task lists | `[ ]` and `[x]` items are recognized |
| Automatic links | Bare URLs are recognized as links |
| Strikethrough (emphasis extras) | `~~text~~` is recognized |
| Alert blocks | `> [!NOTE]` style alerts are recognized (they classify as block quotes) |
| Mathematics | `$$` blocks and `$` inlines are recognized |

The pipeline is a constant of the tool and is not configurable: a configurable parse model would change
which constructs exist and therefore what "canonical" means. Deliberately excluded: automatic heading
identifiers (they inject synthetic definitions), generic attributes (they break whitespace equivalence),
the advanced extension bundle (unpredictable parse changes), and column-width inference for pipe tables
(it would make compact and aligned rewrites non-equivalent). Trivia tracking is never used.

**ParsedDocument**: A record `(SourceDocument Source, MarkdownDocument Ast, IReadOnlyList<BlockInfo> Blocks,
SuppressionMap Suppressions, ReferenceIndex References)`, created per file.

**BlockInfo**: A record `(Block? Node, BlockKind Kind, int Start, int EndInclusive, int Depth, Block?
Parent)`. `EndInclusive` is the effective end (never a line terminator, see Q-1 and Q-2). `Node` is `null`
only for `UnreferencedFootnote`.

**BlockKind**: `Heading`, `Paragraph`, `FencedCode`, `UnclosedFence`, `IndentedCode`, `HtmlBlock`,
`ThematicBreak`, `BlockQuote` (alert blocks also classify as this), `List`, `ListItem`, `Table`,
`GridTable`, `FrontMatter`, `LinkReferenceDefinition`, `Footnote`, `UnreferencedFootnote`, `MathBlock`, and
`Other`. A grid table shares the parser's table type and is recognized by its leading `+`. A fenced block
without a closing fence is `UnclosedFence`.

**LeafLine**: A record `(int Line, int LineStart, int ContentStart, int ContentEnd, bool IsLazy)` describing
one source line of a paragraph or heading.

Quirks and rules handled by the unit (each entry gives the parser behavior, then the handling):

- **R-1**: Reference-definition and footnote groups are synthetic shells with garbage ranges. Group shells
  and nodes with an empty range (end before start) are dropped; their real children are kept.
- **Q-1**: An unclosed fence reports only its opening line. The kind is `UnclosedFence` and the effective end
  is the effective end of the parent, or the end of the file.
- **Q-2**: Lists, items, and indented code may include the trailing newline. The effective end is trimmed so
  it is never a line feed.
- **Q-3**: Footnote definitions without a reference are dropped, and group order is not source order. Blocks
  are sorted by (`Start`, `Depth`); unreferenced definitions are found by a line scan for `[^label]:` outside
  every leaf block and become `UnreferencedFootnote` with `Node` null.
- **Q-4**: Multi-line code spans differ between LF and CRLF input. Only LF-normalized text is ever parsed.
- **Q-5**: Lazy continuation lines look like ordinary lines. `GetLines` flags them with `IsLazy`.
- **Q-8**: Paragraph lines are released after parsing. `GetLines` rebuilds the lines from the source text.
- **R-4**: The content start of a continuation line is not reported. It is the start of the first inline on
  that line; lines that begin inside a multi-line construct fall back to stripping one quote marker per
  enclosing block quote and then whitespace.
- **R-5**: The prefix of continuation lines is not reported. It is the first-line prefix with every character
  except `>`, space, and tab replaced by a space.

Tab-indented containers (lists, quotes, and code blocks indented with tabs) depend on the parser's tab
stop of four; the lazy-line comparison therefore measures prefix width with tab stops of four.

#### Key Methods

**Parse**: Parses a document.

- *Parameters*: `SourceDocument source`.
- *Returns*: `ParsedDocument`.
- *Preconditions*: `source` is not null.
- *Postconditions*: `Blocks` is sorted by (`Start`, `Depth`) with enclosing blocks first; `Suppressions` and
  `References` describe the same parse.

Steps: parse `source.Text` with the shared pipeline; walk the tree (a private flattener) skipping group
shells and empty-range nodes and recording depth and parent; classify each block; clamp the range to the
text and trim trailing line feeds; extend unclosed fences; add unreferenced footnotes; sort; then build
`SuppressionMap` and `ReferenceIndex`. Table rows and cells are not walked.

**GetLines**: Rebuilds the source lines of a paragraph or heading.

- *Parameters*: `ParsedDocument document`, `BlockInfo leaf`.
- *Returns*: `IReadOnlyList<LeafLine>` — one entry per source line.
- *Preconditions*: arguments are not null and `leaf` belongs to `document`.

The first line's content starts at the block start. For each continuation line the content start is the
first inline starting on the line (when only container prefix precedes it), otherwise the fallback strip
above. A continuation line is lazy when its prefix has fewer quote markers, or a smaller visual width, than
the expected continuation prefix, ignoring the single optional space after each quote marker. A setext
heading yields its text line and its underline line.

**GetContinuationPrefix**: Returns the prefix every continuation line would carry if it were not lazy
(R-5): the text before the block on its first line, with list markers, numbers, and footnote labels turned
into spaces and `>`, space, and tab kept.

#### Error Handling

Argument errors raise `ArgumentNullException`. Exceptions raised by Markdig are not caught here; they
propagate to the caller, and `LintMarkEngine` reports them as `file-parse-error` for that file only. The
only regular expression (footnote definition marker) has a match timeout. A block range that would fall
outside the text is clamped.

#### Dependencies

- **Markdig** — parsing and the syntax tree (see the Markdig OTS design).
- **SourceDocument** — the normalized text and the line table.
- **SuppressionMap** and **ReferenceIndex** — built for every document.

#### Callers

- **LintMarkEngine** — parses every file it reads (the call can be replaced in tests through a seam).
