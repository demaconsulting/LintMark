#### ParagraphFormatter

![Blocks Structure](BlocksView.svg)

##### Purpose

`ParagraphFormatter` decides what to do about the lines of one paragraph that are wider than the line length. When a
line can be broken, it proposes one edit that fills the paragraph up to the width (the `fill` mode of the `line-length`
rule); it reports a line it cannot shorten safely, and it leaves every other paragraph exactly as written. It is
overflow-only: a paragraph in which no line is too wide is never reflowed, so hand-wrapped text that fits stays as it
is. It never changes what a paragraph renders to: the wrapped paragraph is proved to render the same before the edit is
proposed. Its supporting type `ParagraphOutcome` (the result of one call) has no unit of its own and is described in
this document.

##### Data Model

`ParagraphFormatter` is an internal sealed class with one field and one property.

- `_rendersTheSame`: a `Func<string, string, bool>` that tells whether the paragraph as written and the wrapped
  paragraph, each as stand-alone LF-normalized text, render the same. The public constructor installs the proof of the
  tool (see *Proofs* below); an internal constructor takes the function as its only argument, so that a test can
  simulate a wrap that the proof refuses.
- `RuleIds`: an immutable array of `line-length`, `trailing-whitespace`, `hard-tabs`, `emphasis`, `strong`, `bare-urls`,
  `code-span-space`, `list-indent`, `list-marker-space`, and `blockquote-space`, in this order. These are the rules that
  edit the text of a paragraph (the first is its own); a suppression of any of them inside the paragraph keeps the
  paragraph as written, because a wrap rewrites the whitespace, the line starts, and the container prefix of every line.

The formatter is **not** an `IBlockFormatter` and claims no lines. `DocumentFormatter` holds it in its own optional
constructor parameter and calls it in a second stage, on the text that every other rule has produced (see
*DocumentFormatter Design*). Registering it as a block formatter would claim every paragraph and silence the whitespace
and inline rules for it. `RuleIds` is therefore read by the formatter itself and not by the document formatter.

**ParagraphOutcome**: A sealed record, immutable, with the members `Edit` (a `TextEdit?` attributed to the rule
`line-length`, or `null`), `OverflowOffset` (the offset, in the text the formatter was given, of the first character
beyond the line length on the first line that is too wide and could be broken; -1 for none), `OverflowWidth` (the
display width of that line; 0 for none), and `HasResidualOverflow` (whether a line that could be broken is still too
wide: after the edit when there is one, otherwise because the paragraph is not rewritten). The static property `None`
is the outcome of a paragraph that needs no report and no edit (-1, 0, no edit, no residual). The constructor rejects an
inconsistent outcome: an offset below -1 (`ArgumentOutOfRangeException`), a negative width
(`ArgumentOutOfRangeException`), an edit, a residual overflow, or a width without an offset, and a default `TextEdit`
(`ArgumentException`). Offsets and the edit refer to the text that the formatter was given, not to the original file;
the document formatter translates them.

**What is wrapped, reported, and exempt**: A line is *too wide* when its display width, container prefix included and
trailing blanks (the two spaces of a hard break among them) excluded, is greater than the `line-length` setting. A
too-wide line is *breakable* when the tokenizer counts at least one place inside it where a break may go. The decision
table for a paragraph:

| Situation | Outcome |
| --- | --- |
| A breakable line is too wide, the paragraph is eligible, the wrap passes both proofs | One edit, at the first one |
| After the wrap a line of more than one atom is still too wide | Residual overflow, also with an edit |
| The tokenizer refuses the paragraph | Reported only (residual), no edit |
| A continuation line of a paragraph inside a list item is lazy | Reported only (residual), no edit |
| A line after a hard break would start with block syntax | Reported only (residual), no edit |
| The stand-alone proof refuses the wrap | Reported only (residual), no edit |
| The proof inside the list or quote refuses the wrap | Reported only (residual), no edit |
| Every too-wide line is a single atom (long link, URL, autolink, code span, image) | `None` |
| No line is too wide | `None` |
| Setting is not `fill`, `line-length` is 0, or the rule `line-length` is switched off | `None` |
| The paragraph is in a footnote, or its first line is protected | `None` |
| A rule of `RuleIds` is suppressed anywhere in the paragraph | `None` |
| The paragraph or its prefixes hold a tab | `None` |
| A content line is Pandoc structure (see `BlockSyntaxGuard`) | `None` |

Headings, tables, code, HTML, math, front matter, and link reference definitions are not paragraphs and never reach the
formatter. Without a tokenization a line counts as breakable when its content holds a blank between two other
characters.

**Widths and positions**: Widths are display columns (`TextWidth.Of`), measured on the exact text of the line. The width
available to the first line of the first segment is the line length minus the width of the text before the paragraph on
its line; the width of every other line is the line length minus the width of the continuation prefix; both are at least
one. The overflow offset is the offset of the first grapheme cluster of the first breakable too-wide line whose right
edge lies beyond the line length (the last character of the line when none does).

**Container prefixes**: The first line keeps the text before the paragraph as written. Every continuation line is
written with the continuation prefix of the paragraph (`DocumentParser.GetContinuationPrefix`): the text before the
paragraph on its first line with every character other than `>`, a space, and a tab replaced by a space. A list item's
lines are therefore indented under its text and a quote's lines keep their markers. A lazy continuation line of a quote
gets the quote markers like every other line; a lazy continuation line of a paragraph in a list item is never rewritten
(rewriting it would make the list formatter consider the item movable in the next run and the result unstable).

**Proofs**: A wrap stands only when two renderings agree. The *stand-alone proof* parses the content lines of the
paragraph as written (without container prefixes, joined with line feeds) and the wrapped lines, requires both to be
exactly one paragraph, and compares the pages with `HtmlEquivalenceChecker.Check`, which normalizes whitespace. The
*container proof* parses the lines of the paragraph with their prefixes, as written and as wrapped; when the original is
read as one block quote or one list, the wrapped text must be read as one block of the same kind and render the same.
The second proof exists because the parser reads some first lines differently when text follows them (for example
`>http://x y`, depending on whether the URL ends the line). Both proofs are defense in depth next to the file-level
checks of the `Safety` subsystem, which still run on every rewrite.

##### Key Methods

**Constructors**: The public constructor has no parameters and installs the stand-alone proof. The internal constructor
takes `Func<string, string, bool> rendersTheSame` (the stand-alone proof) and throws `ArgumentNullException` for a null
function. The container proof is not replaceable.

**IsEnabled** (static): Tells whether the settings ask for wrapping.

- *Parameters*: `LintMarkConfig config`.
- *Returns*: `true` when `config.Wrap` is `fill`, `config.LineLength` is not 0, and the rule `line-length` is switched
  on in `config.Rules`.
- *Errors*: `ArgumentNullException` for a null configuration.

`wrap: sentence` and `wrap: semantic` are accepted by the configuration and not applied: `IsEnabled` is false for them,
so nothing is wrapped and nothing is reported.

**Format**: Decides what to do about one paragraph.

- *Parameters*: `FormatContext context`, `BlockInfo block` (a paragraph of `context.Document`).
- *Returns*: a `ParagraphOutcome`; `ParagraphOutcome.None` when nothing is to be reported or changed.
- *Preconditions*: neither argument is null and the block is a paragraph of the document of the context (the second
  point is not verified).
- *Postconditions*: nothing is written and the document is not changed; the edit, when there is one, replaces the whole
  text of the paragraph from its first character to its last line, and every line of the replacement is at most as wide
  as the width available to it unless it holds a single atom or the guard allowed no better break.
- *Errors*: `ArgumentNullException` for a null argument; `ArgumentException` for a block that is not a paragraph. Any
  other situation is an outcome, not an exception.

Algorithm:

1. Validate the arguments. Return `None` when the settings do not ask for wrapping (`IsEnabled`), when the block has no
   inline content, or when the paragraph is exempt: its first line is in a footnote or protected
   (`LineOwnership.IsInFootnote`, `IsProtected`), or a rule of `RuleIds` is suppressed over its range
   (`SuppressionMap.IsSuppressedRange`).
2. Read the lines with `DocumentParser.GetLines` and measure each (container prefix included, trailing blanks
   excluded). Return `None` when none is wider than the line length.
3. Return `None` when the text from the start of the first line to the end of the paragraph holds a tab, when the
   continuation prefix holds a tab, or when a content line is Pandoc structure
   (`BlockSyntaxGuard.IsPandocStructuralLine`).
4. Tokenize with `AtomTokenizer.Tokenize`. Find the first too-wide line that is breakable (see above). Return `None`
   when there is none. Compute its overflow offset and width.
5. Report without an edit (an outcome with a residual overflow) when the tokenizer refused the paragraph, when a line is
   lazy and an ancestor of the paragraph is a list item, or when the first atom of a segment after the first (which
   starts after a hard break) is refused by `BlockSyntaxGuard.IsGuardedLineStart`: such a line start cannot be moved.
6. Wrap each segment with `LineWrapper.Wrap`, the first segment with the width of the first line and every other
   segment with the continuation width for its first line, and remember whether any result has a breakable overflow. A
   segment that ends with a hard break written with spaces gets exactly two spaces appended to its last line; one that
   ends with a backslash keeps it as part of its last atom. The lines are joined with a line feed and the continuation
   prefix.
7. When the replacement equals the text of the paragraph, return the outcome without an edit (the residual flag is the
   overflow of the wrap).
8. Run the stand-alone proof and the container proof. When either refuses, return the outcome with a residual overflow
   and no edit. Otherwise return the outcome with one `TextEdit` (start of the paragraph, its length, the replacement,
   the rule `line-length`) and the residual flag of the wrap.

Properties of the result: it is a pure function of the text, the settings, and the suppressions, so the same input gives
the same edit on every thread and in every stored form of the file (LF, CRLF, with or without a byte order mark, since
the text is LF-normalized before it reaches the formatter). It depends on the atoms and the widths only, never on how
the words were laid out before, so wrapping wrapped text proposes nothing; the document formatter additionally *settles*
each wrap against the other inline rules (see *DocumentFormatter Design*).

**RenderTheSame** and **RenderTheSameInContainer** (private): The two proofs described above.

##### Error Handling

`ArgumentNullException` for a null argument and `ArgumentException` for a block that is not a paragraph. Everything that
makes a paragraph unsafe to rewrite (a refused tokenization, a lazy line in a list item, block syntax after a hard
break, a refused proof) is a result: the paragraph stays as written and the outcome says that its overflow cannot be
fixed. A failure of the HTML comparison is a refusal of the proof, because `HtmlEquivalenceChecker` fails closed. No
exception of the helper units is caught here; the tokenizer and the guard turn a regular expression timeout into a
refusal themselves.

##### Dependencies

- **FormatContext**, **LineLayout**, **LineOwnership**, and **TextEdit** (`Formatting` subsystem) - the document, the
  settings, the line arithmetic, the footnote and protected lines, and the edit.
- **AtomTokenizer**, **TokenizedParagraph**, **ParagraphSegment**, **HardBreakKind**, **LineWrapper**,
  **WrappedLines**, and **BlockSyntaxGuard** (`Inline` subsystem) - the words, the fill, and the line starts that must
  not be created.
- **ParsedDocument**, **BlockInfo**, **BlockKind**, **LeafLine**, **DocumentParser**, and the suppression data of the
  parsed document (`Document` subsystem) - the paragraph, its lines (`GetLines`), the continuation prefix
  (`GetContinuationPrefix`), the pipeline that the proofs parse with, and the suppression ranges.
- **LintMarkConfig** and **WrapMode** (`Configuration` subsystem) - the `line-length` and `wrap` settings and the rule
  switch.
- **RuleCatalog** (`Linting` subsystem) - the rule identifiers.
- **HtmlEquivalenceChecker** and **EquivalenceResult** (`Safety` subsystem) - the comparison of the two renderings.
- **TextWidth** (`Utilities` subsystem) - the display width of a line and of a prefix.
- **Markdig** (OTS) - the paragraph, quote, list, and list item blocks, and the parse of the two proofs.

##### Callers

- **DocumentFormatter** - gives every paragraph of the text that the other rules produce to `Format` in its second
  stage, reads `IsEnabled` to decide whether the stage runs at all, and gives every wrapped paragraph to `Format` a
  second time when it settles the wraps.
- **LintMarkEngine** - creates the instance that the production `DocumentFormatter` is configured with.
