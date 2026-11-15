### SuppressionMap

#### Verification Approach

`SuppressionMap` is verified with unit tests in `SuppressionMapTests.cs`. The map is built from parsed comment
directives and answers whether a rule is suppressed at a position or in a range. It needs no collaborators other
than an alias resolver passed in by the test, so no mocks are used. Tests build a document with the real parser or
call the build entry point with directive text, then assert on the answers for positions and ranges.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Every documented comment form (disable and enable regions, next-line, line, file, capture and restore, and the
  formatter-ignore forms) suppresses exactly the text it names.
- A directive with a rule list affects only those rules, tracked independently; one without a list affects all.
- Unterminated regions run to the end of the file.
- Unknown directives are ignored, and comments inside code and front matter are not directives.
- A block that intersects a suppressed region counts as suppressed.

#### Test Scenarios

##### Normal operation

**Disable and enable cover the text between**: The region between the two comments is suppressed.

**Rule lists limit the effect**: A disable with rules suppresses only those; an enable with rules re-enables only those;
overlapping regions of different rules are tracked independently.

**Line forms cover one line**: A line directive covers its own line and a next-line directive the following line only.

**File directive covers the whole file**: Everything is suppressed.

**Capture and restore return to the captured state**: A restore without a capture is ignored.

**Ignore-next-block and start/end forms cover a block or a region**: Including the inline comment form.

**Aliases resolve to canonical rules**: The alias resolver maps numbers to rule ids.

##### Boundary conditions

**Unterminated disable runs to the end of the file**: Also an unterminated start form.

**Next-line directive on the last line produces no range**: Nothing is suppressed.

**A block that intersects a region is suppressed**: Partial overlap counts.

##### Error handling

**Unknown directives are ignored**: No range is produced.

**Comments in code and front matter are not directives**: They suppress nothing.
