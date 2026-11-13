### LintMarkConfig

#### Verification Approach

`LintMarkConfig` is verified with unit tests in `LintMarkConfigTests.cs`. The two supporting types that belong to the
same unit, `LinkRefactorOptions` and the setting enumerations, are verified through the same test class. The table
of rule identifiers, legacy numbers, and defaults that the configuration holds states for belongs to the rule
catalog of the `Linting` subsystem and is verified with that unit (see the verification design of the rule catalog);
the tests here use the real catalog through the default configuration and check only that the configuration holds
exactly the states that the catalog defines. The class is a value holder with no dependencies, so no mocking, test
doubles, or file-system access is needed; the tests create configurations from the built-in default with `with`
expressions and assert on the stored values, on the exceptions thrown for refused values, and on equality. The
tests that check immutability use the shared `ReadOnlyAssertions` helper, which casts a collection to its mutable
interfaces and proves that every attempted change throws `NotSupportedException`.

#### Test Environment

N/A - standard test environment. The tests use no files and no process-wide state.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- The built-in default has the documented value for every setting: the 22 configurable rules enabled except the
  three documented as off, the six default ignore patterns in their documented order, link conversion off with its
  documented option values, and no line ending chosen.
- The smallest and largest value of every numeric range (line length, maximum blank lines, link conversion
  threshold and label length), both list indents, an empty ignore list, and the shortest valid marker are accepted,
  and every setting reads back the value it was given.
- A value outside its range, an undefined enumeration value, a malformed ignore pattern, an incomplete or
  mistaken rule table, and a marker that is not exactly one HTML comment on one line are refused with the documented
  exception type.
- The rule table holds a state for exactly the configurable rules of the rule catalog, starting from the catalog's
  defaults, and is stored with an ordinal comparer.
- Rule states and ignore patterns cannot be changed through casts, and later changes to the array or the rule
  table they were built from do not reach the configuration.
- A copy made with `with` differs from the original only in the chosen setting, and equal content gives equal
  configurations and equal hash codes.

#### Test Scenarios

##### Normal operation

**Default values match the documented defaults**: Every setting of the default configuration is compared with its
documented value (line length 120, fill wrapping, final newline on, ATX headings, dash bullets, list indent 2,
incrementing ordered lists, asterisk emphasis and strong, backtick fences, fenced code blocks, spaces for hard
breaks, dashes for thematic breaks, compact tables, autolinked bare URLs, one blank line, and stripped trailing
whitespace).

**Default rules enable every rule except the documented three**: The default rule table holds a state for each of
the 22 rules that the rule catalog lists as configurable, and only the
descriptive-link-text, dollar-sign-in-shell-code, and table-grid-unformatted rules are off; the line-length and
paragraph-wrap rules are on.

**Default ignore patterns are the documented ones in order**: The six patterns for `.git`, `node_modules`, `.venv`,
`generated`, `thirdparty`, and `.agent-logs` appear in the documented order.

**Default line ending is not specified**: The default configuration has no line ending.

**Link conversion is off by default with the documented option values**: It is disabled, with threshold 60,
conversion in tables on, link-text breaking off, label length 40, and the marker `<!-- Link References -->`.

**A modified copy leaves the original unchanged**: A copy with a line ending differs from the default, and the
default still has none.

**Equal content gives equal configurations**: Two configurations built separately with equal ignore
patterns and an equal rule change are equal and have equal hash codes; changing a pattern, or changing from the
default, makes them unequal.

**Every setting reads back the value it was given**: A configuration with a non-default value for every setting
(line length, wrap mode, line ending, final newline, headings, bullet, list indent, ordered style, emphasis,
strong, code fence, code block style, hard break, thematic break, tables, bare URLs, maximum blank lines, trailing
whitespace, link conversion options, rule states, and ignore patterns) returns each value.

##### Boundary conditions

**Smallest and largest line length are accepted**: Zero (unlimited) and 100,000 are stored.

**Smallest and largest maximum blank line count are accepted**: One and 100,000 are stored.

**Both list indents are accepted**: Two and four are stored.

**Smallest and largest link conversion threshold and label length are accepted**: One and 100,000 are stored for
both numbers, through a modified copy and through the constructor.

**An empty ignore list is accepted**: The patterns can be cleared.

**Markers that are one HTML comment on one line are accepted**: The shortest marker `<!---->`, and markers such as
`<!-- x -->`, `<!--- x -->`, `<!-- a - b -->`, `<!-- a -- b -->`, `<!-- a --->`, `<!--x-->`, and the default marker
are stored, and the shared marker check agrees.

**The default ignore list is an initialized immutable array**: The shared default patterns are an immutable array
that is ready for use.

**A changed source array does not reach the configuration**: After patterns are assigned from a mutable array, a
change to the array leaves the stored patterns as they were.

**A changed source rule table does not reach the configuration**: After a rule table is assigned from a builder's
snapshot, changes to the builder (a rule switched, a rule removed) leave the stored rule states and their count as
they were.

**A rule table with a case-insensitive comparer is stored with an ordinal one**: Looking up
`LINE-LENGTH` fails after the table is assigned, while `line-length` succeeds.

##### Error handling

**A line length outside the range is refused**: `-1` and `100001` throw `ArgumentOutOfRangeException`.

**A list indent other than two or four is refused**: `0`, `1`, `3`, and `8` throw `ArgumentOutOfRangeException`.

**A maximum blank line count outside the range is refused**: `0`, `-3`, and `100001` throw
`ArgumentOutOfRangeException`.

**Every enumeration setting refuses an undefined value**: The value `99` cast to each of the fourteen enumeration
typed settings (wrap, line ending, headings, bullet, ordered style, emphasis, strong, code fence, code block
style, hard break, thematic break, tables, bare URLs, and trailing whitespace) throws
`ArgumentOutOfRangeException`.

**An uninitialized ignore array is refused**: The default value of the immutable array throws
`ArgumentException`.

**A malformed ignore pattern is refused**: A null, empty, blank, multi-line (LF, CRLF, a lone CR, or a next-line, line
separator, or paragraph separator character), or backslash pattern among valid ones throws `ArgumentException`.

**A rule table that is wrong is refused**: A table with an empty key, an unknown key, a legacy number instead of the
rule name, or a missing rule throws `ArgumentException`, and a null table throws `ArgumentNullException`.

**Null link conversion options are refused**: Null throws `ArgumentNullException`.

**A threshold or label length outside the range is refused**: `0`, `-1`, and `100001` throw
`ArgumentOutOfRangeException`, for a modified copy and for the constructor.

**A marker that is not exactly one HTML comment on one line is refused**: An empty marker, plain text, an unclosed or
unopened comment, `<!-->`, `<!--->`, `<!--> -->`, `<!---> -->`, a comment followed by text or by a second comment, and
markers with a line break (LF, CRLF, a lone CR, or a next-line, line separator, or paragraph separator character)
throw `ArgumentException`, the shared marker check returns false for each, and a null marker throws
`ArgumentNullException`, also from the shared marker check.

**The rule table cannot be changed through a cast**: Every attempted change through the mutable dictionary
interface throws `NotSupportedException`.

**The ignore patterns cannot be changed through a cast**: Every attempted change through the mutable list
interfaces throws `NotSupportedException`.
