#### AtomTokenizer

![Inline Structure](InlineView.svg)

##### Purpose

`AtomTokenizer` cuts the text of one paragraph into the words (atoms) that a wrap may move: whitespace-separated words
in which links, images, code spans, inline HTML, and the other constructs that must stay whole are never split. It also
cuts the paragraph at its hard line breaks into segments that are never joined, and it counts, for each source line,
the places where the line could be broken. It never changes the text; when it cannot cut a paragraph safely it says so
(it returns nothing) and the paragraph is left as written. Its supporting types `TokenizedParagraph`,
`ParagraphSegment`, and `HardBreakKind` have no unit of their own and are described in this document.

##### Data Model

`AtomTokenizer` is an internal static class without state. Its patterns are compiled with a match timeout of one second.

**TokenizedParagraph**: A sealed record, immutable, with `Segments` (an immutable array of `ParagraphSegment`, at least
one) and `BreakOpportunitiesPerLine` (an immutable array with one count for each source line of the paragraph: the
number of gaps between two atoms on that line that a wrap may turn into a line break). The constructor rejects a default
array, an empty segment list, a null segment (`ArgumentNullException`), and a negative count (`ArgumentException`).

**ParagraphSegment**: A sealed record, immutable, with `Atoms` (an immutable array of non-empty strings; empty only for
a paragraph without text) and `End` (a `HardBreakKind`). The words before a hard break and the words after it are never
joined, so each segment is wrapped on its own. The constructor rejects a default array, a null or empty atom, and an
undefined `End`.

**HardBreakKind**: An enumeration of how a segment ends: `None` (the end of the paragraph), `Spaces` (a hard line break
written as two or more spaces at the end of the line), and `Backslash` (written as a backslash). A wrap keeps each break
in the form it was written in, because choosing between the forms belongs to the `trailing-whitespace` rule.

**Atoms**: An atom is a string of the paragraph text that a wrap moves as a unit. It may hold spaces (the text of a link
or a code span with a space in it). The following ranges of the paragraph text are marked as whole:

- links and images, with their text, destination, and title (inline, reference, and collapsed forms), and footnote
  references;
- code spans, with their backticks;
- automatic links in angle brackets and bare URLs;
- inline HTML tags, comments, and the like, and character references (entities);
- inline math;
- the task list marker `[ ]` or `[x]`;
- by pattern, because the parser knows nothing about them: a Pandoc attribute group (`{#id .class}`, up to 200
  characters on one line), a citation (`[@key]`, `[-@key, p. 3]`), and a LaTeX command with its braced arguments
  (`\cmd{x}`).

Whitespace outside the marked ranges separates words; a word that is glued to a marked range (`x[a b](c)y`) is one atom
with it. Emphasis and other containers are entered, so a marked range inside emphasis stays whole too.

**Merged atoms**: A gap between two words is *not* a break opportunity, and the two words are joined into one atom
(with one space between them), when the first word ends in an odd number of backslashes (a line break there would become
a hard line break) or the first word is the task list marker (the marker must stay with the text it marks).

**Multi-line atom policy**: A marked range that holds a line break is handled as follows; anything not listed makes the
paragraph cannot be tokenized (the result is `null`):

- A *code span* has each line break replaced by one space when the content is not next to a blank, a tab, or a
  backtick at the break and the span is not padded with a space on both sides; otherwise the paragraph is refused.
- The *text of a link that is not an image* has each line break replaced by one space when every line break of the
  construct is a soft break of the text. A hard break in the text, or a line break in the destination, the title, or a
  nested code span, is refused.
- An *image*, an *automatic link in angle brackets*, and a *bare URL link* that hold a line break are refused.
- *Inline HTML*, an *entity*, *math*, a *footnote reference*, and the *task list marker* that hold a line break are
  refused.

The reason for the padding rule is stability: joined into one line, a code span padded with one space on each side would
be one that `code-span-space` rewrites, so the wrapped text would not be a fixed point of the formatter. A line break
next to a blank, a tab, or a backtick would produce padding or delimiter text that differs from the renderer's reading.

**Invariants**: the atoms of a segment are never empty and never hold a line feed (a line break inside an atom is
replaced by one space); the words keep their order, segment by segment.

##### Key Methods

**Tokenize**: Cuts a paragraph into atoms and hard-break segments.

- *Parameters*: `string text` (the LF-normalized text of the document), `ContainerInline root` (the inline content of
  the paragraph), and `IReadOnlyList<LeafLine> lines` (the lines of the paragraph from `DocumentParser.GetLines`, at
  least one).
- *Returns*: a `TokenizedParagraph`, or `null` when the paragraph cannot be cut safely.
- *Preconditions*: the lines describe the paragraph that `root` was parsed from.
- *Postconditions*: nothing is changed; the result is immutable.
- *Errors*: `ArgumentNullException` for a null argument or a null line; `ArgumentException` for an empty list of lines.
  A regular expression timeout is turned into `null`.

Algorithm:

1. Rebuild the content of the paragraph by joining the content of each line (the part after the container prefix) with
   line feeds, and remember where each line starts in that text and in the document. A line outside the text or lines
   that overlap give `null`.
2. Walk the inline tree and mark ranges, converting the document span of each inline to the rebuilt text; a span that is
   not in the content of a line gives `null`. Links, code spans, the task list marker, inline HTML, entities, math,
   footnote references, and automatic links are marked (applying the multi-line policy above); line breaks that the
   parser reports as hard are counted; every other container is entered.
3. Mark the Pandoc attribute groups, citations, and LaTeX commands by pattern.
4. Find the hard breaks of the text: a line feed outside a marked range that is preceded by two or more spaces
   (`Spaces`) or by an odd number of backslashes (`Backslash`). The number found must equal the number of hard breaks
   the parser reported; a difference gives `null`, because the paragraph is then read differently from how it is
   written.
5. Cut the words (maximal runs without whitespace outside marked ranges) and group them into segments at each hard
   break.
6. Close each segment: merge the words that must not be separated, and for each atom that starts after a gap on the
   same line as the word before it, count one break opportunity for the source line of that word. A gap that holds a
   line feed is not counted: the line is already broken there.

Width is not the tokenizer's business: it does not measure anything, so the same atoms serve every line length.

##### Error Handling

`ArgumentNullException` and `ArgumentException` for invalid arguments. Every property of the paragraph that makes the
atoms untrustworthy (spans that do not fit the lines, a multi-line construct that must not be joined, a hard-break count
that differs from the parser's, a pattern timeout) is reported by the result `null`, never by an exception; the caller
leaves the paragraph as written and reports its overflow as one that cannot be fixed. The constructors of the supporting
types reject invalid values, so an inconsistent result cannot be created.

##### Dependencies

- **LeafLine** (`Document` subsystem) - the lines of the paragraph, as `DocumentParser.GetLines` describes them (the
  tokenizer reads the type and does not call the parser).
- **TokenizedParagraph**, **ParagraphSegment**, and **HardBreakKind** (this unit) - the result.
- **Markdig** (OTS) - the inline tree: `ContainerInline`, `LinkInline`, `CodeInline`, `HtmlInline`, `HtmlEntityInline`,
  `LineBreakInline`, `AutolinkInline`, and the math, footnote link, and task list inlines of the extensions.
- **.NET BCL** - generated regular expressions with a timeout, string handling, and immutable arrays.

##### Callers

- **ParagraphFormatter** (`Blocks` subsystem) - tokenizes each paragraph that has a line wider than the line length and
  uses the break opportunities to find the first line that can be broken.
