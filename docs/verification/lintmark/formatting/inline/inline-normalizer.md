#### InlineNormalizer

##### Verification Approach

`InlineNormalizer` is verified with unit tests in `InlineNormalizerTests.cs`, and its two supporting types are verified
directly with unit tests in `InlineCandidatesTests.cs` (the walk of the inline tree) and `ParagraphProofTests.cs` (the
stand-alone rendering check). The real `DocumentParser`, `SourceDocument`, `LintMarkConfig`, `EditSet`, `SourcePatcher`,
and `RuleCatalog` are used; nothing is replaced by a double. The normalizer is not a block formatter, so most tests
build a `DocumentFormatter` that has no block formatters and only the normalizer, and apply its result. A helper of the
test project (`BlockFormatterAssertions`) asserts that the result renders to the same HTML as the input
(`HtmlEquivalenceChecker`), that it keeps its Pandoc structure (`StructuralTokenChecker`), and that formatting it again
proposes nothing (`IdempotencyChecker`); these Safety checkers are used as test-time oracles, and they are verified in
the `Safety` subsystem. The scenarios assert the exact text, which is the input where the text must be kept as written.
The scenarios that need the document passes as well (a URL that starts a block after a heading) use the production
formatter, which also holds the normalizer. Most scenarios are theories with several inputs, and the settings are varied
through the configuration. Suppression is driven by real suppression comments. The tests call `AddBlockEdits` directly
where they assert on the rules and the shape of the edits, with a real parsed paragraph and a real edit set.

The proof that the paragraph check of the normalizer is needed is a test of the corpus class
`ListQuoteInlineCorpusTests` (see the `Blocks` verification): it creates a normalizer that does not prove its paragraphs
and shows that some generated paragraph then renders differently after the rewrite.

The scenarios of the code span rule (`code-span-space`) are the last theories and tests of `InlineNormalizerTests.cs`.
Most of them format a text through `AssertFormats` of the helper, which asserts the expected text together with the
rendering, structure, and stability checks; the scenarios whose text a whitespace rule of the document passes would
change (a tab inside a code span, a code span across two lines) and the scenarios about headings and tables use a
document formatter that holds only the normalizer and assert that it proposes no edit. The test of the shape of the
edits calls `AddBlockEdits` directly on a real parsed paragraph and a real edit set, and the suppression scenarios use
real suppression comments written with the name of the rule and with its legacy number.

##### Test Environment

N/A - standard test environment.

##### Acceptance Criteria

- All unit tests pass with zero failures.
- Null arguments and a block that is not a paragraph are rejected, and the list of rules (`emphasis`, `strong`,
  `bare-urls`, and `code-span-space`) is immutable and holds only registered rules.
- Emphasis and strong emphasis written with the other delimiter are rewritten to the configured delimiter, on one line
  and across a line break, next to punctuation, with a code span inside, and in link text and image descriptions.
- Asterisks stay inside a word when underscores are configured.
- Emphasis that has other emphasis next to it or inside it, a delimiter character in its content or in a literal of its
  paragraph, an escaped delimiter, or an inline HTML neighbor keeps its text.
- Code spans, inline HTML, inline math, and link destinations keep their emphasis delimiters, each in a test of its own.
- Only paragraphs are normalized, including paragraphs in lists and quotes; headings and table cells keep their text.
- Paragraphs in a footnote are normalized, and emphasis or strong emphasis that ends the last paragraph of the footnote
  is kept as written.
- A bare `http`, `https`, or `ftp` URL is wrapped in angle brackets, with trailing punctuation outside and a pair of
  parentheses inside the URL (`http://x.com/a_(b)`) wrapped with it, also in a list item; it is left as written when
  `bare-urls: leave` is configured.
- `www.` addresses, e-mail addresses, and `mailto:` addresses keep their text.
- A URL that is already a link (in angle brackets, as link text, or as a link destination, a link destination in a list
  item that starts with `[x]`, `[X]`, or `[ ]` included), a URL in a code span or inline HTML, a URL attached to a
  quotation mark, an emphasis delimiter, or a pipe, and an address with an upper-case scheme keep their text.
- A paragraph that holds a bracket keeps its text while the document defines link references.
- A paragraph that cannot be proved to render the same is left as written, and the rest of the file is normalized.
- A rule that is suppressed in the paragraph, by identifier or by alias, is not applied, and every edit and every
  reported problem names one of the others.
- A rule that is not suppressed in the paragraph is still applied.
- Each edit is attributed to the rule of the violation it removes, and no edit inserts text: every edit replaces or
  deletes existing characters.
- A code span on one line that has exactly one space after its opening backticks and one before its closing backticks,
  with a character that is not a space next to each, loses both spaces, also in a list item, a quote, link text, and
  emphasis, and the result renders the same, is stable, and is not changed when formatted again.
- A code span keeps its text when its padding is on one side only or is wider than one space, when it holds only spaces,
  when its content starts or ends with a backtick, when it continues on a second line, when it holds a tab, and in a
  heading or a table cell.
- The code span rule is applied when the other inline rules are off or suppressed, and a suppression of the code span
  rule, by identifier or by alias, leaves the padding and not the other rules.
- Each edit of the code span rule is a deletion of one space, and the rule is credited as `code-span-space`.
- The edits of a paragraph are in start order and a node that the tree holds twice is rewritten once.
- LF, CRLF, and mixed line endings and a byte order mark do not change the edits, and one shared normalizer gives the
  same result on many threads.
- The finder of candidates rejects a null text or tree, and the proof rejects null arguments, a line range that is
  negative, reversed, or beyond the text, an edit outside the content of a line, and content that is not exactly one
  paragraph.
- A document formatter without a normalizer proposes no inline edit.

##### Test Scenarios

###### Normal operation

**Underscore delimiters become asterisks**: `_a_`, `__a__`, text around both, several in one sentence, emphasis across a
line break and across a hard break, emphasis in parentheses, emphasis that holds a code span, and emphasis in link text
are formatted with the default configuration. Each delimiter is an asterisk and the text is otherwise unchanged; text
that already uses asterisks is unchanged. An image description is rewritten like link text (`![_a_](u)` becomes
`![*a*](u)`).

**Asterisk delimiters become underscores when configured**: `*a*`, `**a**`, text around both, and emphasis in
parentheses are formatted with `emphasis: underscore` and `strong: underscore`. Each delimiter is an underscore.

**Paragraphs in containers are normalized**: Paragraphs in a bullet item, an ordered item, a block quote, and a list
inside a block quote are formatted. The emphasis, the strong emphasis, and the URL are rewritten inside the container,
and the container markers stay.

**Paragraphs in a footnote are normalized**: Footnotes with emphasis in the middle of a paragraph, emphasis in the
middle of each of two paragraphs, and a URL at the end are formatted. Each is rewritten.

**Emphasis ending the last paragraph of a footnote is kept**: A footnote whose only paragraph is emphasis, one that ends
with strong emphasis, and one whose last paragraph is emphasis are formatted. The text is unchanged.

**A bare URL is wrapped in angle brackets**: `http`, `https`, and `ftp` URLs in the middle of a sentence, in
parentheses, before a comma or a period, with an underscore or an ampersand inside, with a pair of parentheses inside
(`http://x.com/a_(b)`, also inside parentheses), on a line of its own after a line of text, in a bullet item, in an
ordered item, in a task list item, and two in one paragraph are formatted. Each URL is wrapped, and the punctuation
after it stays outside the brackets.

**A URL that starts a block after a heading is wrapped and separated**: A heading directly followed by a line that holds
only a URL is formatted with the production formatter. The URL is wrapped and a blank line separates it from the
heading, and the result passes every assertion of the helper.

**Bare URLs stay when that is configured**: A paragraph with emphasis and a URL is formatted with `bare-urls: leave`.
The emphasis is rewritten and the URL is as written.

**Reference definitions do not stop paragraphs without a bracket**: A paragraph with underscore emphasis, in a document
that defines a link reference, is formatted. The emphasis is rewritten and the definition is unchanged.

**Each edit is attributed to its rule**: A paragraph with emphasis, strong emphasis, and a URL is given to
`AddBlockEdits`. The edits are credited to `emphasis`, `strong`, and `bare-urls`, and to no other rule.

**The edits replace existing characters**: A paragraph with emphasis and a URL is given to `AddBlockEdits`. Every edit
has a length greater than zero, so none is an insertion; the deletions of the code span rule (below) have the same
property.

**Padding of a code span is removed**: A code span of the word `x` that has one space after its opening backtick and one
before its closing backtick, in the middle of a sentence, is formatted. Both spaces go. The same happens for a span
delimited by two backticks that holds the two words `x y` with one space of padding on each side, a padded span that is
the whole paragraph, two padded spans in one paragraph, a padded span in a bullet item and in a block quote, a padded
span inside link text, and a padded span inside emphasis. Each result renders the same and is stable.

**The edits of the code span rule are two deletions**: A paragraph with one padded code span is given to
`AddBlockEdits`. The edits are two deletions of one character, the space after the opening backtick and the space before
the closing backtick, and both are credited to `code-span-space`.

**The code span rule works without the other inline rules**: A paragraph with underscore emphasis and a padded code
span, below comments that disable `emphasis` and `strong`, is formatted with `bare-urls: leave`. Only the padding goes
and the emphasis stays as written.

**A padded code span is stable and renders the same**: A paragraph with a code span padded with one space on both sides,
a span delimited by two backticks whose content starts with a backtick and has one space of padding, a span with two
spaces of padding on each side, and a span with one space on its left only is formatted. Only the span with one space on
each side changes, and the result is a safe, stable rewrite that is not changed when formatted again.

###### Boundary conditions

**Emphasis inside a word keeps its asterisks**: `a*b*c`, `é*a*`, `*a*b`, and `**a**b` are formatted with underscores
configured. Each is unchanged, because an underscore does not emphasize inside a word.

**Emphasis that could change meaning keeps its text**: Emphasis with other emphasis inside it, next to it, or around
it (`_a *b* c_`, `_a_*b*`, `**_a_**`, `***a***`), with an underscore in a word of the paragraph (`snake_case`), with an
escaped delimiter, with a delimiter character in its content (`_a*b_`), before inline HTML, and next to a lone asterisk
is formatted. The text is unchanged.

**Code spans, inline HTML, inline math, and link destinations keep their delimiters**: Emphasis delimiters in a code
span, in inline HTML (also in an attribute), in inline math (also display math), and in a link destination and an image
destination are formatted, one test for each place. The text is unchanged.

**Headings and table cells are not normalized**: An ATX heading, a setext heading, and a pipe table that hold emphasis
and a URL are formatted. The normalizer alone leaves the text unchanged, and the whole tool changes only the setext
heading, which it writes in ATX style with its emphasis as written.

**A www address keeps its text**: `www.x.com` alone and in a sentence is formatted. The text is unchanged.

**A mail address keeps its text**: `a@b.com`, `mailto:a@b.com`, and `<mailto:a@b.com>` are formatted. The text is
unchanged.

**A URL that is already a link keeps its text**: `<http://x.com>`, a link whose text is a URL, a link with a URL
destination, and a link in a bullet item, an ordered item, a block quote, and after other text in an item, also where
the link text is `x`, `X`, or a blank (text that looks like a task list marker, which the parser reads as one in the
middle of a list item) are formatted. The text is unchanged, and no angle bracket is added inside the parentheses of a
link.

**A URL in a code span or in inline HTML keeps its text**: A URL in a code span and a URL in the attribute of an HTML
tag are formatted. The text is unchanged.

**A URL that is attached to other text keeps its text**: A URL in double quotes, inside emphasis, and after a pipe is
formatted. The text is unchanged.

**An address with an upper-case scheme keeps its text**: `HTTP://X.COM/a` and a sentence with `Http://x.com/a.` are
formatted. The text is unchanged, because the parser does not read such an address as a bare URL.

**A bracket and link reference definitions keep the paragraph as written**: A paragraph that holds a bracket, in a
document that defines a link reference (`[_a_]` with its definition, and `[x] _a_ http://x.com` with a definition for
`x`), is formatted. The text is unchanged, because the bracket could become a link.

**A paragraph that may render differently is left and the rest is normalized**: A paragraph with an unclosed declaration
(`<!x`) before a URL and a plain paragraph with underscore emphasis are formatted. The first paragraph is exactly as
written, because wrapping the URL would close the declaration, and the second paragraph is rewritten.

**The proof of a paragraph is what prevents a mismatch**: The same paragraphs are formatted by a normalizer with the
proof and by one without it (test of `ListQuoteInlineCorpusTests`). With the proof no paragraph renders differently, and
without it at least one does, which shows that the guards alone are not enough.

**A code span with a backtick at an edge keeps its padding**: Spans delimited by two backticks whose content starts with
a backtick, ends with a backtick, or starts and ends with one, each with one space of padding on both sides, are
formatted. The text is unchanged, because the spaces keep the content apart from the delimiters.

**A code span with uneven or wider padding keeps its text**: A span with a space on its left only, one on its right
only, two spaces on both sides, two on the left and one on the right, and one on the left and two on the right are
formatted. The text is unchanged.

**A code span of spaces keeps its text**: Spans that hold one, two, and three spaces and nothing else are formatted.
The text is unchanged.

**A code span across lines or with a tab keeps its text**: A padded span that continues on the next line, and a padded
span with a tab between the padding and the word, are formatted with a document formatter that holds only the
normalizer. The text is unchanged and no edit is proposed (the whitespace rules of the document passes would expand the
tab, so the tool as a whole is not used for this scenario).

**The code spans of headings and table cells keep their text**: An ATX heading and a pipe table cell that hold a padded
code span are formatted with a document formatter that holds only the normalizer. The text is unchanged and no edit is
proposed.

**A suppressed code span rule keeps the padding and the other rules apply**: A paragraph with underscore emphasis and a
padded code span, below a comment that disables `code-span-space` or `MD038`, is formatted. The emphasis is rewritten,
the code span keeps its padding, and no edit is credited to the code span rule.

**A suppressed rule is not applied, by identifier or by alias**: A paragraph in a region that disables `emphasis`,
`MD049`, `strong`, `MD050`, `bare-urls`, or `MD034` is formatted. The disabled rule is not applied, the edits and the
reported problems carry exactly the identifiers of the rules that remain, and none carries the identifier of the
disabled rule.

**A rule that is not suppressed is still applied**: A paragraph with emphasis, strong emphasis, or a URL, in a region
that disables a different rule (`emphasis`, `MD050`, or `bare-urls`), is formatted. The rule that the comment does not
name is applied, and its edit is credited to that rule.

**LF, CRLF, mixed line endings, and a byte order mark do not change the edits**: A text with emphasis, strong emphasis,
and a URL is read from CRLF bytes, from bytes with every third line ending a bare line feed, and from bytes with a byte
order mark. The edits equal those of the LF text.

**A normalizer that is not registered proposes nothing**: A paragraph with emphasis and a URL is formatted by a document
formatter without a normalizer. The text and the edits are unchanged.

**The candidates are returned in start order with their rules**: A paragraph with a URL, strong emphasis, and emphasis,
in this order, is given to `InlineCandidates.Find`. The edits are the two brackets of the URL, the two strong
delimiters, and the two emphasis delimiters, in that order, carrying `bare-urls`, `strong`, and `emphasis`.

**No candidate when the rules are off or nothing needs a rewrite**: Paragraphs are given to `InlineCandidates.Find` with
all rules off, and with all rules on but only canonical text. No edit is returned.

**An inline that the tree holds twice is rewritten once**: A hand-built tree whose two containers each hold an emphasis
inline over the same text is given to `InlineCandidates.Find`. The two delimiters are rewritten once each.

**The proof accepts edits that keep the rendering**: The emphasis of a two-line paragraph is rewritten, and
`ParagraphProof.Holds` returns `true`.

**The proof refuses edits that change the rendering**: The edits that wrap a URL after an unclosed declaration are given
to `ParagraphProof.Holds`, which returns `false` because the angle bracket would be swallowed.

**The proof refuses what it cannot check**: Lines with a negative content start, a reversed content range, and a content
end beyond the text, an edit on a block quote marker (outside the content of the line), and the content of a heading and
of two paragraphs are given to `ParagraphProof.Holds`. Each call returns `false`.

###### Error handling

**A missing document, block, configuration, or edit set is rejected**: `AddBlockEdits` is called with each argument
null. Each call throws `ArgumentNullException` that names the parameter. `InlineCandidates.Find` is called with a null
text and a null tree, and `ParagraphProof.Holds` with a null text, a null line list, a null line, and a default edit
array; each call throws `ArgumentNullException` that names the parameter.

**A block that is not a paragraph is rejected**: `AddBlockEdits` is called for a heading block. It throws
`ArgumentException` that names `block`.

**The rule list is immutable**: The `RuleIds` property lists `emphasis`, `strong`, `bare-urls`, and `code-span-space`,
each of which is registered in the `RuleCatalog`, and a cast to a mutable type cannot change it.

**One shared instance gives one result on many threads**: One document formatter that holds the normalizer is used by
eight threads that start together; each formats a text with emphasis, strong emphasis, and URLs in a paragraph, a quote,
and a list many times. Every result equals the result of a single call.
