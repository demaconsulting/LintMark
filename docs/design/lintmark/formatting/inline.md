### Inline

![Inline Structure](InlineView.svg)

#### Overview

The `Inline` subsystem is part of the `Formatting` subsystem and handles everything that is decided inside the text of
a paragraph: the canonical form of emphasis, strong emphasis, bare URLs and code spans (`InlineNormalizer`), the
opt-in conversion of long inline links to reference links (`LinkRefactorer`), and the three steps of a wrap: cutting a
paragraph into the words a wrap may move (`AtomTokenizer`), laying the words out in lines (`LineWrapper`), and keeping
block syntax away from the start of a line and shapes whose meaning depends on their line breaks from being wrapped
(`WrapGuard` and `BlockSyntaxGuard`, both described in *LineWrapper Design*). The subsystem proposes edits or computes
words and lines and nothing else: it applies no edit, writes no file, and never touches code, HTML, headings, or the
text of a table cell (except the link destinations that `LinkRefactorer` replaces).

The subsystem contains four units: `InlineNormalizer` (with `InlineCandidates`), `LinkRefactorer` (with
`LinkLabelAllocator` and `InlineLinkTail`), `AtomTokenizer` (with `TokenizedParagraph`, `ParagraphSegment`, and
`HardBreakKind`), and `LineWrapper` (with `WrappedLines`, `SentenceSplitter`, `WrapGuard`, and `BlockSyntaxGuard`).
Supporting types are described in the document of their owner.

#### Interfaces

- **InlineNormalizer.AddBlockEdits** (provider to `DocumentFormatter`): adds the edits of the four inline rules
  `emphasis`, `strong`, `bare-urls`, and `code-span-space` for one paragraph that no block formatter claims.
- **LinkRefactorer.IsEnabled** and **LinkRefactorer.Plan** (provider to `DocumentFormatter`): decide which inline links
  of a document become reference links and return the edits, one finding per link, and whether a table changed.
- **AtomTokenizer.Tokenize** (provider to `ParagraphFormatter`): cuts a paragraph into hard-break segments of atoms,
  or returns `null` when the paragraph is refused.
- **LineWrapper.Wrap** (provider to `ParagraphFormatter`): lays atoms out in lines in the layout of a wrap mode.
- **WrapGuard.KeepsAsWritten**, **WrapGuard.OpensBlockByItself**, **WrapGuard.IsInterruptingBlockStart**,
  **BlockSyntaxGuard.IsGuardedLineStart**, and **BlockSyntaxGuard.IsPandocStructuralLine** (providers to
  `ParagraphFormatter`, `LineWrapper`, and, for the block start test, `ListFormatter`): the deterministic wrap guard.

The subsystem consumes the `Document` subsystem (`ParsedDocument`, `BlockInfo`, `LeafLine`, `DocumentParser`,
`ReferenceIndex`, and `TextWidth`), the `Configuration` subsystem (`LintMarkConfig` with `LinkRefactorOptions`,
`WrapMode`, `EmphasisMarker`, and `BareUrlStyle`), the `Linting` subsystem (`RuleCatalog` and `RuleSupport`), the
`Formatting` subsystem (`EditSet`, `TextEdit`, `LineLayout`, and `LineOwnership`), the `Blocks` subsystem (`PipeTable`
and `TableFormatter`, for the rows of a table), and Markdig as OTS (the inline syntax tree).

#### Design

Two kinds of units work here. `InlineNormalizer` and `LinkRefactorer` are planners that return text edits. Neither is
a block formatter: block formatters claim whole blocks and their lines, whereas inline text lies in paragraphs of every
container whose lines the whitespace rules keep. `DocumentFormatter` therefore asks `InlineNormalizer` for each
unclaimed paragraph while it collects the leaf edits, and calls `LinkRefactorer` on the text that all other rules
produce (after the lists, before any wrap), composing its edits with theirs. The wrap units are pure functions:
`ParagraphFormatter` tokenizes a paragraph, asks the wrap guard whether it may be rewritten, wraps each hard-break
segment with `LineWrapper`, and builds the replacement itself.

Correctness is not proven when the tool runs; it rests on deterministic safeguards that make an unsafe rewrite
impossible by construction, and on the development-time corpus harness (`tools/LintMark.Verify`). The safeguards are:
candidates are proposed only for shapes that are known to be safe (`InlineCandidates`); the tokenizer keeps every
construct whole whose parts a line break could change and refuses a paragraph it cannot match to its lines;
the wrap guard keeps a paragraph as written when joining or moving its lines could change how it reads; no wrapped
line starts with block syntax; `LinkRefactorer` copies only destinations it reads with certainty and abandons the
whole plan when the definitions cannot be placed safely; and edits never conflict (see *EditSet Design*). Wherever a
safeguard refuses, the text stays as written.

Wrapping is idempotent by construction: the layout is a function of the atoms and the available widths, so wrapped
text tokenizes to the same atoms and wraps to itself. All units are stateless or hold per-call state only; compiled
patterns have match timeouts, and a pattern that times out gives the answer that leaves the text unchanged. One
instance serves every worker thread.
