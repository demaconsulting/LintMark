#### AtomTokenizer

##### Verification Approach

`AtomTokenizer` is verified with unit tests in `AtomTokenizerTests.cs`. Nothing is replaced by a double: the real
`DocumentParser`, `SourceDocument`, and Markdig inline tree are used, and the lines of a paragraph are the real
`LeafLine` ranges that `DocumentParser.GetLines` returns for it. A test parses a text with a helper of the test project
(`BlockFormatterAssertions`), takes the first paragraph, and tokenizes it from the text of the document, the inline tree
of the paragraph, and its lines. The scenarios assert on the atoms of every segment in order, on the segments and the
hard break kind that ends each, on the number of break opportunities of each line, and on refusal, which is a null
result. Most scenarios are theories whose inputs and expected atoms are written in the test, so each atom can be read
as the exact text that a wrap must keep together. The tests of a refused paragraph use texts whose parse is known to
span lines, and one test builds a list of lines that ends before the link it holds, to show that spans which do not fit
the lines are refused. The tests of arguments use the real parsed paragraph and replace one argument at a time. The
test for concurrent use parses one paragraph once and shares it among eight threads; each tokenizes it many times, and
every description of the result is compared with the description of a single call. The supporting types
(`ParagraphSegment`, `TokenizedParagraph`, and `HardBreakKind`) are verified in the same class.

##### Test Environment

N/A - standard test environment.

##### Acceptance Criteria

- All unit tests pass with zero failures.
- A null text, a null inline tree, a null list of lines, a list of lines that holds a null element, and an empty list of
  lines are rejected, and each exception names the parameter.
- Words are separated at a space or a line break, whatever the amount of whitespace, and the one segment of a paragraph
  without a hard break ends with no hard break.
- A link or an image (inline, full reference, and shortcut reference, with text, destination, and title, also inside
  parentheses and with trailing punctuation) is one atom, and the punctuation next to it stays in its atom.
- A code span with blanks inside, with one or more backticks around it and with a padding space on both sides, is one
  atom.
- An autolink, a bare URL, and a `www.` address are one atom each.
- An inline HTML tag is not split at a blank inside it, and the words around the tags stay in their atoms.
- Inline math, character references, footnote references, Pandoc attribute groups, citations, and LaTeX commands with
  blanks inside are kept whole.
- Escaped characters stay in their words, and CJK, emoji, and combining marks are ordinary words.
- A hard break of two or more spaces ends a segment with the spaces kind, a hard break of a backslash ends a segment
  with the backslash kind and keeps the backslash in the last atom, and an escaped backslash at the end of a line does
  not end a segment.
- A word that ends with an odd number of backslashes and the marker of a task list item are joined with the next word
  into one atom, and a word that ends with an even number of backslashes is not.
- The break opportunities are counted for each line.
- A code span that continues on the next line, and the text of a link that continues on the next line, become one atom
  in which the line break is one space.
- A code span with a blank or a backtick next to its line break, or padded with a space on both sides, is refused.
- A link title or destination on another line, an image, inline HTML, or a hard break inside link text that spans a line
  break is refused.
- Lines whose ranges do not fit the spans of the inline tree are refused.
- The prefixes of a quote and of a list item are not part of the atoms.
- The result cannot be changed through a cast to a mutable interface.
- The segment and the paragraph reject invalid arguments.
- One tokenizer call gives the same result on every thread.

##### Test Scenarios

###### Normal operation

**Plain words are separated at whitespace**: The paragraph `a b   c` and `d` on the next line is tokenized. There is one
segment that holds four atoms (`a`, `b`, `c`, `d`), whatever the amount of whitespace between them, and the segment
ends with no hard break.

**Links and images stay whole**: Paragraphs with an inline link with a title (`[a b](http://x.org/y "t u")`), an image
(`![alt text](i.png)`), a link whose text holds emphasis and a code span (``[a *b* `c d`](u)``), a full reference link
(`[a b][ref]`), a shortcut reference link (`[a b]`), and a link in parentheses with a period after it (`([a b](u)).`)
are tokenized, the references with a definition in the document. Each link or image, with its text, destination, and
title, is one atom, and the parentheses and the period belong to the atom of the link. The words around it are
separate atoms.

**Code spans stay whole**: Three paragraphs are tokenized, each of the word `a`, a code span, and the word `d`. The code
span is `b c` in single backticks, then a span in double backticks that holds a single backtick between blanks, then the
same span in double backticks with a padding space on both sides. Each code span, with its blanks, its backticks, and
its padding spaces, is one atom between the atoms `a` and `d`.

**Angle-bracket links and bare URLs stay whole**: `<http://x.org/a> and http://y.org/b and www.z.org` is tokenized. The
atoms are the autolink, `and`, the bare URL, `and`, and the `www.` address.

**Inline HTML is not split**: `<span class="a b">x y</span> <br/>` is tokenized. The blank inside the opening tag does
not split it, so the atoms are `<span class="a b">x`, `y</span>`, and `<br/>`; the words next to the tags stay glued to
them and the blank between the words still separates.

**Inline math stays whole**: `x $a + b$ y` is tokenized. The atoms are `x`, `$a + b$`, and `y`.

**Entities and footnote references stay whole**: `a &amp; &#35; text[^1] more`, with a footnote definition, is
tokenized. The atoms are `a`, `&amp;`, `&#35;`, `text[^1]`, and `more`.

**Pandoc constructs stay whole**: `x {#id .cls} [@key, p. 3] y\cmd{a b} z` is tokenized. The attribute group
`{#id .cls}`, the citation `[@key, p. 3]`, and the LaTeX command with its braced argument, which stays glued to the
word before it (`y\cmd{a b}`), are one atom each; `x` and `z` are separate atoms.

**Escaped characters stay in their words**: `a \*not emphasis\* b \\ c` is tokenized. The atoms are `a`, `\*not`,
`emphasis\*`, `b`, `\\`, and `c`, so every escape stays with its word.

**Wide and combining characters are ordinary words**: A paragraph of `日本語`, `テキスト`, a family emoji built from
joiners, `e` with a combining acute accent, and two smiling faces, separated by spaces, is tokenized. Each is one atom
and none is split inside.

**A hard break of spaces ends a segment**: The line `a b` followed by three spaces, with `c d` on the next line, is
tokenized. There are two segments. The first holds `a` and `b` and ends with the spaces kind of hard break, and the
second holds `c` and `d` and ends with no hard break.

**A hard break of a backslash ends a segment**: `a b\` with `c d` on the next line is tokenized. There are two segments.
The first holds `a` and `b\` (the backslash stays in the last atom) and ends with the backslash kind of hard break. The
paragraph `a b\\` with `c d` on the next line, where the line ends with an escaped backslash, is one segment of the
atoms `a`, `b\\`, `c`, and `d`, because that line end is not a hard break.

**No break follows a word that ends with a backslash**: `see C:\temp\ next word` and `see C:\temp\\ next word` are
tokenized. With one trailing backslash the word is joined with the next one (`see`, `C:\temp\ next`, `word`), because a
break after it would become a hard break. With two trailing backslashes the word stands alone (`see`, `C:\temp\\`,
`next`, `word`).

**No break follows the task list marker**: The list item `- [ ] task item text` is tokenized. The atoms are `[ ] task`,
`item`, and `text`, so the marker is joined with the first word of the item.

**Break opportunities are counted for each line**: The paragraph `a b c d`, `[long link text](u)`, and `x [y z](u)`, one
per line, is tokenized. The counts per line are 3, 0, and 1: three gaps between four words, no gap in a line that is one
atom, and one gap before the link in the last line.

**A code span over two lines is joined**: The paragraph `a` and `b` on the first line, and `c` and `d` on the second,
with the code span `b c` split at its blank so that the line break is inside it, is tokenized. The atoms are `a`, the
code span `b c` (the line break is one space), and `d`.

**The text of a link over two lines is joined**: The paragraph `[a` on the first line and `b](u) c` on the second, with
the link text continued on the second line, is tokenized. The atoms are `[a b](u)` and `c`, with the line break
replaced by one space.

**Quote and list item prefixes are not atoms**: A block quote of `a b` and `c d` and a list item of `a b` with the
continuation line `c d` are tokenized. In both, the atoms are `a`, `b`, `c`, and `d`, and no marker or indent is in an
atom.

**The result cannot be changed**: The paragraph `a b` and `c` is tokenized. The list of segments, the counts of break
opportunities, and the atoms of the first segment are each cast to a mutable list interface, and each reports that it is
read-only.

**Concurrent calls give the same result**: One parsed paragraph with a link, a code span, a hard break of spaces, inline
HTML with a blank in an attribute, a hard break of a backslash, and a Pandoc attribute group is tokenized once. Eight
threads then tokenize it a hundred times each. Every description of the result (the segments with their hard break
kinds and atoms, and the counts) equals the description of the single call.

###### Boundary conditions

**A code span padded on one side only is joined**: The paragraph `a` and a code span that starts with a space inside the
backtick and continues on the second line, ending next to the closing backtick, is tokenized. The atoms are `a`, the
code span with the line break as one space (the padding space stays at its start), and `d`, because the span is not
padded on both sides.

###### Error handling

**Missing or invalid arguments are rejected**: `Tokenize` is called for the paragraph `a b` with a null text, a null
inline tree, a null list of lines, a list of lines that holds a null element, and an empty list of lines. The first
three calls throw `ArgumentNullException` that names `text`, `root`, and `lines`. The call with a null element throws
`ArgumentNullException` that names `lines`, and the call with an empty list throws `ArgumentException` that names
`lines`.

**A code span with a blank or a delimiter at the break is refused**: Four paragraphs of two lines each are tokenized,
and in each a code span has its line break inside it. In the first a space comes before the break, in the second the
break comes right after the opening backtick, in the third right before the closing backtick, and in the fourth a
backtick comes before the break in a span of double backticks. Each result is null, because joining the lines could
change the span.

**A code span padded on both sides over two lines is refused**: A paragraph whose code span starts with a space after
the opening backtick, continues on the second line, and ends with a space before the closing backtick is tokenized. The
result is null, because on one line the code span rule would remove the padding and the text would not be stable.

**A multi-line construct that cannot be joined is refused**: Five paragraphs are tokenized, each followed by an `x` or a
`y` word: a link whose title is on the second line, an image whose description continues on the second line, an inline
HTML tag whose attribute is on the second line, a link whose text holds a hard break of spaces, and a link whose text
holds a hard break of a backslash. Each result is null.

**Lines that do not fit the spans are refused**: The paragraph `[a](b) c` is parsed, and the lines passed are one line
that covers only the first three characters, so it ends before the link does. The result is null.

**The supporting types reject invalid arguments**: A `ParagraphSegment` is created with a default array of atoms, an
array that holds a null, an array that holds an empty string, and a hard break kind that is not defined. The calls
throw `ArgumentException`, `ArgumentNullException`, `ArgumentException`, and `ArgumentOutOfRangeException`. A
`TokenizedParagraph` is created with a default array of segments, an empty array, an array that holds a null, a default
array of counts, and a count of minus one. The calls throw `ArgumentException`, `ArgumentException`,
`ArgumentNullException`, `ArgumentException`, and `ArgumentException`. The atoms of a valid segment equal the array that
it was created with.
