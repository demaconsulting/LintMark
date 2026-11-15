#### ListFormatter

##### Verification Approach

`ListFormatter` (with `ListModel` and its plan) is verified with unit tests in `ListFormatterTests.cs`. The real parser,
`LintMarkConfig`, `EditSet`, and `RuleCatalog` are used; nothing is replaced by a double. `BlockFormatterAssertions`
formats each text with the production formatters, and also with the list formatter alone where another rule would
legitimately change a different part of the text. It asserts the exact text, that the result renders the same and keeps
its structure, and that a second run is stable. Most scenarios are theories, and the settings (`bullet`,
`ordered-style`, and `list-indent`) are varied through the configuration. Suppression uses real comments, by identifier
and by alias.

##### Test Environment

The tests need the standard test runner and in-memory text. The shared helper `BlockFormatterAssertions` calls the
HTML-equivalence and structure checks of the development tool `tools/LintMark.Verify` as a test oracle, so the test
project references that tool. The tool is not shipped and is not a software item; it is built with the tests and needs
no network access and no Pandoc installation.

##### Acceptance Criteria

- All unit tests pass with zero failures.
- Only the outermost list of a nest is accepted, and null arguments are rejected.
- Every bullet is written with the configured marker at any depth, and lists that stand next to each other are never
  merged. A marker that would make a thematic break, or a plus sign that would look like a grid border, is never
  written.
- An ordered list keeps its first number and delimiter and counts up or repeats it as configured. A first number with
  leading zeros, or a count that needs more than nine digits, is left as written.
- A number that gains or loses a digit moves its item content with it, except in a quote, where the numbers stay.
- Items start in one column, top-level bullets start at the margin, nested bullets are indented as configured, and
  extra spaces after a marker become one.
- Content of an item (code, HTML, quotes, tables, Pandoc syntax) moves with the item and keeps its structure.
- A list whose columns cannot change safely keeps them, and the markers are still written where that is possible.
- Loose lists stay loose, tight lists stay tight, and a canonical list gets no edit.
- Every edit is credited to the rule it removes, and a suppressed rule leaves the list as written.

##### Test Scenarios

**Bullet markers become the configured character**: Lists with asterisks and plus signs, nested lists, a list in a
quote, and a bullet list in an ordered item are written with the default or configured marker.

**Adjacent bullet lists keep their markers**: Two lists with different markers and no separator keep them, also nested
in one item. Lists separated by another block are both converted.

**Marker change that would change the structure is skipped**: Items whose text is a thematic break, empty items, and
plus markers before grid-border-like text keep their markers. Border-like text without a plus sign does not stop the
conversion.

**Ordered numbers count up, repeat, or stay**: Numbers count up from the first one by default, repeat it with the one
style, and stay when the first number has leading zeros or counting would need ten digits. Nine digits are counted up.

**A number that changes width moves its item**: Ten items with a continuation line gain or lose a digit, and the content
moves. In a quote the numbers stay.

**Marker spacing and alignment are normalized**: Several spaces after a marker become one, items are aligned, top-level
bullets start at the margin, and nested bullets use the configured indent. Other nesting keeps its offset.

**Item content moves with the item**: Code, HTML, quotes, tables, Pandoc syntax, task boxes, and nested lists keep their
structure.

**Columns stay where a change would alter the meaning**: Lazy lines, tabs, five spaces, grid tables, lists in quotes,
and lines that would join the item keep their columns. The markers are still written where possible. A list in a
footnote is left as written.

**A nested marker offset by one column keeps its lines where the parser reads them**: Four minimized documents (a
setext underline, a quote followed by a list, and two lists that start on the marker line, each in an item that moves
from the margin) are the regression tests of issue 22. They are formatted so that every line keeps its place relative
to the content column the parser reads (rule G-5), or the nest keeps its columns, and `ParserContentColumn` follows the
rule of the parser for the markers at and past the content column of the item around them.

**Loose and tight lists stay as they are**: Blank lines stay where they were.

**Edits are credited to their rules**: Marker, number, indent, and spacing edits are reported under `bullet`,
`ordered-style`, `list-indent`, and `list-marker-space`.

**Suppression leaves the list alone**: Each rule, by identifier or alias, or all rules, keeps the list, and a list after
the region is formatted.

**Unusual layouts are safe and stable**: A list without a final line ending, after blank lines, and directly after text
is formatted safely, and every list shape of the synthetic documents is safe and stable with every setting.

**Arguments and blocks are checked**: A null argument and a block that is not an outermost list are rejected, and the
rule list is immutable.
