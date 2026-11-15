#### LineWrapper

![Inline Structure](InlineView.svg)

##### Purpose

`LineWrapper` lays the atoms of a paragraph segment out in lines: in the layout of the `fill`, `sentence`, or `semantic`
wrap mode, within a width measured in display columns, never splitting an atom and never starting a line with block
syntax. The unit also owns the wrap guard: `WrapGuard` decides whether a whole paragraph must stay as written, and
`BlockSyntaxGuard` decides which words must not start a line. `SentenceSplitter` finds the sentence and clause ends.

##### Data Model

`LineWrapper`, `SentenceSplitter`, `WrapGuard`, and `BlockSyntaxGuard` are internal static classes without state; their
patterns have a match timeout of one second, and a pattern that times out gives the answer that keeps the text as it is.

**WrappedLines**: an immutable record with `Lines` (in order, none empty, without container prefix or trailing
whitespace) and `HasBreakableOverflow` (a line that holds more than one atom is still wider than its width because no
allowed break exists).

**Widths**: an atom is measured once with `TextWidth.Of`; a line is its atoms joined with one space. The caller passes
`firstWidth` and `continuationWidth`, the line length minus the width of the container prefix of the first and of the
other lines (at least 1; a line length of 0 is a very large width). An atom wider than its width stands alone on its
line over width, and a line with one atom is never a breakable overflow.

##### Key Methods

**LineWrapper.Wrap** (two overloads; the one without a mode is `fill`):
`Wrap(atoms, firstWidth, continuationWidth[, mode])` returns `WrappedLines`. At least one atom is expected. The layout
(`Layout`) is a greedy fill that depends only on the atoms and the two widths, so wrapped text wraps to itself:

1. `fill`: add the next atom when the line plus one space plus the atom fits; otherwise break before it.
2. `sentence`: every atom that `SentenceSplitter.FindSentenceStarts` reports starts a line; a sentence that is too wide
   is filled as in `fill`.
3. `semantic`: as `sentence`, but when a line is full the break goes at the latest clause start
   (`SentenceSplitter.FindClauseStarts`, found by `FindClauseBreak`) after which the rest still fits the new line,
   and falls back to the fill break when none does.
4. A starting place that `BlockSyntaxGuard.IsGuardedLineStart` refuses is never a break: a refused sentence or clause
   start is dropped, and a refused fill break moves to the nearest earlier atom that may start a line
   (`FindEarlierBreak`) when the atoms from there still fit. When no break exists the atom stays on the line over
   width and `HasBreakableOverflow` is set.

**SentenceSplitter**: works on whole atoms, so a terminator inside a link or code span is never seen. `EndsSentence`
is true when an atom, after trailing closers (quotes, emphasis marks, an unbalanced `)` or `]`, a footnote
reference), ends with `.`, `!`, `?`, or their East Asian forms and the next atom starts with an upper-case letter,
a digit, a caseless letter, or a code span. It does not split after an ellipsis, a known abbreviation (`e.g.`, `Dr.`,
`etc.`; the list is a constant), a one-letter initial, a dotted abbreviation (`U.S.`), or a number of one or two
digits. `EndsClause` is true for a trailing comma, semicolon, colon, or dash (not `std::` or `:smile:`).

**WrapGuard.KeepsAsWritten**:
`KeepsAsWritten(document, block, inline, lines, continuationPrefix, breakLinkText)` is true
when joining or moving the lines could change how the paragraph reads. The rules, each a deterministic text test:

- W-1 a block quote paragraph that starts with an alert marker (`[!`).
- W-2 a line (the first included) whose first word starts a block (`IsInterruptingBlockStart`: block syntax, except an
  ordered marker with a number other than 1, which cannot interrupt a paragraph).
- W-3 a line break at the start or end of link text, or a blank there when link text may be broken.
- W-4 a literal `<` with a line break before the next `>`, or two literal `$` with a line break between them.
- W-5 a continuation line indented more than its container prefix needs.
- W-6 `[`, blanks that are not exactly one space, `]` (a task marker look-alike).
- W-8 a quote marker directly followed by text.
- W-9 a line that ends with the shape of a grid table border (`+---+`, `+ ---`).

**WrapGuard.OpensBlockByItself** (rule W-7): true when the first line that the wrap writes is one complete HTML tag or
one word that the block syntax guard refuses (`---`, `+---+`, `#`).

**BlockSyntaxGuard**: `IsGuardedLineStart(atom)` is true for a first word that could be a bullet or ordered marker,
thematic break, setext underline, heading marker, quote, fence, table pipe or delimiter, reference or footnote
definition, block-level HTML, or a Pandoc construct (fenced div, LaTeX command, `$$`, raw attribute, definition list
marker, fancy or example list marker). It is deliberately wider than the parser, because it judges a word without its
context. `IsCommonMarkBlockStart` is the same without the Pandoc words. `IsPandocStructuralLine(line)` is true for a
content line that marks its paragraph as Pandoc structure. The guard never escapes anything.

##### Error Handling

`ArgumentOutOfRangeException` for a width below 1; `ArgumentNullException` or `ArgumentException` for a null or empty
argument of the public methods. Refusals are results, not errors: a guarded atom or an atom wider than the width gives
`HasBreakableOverflow` or an over-width line, and the wrap guard answers `true`. Nothing is caught except pattern
timeouts, which take the safe answer (no sentence split; a guarded word; a paragraph kept as written).

##### Dependencies

- **TextWidth**, **ParsedDocument**, **BlockInfo**, **LeafLine**, **DocumentParser** (`Document` subsystem) - widths,
  the text, and the prefix comparison of W-5.
- **WrapMode** (`Configuration` subsystem) - the layout.
- **Markdig** (OTS) - the inline tree for W-3 and W-4.

##### Callers

- **ParagraphFormatter** (`Blocks` subsystem) - `Wrap` per segment, `KeepsAsWritten` and `OpensBlockByItself` before
  and after, and `IsPandocStructuralLine` and `IsGuardedLineStart` for the lines after hard breaks.
- **ListFormatter** (`Blocks` subsystem) - `WrapGuard.IsInterruptingBlockStart` for the first word of a continuation
  line of a paragraph in an item.
