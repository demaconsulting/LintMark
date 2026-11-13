#### BlockQuoteFormatter

##### Verification Approach

`BlockQuoteFormatter` is verified with unit tests in `BlockQuoteFormatterTests.cs`. The real `DocumentParser`,
`SourceDocument`, `LintMarkConfig`, `EditSet`, `SourcePatcher`, and `RuleCatalog` are used; nothing is replaced by a
double. A helper of the test project (`BlockFormatterAssertions`) formats a text with the production formatters (the five
block formatters, the inline normalizer, and the document passes), with the production formatters on the bytes that would
be on disk (LF, CRLF, mixed line endings, and a byte order mark), and with the quote formatter alone, and compares each
with the expected text. The helper also asserts that the result renders to the same HTML as the input
(`HtmlEquivalenceChecker`), keeps its Pandoc structure (`StructuralTokenChecker`), and is stable when formatted again
(`IdempotencyChecker`); these Safety checkers are used as test-time oracles, and they are verified in the `Safety`
subsystem. The scenarios about quotes that must keep their spaces or a lazy line, tab, or five-space line are theories
that assert the exact text, which is the input. Suppression is driven by real suppression
comments, named by identifier and by alias. The tests of null arguments and of the wrong block kind use blocks of real
parsed documents; the wrong block kind includes a quote that is nested in another quote. The test for concurrent use
parses one document once and shares it, with one formatter instance, among several threads, and compares each result
with the expected text.

##### Test Environment

N/A - standard test environment.

##### Acceptance Criteria

- All unit tests pass with zero failures.
- Only the outermost block quote of a nest is accepted; null arguments and a block that is not an outermost quote are
  rejected; the list of rules (`blockquote-space`) is immutable and holds a registered rule.
- Two to four spaces after a quote marker become one space on the paragraph, heading, and alert lines of a quote, also
  after the markers of nested quotes.
- A line with five or more spaces after the marker, a tab, or no marker (a lazy line) keeps its text, and the marked lines
  of the same paragraph are still collapsed.
- Setext headings, tables, fenced code, HTML, indented code, and math keep their text when they have spaces after the
  marker.
- A list, an ordered list, and a footnote in a quote keep their text when they have spaces after the marker, because the
  spaces are indentation there.
- A quote inside a list item is left as written.
- A quote line that holds only the marker is left as written by the quote formatter, and the whole tool trims it.
- Every edit is attributed to the `blockquote-space` rule.
- A quote in which the rule is suppressed, by identifier or by alias, is left as written.
- CRLF line endings, mixed line endings, and a byte order mark do not change the edits, and one shared parsed document
  gives the same result on many threads.

##### Test Scenarios

###### Normal operation

**Two to four spaces after a marker become one**: Quotes with two, three, and four spaces after the marker, with gaps of
different width on several lines, and with a wide gap on the first or the second line only are formatted. One space
follows each marker.

**The markers of nested quotes are written with one space each**: `>  >  a`, `>  > a`, `> >   a`, and `>  >  >  a` are
formatted. Each marker is followed by one space.

**Heading and alert lines are normalized**: A heading with a wide gap, a heading followed by a text line with a wide gap,
and an alert line (`[!NOTE]`) with its text line are formatted. One space follows each marker.

**Every edit is attributed to the quote rule**: A quote with two wide gaps is formatted by the quote formatter alone. Two
edits result, both credited to `blockquote-space`.

###### Boundary conditions

**Five spaces, a tab, and a lazy line are left alone**: A paragraph line with five spaces after the marker, a quote that
holds only indented code, a tab after the marker, and a lazy line after a quoted line are formatted. Each is unchanged,
in every line ending.

**A lazy line keeps its text while the marked lines are collapsed**: A paragraph in a quote with two wide gaps and a lazy
line is formatted. The two marked lines are collapsed and the lazy line is as written.

**Other blocks in a quote keep their text**: A setext heading, a pipe table, a fenced code block, an HTML block,
indented code, and a math block in a quote, each with a wide gap after the marker, are formatted. The text is unchanged.

**A list or a footnote in a quote keeps its text**: A bullet list and an ordered list with a wide gap, a list with an
indented continuation line, and a footnote definition with a wide gap in a quote are formatted. The text is unchanged.

**A quote in a list item is left alone**: An item that holds a quote with a wide gap is formatted. The text is unchanged.

**A marker-only line is left to the whitespace rule**: A quote with a line that holds only the marker and spaces between
two wide lines is formatted. The quote formatter alone collapses the gaps of the two paragraph lines and leaves the
marker-only line with its spaces; the whole tool then removes the spaces of that line.

**CRLF line endings, mixed line endings, and a byte order mark do not change the edits**: A quote with wide gaps and a
heading is read from LF text, from CRLF text, from text with mixed line endings, and from bytes with a byte order mark.
The edits are the same for each.

**A suppressed rule leaves the quote alone**: A quote with a wide gap follows a comment that disables `blockquote-space`,
`MD027`, or all rules. The quote is unchanged, and no edit and no problem result.

###### Robustness

**One shared instance gives one result on many threads**: One formatter and one parsed document with two quotes are used
by several threads that start together; each formats it many times. Every result is the expected text.

###### Error handling

**A missing block, context, or edit set is rejected**: `CanFormat` with a null block, and `Format` with each null
argument, throw `ArgumentNullException` that names the parameter.

**A block that is not an outermost quote is rejected**: `Format` for a paragraph block and for a quote that is nested in
another quote throws `ArgumentException` that names `block`. `CanFormat` accepts only the outermost quote of a nest (a
quote in a list item counts as outermost) and no other block.

**The rule list is immutable**: The `RuleIds` property lists `blockquote-space`, which is registered, and a cast to a
mutable type cannot change it.
