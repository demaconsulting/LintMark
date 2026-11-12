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
Parent)`. `Start` is the first and `EndInclusive` the last visible character of the block; the end is the
effective end and never a line terminator (see *Trailing line feed in ranges* below). `Depth` is 0 for a
top-level block, and `Parent` is the parser node of the enclosing block (`null` at top level). `Node` is
`null` only for an `UnreferencedFootnote` that was found by the line scan. A block's `Start` is not always
the parser's own start: footnote definitions start at their marker, and a paragraph or setext heading that
follows link reference definitions starts after them (see below), so `Start` can differ from the node's
`Span.Start`.

**BlockKind**: `Heading`, `Paragraph`, `FencedCode`, `UnclosedFence`, `IndentedCode`, `HtmlBlock`,
`ThematicBreak`, `BlockQuote` (alert blocks also classify as this), `List`, `ListItem`, `Table`,
`GridTable`, `FrontMatter`, `LinkReferenceDefinition`, `Footnote`, `UnreferencedFootnote`, `MathBlock`, and
`Other`. A grid table shares the parser's table type and is recognized by its leading `+`.

- `UnclosedFence` is a fenced code block **or math block** that has no closing delimiter (the parser's math
  block is a fenced block, so both are recognized by the missing closing fence). A closed math block is
  `MathBlock`.
- `UnreferencedFootnote` means that the parser produced no `Footnote` block for the footnote definition:
  nothing refers to it, its label repeats an earlier definition, or it stands inside a block quote or list
  item. The name is historical; the kind covers all three cases.
- `Footnote` is a footnote definition for which the parser did produce a `Footnote` block.

Both footnote kinds start at the `[` of the `[^label]:` marker (indentation excluded) and end at the last
visible character of the definition. `BlockKind`, `BlockInfo`, and `ParsedDocument` are declared with this unit
(see *Dependencies* for the consumers).

**LeafLine**: A record `(int Line, int LineStart, int ContentStart, int ContentEnd, bool IsLazy)` describing
one source line of a paragraph or heading.

**Pipeline**: An `internal static` read-only property holding the fixed pipeline described above. It is built
when the type is first used and never changes afterwards, so it can be shared by all threads. Only `Parse`
uses it; its `internal` access level is not a contract.

#### Parser Behaviors Compensated

The parser's results do not match what a reader sees in several cases. Each behavior is named here; each entry
gives what the parser does, then how the unit handles it. The units and the tests refer to the behaviors by
these names.

- **Group shells**: The parser wraps reference definitions and footnotes in synthetic group nodes whose ranges
  are meaningless, and some nodes have an empty range (end before start). Group nodes and empty-range nodes
  are dropped; their real children are kept, and the blocks are sorted into source order.
- **Unclosed fence extent**: An unclosed fenced code or math block reports only its opening line. The block
  becomes `UnclosedFence` and ends where its parent ends (the end of the file at top level, the end of the
  quote or list item otherwise).
- **Trailing line feed in ranges**: Lists, list items, and indented code can include the line feed after
  their last line. The end is moved back so that it is never a line feed.
- **Definitions reported at document root**: The parser keeps every link reference definition in a group at
  the document root, even when it stands inside a quote or list item. The definition's own range is correct,
  so depth and parent are resolved from containment (see *Parse*).
- **Paragraph and heading overlapping definitions**: A paragraph (or a setext heading, whose text line is
  read as paragraph text first) that follows link reference definitions directly includes them in its range.
  The block's `Start` is moved to the first line after its last definition, past the container prefix, so that
  it no longer overlaps; the node is kept, so `Start` differs from the node's `Span.Start`. An ATX heading
  is a separate block and needs no correction.
- **Dropped footnote definitions**: The parser produces no node for a footnote definition that nothing
  refers to or whose label repeats an earlier definition. These are found by a line scan (see *Parse*) and
  become `UnreferencedFootnote` blocks with a null `Node`.
- **Footnote range starts after the marker**: For a footnote that the parser keeps, the parser's span starts
  after the `[^label]:` marker. The block start is moved back to the `[`, found from the label's own span and
  checked against the text; when the check fails the parser's start is kept.
- **Footnote definitions read as link definitions**: Inside a block quote or list item the parser reads
  `[^label]: text` as a link reference definition whose label begins with `^`. Such a definition (label starts
  with `^` and has no whitespace) is classified `UnreferencedFootnote` and keeps the parser's
  `LinkReferenceDefinition` node; its range is that definition's own range. Continuation lines of such a
  footnote are separate paragraph blocks (a known limitation: the parser does not report them as part of the
  definition). Because the kind is not `LinkReferenceDefinition`, the `ReferenceIndex` does not list it.
- **Container ends before its last child**: The parser's span of a container can end before a child that it
  nests; a footnote followed by an indented HTML block is the known case, where the footnote's span ends inside
  the block. After the walk, the end of each container is extended to the end of its last emitted child,
  innermost first, so that the extension cascades through nested containers and every block lies inside its
  parent. Ends are only extended, never shortened.
- **CRLF code-span difference**: Multi-line code spans parse differently for LF and CRLF input. Only
  LF-normalized text is ever parsed, which `SourceDocument` guarantees.
- **Lazy continuation lines**: A lazy line (a continuation that omits the container prefix) looks like any other
  paragraph line. `GetLines` flags it with `IsLazy`.
- **Released paragraph lines**: The parser releases the line information of a paragraph after parsing.
  `GetLines` rebuilds the lines from the source text and the block range.
- **Continuation content start and prefix not reported**: The parser does not say where the content of a
  continuation line starts or what prefix it carries. The content start is the start of the first inline on the
  line; lines that begin inside a multi-line construct (a code span) or carry no inline (a setext underline) fall
  back to stripping one quote marker per enclosing block quote and then whitespace. The prefix is derived from
  the first line (see *GetContinuationPrefix*).

Tab-indented containers (lists, quotes, and code blocks indented with tabs) depend on the parser's tab
stop of four; the lazy-line comparison therefore measures prefix width with tab stops of four.

#### Key Methods

**Parse**: Parses a document.

- *Parameters*: `SourceDocument source`.
- *Returns*: `ParsedDocument`.
- *Preconditions*: `source` is not null.
- *Postconditions*: `Blocks` is sorted by (`Start`, `Depth`) with enclosing blocks first; siblings do not
  overlap and every block lies inside its parent (a container ends no earlier than its last child);
  `Suppressions` and `References` describe the same parse.

Steps: parse `source.Text` with the shared pipeline, then flatten the syntax tree with a private flattener:

1. **Walk** the tree, skipping group shells and empty-range nodes (keeping their real children) and recording
   depth and parent. For each block, classify it, clamp its range to the text, move a footnote's start back to
   its marker, extend an unclosed fence to the end of its parent, and trim trailing line feeds. The effective
   end of each block is remembered so that an unclosed fence inside it can end with it. Table rows and cells
   are not walked. Link reference definitions found in the groups are collected, not emitted.
2. **Extend the containers**. In reverse emission order (every block is emitted before its children, so each
   child is final before its parent is updated), the end of a block's parent is raised to the block's end when
   the parent ends earlier (see *Container ends before its last child*).
3. **Add the definitions**. Every collected definition is emitted with its parent resolved from containment:
   the innermost emitted block quote, list, list item, or footnote whose range contains the definition's
   range, and depth one more than that block (top level, depth 0, when none contains it). A definition whose
   label starts with `^` is classified `UnreferencedFootnote` (see *Footnote definitions read as link
   definitions*).
4. **Trim paragraphs and headings after definitions**. For each paragraph or heading that contains definition
   blocks, `Start` moves to the first line after its last definition, past the container prefix: whitespace
   and at most one `>` per enclosing block quote, so a `>` that is text (an indented line after a definition
   outside any quote) stays in the block.
5. **Add the dropped footnotes** (below).
6. **Sort** by (`Start`, `Depth`). The sort is stable, so blocks with the same start and depth keep their
   emission order.

Then `SuppressionMap` and `ReferenceIndex` are built from the blocks and the tree.

*Complexity*: the flattener is O(n log n) in the number of blocks (and lines). No step scans the block list once
per definition, paragraph, or footnote: the containers and the definitions are visited in order of their start
with a stack of the containers that are still open (step 3); the definitions are sorted by start and a binary
search finds those inside a paragraph (step 4); and the leaf, footnote, and all-block ranges are indexed by start
with a running maximum of the end, so "is this offset inside a block" is a binary search (step 5 and
`FindDefinitionEnd`). Each line is examined a bounded number of times, because the scan that extends a dropped
footnote stops at the next block or definition marker. The results are identical to those of a plain scan. A
document with 160000 blocks (about 0.8 MB) is parsed in about a second, most of which is the Markdown parser.

*Dropped footnote detection* is coverage based; labels are not compared with each other, so letter case and
repeated labels need no special handling. It runs only when the text contains `[^`. A line is a footnote
definition marker when, after at most three spaces, it starts with `[^`, a label of one or more characters
that are neither `]` nor whitespace, and `]:` (a culture-invariant expression with a match timeout). The
marker becomes an `UnreferencedFootnote` block with a null `Node`, depth 0, and no parent, unless its first
character already lies inside a leaf block (a code block, a paragraph, or an earlier definition: such a line is
only text) or inside a `Footnote` block. A repeated label therefore gets a block because the parser
produced no `Footnote` block for the repeated definition, and a referenced footnote gets none because its own
block covers its marker.

The end of a dropped footnote is found by a heuristic (`FindDefinitionEnd`) because the parser reports nothing
for it. The definition ends at the end of its marker line and is extended over each following line that
belongs to the definition: a following non-blank line continues it unless the line lies inside another block,
starts another footnote definition marker, or follows a blank line without being indented (four spaces or a
tab). Blank lines are included only when a continuing line follows them, so the range never ends on blank
text.

**GetLines**: Rebuilds the source lines of a paragraph or heading.

- *Parameters*: `ParsedDocument document`, `BlockInfo leaf`.
- *Returns*: `IReadOnlyList<LeafLine>` — one entry per source line.
- *Preconditions*: arguments are not null; `leaf.Kind` is `Paragraph` or `Heading`; the range of `leaf` lies
  inside the text of `document`; `leaf` is a block of `document`.

The first three preconditions are checked: a null argument raises `ArgumentNullException`, and a block of any
other kind or with a range outside the text raises `ArgumentException`. That `leaf` is one of the blocks of
`document` is **not** verified, because checking would search the block list on every call; passing a block
of another document gives an unspecified result.

The first line's content starts at the block start. For each continuation line the content start is the
first inline starting on the line (when only container prefix precedes it), otherwise the fallback strip
described under *Continuation content start and prefix not reported*. A continuation line is lazy when its
prefix has fewer quote markers, or a smaller visual width, than the expected continuation prefix, ignoring the
single optional space after each quote marker. A setext heading yields its text line and its underline line.
A paragraph that follows definitions starts at its own text, because its `Start` was moved.

**GetContinuationPrefix**: Returns the prefix every continuation line would carry if it were not lazy.

- *Parameters*: `ParsedDocument document`, `BlockInfo leaf`.
- *Returns*: `string`.
- *Preconditions*: the same as for `GetLines`.

The prefix is the text before the block on its first line with every character except `>`, space, and tab
replaced by a space (so `-` gives two spaces and `> 1.` gives `>` followed by four spaces). When that text
ends with a footnote marker, the marker is replaced by the **four-space indent** that a footnote body requires,
not by the width of the marker; text before the marker is blanked in the same way. A footnote paragraph
therefore treats a continuation indented by four spaces as not lazy whatever the length of the label.

`GetLines` and `GetContinuationPrefix` have **no production caller yet**: they are verified by unit and
subsystem tests only. They are kept because the next phase consumes them. Line geometry is a separate
responsibility from parsing; the design intends it to become its own unit when it is first consumed.

#### Error Handling

Argument errors raise `ArgumentNullException`. `GetLines` and `GetContinuationPrefix` raise
`ArgumentException` for a block that is not a paragraph or heading and for a block whose range is outside
the document text. Exceptions raised by Markdig are not caught here; they propagate to the caller, and
`LintMarkEngine` reports them as `file-parse-error` for that file only. Both regular expressions (footnote
definition marker and footnote marker at the end of a prefix) have a match timeout. A block range that would
fall outside the text is clamped.

#### Dependencies

- **Markdig** — parsing and the syntax tree (see the Markdig OTS design).
- **SourceDocument** — the normalized text and the line table.
- **SuppressionMap** and **ReferenceIndex** — built for every document by calling their `Build` methods.

`SuppressionMap` and `ReferenceIndex` in turn read the `BlockInfo` and `BlockKind` types that are declared with
this unit. This is a cycle at the level of types only: the parser calls the two units, and they never call
back into the parser, so there is no cycle of behavior. The tests of the two units use `DocumentParser.Parse`
to build their inputs, which is a test-time dependency only.

#### Callers

- **LintMarkEngine** — parses every file it reads (the call can be replaced in tests through a seam).
- `GetLines` and `GetContinuationPrefix` have no production callers yet (see *Key Methods*).
