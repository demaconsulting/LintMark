#### ThematicBreakFormatter

##### Verification Approach

`ThematicBreakFormatter` is verified with unit tests in `ThematicBreakFormatterTests.cs`. The real `DocumentParser`,
`SourceDocument`, `LintMarkConfig`, `EditSet`, and `SourcePatcher` are used; nothing is replaced by a double. A helper of
the test project (`BlockFormatterAssertions`) applies the formatter alone, without the document passes, patches the
text, and asserts that the result is the expected text, that it renders to the same HTML as the input, that it keeps its
Pandoc structure, and that the formatter has nothing more to do with the result. Most scenarios are theories with several
inputs, and the break style is varied through the configuration. Suppression is driven by real suppression comments. One
test builds a document by hand (`with`) whose block list lacks the break, to reach a branch that the parser cannot
produce. The test for concurrent use parses one document once and shares it, with one formatter instance, among eight
threads that start together at a barrier; each thread formats it many times into its own edit set, and every result is
compared with the result of a single call.

##### Test Environment

N/A - standard test environment.

##### Acceptance Criteria

- All unit tests pass with zero failures.
- A thematic break is accepted and nothing else, in particular not a setext underline; null arguments and a block that is
  not a break are rejected; the list of rules is immutable.
- Every written form of a break becomes three characters of the configured style, the indentation and the container prefix
  stay, and trailing whitespace is removed.
- A break is left as written where the conversion would underline text, start or close front matter, join a list marker
  of the same character, or look like a grid table border after a plus sign; trailing whitespace is still removed there.
- Each edit is attributed to the rule that it enforces, and a canonical break produces no edit.
- A break in which any rule of the formatter is suppressed is left as written, trailing whitespace included.
- CRLF line endings, mixed line endings, and a byte order mark do not change the edits, and one shared parsed document
  gives the same result on many threads.

##### Test Scenarios

###### Normal operation

**Every form of a break becomes three dashes**: Breaks written with three and more asterisks, underscores, and dashes,
with spaces and tabs between the characters, are formatted. Each becomes `---`.

**The configuration selects the style**: The same breaks are formatted with `thematic-break: asterisks` and with
`underscores`. Each becomes `***` or `___`, and a break of the configured style is unchanged.

**A break on the first line follows the configuration**: A break on the first line of the file, with asterisks or
underscores as the configured style, is converted, because the guard concerns the dash form only.

**The indentation of a break is kept**: Breaks indented by two and three spaces are formatted. The indentation stays.

**Trailing whitespace is removed**: Breaks with trailing spaces and a tab are formatted. The whitespace goes and the
break becomes canonical, also when a guard keeps its characters (`+ ***`, `- ***`).

**A break in a container is converted in place**: Breaks in a quote and a list item are formatted. The prefix stays.

**A break after a complete block is converted**: A break directly after a heading, fenced code, indented code, a list, a
quote, and a pipe table is formatted. It becomes `---`, because the blocks are complete in themselves. A break after an
ordered list marker (`1. ***`, `2) * * *`) is converted as well.

**Each edit is attributed to its rule**: A break written with asterisks and trailing whitespace is formatted. The edits
are credited to `thematic-break` and `trailing-whitespace`.

**A canonical break is left alone**: `---` on its own line is formatted. No edit results.

###### Boundary conditions

**A break under text stays as written**: Breaks directly under a paragraph, a list item, a quote line, and a link
reference definition are formatted. They are unchanged, because dashes would underline the text, and trailing whitespace
after such a break is still removed. A break directly under front matter or an HTML block counts as under text and is
unchanged as well (the guard is conservative).

**A break on the first line of a file stays as written**: A break with asterisks, underscores, or spaced dashes on the
first line is formatted with the default configuration. It is unchanged, because dashes could open front matter.

**A dashes line after a first line of dashes stays as written**: A document that starts with dashes and has a second
dashes line is formatted. The second line is unchanged, because converting it could close front matter that the document
never had.

**A break after front matter is converted**: A break after real front matter is formatted. It becomes `---`.

**A break after a list marker stays as written**: `- ***` with the default configuration and `* ---` and `* ___` with
the asterisk configuration are formatted. Each is unchanged, because the converted line would be a thematic break of its
own and the list would be lost; `- ***` with the underscore configuration is converted.

**A break after a plus sign list marker stays as written when dashes are wanted**: `+ ***`, `+ * * *`, `+ ___`, `> + ***`
are formatted with the default thematic break style, with the production formatters (with `bullet: plus`, so that the list
formatter keeps the marker; it only closes up an extra gap after it) and with the formatter alone. Each is
unchanged, because `+ ---` looks like the border of a grid table, and the result renders the same and is stable. With the
asterisk configuration `+ * * *` becomes `+ ***`, and with the underscore configuration `+ ***` and `+ - - -` become
`+ ___`; the production formatters are given `bullet: plus` for these inputs too.

**A break that is missing from the block list has no previous sibling (defensive, no requirement)**: A break directly
under text is formatted in a hand-built document whose block list does not hold the break. Nothing is thrown, the text
above is not taken for a sibling, and the break is converted.

**A suppressed rule leaves the break alone**: A comment that disables `thematic-break`, `MD035`, `trailing-whitespace`,
`MD009`, or all rules precedes a break with trailing whitespace. The break and the whitespace are unchanged, and nothing
is reported. A break after a comment that enables the rule again is formatted.

**CRLF line endings, mixed line endings, and a byte order mark do not change the edits**: A document parsed from bytes
with CRLF line endings or mixed line endings, and one with a byte order mark, give the same edits as the LF text (separate
tests for the line endings and for the mark), and the formatted bytes keep the endings and the mark.

###### Error handling

**A missing block, context, or edit set is rejected**: `CanFormat` with a null block, and `Format` with each null
argument, throw `ArgumentNullException`.

**A block that is not a break is rejected**: `Format` for a paragraph block throws `ArgumentException`. A setext
underline, a list item, and fenced content that looks like a break are not accepted by `CanFormat`.

**The rule list is immutable**: The `RuleIds` property lists `thematic-break` and `trailing-whitespace`, and a cast to a
mutable type cannot change it.

**One shared instance gives one result on many threads**: One formatter and one parsed document are used by eight threads
that start together; each formats it many times into its own edit set. Every result equals the result of a single call.
