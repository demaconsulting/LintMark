### Diagnostic

![Linting Structure](LintingView.svg)

#### Purpose

`Diagnostic` is the single model of a finding about a file: a problem in the file, in its configuration, or in an
attempt to rewrite it. Read and parse failures, configuration problems, formatting violations, and safety refusals are
all created, de-duplicated, ordered, and reported as `Diagnostic` values, so one reporter renders everything. Its
supporting type `DiagnosticKind` is described in this document.

#### Data Model

`Diagnostic` is a sealed record with value equality over all members. It is immutable and may be shared between
threads.

| Member | Type | Meaning |
| --- | --- | --- |
| `RuleId` | `string` | Kebab-case identifier of the rule the finding is reported under |
| `Kind` | `DiagnosticKind` | Whether rewriting the file removes the finding |
| `Path` | `string` | Full path of the file; the reporter makes it relative |
| `Line` | `int` | 1-based line |
| `Column` | `int` | 1-based column |
| `Message` | `string` | Single-line description without the path |
| `Fixable` | `bool` (derived) | `Kind == Formatting`; computed, never stored |

**DiagnosticKind**: An enumeration with two members. `LintOnly` is a finding that is only reported (file problems,
configuration problems, safety refusals, and lint rules). `Formatting` is a violation of canonical form that
rewriting the file removes.

**Invariants**:

- The rule identifier is kebab-case: non-empty, starting with a lowercase ASCII letter, made of lowercase ASCII
  letters and digits separated by single hyphens, with no trailing hyphen.
- The kind is a defined member.
- The path is not blank, line and column are at least one, and the message is not blank.
- The message is a single line: it contains none of CR, LF, NEL (U+0085), line separator (U+2028), or paragraph
  separator (U+2029). A producer that has a multi-line text replaces the line breaks before creating the diagnostic.
- `Fixable` is derived from `Kind`, so the flag and the kind can never disagree.

The identifier is checked for its shape only, not against `RuleCatalog`, so a unit that reports can be tested without
the registry. A test proves that every identifier the tool emits is registered.

#### Key Methods

**Constructor**: Creates a diagnostic.

- *Parameters*: `string ruleId`, `DiagnosticKind kind`, `string path`, `int line`, `int column`, `string message`.
- *Preconditions*: the invariants above.
- *Postconditions*: an immutable diagnostic whose members equal the arguments.

Steps: (1) reject null for the rule identifier, path, and message; (2) check the identifier with a scan that accepts a
lowercase ASCII letter first and then letters, digits, and a hyphen that does not follow another hyphen, and that
rejects a trailing hyphen; (3) check that the kind is a defined member; (4) check the path, the line and column
ranges, and the message (blank and line breaks); (5) store the members.

**Equals and GetHashCode**: Value equality over all six stored members, as generated for a record. Two findings that
are equal are the same finding, which `TextReporter` relies on to report a finding once however many files reach it.

#### Error Handling

All errors are argument errors raised by the constructor, at the point where an invalid value would enter the record;
nothing is logged and nothing is retried. `ArgumentNullException` for a null rule identifier, path, or message;
`ArgumentException` for an identifier that is not kebab-case, a blank path or message, and a message with a line break;
`ArgumentOutOfRangeException` for a kind that is not a defined member and for a line or column below one. Producers
validate or sanitize their input first, so these exceptions signal programming errors.

#### Dependencies

- **.NET BCL** — the standard exception types and character classification. No other tool units are used.

#### Callers

- **DocumentFormatter** — creates the `line-ending` formatting diagnostic.
- **LintMarkEngine** — creates every other finding (read, parse, format, write, safety, grid table, and configuration
  problems) and carries them in `FileResult`.
- **TextReporter** — orders, de-duplicates, and writes diagnostics.
