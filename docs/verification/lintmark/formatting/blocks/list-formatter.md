#### ListFormatter

##### Verification Approach

`ListFormatter` is verified with unit tests in `ListFormatterTests.cs`. The real `DocumentParser`, `SourceDocument`,
`LintMarkConfig`, `EditSet`, `SourcePatcher`, and `RuleCatalog` are used; nothing is replaced by a double. A helper of
the test project (`BlockFormatterAssertions`) formats a text in three ways and compares each with the expected text:
with the production formatters (heading, code block, thematic break, list, and block quote formatters, the inline
normalizer, and the document passes), with the production formatters on the bytes that would be on disk (LF, CRLF, mixed
line endings, and a byte order mark), and with the list formatter alone. The helper also asserts that the result renders
to the same HTML as the input (`HtmlEquivalenceChecker`), keeps its Pandoc structure (`StructuralTokenChecker`), and is
stable when formatted again (`IdempotencyChecker`); these Safety checkers are used as test-time oracles, and they are
verified in the `Safety` subsystem. The scenarios about lists that must keep their columns or markers assert the exact
text, which is the input; where another rule of the tool legitimately changes a different part of the text, the scenario
asserts that the list formatter alone leaves the input unchanged and gives the exact text that the production formatters
produce. Most scenarios are theories with several inputs, and the settings
(`bullet`, `ordered-style`, and `list-indent`) are varied through the configuration. Suppression is driven by real
suppression comments, named by identifier and by alias. The tests of null arguments and of the wrong block kind use
blocks of real parsed documents; the wrong block kind includes a list that is nested in another list. The test for
concurrent use parses one document once and shares it, with one formatter instance, among several threads; each formats
it many times, and every result is compared with the result of a single call. The last scenario runs every list shape of
the synthetic nesting documents of the test project through the production formatter with two sets of list settings.

##### Test Environment

N/A - standard test environment.

##### Acceptance Criteria

- All unit tests pass with zero failures.
- Only the outermost list of a nest is accepted; null arguments and a block that is not an outermost list are rejected;
  the list of rules (`bullet`, `ordered-style`, `list-indent`, and `list-marker-space`) is immutable and holds only
  registered rules.
- Every item of a bullet list is written with the configured marker character, at any depth and inside quotes.
- Two bullet lists that stand next to each other, also inside one item, are never merged, and a marker that would make a
  line a thematic break, would make an empty item a different line, or (for the plus sign) would look like a grid table
  border never changes the meaning of the text; text that looks like a grid table border does not keep another marker
  character from being written.
- An ordered list keeps the number it starts with and keeps its delimiter, counts up from it or repeats it as configured,
  and is left as written when the first number has leading zeros or counting would need more than nine digits.
- A number that gains or loses a digit moves the content of its item with it, and where the item cannot move (in a quote)
  the numbers stay.
- More than one space after a marker becomes one space, and the content of the item moves with the marker.
- The items of a list start in the same column, a top-level bullet list starts at the margin, and a nested bullet list
  in a bullet item is indented by the configured number of columns; other nesting keeps its offset.
- Code, HTML, quotes, tables, and Pandoc syntax inside an item move with the item and keep their structure.
- A list keeps its meaning where a line is a lazy continuation, a tab follows a marker or indents a line, five spaces make
  the content of an item indented code, a grid table is inside an item, or a following line would join the item: the text
  is unchanged (apart from what another rule of the tool changes). A list in a quote keeps its columns, and a list in a
  footnote is left as written. Where the columns stay but the marker characters can still be written, they are.
- A list that is already canonical gets no edit.
- Loose lists stay loose and tight lists stay tight.
- Every edit is attributed to the rule of the violation it removes.
- A list in which any rule of the formatter is suppressed, by identifier or by alias, is left as written; a list after the
  region is formatted.
- CRLF line endings, mixed line endings, and a byte order mark do not change the edits, and one shared parsed document
  gives the same result on many threads.
- Every list shape of the synthetic nesting documents is safe and stable with every list setting.

##### Test Scenarios

###### Normal operation

**Every bullet marker becomes the default character**: Lists with asterisks and plus signs, a nested list with a different
marker, a list in a block quote, and a bullet list in an ordered item are formatted with the default configuration. Each
marker is a dash, and a list that already uses dashes is unchanged.

**The configured bullet is used**: Lists are formatted with `bullet: asterisk`, `plus`, and `dash`. Every item of every
list, nested lists included, gets the configured character.

**Bullet lists that another block separates are both converted**: Two bullet lists with different markers and an HTML
comment between them are formatted. Both use the default marker, because they are not adjacent.

**Ordered numbers count up from the first number**: Lists numbered `1. 1. 1.`, `1. 3. 7.`, `5. 5.`, `0. 0.`, `1) 1)`,
and a list in a block quote are formatted with the default configuration. The numbers count up from the first number, the
delimiter stays, and a list that already counts is unchanged.

**The one style repeats the first number**: Lists numbered `1. 2. 3.`, `5. 6.`, and `1) 2)` are formatted with
`ordered-style: one`. Every item has the first number.

**A number that gains or loses a digit moves its item**: A list of ten items with a continuation line under the tenth is
formatted with the default configuration and with `ordered-style: one`. The tenth marker gains or loses a digit, and the
continuation line moves by the same number of columns.

**More than one space after a marker becomes one**: Items with several spaces after a dash or a number, with a continuation
line, with a nested list, with a second paragraph, with a fenced block, with indented code, with a block quote, and a
nested list in an ordered item are formatted. One space follows each marker and everything in the item moves with it.

**Items are aligned and top-level bullets start at the margin**: Bullet lists indented by one, two, and three columns, a
list whose second item is indented by one column more than the first, and ordered lists indented by two columns and with
items in different columns are formatted. The items of a bullet list start at the margin, the items of an ordered list
start in the column of the first item, and the continuation lines move with their items.

**Nested bullets are indented as configured**: Bullet lists in bullet items, at two and three levels and with a continuation
line, are formatted with `list-indent: 2` and `4`. Each nested list starts the configured number of columns right of its
parent marker.

**Loose and tight lists stay loose and tight**: A tight list, a loose list, and an ordered loose list with second
paragraphs, all with wide gaps, are formatted. The blank lines stay where they were.

**Code, HTML, quotes, and tables in an item move with it**: An item that holds a fenced block with a blank line, an HTML
block, a block quote, and a pipe table is formatted. Everything moves by the same number of columns and the content of
each block is unchanged.

**Pandoc syntax in an item moves with it**: Items that hold a fenced div, display math, a citation with an attribute line,
and a page break command are formatted. Each construct moves with the item and keeps its structure.

**Task items, empty items, and a list in an item are handled**: A task list, a list with an empty item, and an item that
starts with a nested list are formatted. Markers are rewritten and the empty item and the task boxes stay.

**Each edit is attributed to its rule**: A document with a wrong marker, a wrong number, an over-indented item, and extra
spaces is formatted by the list formatter alone. The edits are credited to `bullet`, `list-indent`, `list-marker-space`,
and `ordered-style`, and to no other rule, and each of those rules is one that the formatter lists.

###### Boundary conditions

**Adjacent bullet lists keep their markers**: Two bullet lists with different markers, separated by a blank line only, are
formatted. Each keeps its marker, because one marker would merge the lists.

**A marker change that would change the structure leaves the marker**: `* ---`, `+ ---`, and `* - - -` (a converted marker
would make a thematic break) are formatted: the list formatter alone leaves each, and the tool writes the thematic break
`- - -` as `---` while the list marker stays an asterisk. `* ___` (the text is a break of its own, which the
thematic break rule writes as `---`) is formatted: the list formatter alone leaves it, and the tool changes only the
break. No marker change turns an item into a thematic break.

**A list with an empty item keeps its markers**: `* a` followed by a marker alone, a marker alone followed by `* a`, and
three plus sign items with an empty one in the middle are formatted. The text is unchanged.

**A plus sign marker keeps its list when the text looks like a grid table border**: `- :--`, `- =`, `- :-:` and `- b`,
and `* ===` and `* b` are formatted with `bullet: plus`, and `+ :--`, and `+ ===` and `+ b` with the default
configuration. Each list keeps its markers, because a plus sign marker, written or replaced, followed by such text could
be read as the border of a grid table.

**Text that looks like a grid table border does not stop a conversion without a plus sign**: `* :--`, `* =`, and `* :-:`
with `* b` are formatted with the default configuration. Each marker becomes a dash, because neither marker is a plus sign.

**A first number with leading zeros leaves the numbers**: `01.` and `01.` are formatted. The numbers are unchanged.

**Numbers of nine digits are counted up**: `999999998.` twice is formatted. The second number becomes `999999999`.

**A number of ten digits leaves the numbers**: `999999999.` twice, and `999999998.` three times, are formatted. The
numbers are unchanged, because counting would need ten digits.

**A number that gains a digit in a block quote leaves the numbers**: Ten items numbered one in a block quote are formatted.
The numbers are unchanged, because an item cannot move inside a quote.

**A list whose columns cannot change safely stays as written**: A list with a lazy continuation line (bullet and ordered),
a tab after the marker (bullet and ordered), five spaces after the marker, a tab-indented child, and a tab-indented line
after a blank line is formatted. The text is unchanged. Each cause is a scenario of its own: lazy line, tab after the
marker, tab indent, and five spaces.

**The markers are still written when the columns cannot change**: An indented bullet list directly followed by an ordered
list in the same column is formatted. The list formatter alone writes the dash marker and keeps the column, because
moving the list to the margin would pull the next line into it; the tool adds the blank line between the lists.

**Other nesting keeps its offset**: A bullet list in an ordered item, an ordered list in a bullet item, an ordered list
in an ordered item, and a bullet list indented by one column and by three columns more than the content of an ordered item
are formatted. Each is unchanged, so the offset to the content of the parent item is as written. A line that is indented
four or more columns more than the content is no nested list for the parser, so no larger offset exists.

**Adjacent bullet lists in one item keep their markers**: Two nested bullet lists with different markers and a blank line
between them, in a bullet item and in an ordered item, are formatted. Each keeps its markers, because one marker would
merge them.

**A list in a quote keeps its columns**: Lists with wide gaps and indentation in a block quote are formatted. The text is
unchanged.

**A list in a footnote is left as written**: A list with an asterisk marker, a list with a wide gap, and an ordered list
of repeated numbers in the body of a referenced footnote are formatted. The text is unchanged.

**A list keeps its columns where a following line would join the item**: A list followed by a different marker in the
same column, a list followed by a blank line and a line indented by one column more, and a list followed by a
whitespace-only line and an indented line are formatted. The list formatter alone leaves the text unchanged. The tool adds
a blank line between the two lists of the first example and removes the blanks of the whitespace-only line of the third,
and changes nothing else.

**A grid table in an item keeps its columns**: An item with a grid table is formatted. The text is unchanged.

**A list in an unusual file layout is safe**: A list without a final line ending, one after blank lines, one with several
blank lines between its items, and one directly after a line of text are formatted. Each result is safe and stable.

**CRLF line endings, mixed line endings, and a byte order mark do not change the edits**: A list that needs a marker, a
number, a column, and a space is read from LF text, from CRLF text, from text with mixed line endings, and from bytes with
a byte order mark. The edits are the same for each.

**A suppressed rule leaves the list alone**: A list with a wide gap, an asterisk marker, and a repeated number follows a
comment that disables `bullet`, `ordered-style`, `list-indent`, `list-marker-space`, `MD004`, `MD005`, `MD007`, `MD029`,
`MD030`, or all rules. The list is unchanged, and no edit and no problem result.

**A canonical list gets no edit**: Lists with dashes, with counting numbers, with nested bullets at two columns, with a
nested bullet list in an ordered item and a second paragraph, and in a block quote are formatted by the list formatter
alone and by the whole tool. The list formatter proposes no edit and the tool reports no problem.

**A list after a region that enabled the rule again is formatted**: A list in a region that disables `bullet` and a list
after the comment that enables it again are formatted. Only the list after the region changes.

###### Robustness

**Every list shape of the synthetic documents is safe and stable**: The synthetic nesting documents with a list in their
container chain are formatted with `bullet: asterisk`, `list-indent: 2`, and `ordered-style: incrementing`, and with
`bullet: plus`, `list-indent: 4`, and `ordered-style: one`. Each result is safe and stable.

**One shared instance gives one result on many threads**: One formatter and one parsed document are used by several threads
that start together; each formats it many times. Every result equals the result of a single call and is the expected text.

###### Error handling

**A missing block, context, or edit set is rejected**: `CanFormat` with a null block, and `Format` with each null
argument, throw `ArgumentNullException` that names the parameter.

**A block that is not an outermost list is rejected**: `Format` for a paragraph block and for a list that is nested in
another list throws `ArgumentException` that names `block`.

**The rule list is immutable**: The `RuleIds` property lists `bullet`, `ordered-style`, `list-indent`, and
`list-marker-space`, each of which is registered, and a cast to a mutable type cannot change it.
