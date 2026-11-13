### Inline

#### Verification Approach

The `Inline` subsystem is verified by integration tests in `InlineSubsystemTests.cs`. It has one unit, the
`InlineNormalizer`, which is not a block formatter: the `DocumentFormatter` calls it for the paragraphs that no block
formatter claims. The tests therefore use the production composition of the tool, built by the helper
`BlockFormatterAssertions.ProductionFormatter()` (the five block formatters, the `InlineNormalizer`, and the document
passes), together with the real `DocumentParser`, `EditSet`, and `SourcePatcher`. No unit is replaced by a double.

The helper `BlockFormatterAssertions` formats a text and asserts every property that a rewrite of the tool must have,
and the tests never relax an assertion to make a case pass. Besides the expected text, it asserts that the result renders
to the same HTML (`HtmlEquivalenceChecker`), keeps its Pandoc structure (`StructuralTokenChecker`), is stable when
formatted again (`IdempotencyChecker` and a direct second run), has ordered edits inside the text, reports a problem exactly
when the text changed, and is the same when the formatter is called from several threads. These checkers are the ones that
the `Engine` subsystem runs before it writes a file; the tests use them as oracles, and they are verified in the `Safety`
subsystem.

The subsystem tests cover the behavior that crosses the unit boundary: which paragraphs are normalized, which text keeps
its form, and the effect of the three settings (`emphasis`, `strong`, and `bare-urls`). The branches of the normalizer
(each guard, the per-paragraph proof, suppression, and rejected arguments) are verified by the unit tests and are
described in the unit document. The corpus of generated paragraphs, which also contains the test that shows that the
proof of a paragraph is needed, is part of the `Blocks` verification (`ListQuoteInlineCorpusTests.cs`).

#### Test Environment

N/A - standard test environment. The tests use only in-memory text and bytes; no file is read or written.

#### Acceptance Criteria

- All integration tests pass with zero failures.
- Emphasis and strong emphasis written with underscores are written with asterisks by default, and the other way round
  when underscores are configured, except inside a word, where asterisks stay.
- A bare URL is wrapped in angle brackets by default and stays as written when that is configured.
- Code, link destinations, inline HTML, math, addresses that are not wrapped, headings, and table cells keep their text;
  emphasis in link text is written like other emphasis.
- Paragraphs inside lists and block quotes are normalized like any other paragraph.
- A paragraph that cannot be shown to render the same after the rewrite stays as written (nested emphasis, a literal
  delimiter, a bracket while link references are defined, and an unclosed declaration before a URL), and the rest of the
  file is still normalized.
- Every rewrite renders to the same HTML, keeps its Pandoc structure, and is stable when formatted again.
- The same text gives the same result on many threads.

#### Test Scenarios

##### Normal operation

**Underscore delimiters become asterisks by default**: Paragraphs with `_a_`, `__a__`, and `A _b_ and __c__.` are
formatted with the default configuration. The delimiters are asterisks, and the result passes every assertion of the
helper.

**Asterisk delimiters become underscores when configured**: Paragraphs with `*a*` and `**a**` are formatted with
`emphasis: underscore` and `strong: underscore`. The delimiters are underscores. `a*b*c`, where the emphasis is inside a
word, keeps its asterisks, because an underscore does not emphasize inside a word.

**Bare URLs are wrapped unless that is configured otherwise**: `see http://x.com.` and `see (https://x.org/a)` are
formatted with the default configuration. The URLs are wrapped in angle brackets, and the period and the parentheses stay
outside. `see http://x.com.` is formatted with `bare-urls: leave` and is unchanged.

**Paragraphs in containers are normalized**: Paragraphs in a bullet item, in a block quote, and in the second paragraph
of an ordered item are formatted. Emphasis, strong emphasis, and URLs are rewritten inside the container, and the
container markers stay.

##### Boundary conditions

**Code, link destinations, HTML, math, and addresses keep their text**: Paragraphs with a code span that holds emphasis
and a URL, a link whose text is emphasis and whose destination holds underscores, a link whose text is plain, inline HTML
around emphasis and a URL, inline math, `www.x.com` with an e-mail address and a `mailto:` address, and URLs that are
already in angle brackets are formatted. The text is unchanged, except that emphasis in the link text is rewritten like
other emphasis (`[_a_](http://x.com/_b_)` becomes `[*a*](http://x.com/_b_)`) and the destination is unchanged.

**Headings and table cells keep their text**: A heading and a pipe table with emphasis and a URL in the text and the
cells are formatted. They are unchanged.

**A paragraph that may render differently is left alone**: A paragraph with an unclosed declaration (`<!x`) before a URL,
and a plain paragraph with underscore emphasis after it, are formatted. The first paragraph is exactly as written and the
second paragraph is rewritten.

**Paragraphs that cannot be shown safe are kept**: A paragraph with emphasis inside emphasis, a paragraph with emphasis
and a literal underscore (`snake_case`), a paragraph with a bracket in a document that defines a link reference, and a
paragraph with an unclosed declaration before a URL (followed by a plain paragraph with emphasis) are formatted. Each of
the four paragraphs is unchanged, and the plain paragraph after the last one is rewritten.

##### Robustness

**The same text gives the same result on many threads**: A text with every kind of rewrite in a paragraph, a list item,
and a block quote is formatted by eight threads that share one production formatter and start together. Every result
equals the result of a single call.
