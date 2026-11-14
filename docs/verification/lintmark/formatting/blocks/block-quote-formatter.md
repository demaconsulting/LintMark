#### BlockQuoteFormatter

##### Verification Approach

`BlockQuoteFormatter` is verified with unit tests in `BlockQuoteFormatterTests.cs`. The real `DocumentParser`,
`SourceDocument`, `LintMarkConfig`, `EditSet`, `SourcePatcher`, and `RuleCatalog` are used; nothing is replaced by a
double. A helper of the test project (`BlockFormatterAssertions`) formats a text with the production formatters (the
five block formatters, the inline normalizer, and the document passes), with the production formatters on the bytes that
would be on disk (LF, CRLF, mixed line endings, and a byte order mark), and with the quote formatter alone, and compares
each with the expected text. The helper also asserts that the result renders to the same HTML as the input
(`HtmlEquivalenceChecker`), keeps its Pandoc structure (`StructuralTokenChecker`), and is stable when formatted again
(`IdempotencyChecker`); these Safety checkers are used as test-time oracles, and they are verified in the `Safety`
subsystem. The scenarios about quotes that must keep their spaces or a lazy line, tab, or five-space line are theories
that assert the exact text, which is the input (or the text where the lines around it are collapsed). Suppression is
driven by real suppression comments, named by identifier and by alias. The tests of null arguments and of the wrong
block kind use blocks of real parsed documents; the wrong block kind includes a quote that is nested in another quote.
The test for concurrent use parses one document once and shares it, with one formatter instance, among several threads,
and compares each result with the expected text.

##### Test Environment

N/A - standard test environment.

##### Acceptance Criteria

- All unit tests pass with zero failures.
- Only the outermost block quote of a nest is accepted; null arguments and a block that is not an outermost quote are
  rejected; the list of rules (`blockquote-space`) is immutable and holds a registered rule.
- Two to four spaces after a quote marker become one space on the paragraph, heading, and alert lines of a quote, also
  after the markers of nested quotes.
- A line with five or more spaces after the marker keeps its text.
- A line with a tab in its prefix (after the first marker, after a nested marker, or on a later line) keeps its text, and
  the other lines of the quote are still collapsed.
- A line without a marker for its quote (a lazy line, also in a nested quote) keeps its text, and the marked lines of the
  same quote are still collapsed.
- Fenced code, indented code, HTML, math, setext headings, and tables each keep their text when they have spaces after the
  marker.
- A list and a footnote in a quote keep their text when they have spaces after the marker, because the spaces are
  indentation there.
- A quote inside a list item, also where the list is inside a quote, is left as written.
- A quote in canonical form gets no edit and no problem is reported.
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

**Five spaces are left alone**: A paragraph line with five spaces after the marker and a quote that holds only indented
code are formatted. Each is unchanged, in every line ending.

**A tab in the prefix is left alone**: A tab after the marker, a tab on the second line of a quote, a tab after the marker
of a nested quote with and without a wide gap before it, and a quote whose first line has a wide gap and whose second line
has a tab are formatted. The lines with a tab are as written; the wide gap of the first line of the last quote is
collapsed.

**A lazy line is left alone**: A lazy line after a quoted line, a paragraph in a quote with two wide gaps and a lazy line,
and a nested quote whose second line has one marker only are formatted. The lazy lines are as written and the marked lines
that have a wide gap are collapsed.

**Code in a quote keeps its text**: A fenced code block and indented code in a quote, with a wide gap after the marker,
are formatted. The text is unchanged.

**An HTML block in a quote keeps its text**: An HTML block with a wide gap after the marker is formatted. The text is
unchanged.

**A math block in a quote keeps its text**: A math block with a wide gap after the marker is formatted. The text is
unchanged.

**A setext heading in a quote keeps its text**: A setext heading with a wide gap after the marker is formatted. The text
is unchanged.

**A table in a quote keeps its text**: A pipe table with a wide gap after the marker is formatted. The text is unchanged.

**A list in a quote keeps its text**: A bullet list and an ordered list with a wide gap, and a list with an indented
continuation line, in a quote are formatted. The text is unchanged.

**A footnote in a quote keeps its text**: A footnote definition with a wide gap in a quote is formatted. The text is
unchanged.

**A quote in a list item is left alone**: An item that holds a quote with a wide gap, and a quote in an item of a list
that is itself in a quote, are formatted. The text is unchanged.

**A canonical quote gets no edit**: Quotes with several lines, nested markers, a heading, a marker-only line, and an alert
are formatted by the quote formatter alone and by the whole tool. The quote formatter proposes no edit and the tool reports
no problem.

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
