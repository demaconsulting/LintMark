#### AtomTokenizer

![Inline Structure](InlineView.svg)

##### Purpose

`AtomTokenizer` cuts the text of a paragraph into the words (atoms) that a wrap may move: whitespace-separated words
in which links, images, code spans, inline HTML and the other constructs that must stay whole are never split. It also
cuts the paragraph into segments at its hard line breaks, which are never joined, and refuses a paragraph it cannot cut
with certainty. It decides nothing about widths or layout; that is the work of `LineWrapper`.

##### Data Model

`AtomTokenizer` is an internal static class without state; its compiled patterns have a match timeout of one second.

**TokenizedParagraph**: A sealed record, immutable, with `Segments` (an immutable array of `ParagraphSegment`, at least
one, in order) and `BreakOpportunitiesPerLine` (for each source line, the number of gaps between two atoms on that line
that a wrap may turn into a line break).

**ParagraphSegment**: A sealed record with `Atoms` (the atoms between two hard line breaks, none empty) and `End` (a
`HardBreakKind`).

**HardBreakKind**: `None` (the segment ends the paragraph), `Spaces` (two or more spaces at the end of the line), and
`Backslash` (a backslash at the end of the line). The wrap keeps each hard break in the form it was written in, because
the choice between the forms belongs to the `trailing-whitespace` rule.

An atom is a string without line feeds: a line break inside an atom is replaced by one space. Atom widths are not
stored; `LineWrapper` measures an atom with `TextWidth.Of` when it lays the atoms out.

Private helpers: `FlatText` rebuilds the text of the paragraph from its `LeafLine` values (the content of each line
joined with line feeds) and converts between document offsets and paragraph offsets; `MarkState` holds the marks while
the inline tree is visited; `Word` is a word with its position and whether a line break separates it from the word
before.

##### Key Methods

**Tokenize**: Cuts a paragraph into atoms and segments.

- *Parameters*: `string text` (the LF-normalized document text), `ContainerInline root` (the inline content),
  `IReadOnlyList<LeafLine> lines`, and `bool breakLinkText` (default `false`).
- *Returns*: a `TokenizedParagraph`, or `null` when the paragraph is refused.
- *Postconditions*: nothing is modified; joining the atoms of a segment with single spaces gives the words of the
  paragraph with its constructs unchanged.

Algorithm:

1. Rebuild the paragraph text from the lines. When a line lies outside the text or overlaps the previous one, refuse.
2. Mark, from the inline tree, the characters that must stay whole (`MarkInlineAtoms`): links and images with their
   text and destination, code spans, autolinks and bare URLs, inline HTML, character references, inline math, footnote
   references, and the task list marker. A pattern marks Pandoc attribute groups (`{#id .class}`), citations
   (`[@key]`), and LaTeX commands (`\cmd{x}`), which the parser does not know.
3. Check the line breaks inside marked constructs. A line break is replaced by one space only where that cannot change
   the meaning: in the text of a link that is not an image, and in a code span whose break does not touch a blank or a
   backtick and that is not padded with a space on both sides (joined, `code-span-space` would rewrite it). A break in
   any other construct (inline HTML, math, an image, a link destination or title, an autolink) refuses the paragraph.
4. With `breakLinkText`, only the destination part of an inline link (read by `InlineLinkTail.TryRead`) stays whole and
   the link text is read like any other text; a link whose destination part is not read with certainty stays whole.
5. Find the hard line breaks outside marked constructs: two or more spaces, or an odd number of backslashes, before a
   line feed. Their count must equal the hard breaks that the parser reports, otherwise refuse.
6. Walk the text: a word runs to the next whitespace outside a marked range, so `x[a b](c)y` is one atom. A hard break
   closes the segment. Words are merged into one atom with the next word when a break after them is forbidden: the word
   ends with an odd number of backslashes (the break would become a hard break) or it ends the task list marker.
   `BreakOpportunitiesPerLine` counts, per line, each atom that starts after a gap on the same line.

##### Error Handling

A refused paragraph is not an error: `Tokenize` returns `null` and the caller (`ParagraphFormatter`) leaves the
paragraph as written and reports an overflow it cannot fix. A pattern that times out (`RegexMatchTimeoutException`) is
treated the same way. `ArgumentNullException` is thrown for a null argument. Nothing else is caught.

##### Dependencies

- **InlineLinkTail** (`LinkRefactorer` unit) - reads the destination part of an inline link.
- **LeafLine** and **DocumentParser** (`Document` subsystem) - the lines of the paragraph.
- **Markdig** (OTS) - the inline syntax tree (`ContainerInline`, `LinkInline`, `CodeInline`, and the other inlines).
- **.NET BCL** - immutable arrays and compiled regular expressions.

##### Callers

- **ParagraphFormatter** (`Blocks` subsystem) - tokenizes each paragraph it may wrap, with the `break-link-text`
  option, and uses `BreakOpportunitiesPerLine` to find a line that is too wide and can be broken.
