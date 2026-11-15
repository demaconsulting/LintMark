#### TableFormatter

##### Verification Approach

`TableFormatter` (with the `PipeTable` model) is verified with unit tests in `TableFormatterTests.cs`. The real parser,
`LintMarkConfig`, and `EditSet` are used; nothing is replaced by a double. `PipeTable` is tested directly (parsing cells,
pipes, and alignment, composing the compact and aligned text, and stability of the composed text), and `TableFormatter`
is tested through `BlockFormatterAssertions`, which asserts the exact text, that the result renders the same, and that a
second run is stable. Most scenarios are theories with several tables, including wide characters.

##### Test Environment

N/A - the tests need only the standard test runner and in-memory text.

##### Acceptance Criteria

- All unit tests pass with zero failures.
- Only pipe tables are accepted: grid tables and text are not.
- The compact layout writes one blank inside each pipe with a delimiter row of three dashes and keeps the alignment
  colons. The aligned layout pads every column to its widest cell, measured in display columns.
- Leading and trailing pipes are written, and the cell text is kept byte for byte.
- An aligned table that does not fit the line length falls back to compact.
- A table in a container keeps its prefix.
- Rows of another length, tables the parser is not sure about, and pipes inside inline constructs are never repaired or
  changed.
- Edits are credited to the rule of the change, and a suppressed rule leaves the table as written.

##### Test Scenarios

**Compact layout is written from the cells**: Tables with loose, missing, or extra outer pipes and spaces, with
alignment colons, empty cells, and wide characters are written in the compact layout.

**Compact table gets no edit**: A table that is already compact proposes nothing.

**Aligned layout pads the columns**: With the aligned setting every column is padded to its widest cell, and aligned and
compact inputs give the same result.

**Wide table falls back to compact**: An aligned table that would exceed the line length is written compact.

**Cell text is kept**: Cell text, including inline constructs with a pipe, stays byte for byte.

**Table in a container keeps its prefix**: Tables in a list item or a quote are written with their prefix, compact and
aligned.

**Rows of other length are not repaired**: Rows with fewer or more cells keep their cells.

**Uncertain table is left as written**: A table the parser cannot read with certainty is not edited.

**Awkward tables are safe and stable**: Unusual shapes are formatted safely, and the composed table is stable.

**Edits carry the rule of the change**: Pipe edits are credited to `table-pipe-style`, layout edits to
`table-column-style`, and whitespace to `trailing-whitespace`.

**Suppressed rule leaves the table**: A suppressed rule keeps the table as written.
