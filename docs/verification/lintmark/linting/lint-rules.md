### LintRules

#### Verification Approach

`LintRules` covers the lint rule engine and the four rule families (headings, links, structure, and tables), verified
with unit tests in `LintRuleEngineTests.cs`, `HeadingRulesTests.cs`, `LinkRulesTests.cs`, `StructureRulesTests.cs`,
and `TableRulesTests.cs`. The rules read a real parsed document and a real configuration, so the tests parse small
inline Markdown documents with the real parser and run the rule or the engine through the `LintRuleTestSupport`
helper, asserting on the reported rule id, position, and message. No mocks are used. Engine scenarios use the real
default rule set and the real rule catalog.

#### Test Environment

N/A - standard test environment. The tests use inline documents only and no files.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Each rule reports its documented violation at the documented position and does not report conforming or
  uncertain input (literal asterisks, escapes, automatic links, unresolved tables, protected text).
- The engine reports only enabled rules, honors suppression by id or number, returns findings in position order,
  gives the same findings for any rule order and for parallel use, and produces only lint-only (never fixable) findings.
- Every rule id that a family declares is a registered, lint-only, configurable rule.
- Rules never throw on awkward documents.

#### Test Scenarios

##### Normal operation

**Heading level skip, repeated text, trailing punctuation, and emphasis as heading are reported**: Question marks and
entities are not punctuation findings; headings inside quotes are read; a punctuated emphasis-only paragraph is not
a heading.

**Links without a destination, images without alternative text, and undefined references are reported**: Automatic
links are not reported.

**Fragment links are checked against headings and anchors**: A heading is slugified before the comparison.

**Unused and repeated definitions and unused footnotes are reported**: A definition used in a footnote or table counts
as used.

**Reversed links, spaces in link text, and non-descriptive link text are reported**: Allowed spaces are not.

**Fence without a language, unclosed fence, blank line between quotes, shell prompts, a heading without space, and
asymmetric emphasis are reported**: Valid emphasis and escapes are not.

**Rows with another cell count, a table that is too wide when compact, and grid tables are reported**: An uncertain
table reports nothing.

**Engine reports triggered rules in position order**: Findings of all families come out sorted.

##### Boundary conditions

**A rule switched off is not asked**: Configuration decides which rules report.

**Suppression by legacy number silences only that rule**: Others still report.

**Any rule order and parallel use give the same findings**: The result is deterministic.

**All blocks of one document are read in order**: Inline text of every block kind is scanned.

##### Error handling

**Awkward documents do not throw**: Strange link and definition input produces findings or nothing.

**Default rules never produce fixable findings**: Lint-only rules never claim a fix.

**Declared rule ids are registered**: Each id of the families is a lint-only configurable rule of the catalog.
