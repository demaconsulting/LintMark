### Inline

![Inline Structure](InlineView.svg)

#### Overview

The `Inline` subsystem is part of the `Formatting` subsystem. It rewrites the inline syntax inside paragraphs: the
delimiter characters of emphasis and strong emphasis, and bare URLs. It proposes edits and nothing else; it never
applies an edit, never writes a file, and never touches code, HTML, headings, or table cells. The subsystem contains one
unit, `InlineNormalizer`, and two supporting types that have no unit of their own and are described in
*InlineNormalizer Design*: `InlineCandidates`, which walks the inline tree of a paragraph and decides which shapes are
safe to rewrite, and `ParagraphProof`, which renders the paragraph before and after the edits and keeps the edits only
when the two renderings are identical.

- `InlineNormalizer` enforces `emphasis` (`_x_` and `*x*` are the same emphasis; the `emphasis` setting decides which is
  written, `*` by default), `strong` (the same for `__x__` and `**x**`, setting `strong`), and `bare-urls` (a bare
  `http://`, `https://`, or `ftp://` URL becomes `<url>`; the setting value `leave` switches the rule off).

The unit is not a block formatter. Block formatters claim whole blocks and edit their lines, whereas inline text lies in
paragraphs of every kind of container (lists, block quotes, and footnotes included), whose lines the whitespace rules
must keep. The normalizer therefore claims nothing: `DocumentFormatter` asks it for each paragraph that no block
formatter claims, and its edits replace characters inside the text of a line.

#### Interfaces

**InlineNormalizer.AddBlockEdits**: Adds the inline edits of one paragraph.

- *Type*: In-process .NET instance method on an internal sealed class.
- *Role*: Provider (to `DocumentFormatter`).
- *Contract*: Takes the `ParsedDocument`, the paragraph `BlockInfo`, the effective `LintMarkConfig`, and the `EditSet` of
  the file, and adds the edits that bring the emphasis, strong emphasis, and bare URLs of the paragraph into canonical
  form. `RuleIds` lists the three rules; the normalizer checks the suppression of each rule over the whole paragraph
  itself and applies no rule that is suppressed there. The edits are replacements of existing characters (a delimiter
  run, the first or last character of a URL), each attributed to the rule it enforces, never zero-length insertions.
- *Constraints*: Throws `ArgumentNullException` for a null argument and `ArgumentException` for a block that is not a
  paragraph. The constructor argument `proveParagraphs` (default `true`) switches the per-paragraph proof; only tests turn
  it off. Stateless apart from that switch, so one instance serves every worker thread.

The subsystem consumes the `Document` subsystem (`ParsedDocument`, `BlockInfo`, `BlockKind`, `DocumentParser` for the
pipeline and the lines of a paragraph, and the suppression and reference data), the `Configuration` subsystem
(`LintMarkConfig` and the settings `emphasis`, `strong`, and `bare-urls`), the `Linting` subsystem (`RuleCatalog`
identifiers), and the `Formatting` subsystem (`EditSet` and `TextEdit`). It is consumed by `DocumentFormatter`, and the
`Engine` subsystem creates the production instance.

#### Design

The normalizer is written for one safety rule: an edit is proposed only when the paragraph renders to the same HTML
afterwards. Two layers enforce it. The first layer, `InlineCandidates`, works on the inline syntax tree and accepts a
node only when it has a shape that is known to be safe: emphasis with exactly one or two delimiters and no other emphasis
or delimiter character inside, next to it, or anywhere as literal text in the paragraph; a URL that has a known scheme,
none of the characters that would end or change an angle-bracket autolink, and prose around it. Everything the walk does
not recognize is left alone. The second layer, `ParagraphProof`, is mandatory in the shipped tool: the edits of a
paragraph are applied to a stand-alone copy of its content, and they are kept only when both copies render to
byte-equal HTML. A paragraph that fails the proof keeps all its text, and fails it again in the next run.

A paragraph is also left alone when the document defines link references and the paragraph holds a square bracket,
because a rewritten label could stop matching its definition, which the stand-alone proof cannot see. The proof is a
local copy of the safety property and not a replacement for the checks of the `Engine` subsystem (HTML equivalence,
structural tokens, idempotency), which still run on every rewrite.

Edits of the normalizer never overlap each other (overlapping candidates make the tree untrustworthy and are dropped) and
never collide with the edits of other rules: they cover characters inside the text, whereas the whitespace passes edit
trailing runs and tabs after the first text of a line, and the block spacing pass inserts at the start of a line.

Known limits, stated honestly: paragraphs of a footnote are normalized like any other, except that emphasis or strong
emphasis that ends the last paragraph of a footnote is kept as written (the parser appends a link back to the reference
after the last inline of that paragraph, which gives the emphasis a neighbor that the check of the neighbors refuses),
URLs and emphasis elsewhere in a footnote convert, and a hostile document
that uses quirks of the parser may be left unchanged because the proof refuses it. Both leave the text as written and
report nothing; neither can change the meaning of a document. Spaces inside emphasis markers, code spans, and link text
(MD037, MD038, and MD039) are not fixed at all, because removing the space changes how the text renders; they are planned
as lint-only rules of a later addition to the `Linting` subsystem, and their aliases are not registered.

Thread safety: the unit and its supporting types are stateless (the one field is an immutable switch), and the Markdown
pipeline they render with is built once and is immutable, so one instance may format several documents at once, each
with its own `EditSet`.
