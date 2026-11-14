#### InlineNormalizer

![Inline Structure](InlineView.svg)

##### Purpose

`InlineNormalizer` proposes the edits that write the emphasis, strong emphasis, and bare URLs of a paragraph in the
canonical form: emphasis and strong emphasis with the configured delimiter character (`*` by default), and a bare
`http://`, `https://`, or `ftp://` URL wrapped in angle brackets. It enforces the rules `emphasis`, `strong`, and
`bare-urls`. It never changes what a paragraph renders to: it proposes only the shapes that are safe to rewrite and
proves each proposal by rendering. Its supporting types `InlineCandidates` (the walk of the inline tree and its guards)
and `ParagraphProof` (the stand-alone rendering check) have no unit of their own and are described in this document.

##### Data Model

`InlineNormalizer` is an internal sealed class with one field.

- `_prove`: a `bool`, set by the constructor argument `proveParagraphs` (default `true`), that says whether the edits of
  a paragraph are proved before they are kept. The shipped tool always proves; `false` exists so that a test can show
  that the guards alone are not enough.
- `RuleIds`: an immutable array of `emphasis`, `strong`, and `bare-urls`, in this order. The normalizer is not an
  `IBlockFormatter`, so `DocumentFormatter` does not read this list; the normalizer checks the suppressions itself.

The normalizer claims no lines. `DocumentFormatter` asks it for each paragraph that no block formatter claims and that is
not protected (paragraphs in lists, block quotes, and footnotes included, headings and table cells excluded), so the
whitespace rules keep their lines. Its edits replace characters inside the text of a line (a delimiter run, the first or
the last character of a URL); they never touch whitespace and are never zero-length insertions, so they cannot conflict
with the whitespace edits of the passes or with the blank lines that the block spacing pass inserts.

**InlineCandidates** is a static class. Its `Find` method returns the edits of one paragraph as an immutable array,
without overlaps and in start order. It holds the list of schemes (`http://`, `https://`, `ftp://`).

**ParagraphProof** is a static class. Its `Holds` method says whether a set of edits leaves the rendering of one
paragraph unchanged.

Invariants: every edit carries the identifier of the rule that it enforces (`emphasis` for a single delimiter, `strong`
for a double one, `bare-urls` for a URL); the edits of one paragraph never overlap; a paragraph either gets all of its
edits or none of them.

##### Key Methods

**Constructor**: Creates a normalizer.

- *Parameters*: `bool proveParagraphs` (default `true`).

**AddBlockEdits**: Adds the edits of one paragraph.

- *Parameters*: `ParsedDocument document`, `BlockInfo block` (a paragraph of the document), `LintMarkConfig config`,
  `EditSet edits`.
- *Preconditions*: no argument is null and the block is of kind `Paragraph`.
- *Postconditions*: `edits` holds all the edits that make the paragraph canonical and that the proof accepts, or none; a
  canonical paragraph adds none.

Algorithm: (1) a block whose node has no inline content adds nothing. (2) The rule states are fixed: `emphasis` is off
when it is suppressed anywhere in the paragraph (`SuppressionMap.IsSuppressedRange`), otherwise the target is
`config.Emphasis`; `strong` likewise with `config.Strong`; `bare-urls` is on when `config.BareUrls` is `autolink` and the
rule is not suppressed (the value `leave` switches it off). When all three are off the paragraph is left alone. (3) When
the document defines link references and the text of the paragraph holds a `[`, the paragraph is left alone: a rewritten
label could stop matching its definition, which the stand-alone proof cannot see. (4) `InlineCandidates.Find` returns the
edits; none means nothing to do. (5) Unless proving is off, `ParagraphProof.Holds` must accept the edits. (6) The edits
are added to the `EditSet`.

**InlineCandidates.Find**: Finds the edits of one paragraph.

- *Parameters*: `string text` (the LF-normalized text of the document), `ContainerInline root` (the inline content of
  the paragraph), the target of emphasis and of strong emphasis (`EmphasisMarker?`, `null` when the rule is off), and
  `bool urls`.
- *Returns*: the edits, without overlaps and ordered by start offset; empty when there is no safe candidate.
- *Errors*: `ArgumentNullException` for a null text or root.

Algorithm: the inline tree is walked once, keeping track of whether the walk is inside an emphasis node written with `*`
or `_` and whether it is inside the text of a link. A scan that precedes the walk looks for a literal `*` or `_` in any
literal inline of the paragraph, except in the text of automatic links, which is part of the link. The paragraph then
contributes **emphasis candidates** and **URL candidates**.

An emphasis node is a candidate when all of these hold:

- its delimiter character is `*` or `_`, and the paragraph holds no literal delimiter character (an unpaired or escaped
  delimiter changes how the others pair);
- it is not inside another emphasis node written with `*` or `_`, and it holds no emphasis node itself;
- its delimiter count is one (rule `emphasis`) or two (rule `strong`), the target of that rule is set, and the target
  character differs from the delimiter character of the node (a run of three is never touched);
- its source text starts and ends with exactly the delimiters and has content between them;
- neither `*` nor `_` occurs in the content or in the character just before or just after the node;
- the previous and next inline are absent, plain text, a line break, a code span, or a link, so that no other
  delimiter can pair with these;
- for the target `_`, the characters before and after the node are outside the text, a blank, or an ASCII character that
  is not a letter or digit, because an underscore inside a word is not emphasis.

The edits replace the delimiter run at each end by a run of the target character of the same length (two edits per
node).

A URL is a candidate when it is an autolink that the parser found in the text (a bare URL), is not inside the text of
a link, is not the destination of a link (the text before it is not `](`, which also covers a destination that the parser
reads as a task list marker, `[x]`, `[X]`, or `[ ]`, followed by a URL in parentheses in a list item), its source starts
with `http://`, `https://`, or `ftp://` and holds none of a blank, `<`, `>`, `|`, a line feed, or a tab, and it stands
where prose puts a URL: after a blank (a line start included) or `(`, and before a blank, the end of the text, or one of
`.`, `,`, `;`, `:`, `!`, `?`, `)`. The parser reads only a lower-case scheme as a bare URL, so an address such as
`HTTP://X.COM/a` is no candidate and stays as written. A pair of parentheses inside the URL is part of it
(`http://x.com/a_(b)` is wrapped as a whole). The edits replace the first
character `c` of the URL by `<c` and the last character `d` by `d>`. URLs that start with `www.`, e-mail addresses,
`mailto:` links, and URLs in headings, table cells, link text, code, and HTML stay as written, because wrapping them
renders differently or is not a bare URL.

The result is made distinct (an inline tree that holds the same node twice proposes the same edit twice, and one stands),
and when edits remain that overlap without being equal the tree cannot be trusted and the result is empty.

**ParagraphProof.Holds**: Proves that edits leave a paragraph's rendering unchanged.

- *Parameters*: `string text`, `IReadOnlyList<LeafLine> lines` (from `DocumentParser.GetLines`), and
  `ImmutableArray<TextEdit> edits`.
- *Returns*: `true` only when the stand-alone content of the paragraph is read as exactly one paragraph, every edit lies
  inside the content of one line, and the edited content renders to the same HTML as the original.
- *Errors*: `ArgumentNullException` for a null argument, a null element of `lines`, or a default edit array.

Algorithm: the content of the paragraph is rebuilt from the content range of each line (the part after the container
prefix) and joined with line feeds, which is what the inline parser reads. A line whose range is negative, reversed, or
beyond the text makes the proof fail. The edits are translated into that content, from the last to the first; an edit
that does not lie inside the content of one line makes the proof fail. The original content must parse as exactly one
block, a paragraph. Both texts are rendered with Markdig and the pipeline of `DocumentParser`, and the proof holds only
when the two HTML strings are equal character for character.

Why the proof exists: the guards keep the known ways out of the candidates, but guards alone were measured to be
insufficient (adjacent delimiter runs that pair differently once one of them is rewritten, an earlier unclosed
construct that swallows an inserted `>`). The proof is a local copy of the safety property; it does not replace the
file-level checks of the `Engine` subsystem, which still run on every rewrite. A paragraph whose proof fails is left as
written, and fails it again in the next run, so formatting stays idempotent. After a conversion no candidate remains,
because the delimiter now equals the target and a URL in angle brackets is no bare URL.

Known limits: a paragraph that the proof or a guard refuses is left as written even though a human could convert it (a
hostile document that uses quirks of the parser, a paragraph with a literal `*` or `_`, and a paragraph with a `[` in a
document that has link reference definitions). Emphasis or strong emphasis that ends the last paragraph of a footnote is
kept as written and nothing is reported (the parser appends a link back to the reference after the last inline of that
paragraph, so the emphasis has a neighbor that is neither text nor a code span, a line break, or a link, and the check
of the neighbors refuses it). The proof stands behind every edit that does get through. URLs and emphasis anywhere else
in a footnote, a second paragraph included, are converted. `***text***` is left as written, and so is an emphasis that
would have to become an underscore inside a word.

##### Error Handling

`ArgumentNullException` for a null argument and `ArgumentException` for a block that is not a paragraph. A paragraph
whose tree is not what the guards expect, whose edits overlap, or whose proof fails produces no edit and no violation.
An edit that conflicts with another edit would make `EditSet.Add` throw `InvalidOperationException`; that is a defect
and `LintMarkEngine` turns it into a `file-format-error` for the file. Nothing is caught here.

##### Dependencies

- **EditSet** and **TextEdit** (`Formatting` subsystem) - the edits.
- **ParsedDocument**, **BlockInfo**, **BlockKind**, **DocumentParser**, **LeafLine**, and the suppression and reference
  data of the parsed document (`Document` subsystem) - the paragraph, its lines (`GetLines`), the pipeline that the
  proof renders with, the suppression ranges, and the link reference definitions.
- **LintMarkConfig**, **EmphasisMarker**, and **BareUrlStyle** (`Configuration` subsystem) - the `emphasis`, `strong`,
  and `bare-urls` settings.
- **RuleCatalog** (`Linting` subsystem) - the rule identifiers of the edits.
- **Markdig** (OTS) - the inline tree (`EmphasisInline`, `LinkInline`, `LiteralInline`, and the other inlines) and the
  HTML rendering of the proof.

##### Callers

- **DocumentFormatter** - calls `AddBlockEdits` for each unclaimed, unprotected paragraph while it collects the edits
  of the unclaimed blocks.
- **LintMarkEngine** - creates the instance that the production `DocumentFormatter` is configured with.
