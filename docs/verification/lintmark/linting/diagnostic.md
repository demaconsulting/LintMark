### Diagnostic

#### Verification Approach

`Diagnostic` is verified with unit tests in `DiagnosticTests.cs`. The record is a plain value type with no
dependencies, so no test doubles are used. The tests share a private `Create` helper that builds a valid diagnostic
and lets a test replace only the argument under test, so each test shows exactly which value is invalid. Tests call
the constructor directly and assert on the exposed members, on the exception type and its parameter name, and on a
fragment of the message where the requirement depends on it. Value equality is asserted through `Assert.Equal`, the
`Equals` method, `GetHashCode`, and a `HashSet`.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- A created diagnostic exposes the rule identifier, kind, path, line, column, and message that it was given.
- A null rule identifier, path, or message throws `ArgumentNullException` that names the parameter.
- A rule identifier that is empty, has a capital letter, starts with a digit or hyphen, ends with a hyphen, has a
  double hyphen, or has an underscore, space, or non-ASCII letter throws `ArgumentException`.
- A blank path or message, and a message that holds any kind of line break, throws `ArgumentException`.
- A line or column below one, and a kind that is not defined, throws `ArgumentOutOfRangeException` that names the
  parameter.
- `Fixable` is true exactly for the formatting kind.
- Diagnostics with equal values are equal and have equal hash codes, and a difference in any single value makes
  them unequal.

#### Test Scenarios

##### Normal operation

**A created diagnostic exposes the values it was given**: A diagnostic is created with a rule identifier, the
lint-only kind, a path, line 4, column 9, and a message. Every member returns the value that was passed in.

**Kebab-case rule identifiers are accepted**: Diagnostics are created with the identifiers `a`, `line-length`, `a1`,
and `rule-2a-b3`. Each is accepted and kept, which shows that digits and single hyphens are allowed after the first
letter.

**A formatting diagnostic is fixable**: A diagnostic of the formatting kind is created. Its `Fixable` value is true.

**A lint-only diagnostic is not fixable**: A diagnostic of the lint-only kind is created. Its `Fixable` value is
false.

**Diagnostics with the same values are equal**: Two separately created diagnostics with identical values are compared
with `Assert.Equal`, with `Equals`, and by hash code. They are equal and the hash codes match.

**Equal diagnostics collapse in a hash set**: Two equal diagnostics and one that differs in the column are added to a
`HashSet`. The set holds two entries.

##### Boundary conditions

**The first line and column are accepted**: A diagnostic is created at line 1 and column 1, the smallest valid
position. The position is kept.

**Diagnostics that differ in the column are different**: Two diagnostics that differ only in the column (7 and 8) are
not equal.

**A difference in any single value makes diagnostics different**: Starting from one base diagnostic, a variant is
created that differs in the rule identifier only, in the kind only, in the path only, in the line only, and in the
message only. None of the variants equals the base.

**A diagnostic is never equal to null**: A diagnostic is compared with a null reference using `Equals`. The result is
false.

##### Error handling

**A missing rule identifier, path, or message is rejected**: The constructor is called with a null rule identifier,
then a null path, then a null message. Each call throws `ArgumentNullException` and the parameter name is `ruleId`,
`path`, and `message` in turn.

**A rule identifier that is not kebab-case is rejected**: The constructor is called with an identifier that has an
uppercase letter (`Line-length`), a trailing hyphen (`line-`), a doubled hyphen (`line--length`), no characters at all,
a first character that is a digit or hyphen (`1rule` and `-rule`), or a character that is not a lowercase ASCII letter,
digit, or hyphen (`line_length`, `line length`, and a name with a letter that has an umlaut). Each call throws
`ArgumentException` for the parameter `ruleId`; for the invalid characters the message also mentions kebab-case.

**A blank path is rejected**: The constructor is called with an empty path and with a path of spaces. Each call
throws `ArgumentException` for the parameter `path`.

**A line or column below one is rejected**: The constructor is called with line 0 and line -1, and then with column 0
and column -1. Each call throws `ArgumentOutOfRangeException` for the parameter `line` or `column` in turn, because
positions start at one.

**A blank message is rejected**: The constructor is called with an empty message and with a message of white space.
Each call throws `ArgumentException` for the parameter `message`.

**A message that spans several lines is rejected**: The constructor is called with messages that hold a line feed, a
carriage return, a carriage return and line feed, a next-line character, a line separator, and a paragraph separator.
Each call throws `ArgumentException` for the parameter `message`, and the exception message states that a single line
is required.

**A kind that is not defined is rejected**: The constructor is called with the number 99 cast to the kind. The call
throws `ArgumentOutOfRangeException` for the parameter `kind`.
