### ReferenceIndex

#### Verification Approach

`ReferenceIndex` is verified with unit tests in `ReferenceIndexTests.cs`. Test inputs are built by parsing
small Markdown texts with `DocumentParser.Parse` (a test-time dependency: the index is built from the real
block list, so the handling of the parser's definition groups and of footnote definitions is exercised for
real), and the tests then inspect `Definitions` and call `TryGet` and `NormalizeLabel`. No mocking is used.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Every definition has the correct label, destination, title, range, and line, in source order, wherever it
  stands, including inside block quotes and list items.
- Lookup ignores case and whitespace differences; the first of duplicate definitions wins and all stay listed.
- Footnote definitions never appear as link reference definitions, wherever they stand.
- The definition list cannot be modified by a caller that casts it.

#### Test Scenarios

##### Normal operation

**Definition list cannot be changed through a cast**: `Definitions` is not a `List<T>` or an array, every
attempted change throws `NotSupportedException`, and lookups still find the definitions.

**Definitions carry label, destination, title, range, and line**: A definition with a title and one without
report their label, URL, title (or none), exact range, and 1-based line.

**Lookup ignores case and whitespace differences**: Queries that differ from the label in case, in extra spaces,
and in a tab all find the definition.

**First of duplicate labels wins and both are kept**: A label defined twice resolves to the first definition,
and both stay in `Definitions`.

**Definition inside a block quote is indexed**: The definition's exact range, label, and line are reported.

**Definition inside a list item is indexed**: The definition's exact range, label, and line are reported.

**Definitions in all nesting positions are indexed in source order**: Top-level, quoted, listed, and nested
definitions are all listed in the order they appear.

**Label normalization trims, collapses whitespace, and folds case**: Several labels, including one with a tab and
line feed and an empty one, normalize to the expected text.

##### Boundary conditions

**Document without definitions gives an empty index**: `Definitions` is empty.

**Headings add no definitions**: Headings produce no synthetic definitions.

**Footnote definitions are excluded**: Referenced and unreferenced footnotes are not in the index.

**Footnote definition inside a block quote is excluded**: The parser reads it as a link reference definition
with a label that begins with `^`, and the index does not list it.

**Footnote definition inside a list item is excluded**: The same for a list item.

##### Error handling

**Null arguments are rejected**: `Build` throws `ArgumentNullException` for a null argument.
