### Inline

![Inline Structure](InlineView.svg)

#### Overview

The `Inline` subsystem is part of the `Formatting` subsystem. It rewrites the inline syntax inside paragraphs: the
delimiter characters of emphasis and strong emphasis, bare URLs, and the padding of code spans. It also holds the three
units that the paragraph formatter of the `Blocks` subsystem needs to wrap a paragraph: the tokenizer that cuts a
paragraph into the words a wrap may move, the line wrapper that fills lines with them, and the guard that keeps block
syntax away from the start of a line. It proposes edits and nothing else; it never applies an edit, never writes a
file, and never touches code, HTML, headings, or table cells. The subsystem contains four units: `InlineNormalizer`,
`AtomTokenizer`, `LineWrapper`, and `BlockSyntaxGuard`. The supporting types `InlineCandidates`, which walks the inline
tree of a paragraph and decides which shapes are safe to rewrite, and `ParagraphProof`, which renders the paragraph
before and after the edits and keeps the edits only when the two renderings are identical, have no unit of their own
and are described in *InlineNormalizer Design*; the supporting types of the other units are described in *AtomTokenizer
Design* (`TokenizedParagraph`, `ParagraphSegment`, and `HardBreakKind`) and *LineWrapper Design* (`WrappedLines`).

- `InlineNormalizer` enforces `emphasis` (`_x_` and `*x*` are the same emphasis; the `emphasis` setting decides which is
  written, `*` by default), `strong` (the same for `__x__` and `**x**`, setting `strong`), `bare-urls` (a bare
  `http://`, `https://`, or `ftp://` URL becomes `<url>`; the setting value `leave` switches the rule off), and
  `code-span-space` (a code span padded with one space on each side loses both spaces; the rule has no setting).
- `AtomTokenizer` cuts a paragraph into atoms (words in which links, code spans, inline HTML, and the like stay whole)
  and hard-break segments, and counts the places where each line could be broken.
- `LineWrapper` fills lines with atoms up to a width measured in display columns, never starting a line with an atom
  that the guard refuses.
- `BlockSyntaxGuard` tells which words would be read as block syntax at the start of a line, and which lines mark a
  paragraph as Pandoc structure.

The three wrap units are pure computations over strings and arrays; they change no text. `InlineNormalizer` is not a
block formatter. Block formatters claim whole blocks and edit their lines, whereas inline text lies in paragraphs of
every kind of container (lists, block quotes, and footnotes included), whose lines the whitespace rules must keep. The
normalizer therefore claims nothing: `DocumentFormatter` asks it for each paragraph that no block formatter claims, and
its edits replace or delete characters inside the text of a line.

#### Interfaces

**InlineNormalizer.AddBlockEdits**: Adds the inline edits of one paragraph.

- *Type*: In-process .NET instance method on an internal sealed class.
- *Role*: Provider (to `DocumentFormatter`).
- *Contract*: Takes the `ParsedDocument`, the paragraph `BlockInfo`, the effective `LintMarkConfig`, and the `EditSet`
  of the file, and adds the edits that bring the emphasis, strong emphasis, bare URLs, and code spans of the paragraph
  into canonical form. `RuleIds` lists the four rules; the normalizer checks the suppression of each rule over the whole
  paragraph itself and applies no rule that is suppressed there. The edits replace existing characters (a delimiter run,
  the first or last character of a URL) or delete one (the padding of a code span), each attributed to the rule it
  enforces, never zero-length insertions.
- *Constraints*: Throws `ArgumentNullException` for a null argument and `ArgumentException` for a block that is not a
  paragraph. The constructor argument `proveParagraphs` (default `true`) switches the per-paragraph proof; only tests
  turn it off. Stateless apart from that switch, so one instance serves every worker thread.

**AtomTokenizer.Tokenize**: Cuts a paragraph into atoms.

- *Type*: In-process .NET static method.
- *Role*: Provider (to `ParagraphFormatter` of the `Blocks` subsystem).
- *Contract*: Takes the LF-normalized text, the inline content of a paragraph, and its lines, and returns the segments
  between hard line breaks with their atoms and the number of break opportunities per source line, or `null` when the
  paragraph cannot be cut safely (see *AtomTokenizer Design*).
- *Constraints*: Throws `ArgumentNullException` for a null argument and `ArgumentException` for an empty list of lines.
  Static and stateless.

**LineWrapper.Wrap**: Fills lines with atoms.

- *Type*: In-process .NET static method.
- *Role*: Provider (to `ParagraphFormatter`).
- *Contract*: Takes the atoms and the widths of the first and of the other lines in display columns and returns the
  lines and whether a line that could be broken is still too wide (see *LineWrapper Design*).
- *Constraints*: Throws for a default, empty, or null-holding array of atoms, an empty atom, or a width below one.
  Static and stateless.

**BlockSyntaxGuard.IsGuardedLineStart** and **BlockSyntaxGuard.IsPandocStructuralLine**: Answer whether a word may start
a line and whether a line marks Pandoc structure.

- *Type*: In-process .NET static methods.
- *Role*: Provider (to `LineWrapper` and `ParagraphFormatter`).
- *Contract*: See *BlockSyntaxGuard Design*. A regular expression that times out gives the answer that keeps the text
  unchanged.
- *Constraints*: Throws `ArgumentNullException` for a null argument and, for the word test, `ArgumentException` for an
  empty word. Static and stateless.

The subsystem consumes the `Document` subsystem (`ParsedDocument`, `BlockInfo`, `BlockKind`, `LeafLine`,
`DocumentParser` for the pipeline and the lines of a paragraph, and the suppression and reference data), the
`Configuration` subsystem (`LintMarkConfig` and the settings `emphasis`, `strong`, and `bare-urls`), the `Linting`
subsystem (`RuleCatalog` identifiers), the `Formatting` subsystem (`EditSet` and `TextEdit`), and the `Utilities`
subsystem (`TextWidth`, for the width of an atom). It is consumed by `DocumentFormatter` (the normalizer) and by
`ParagraphFormatter` of the `Blocks` subsystem (the three wrap units), and the `Engine` subsystem creates the production
normalizer.

#### Design

The normalizer is written for one safety rule: an edit is proposed only when the paragraph renders to the same HTML
afterwards. Two layers enforce it. The first layer, `InlineCandidates`, works on the inline syntax tree and accepts a
node only when it has a shape that is known to be safe: emphasis with exactly one or two delimiters and no other
emphasis or delimiter character inside, next to it, or anywhere as literal text in the paragraph; a URL that has a known
scheme, none of the characters that would end or change an angle-bracket autolink, and prose around it; a code span on
one line that is padded with exactly one space on each side and whose content does not begin or end with a backtick.
Everything the walk does not recognize is left alone. The second layer, `ParagraphProof`, is mandatory in the shipped
tool: the edits of a paragraph are applied to a stand-alone copy of its content, and they are kept only when both copies
render to byte-equal HTML. A paragraph that fails the proof keeps all its text, and fails it again in the next run.

A paragraph is also left alone when the document defines link references and the paragraph holds a square bracket,
because a rewritten label could stop matching its definition, which the stand-alone proof cannot see. The proof is a
local copy of the safety property and not a replacement for the checks of the `Engine` subsystem (HTML equivalence,
structural tokens, idempotency), which still run on every rewrite.

Edits of the normalizer never overlap each other (overlapping candidates make the tree untrustworthy and are dropped)
and never collide with the edits of other rules: they cover characters inside the text, whereas the whitespace passes
edit trailing runs and tabs after the first text of a line, and the block spacing pass inserts at the start of a line.

The wrap units follow a different rule: they never decide anything about the document, they only compute words and
lines. A wrap is the replacement of the blanks between the words of a paragraph by line breaks, so the units are built
to make sure that nothing else changes. The tokenizer keeps every construct whole whose parts a line break would change
or break (links with their destination, code spans, inline HTML, math, footnote references, entities, Pandoc attributes
and citations), never allows a break after a word that ends in an odd number of backslashes (the break would become a
hard line break) or after the task list marker, keeps hard line breaks where they are by cutting the paragraph into
segments, and refuses a paragraph that it cannot match to its lines or whose hard breaks it counts differently from the
parser. The wrapper fills greedily by display columns and never lets a guarded word start a line: it moves the break to
an earlier word when the text still fits, and otherwise leaves the line too wide and says so. The guard is wider than
the parser on purpose, because it judges a word without its context. The result of the wrap is a function of the atoms
and the widths, so wrapping wrapped text changes nothing; the paragraph formatter and the document formatter prove the
rest (see *ParagraphFormatter Design* and *DocumentFormatter Design*).

Known limits, stated honestly: paragraphs of a footnote are normalized like any other, except that emphasis or strong
emphasis that ends the last paragraph of a footnote is kept as written (the parser appends a link back to the reference
after the last inline of that paragraph, which gives the emphasis a neighbor that the check of the neighbors refuses),
URLs and emphasis elsewhere in a footnote convert, and a hostile document
that uses quirks of the parser may be left unchanged because the proof refuses it. Both leave the text as written and
report nothing; neither can change the meaning of a document. A code span that is padded unevenly or with more than one
space, that holds only spaces, that begins or ends with a backtick, that spans several lines, or that lies in a heading
or a table cell keeps its padding. Spaces inside emphasis markers and link text (MD037 and MD039) are not fixed at all,
because removing the space changes how the text renders; their aliases are not registered.

Thread safety: the units and their supporting types are stateless (the one field of the normalizer is an immutable
switch; the tokenizer and the guard hold compiled patterns, which are immutable and safe for concurrent use), and the
Markdown pipeline they render with is built once and is immutable, so one instance may format several documents at once,
each with its own `EditSet`.
