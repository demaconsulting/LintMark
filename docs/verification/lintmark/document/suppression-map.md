### SuppressionMap

#### Verification Approach

`SuppressionMap` is verified with unit tests in `SuppressionMapTests.cs`. Test inputs are built by parsing
small Markdown texts with `DocumentParser.Parse` (a test-time dependency: the map is built from a real
syntax tree and block list, so that comment recognition is exercised against the real parser), and the
tests then ask the map `IsSuppressed` and `IsSuppressedRange` questions or inspect `Ranges`. The alias
resolver is exercised with a test function. No mocking is used.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Each directive family and form suppresses exactly the documented text.
- Unterminated regions run to the end of the file.
- Comment-like text inside code and front matter never suppresses anything.
- Rule identifiers are matched ignoring case after alias resolution.
- The ranges are sorted by start offset, and invalid arguments are rejected.

#### Test Scenarios

##### Normal operation

**Disable and enable comments suppress the text between them**: Text between a disable and an enable comment is
suppressed and text outside is not.

**Disable with rule names suppresses only those rules**: Other rules stay active, and rule names match ignoring
letter case.

**Enable with rule names after a bare disable re-enables only those rules**: The rest of the rules stay
suppressed.

**Disable regions of different rules are tracked independently**: Nested disable and enable comments of
different rules do not affect one another.

**Disable-line covers its own line only**: The line that holds the comment is suppressed and no other line.

**Disable-next-line covers the following line only**: The line after the comment is suppressed and no other line.

**Disable-file covers the whole file**: The text before the comment is suppressed as well.

**Restore returns to the captured state**: A restore brings back the state saved by the matching capture.

**Prettier-ignore covers the next block**: A comment before a table suppresses the whole table, not only the
next line.

**Prettier-ignore before a list covers the whole list**: Every item of the list is suppressed.

**Prettier-ignore start and end cover the text between them**: All rules are suppressed in the region.

**Inline comment is a directive**: An inline comment inside a paragraph suppresses as a lintmark directive.

**Rule identifiers pass through the alias function**: An alias resolver is applied to identifiers in directives
and in queries, so an alias and its target match.

**Range that intersects a suppressed region is suppressed**: `IsSuppressedRange` reports a block that overlaps a
suppressed region.

**Ranges are sorted by start offset**: `Ranges` is ordered by start.

##### Boundary conditions

**Document without directives suppresses nothing**: There are no ranges.

**Disable without enable runs to the end of the file**: An unterminated region covers all following text.

**Prettier-ignore-start without an end runs to the end of the file**: An unterminated start comment covers all
following text.

**Disable-next-line on the last line produces no range**: There is no following line to suppress.

**Restore without capture is ignored**: It has no effect on the suppressed state.

##### Error handling

**Unknown directives are ignored**: Unrelated comments, unknown directive names, and malformed comments suppress
nothing and cause no failure.

**Comments in code and front matter are not directives**: Comment-like text inside code spans, code blocks, and
front matter suppresses nothing.

**Prettier comments inline are ignored**: A prettier directive inside a paragraph has no effect, because only
block-level comments carry them.

**Null arguments are rejected**: `Build` throws `ArgumentNullException` for a null argument.
