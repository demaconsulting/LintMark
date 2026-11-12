### LintMarkConfig

#### Verification Approach

`LintMarkConfig` is verified with unit tests in `LintMarkConfigTests.cs`, and its rule-identifier table
(`RuleIdentifiers`) with unit tests in `RuleIdentifiersTests.cs`. The two supporting types that belong to the same
unit, `LinkRefactorOptions` and the setting enumerations, are verified through the same test classes. The class is a
value holder with no dependencies, so no mocking, test doubles, or file-system access is needed; the tests create
configurations from the built-in default with `with` expressions and assert on the stored values, on the
exceptions thrown for refused values, and on equality. The tests that check immutability use the shared
`ReadOnlyAssertions` helper, which casts a collection to its mutable interfaces and proves that every attempted
change throws `NotSupportedException`. One scenario switches the current culture to Turkish for the duration of
the test to prove that rule names are matched without regard to culture.

#### Test Environment

N/A - standard test environment. The tests use no files; the Turkish-culture scenario restores the original
culture when it ends.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- The built-in default has the documented value for every setting: the 22 configurable rules enabled except the
  three documented as off, the six default ignore patterns in their documented order, link conversion off with its
  documented option values, and no line ending chosen.
- The smallest and largest value of every numeric range, an empty ignore list, and the shortest valid marker are
  accepted, and every setting reads back the value it was given.
- A value outside its range, an undefined enumeration value, a malformed ignore pattern, an incomplete or
  mistaken rule table, and a marker that is not a single-line HTML comment are refused with the documented
  exception type.
- A rule is identified by its name or legacy number in any letter case, in every culture; the legacy numbers of
  removed rules are recognized but are not rules.
- Rule states and ignore patterns cannot be changed through casts, and later changes to the array or the rule
  table they were built from do not reach the configuration.
- Supporting: a copy made with `with` differs from the original only in the chosen setting, and equal content gives
  equal configurations and equal hash codes. This is needed once the line ending from the editor settings is
  applied, so it has no requirement until the formatting phase.

#### Test Scenarios

##### Normal operation

**Default values match the documented defaults**: Every setting of the default configuration is compared with its
documented value (line length 120, fill wrapping, final newline on, ATX headings, dash bullets, list indent 2,
incrementing ordered lists, asterisk emphasis and strong, backtick fences, fenced code blocks, spaces for hard
breaks, dashes for thematic breaks, compact tables, autolinked bare URLs, one blank line, and stripped trailing
whitespace).

**Default rules enable every rule except the documented three**: The default rule table holds 22 rules, and only
the descriptive-link-text, dollar-sign-in-shell-code, and table-grid-unformatted rules are off; the line-length
and paragraph-wrap rules are on.

**Default ignore patterns are the documented ones in order**: The six patterns for `.git`, `node_modules`, `.venv`,
`generated`, `thirdparty`, and `.agent-logs` appear in the documented order.

**Default line ending is not specified**: The default configuration has no line ending.

**Link conversion is off by default with the documented option values**: It is disabled, with threshold 60,
conversion in tables on, link-text breaking off, label length 40, and the marker `<!-- Link References -->`.

**The identifier table disables exactly the documented three rules by default**: The default table of the identifier
class turns off only those three rules.

**A modified copy leaves the original unchanged (supporting)**: A copy with a line ending differs from the default,
and the default still has none.

**Equal content gives equal configurations (supporting)**: Two configurations built separately with equal ignore
patterns and an equal rule change are equal and have equal hash codes; changing a pattern, or changing from the
default, makes them unequal.

**Every setting reads back the value it was given**: A configuration with a non-default value for every setting
(line length, wrap mode, line ending, final newline, headings, bullet, list indent, ordered style, emphasis,
strong, code fence, code block style, hard break, thematic break, tables, bare URLs, maximum blank lines, trailing
whitespace, link conversion options, rule states, and ignore patterns) returns each value.

**A rule name or legacy number resolves to its canonical rule name**: The name `heading-increment` resolves to
itself, and each of the 16 documented legacy numbers (for example `MD013`) resolves to its documented rule name.

**Rule names and legacy numbers are matched in any letter case**: `LINE-LENGTH`, `Line-Length`, `md013`, and `Md013`
all resolve to the rule `line-length`.

**Matching does not depend on the culture**: With the Turkish culture current, the upper-case names `LINE-LENGTH`
and `LINK-EMPTY` still resolve to their rules.

**The configurable rules are sorted, unique, and covered by the defaults**: The 22 rule names are in ordinal order,
without duplicates, and each has a default; the default table has the same size.

**Legacy numbers are unique and map to configurable rules**: Every legacy number resolves to a configurable rule, no
rule has two numbers, no number is also a dropped number, and there are nine dropped numbers.

##### Boundary conditions

**Smallest and largest line length are accepted**: Zero (unlimited) and 100,000 are stored.

**An empty ignore list is accepted**: The patterns can be cleared.

**The shortest valid definitions marker is accepted**: The empty comment `<!---->` is stored.

**The default ignore list is an initialized immutable array**: The shared default patterns are an immutable array
that is ready for use.

**A changed source array does not reach the configuration**: After patterns are assigned from a mutable array, a
change to the array leaves the stored patterns as they were.

**A changed source rule table does not reach the configuration**: After a rule table is assigned from a builder's
snapshot, changes to the builder (a rule switched, a rule removed) leave the stored rule states and their count as
they were.

**Legacy numbers of removed rules are recognized but are not rules**: `MD002`, `MD006`, `MD008`, `MD025`, `MD033`,
`MD041`, `MD043`, `MD044`, and `MD057` (and a lower-case spelling) do not resolve to a rule but are reported as
dropped.

**An unknown name is neither a rule nor dropped**: An empty name, `no-such-rule`, `MD009` (a rule that the formatter
always applies), and `MD999` do not resolve and are not dropped.

**A rule table with a case-insensitive comparer is stored with an ordinal one (supporting)**: Looking up
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

**An uninitialized ignore array is refused**: The default value of the immutable array throws `ArgumentException`.

**A malformed ignore pattern is refused**: A null, empty, blank, multi-line (LF or CRLF), or backslash pattern among
valid ones throws `ArgumentException`.

**A rule table that is wrong is refused**: A table with an empty key, an unknown key, a legacy number instead of the
rule name, or a missing rule throws `ArgumentException`, and a null table throws `ArgumentNullException`.

**Null link conversion options are refused**: Null throws `ArgumentNullException`.

**A threshold or label length outside the range is refused**: `0`, `-1`, and `100001` throw
`ArgumentOutOfRangeException`, for a modified copy and for the constructor.

**A marker that is not a single-line HTML comment is refused**: An empty marker, plain text, an unclosed or
unopened comment, `<!-->`, and markers with a line break throw `ArgumentException`, and a null marker throws
`ArgumentNullException`.

**A null name is refused by the rule lookups (supporting)**: Resolving null, and asking whether null is dropped,
throw `ArgumentNullException`.

**The rule table cannot be changed through a cast**: Every attempted change through the mutable dictionary
interface throws `NotSupportedException`.

**The ignore patterns cannot be changed through a cast**: Every attempted change through the mutable list
interfaces throws `NotSupportedException`.
