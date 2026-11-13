#### InlineNormalizer

##### Verification Approach

`InlineNormalizer` is verified with unit tests in `InlineNormalizerTests.cs`. The real `DocumentParser`,
`SourceDocument`, `LintMarkConfig`, `EditSet`, `SourcePatcher`, and `RuleCatalog` are used; nothing is replaced by a
double. The normalizer is not a block formatter, so most tests build a `DocumentFormatter` that has no block formatters
and only the normalizer, and apply its result. A helper of the test project (`BlockFormatterAssertions`) asserts that the
result renders to the same HTML as the input (`HtmlEquivalenceChecker`), that it keeps its Pandoc structure
(`StructuralTokenChecker`), and that formatting it again proposes nothing (`IdempotencyChecker`); these Safety checkers
are used as test-time oracles, and they are verified in the `Safety` subsystem. The scenarios assert the exact text,
which is the input where the text must be kept as written. The scenarios that need the document
passes as well (a URL that starts a block after a heading) use the production formatter, which also holds the
normalizer. Most scenarios are theories with several inputs, and the settings are varied through the configuration.
Suppression is driven by real suppression comments. The tests call `AddBlockEdits` directly where they assert on the rules
and the shape of the edits, with a real parsed paragraph and a real edit set.

The proof that the paragraph check of the normalizer is needed is a test of the corpus class `ListQuoteInlineCorpusTests`
(see the `Blocks` verification): it creates a normalizer that does not prove its paragraphs and shows that some generated
paragraph then renders differently after the rewrite.

##### Test Environment

N/A - standard test environment.

##### Acceptance Criteria

- All unit tests pass with zero failures.
- Null arguments and a block that is not a paragraph are rejected, and the list of rules (`emphasis`, `strong`, and
  `bare-urls`) is immutable and holds only registered rules.
- Emphasis and strong emphasis written with the other delimiter are rewritten to the configured delimiter, on one line
  and across a line break, next to punctuation, with a code span inside, and in link text and image descriptions.
- Asterisks stay inside a word when underscores are configured.
- Emphasis that has other emphasis next to it or inside it, a delimiter character in its content or in a literal of its
  paragraph, an escaped delimiter, or an inline HTML neighbor keeps its text.
- Code spans, inline HTML, inline math, and link destinations keep their text.
- Only paragraphs are normalized, including paragraphs in lists and quotes; headings and table cells keep their text.
- Paragraphs in a footnote are normalized, except emphasis or strong emphasis that ends the last paragraph of the
  footnote, which is kept as written.
- A bare `http`, `https`, or `ftp` URL is wrapped in angle brackets, with trailing punctuation and balanced parentheses
  outside, and is left as written when `bare-urls: leave` is configured.
- `www.` addresses, e-mail addresses, `mailto:` addresses, URLs that already have angle brackets, and URLs in links,
  code, HTML, or emphasis keep their text.
- A paragraph that holds a bracket keeps its text while the document defines link references.
- A paragraph that cannot be proved to render the same is left as written, and the rest of the file is normalized.
- A rule that is suppressed in the paragraph, by identifier or by alias, is not applied, and the other rules still are.
- Each edit is attributed to the rule of the violation it removes, and every edit replaces existing characters.
- CRLF line endings and a byte order mark do not change the edits, and one shared normalizer gives the same result on
  many threads.
- A document formatter without a normalizer proposes no inline edit.

##### Test Scenarios

###### Normal operation

**Underscore delimiters become asterisks**: `_a_`, `__a__`, text around both, several in one sentence, emphasis across a
line break and across a hard break, emphasis in parentheses, emphasis that holds a code span, and emphasis in link text
are formatted with the default configuration. Each delimiter is an asterisk and the text is otherwise unchanged; text
that already uses asterisks is unchanged. An image description is rewritten like link text (`![_a_](u)` becomes
`![*a*](u)`).

**Asterisk delimiters become underscores when configured**: `*a*`, `**a**`, text around both, and emphasis in parentheses
are formatted with `emphasis: underscore` and `strong: underscore`. Each delimiter is an underscore.

**Paragraphs in containers are normalized**: Paragraphs in a bullet item, an ordered item, a block quote, and a list
inside a block quote are formatted. The emphasis, the strong emphasis, and the URL are rewritten inside the container,
and the container markers stay.

**Paragraphs in a footnote are normalized except emphasis ending the last paragraph**: Footnotes with emphasis in the
middle of a paragraph, emphasis in the middle of each of two paragraphs, and a URL at the end are formatted. Each is
rewritten. A footnote whose only paragraph is emphasis, one that ends with strong emphasis, and one whose last paragraph
is emphasis are formatted and the text is unchanged.

**A bare URL is wrapped in angle brackets**: `http`, `https`, and `ftp` URLs in the middle of a sentence, in parentheses,
before a comma or a period, with an underscore or an ampersand inside, on a line of its own after a line of text, and two
in one paragraph are formatted. Each URL is wrapped, and the punctuation after it stays outside the brackets.

**A URL that starts a block after a heading is wrapped and separated**: A heading directly followed by a line that holds
only a URL is formatted with the production formatter. The URL is wrapped and a blank line separates it from the heading,
and the result passes every assertion of the helper.

**Bare URLs stay when that is configured**: A paragraph with emphasis and a URL is formatted with `bare-urls: leave`. The
emphasis is rewritten and the URL is as written.

**Reference definitions do not stop paragraphs without a bracket**: A paragraph with underscore emphasis, in a document
that defines a link reference, is formatted. The emphasis is rewritten and the definition is unchanged.

**Each edit is attributed to its rule**: A paragraph with emphasis, strong emphasis, and a URL is given to `AddBlockEdits`.
The edits are credited to `emphasis`, `strong`, and `bare-urls`, and to no other rule.

**The edits replace existing characters**: A paragraph with emphasis and a URL is given to `AddBlockEdits`. Every edit has
a length greater than zero, so none is an insertion.

###### Boundary conditions

**Emphasis inside a word keeps its asterisks**: `a*b*c`, `é*a*`, `*a*b`, and `**a**b` are formatted with underscores
configured. Each is unchanged, because an underscore does not emphasize inside a word.

**Emphasis that could change meaning keeps its text**: Emphasis with other emphasis inside it, next to it, or around
it (`_a *b* c_`, `_a_*b*`, `**_a_**`, `***a***`), with an underscore in a word of the paragraph (`snake_case`), with an
escaped delimiter, with a delimiter character in its content (`_a*b_`), before inline HTML, and next to a lone asterisk
is formatted. The text is unchanged.

**Code, HTML, math, and destinations keep their text**: Emphasis and URLs in a code span, in inline HTML, in inline
math, in a link destination, and in an image destination are formatted. The text is unchanged.

**Headings and table cells are not normalized**: An ATX heading, a setext heading, and a pipe table that hold emphasis and
a URL are formatted. The normalizer alone leaves the text unchanged, and the whole tool changes only the setext heading,
which it writes in ATX style with its emphasis as written.

**Other addresses and links keep their text**: `www.x.com`, `a@b.com`, `mailto:a@b.com`, `<http://x.com>`,
`<mailto:a@b.com>`, a link whose text is a URL, a link with a URL destination, a URL in a code span, a URL in the
attribute of an HTML tag, a URL in double quotes, a URL inside emphasis, and a URL after a pipe are formatted. The text
is unchanged.

**A bracket and link reference definitions keep the paragraph as written**: A paragraph that holds a bracket, in a
document that defines a link reference (`[_a_]` with its definition, and `[x] _a_ http://x.com` with a definition for
`x`), is formatted. The text is unchanged, because the bracket could become a link.

**A paragraph that may render differently is left and the rest is normalized**: A paragraph with an unclosed declaration
(`<!x`) before a URL and a plain paragraph with underscore emphasis are formatted. The first paragraph is exactly as
written, because wrapping the URL would close the declaration, and the second paragraph is rewritten.

**The proof of a paragraph is what prevents a mismatch**: The same paragraphs are formatted by a normalizer with the proof
and by one without it (test of `ListQuoteInlineCorpusTests`). With the proof no paragraph renders differently, and
without it at least one does, which shows that the guards alone are not enough.

**A suppressed rule is not applied, by identifier or by alias**: A paragraph in a region that disables `emphasis`,
`MD049`, `strong`, `MD050`, `bare-urls`, or `MD034` is formatted. The disabled rule is not applied and the other two still
are, and no problem is reported for the disabled rule.

**CRLF line endings and a byte order mark do not change the edits**: A text with emphasis, strong emphasis, and a URL is
read from CRLF bytes and from bytes with a byte order mark. The edits equal those of the LF text.

**A normalizer that is not registered proposes nothing**: A paragraph with emphasis and a URL is formatted by a document
formatter without a normalizer. The text and the edits are unchanged.

###### Error handling

**A missing document, block, configuration, or edit set is rejected**: `AddBlockEdits` is called with each argument null.
Each call throws `ArgumentNullException` that names the parameter.

**A block that is not a paragraph is rejected**: `AddBlockEdits` is called for a heading block. It throws
`ArgumentException` that names `block`.

**The rule list is immutable**: The `RuleIds` property lists `emphasis`, `strong`, and `bare-urls`, each of which is
registered in the `RuleCatalog`, and a cast to a mutable type cannot change it.

**One shared instance gives one result on many threads**: One document formatter that holds the normalizer is used by eight
threads that start together; each formats a text with emphasis, strong emphasis, and URLs in a paragraph, a quote, and a
list many times. Every result equals the result of a single call.
