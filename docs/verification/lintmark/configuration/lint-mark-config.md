### LintMarkConfig

#### Verification Approach

`LintMarkConfig` is verified with unit tests in `LintMarkConfigTests.cs` for the marker check of its supporting
type `LinkRefactorOptions`. The configuration itself is an immutable value holder with no dependencies, so no
mocks, test doubles, or file access are needed. Its defaults and the way every setting is stored are exercised
through the `ConfigLoader` unit tests (the empty file and the full sample file) and the `Configuration` subsystem
tests, which compare loaded configurations with the built-in default. The table of rule states that the
configuration holds comes from the real rule catalog.

#### Test Environment

N/A - standard test environment. The tests use no files and no process-wide state.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- A link-definitions marker that is exactly one HTML comment on one line is accepted, including the shortest one.
- Any other marker is rejected.
- The built-in default holds the documented value for every setting (verified through the loader scenarios named
  above).

#### Test Scenarios

##### Normal operation

**Single-line HTML comments are valid markers**: `<!---->`, `<!-- x -->`, `<!--- x -->`, comments with single or
double dashes inside, `<!--x-->`, and the default marker are accepted.

**Defaults match the documented defaults**: The default configuration read back by the loader tests has line length
120, fill wrapping, final newline on, ATX headings, dash bullets, and the other documented values.

##### Boundary conditions

**The shortest marker is accepted**: `<!---->` is valid.

**Markers that look like a comment but are not are rejected**: `<!-->`, `<!--->`, `<!--> -->`, and `<!---> -->`
are invalid.

##### Error handling

**Text that is not a comment is rejected**: An empty marker, plain text, an unclosed or unopened comment, and a
comment followed by text or by a second comment are invalid.

**A marker with a line break is rejected**: Line feed, carriage return, next-line, line separator, and paragraph
separator characters inside the marker make it invalid.
