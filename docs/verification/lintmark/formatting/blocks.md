### Blocks

#### Verification Approach

The `Blocks` subsystem is verified by integration tests in `BlocksSubsystemTests.cs` and two corpus test classes,
`BlocksCorpusTests.cs` and `ListQuoteInlineCorpusTests.cs` (with its generator `ListQuoteInlineDocuments.cs`). All of them
use the production composition: the real `DocumentParser`, the real `DocumentFormatter` created with `HeadingFormatter`,
`CodeBlockFormatter`, `ThematicBreakFormatter`, `ListFormatter`, and `BlockQuoteFormatter` and with the `InlineNormalizer`
of the `Inline` subsystem, and the real `SourcePatcher`. No unit is replaced by a double. A shared helper,
`BlockFormatterAssertions` (the file `BlockFormatterAssertions.cs`), formats a
text and asserts every property that a rewrite of the tool must have, and the tests never relax an assertion to make a
case pass:

- the formatted text is the expected text;
- it renders to the same HTML, both as LF text and as the CRLF text that would be written to disk (`HtmlEquivalenceChecker`);
- it keeps its Pandoc structure (`StructuralTokenChecker`) and its grid table reading;
- formatting it again proposes no edit and reports no problem (`IdempotencyChecker` and a direct second run);
- the edits are ordered and inside the text, and a problem is reported exactly when the text changed;
- the same result is computed when the formatter is called from several threads (the corpus tests skip this check with
  `checkParallel: false` to save time on thousands of documents; the tests of concurrent use cover it).

These three checks are the ones that the `Engine` subsystem runs before it writes a file; they are collaborators of the
tests and are verified in the `Safety` subsystem.

`BlocksSubsystemTests` covers the interaction of the block formatters with the document passes on hand-written
documents: a document that breaks every whitespace, blank-line, heading, code block, thematic break, and final newline
rule that the subsystem and the passes enforce (14 rules), its canonical form, nested containers, Pandoc constructs,
front matter, HTML, hard breaks, tabs, indented code in many forms, tilde fences, thematic breaks, setext headings,
spacing, suppression comments, and the configured settings. It also covers the canonical forms of lists and block quotes,
a second document that breaks every list and quote rule (`bullet`, `ordered-style`, `list-indent`, `list-marker-space`,
and `blockquote-space`) with the violation of each rule on its line, that lists are never merged or split, that the
structure of a list and the content of a quote are kept, that a second run changes nothing, and that a list or quote rule
that is suppressed by identifier or by alias leaves the block alone. The test of concurrent use parses the document once
and shares it, with one formatter, among eight threads that start together at a barrier; it runs for the document that
breaks the whitespace and heading rules and for the one that breaks the list and quote rules. The tests of line
endings and of the byte order mark compare the edits and violations for the LF text with those for the same text read
from CRLF, mixed, and BOM bytes, in addition to the bytes that are written back. One test moves an inline span of a parsed
heading so that it no longer fits its line, to reach the guard that real input only reaches through a parser defect.
`BlocksCorpusTests` runs the same assertion helper over every Markdown file of the repository as
LF, CRLF, and mixed-ending text with a byte order mark, over generated nested documents (several thousand container
chains, with and without trailing whitespace), and over seeded random hostile documents of seven families (with and
without injected tabs and trailing whitespace). The repository's own files are asserted to need no edit and to give no
problem, which documents that the repository follows its own style. There is one documented carve-out: the standards
under `.github/standards` are owned by the template of the organization and are never edited in this repository. They
write emphasis with underscores, so a file there may have a finding of the `emphasis` rule only; every other finding, and
every finding in any other file, fails the test.

`ListQuoteInlineCorpusTests` runs the same assertion helper over seeded generated documents (the generator is a pure
function of its seed, so the documents are the same on every platform): 4,000 nests of lists with mixed markers, numbers,
spacing, indentation, lazy lines, blank lines, code, and quotes; 2,500 block quotes with every spacing after their
markers (paragraphs, headings, lists, code, tables, alerts, and nested quotes); and 6,000 paragraphs over an alphabet of
delimiters, backticks, brackets, angle brackets, URLs, and punctuation. Each document is formatted as LF text and as the
same text stored with CRLF line endings and with mixed line endings, both with a byte order mark; every stored form must
give the same text as the LF text, the CRLF form must render the same HTML on disk, and the LF result must render the
same HTML and be stable. The tests also assert that a large share of the generated documents changes, so that the corpus
exercises the rules, and they vary the list settings (marker, indent, and numbering) and the inline settings (underscore
delimiters, and URLs left as written). One test shows that the per-paragraph proof of the inline normalizer is needed: it
formats the generated paragraphs with a normalizer that does not prove its paragraphs and counts the results that render
differently (at least one), and counts none with the normal normalizer.

#### Test Environment

N/A - standard test environment, with these collaborators of the tests, none of them replaced by a double:

- the repository working copy: the corpus test reads the Markdown files of the repository from the working copy (found
  from the location of the test assembly) and writes nothing, so it needs a checkout and not only a build output;
- `LintMarkEngine` (`Engine` subsystem), which `BlocksCorpusTests` uses to process every Markdown file the way the tool
  does, with its shipped formatters;
- the test-support helpers of the test project: `RepositoryMarkdownFiles` (finds the files), `SyntheticDocuments`
  (the nesting documents), `RandomDocuments` (the seeded hostile documents), and `ListQuoteInlineDocuments` (the seeded
  list, quote, and paragraph documents, with the generator `Lcg`);
- `InlineNormalizer` (`Inline` subsystem), which every test formatter holds, and whose test-only constructor argument
  `proveParagraphs` is set to `false` by the one test that shows that the proof of a paragraph is needed.

The corpus tests that run the `InlineNormalizer` over generated paragraphs (`ListQuoteInlineCorpus_Format_RandomInlineText*`
and `ListQuoteInlineCorpus_Format_WithoutParagraphProof_SomeParagraphRendersDifferently`) verify the `Inline` subsystem
and the `InlineNormalizer` unit. They are kept in this subsystem's class because they share the generator and the
assertions of the list and quote corpus, and the review set of the `Blocks` subsystem holds that file; the `Inline`
verification documents refer to them.

#### Acceptance Criteria

- All integration and corpus tests pass with zero failures.
- A document that breaks every rule is rewritten to the canonical document, each of the 14 rules is reported once per
  line at the place of its change with the description of its rule, and the canonical document produces no edit and no
  problem.
- Every rewrite renders to the same HTML, keeps its Pandoc structure, and is stable when formatted again.
- Quote and list structure survives every change, code and HTML content is never changed, and a fence that is never
  closed stays as written.
- Lists are written in the canonical form (one marker character, counting numbers, aligned items, nested bullets indented
  as configured, and one space after each marker), block quote markers are followed by one space, two lists are never
  merged and a list is never split, and the structure that depends on columns (lazy lines, tabs, indented code, and the
  content of a quote) is kept.
- Emphasis, strong emphasis, and bare URLs in paragraphs are written canonically, and a list or quote rule that a
  suppression comment disables, by identifier or by alias, leaves its block alone.
- A document with CRLF line endings, mixed line endings, or a byte order mark gets the same edits and violations as the
  LF text without a mark, and the rewrite keeps the mark and writes the majority ending.
- Where a conversion could change the meaning of the text (a setext heading that is nested or multi-line, a braced
  fence, a thematic break under text, at the start of the file, or after a plus sign list marker), the text stays as
  written.
- A block in a region where a rule of its formatter is disabled stays as written, by either directive family, the blocks
  after the region are formatted, and a next-line directive keeps its place directly above its block.
- The configured settings select the fence character, the break style, the maximum of blank lines, and the final newline.
- The same parsed document gives the same result on many threads that start together.
- Every Markdown file of the repository, and every generated document, passes the assertions of the helper; the files of
  the repository need no change, except for the emphasis delimiter of the standards that the template owns.

#### Test Scenarios

##### Normal operation

**A document that breaks every rule becomes canonical**: A document with trailing whitespace, a tab, a long run of
blank lines, setext and closed headings, an indented heading, a tilde fence, indented code, a thematic break written
with asterisks, a list and a table that touch their neighbors, and no final line ending is formatted. The result is the
canonical document, and it passes every assertion of the helper.

**A canonical document is left alone**: The canonical document is formatted. No edit and no problem result.

**Every rule is reported once**: The document that breaks every rule is formatted. One problem is reported for each rule
and line of a change: 22 problems for the 14 rules, each at the line of its change (an insertion at the line it is
inserted before), with the description of its rule, and none twice. A second document that breaks every list and quote
rule (a wrong marker character on two lines, a nested list that is not indented as configured, a repeated number, a gap
after a marker, and a wide gap after nested quote markers) is formatted. One problem is reported for each of the rules
`bullet`, `list-indent`, `ordered-style`, `list-marker-space`, and `blockquote-space` and line, with the description of
its rule, and the result is the canonical text.

**The configured settings select the canonical form**: The same kind of document is formatted with `code-fence: tilde`,
`thematic-break: asterisks`, `max-blank-lines: 2`, and `final-newline: false`; with `thematic-break: underscores`,
`max-blank-lines: 3`, and a final newline; and with the defaults and a maximum of one blank line. Each rule writes the
configured form.

**Headings keep their meaning in the details**: Blanks after the number signs become one space, `#Title` stays, the
indentation of a heading outside a container goes and inside one stays (extra spaces between a list marker and a
heading become one space, by the list marker rule), trailing whitespace goes but not behind a final backslash of a setext
line, and a tab after the number signs of a heading on a later line is handled with stable results.

**Trailing whitespace goes from the lines of the blocks**: The trailing whitespace of a heading, of the two fence lines
of a code block (not of the code between them), and of a break, also one whose characters stay as written, is removed.

**Formatting twice changes nothing the second time**: The result of every document above is formatted again. No edit and
no problem result.

**Lists are written in the canonical form**: Lists with asterisk markers, numbers that repeat, items that start in the
third column, a nested list indented by four columns, a gap of several spaces after a marker with a continuation line,
and a nested list in an ordered item with such a gap are formatted. Each list has dash markers, counting numbers, items
at the margin, nested bullets indented by two columns, one space after each marker, and the content of each item moved
with its marker. Lists whose marker must stay (`* ---`, an empty item, and `* ___`, whose text is a break of its own)
are formatted and their markers are as written.

**Block quotes are written in the canonical form**: A quote with two spaces after the marker, a heading in a quote with
three spaces, and nested quotes with extra spaces are formatted. One space follows each marker.

**A list or quote rule that is suppressed leaves the block alone**: An asterisk list in a region that disables `bullet`
or `MD004`, and a quote with a wide gap in a region that disables `blockquote-space` or `MD027`, are formatted. Each
block is unchanged, and no edit and no problem result.

**A second run changes nothing after lists and quotes**: A document with a nested list with wrong markers and wide gaps,
repeated numbers, a quote with wide gaps, and an item with a second paragraph is formatted, and the result is formatted
again, also as it is stored with other line endings. The second run proposes no edit.

##### Boundary conditions

**Line endings and the byte order mark do not matter**: The document that breaks every rule is formatted from bytes with
CRLF line endings and a byte order mark, with CRLF only, with mixed endings, and with a mark and LF endings. The edits and
the violations equal those for the LF text (the line ending problem excepted), the rewrite keeps the mark, and writing it
with the target line ending gives CRLF throughout. A file with mixed line endings is written with the majority ending.

**Suppressed blocks stay as written**: A setext heading, indented code, and a break inside a region that disables the
rules of their formatters (by `markdownlint` numbers, by `lintmark` names, or all rules) stay as written, and the same
three blocks after the comment that enables the rules again are formatted. A next-line directive keeps one closed heading
as written and the next is formatted, and a next-line directive before a heading, a list, a tilde fence, a table, or
indented code keeps its place directly above the block (no blank line is inserted between them).

**A block with an unexpected shape is left alone (defensive, no requirement)**: The inline span of a parsed heading with
a tab in its text is moved so that it no longer fits the line; the document gets no edit.

**Nested containers keep their structure**: Headings, code, thematic breaks, and lists inside quotes, lists, and each
other are formatted. The containers, indentation, and prefixes stay, and no blank line is added inside a container.

**Hard breaks keep their meaning**: Two spaces stay, more spaces become two, one space is removed, and a backslash break
stays.

**Tabs are expanded only in plain text**: A tab in text and in a heading becomes spaces; a tab in indentation, code,
a link title, a table, or a code span stays.

**Pandoc constructs are kept**: Fenced divs, page break commands, display math, attributes, citations, raw blocks, and
grid tables are formatted together with the lines around them. The constructs are unchanged, trailing whitespace next to
them is removed, and a braced fence keeps its backticks even when the configuration asks for tildes.

**A grid table that is valid is left alone and an invalid one is never rewritten**: A valid grid table next to text is
formatted and stays. A document with an invalid grid table is returned without a rewrite.

**Front matter and leading dashes are kept**: Front matter keeps its whitespace, a first line of dashes is not turned
into front matter, and a heading directly after front matter gets a blank line.

**HTML blocks keep their content**: HTML blocks and comments keep their content and whitespace, and the headings next
to them get blank lines.

**A fence that is never closed stays as written**: An unclosed fence, with the blank lines after it and without a final
line ending, is formatted. Nothing changes.

**Indented code in its many forms becomes fenced code**: Indented blocks directly after a heading, with a tab indent,
with internal blank lines, with content that looks like a fence, and directly before a list are formatted. Each becomes
a fenced block that its own content cannot close.

**Tilde fences are converted correctly or left alone**: Tilde fences that contain backtick fences, and an info string
with a backtick, are formatted. A fence is lengthened where its content needs it, and the fence whose info string has
a backtick stays tilde.

**Thematic breaks keep the meaning of the document**: Breaks written with asterisks and underscores, directly under
text, under a heading, after a list, and a dashed line under text (a setext heading) are formatted. Each is converted or
left as written according to the guards, and the document renders as before. Breaks at the start of a file and after
front matter are covered by the front matter scenario.

**A break after a plus sign list marker stays as written**: A break written with asterisks after a plus sign list marker
(`+ ***`) is formatted with the default configuration. The text is unchanged, because dashes there would look like the
border of a grid table.

**Setext headings are converted only where that is safe**: Single-line, multi-line, nested, and lookalike headings are
formatted. Only the safe ones become ATX headings.

**Closing number signs are removed from headings**: Headings with closing number signs, with extra blanks before them,
and with an empty text are formatted, and so are headings whose text ends in a number sign or an escaped one. Only a
closing sequence is removed.

**Spacing is added only for real block boundaries**: Lists, tables, and fences that touch their neighbors get blank
lines where that is a violation, and text that only looks like a boundary is not separated.

**Lists are never merged or split**: Two bullet lists that are two lists only because their markers differ, and a list
with a nested list, are formatted with `bullet: dash`, `asterisk`, and `plus`. The result holds the same four lists at the
margin as the input, and the three markers at the margin stay different.

**The structure of a list is kept**: A list with a lazy continuation line, with a tab after the marker, and with five
spaces after the marker (indented code) is formatted and the text is unchanged. A list with fenced code, a quote, and a
table in an item is formatted and the whole item moves to the narrower marker. A list of ordered items that holds a
nested bullet list and nested ordered lists three levels deep, a bullet list inside an ordered item, a loose list, a
list in a block quote with wide gaps, and a list in the body of a referenced footnote are formatted and the text is
unchanged.

**The content of a quote is kept**: Fenced code, HTML, a bullet list, a setext heading, a table, indented code, a line
with five spaces after the marker, a tab after the marker, a lazy line, a quote in a list item, a footnote definition, and
a math block, each in a quote with a wide gap, are formatted. The text is unchanged.

##### Robustness

**One shared document gives the same result on many threads**: One formatter instance and one parsed document are shared
by eight threads that start together at a barrier; each formats the document many times. This runs for the document that
breaks the whitespace and heading rules and for the document that breaks the list and quote rules. Every result equals the
result of a single call, and the edits write the expected text.

**The repository and generated documents pass every assertion**: Every Markdown file of the repository (as LF, CRLF, and
mixed endings with a byte order mark), the generated nested documents, and the random hostile documents are formatted.
Each result is safe and stable, and the repository's own files need no edit; the one exception is a file under
`.github/standards`, which is owned by the template and may have findings of the `emphasis` rule only. The corpus tests
do not compute the result on several threads (they pass `checkParallel: false`); the test of concurrent use does.

**Generated lists, quotes, and paragraphs are safe and stable**: The seeded lists, quotes, and paragraphs of the corpus
are formatted in every stored form. Each result is the same text for every line ending, renders the same HTML, and is
stable, and more than a quarter of the lists and quotes and more than a tenth of the paragraphs change. The lists are
formatted again with `bullet: asterisk`, `list-indent: 4`, and `ordered-style: one`, with `bullet: plus`,
`list-indent: 2`, and `ordered-style: incrementing`, and with `bullet: dash`, `list-indent: 4`, and
`ordered-style: one`; the paragraphs are formatted again with underscore emphasis and strong emphasis and with URLs left
as written.

**The paragraph proof is what prevents a mismatch**: The generated paragraphs are formatted with a normalizer that does
not prove its paragraphs and with the normal one. At least one result of the first renders differently from its input,
and no result of the second does.
