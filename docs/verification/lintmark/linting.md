## Linting

### Verification Approach

The `Linting` subsystem is verified by integration tests in `LintingSubsystemTests.cs`. Each test parses a small
document with the real `SourceDocument` and `DocumentParser`, builds the real lint rule engine with the real
rule catalog, and runs it with a real configuration and suppression map, as the `Engine` subsystem does. The
tests assert on the findings: rule id, kind, position, and order. They check that the registry and the checks
agree, that findings of different rule families come back together in position order, and that enablement and
suppression decide which rules report. No mocks are used. The support class `LintRuleTestSupport` builds the
document and runs the engine. Individual checks are verified in the unit tests.

### Test Environment

N/A - standard test environment. The tests use in-memory documents only and no files.

### Acceptance Criteria

- All integration tests pass with zero failures.
- A document that triggers rules of several families gets every finding once, in position order, each with a
  registered rule id and a kind that the catalog declares.
- A rule that is switched off in the configuration, or suppressed by a comment (also by `MDxxx` number), does not
  report, and the others still do.
- Names and numbers resolve to the same rule, and the findings always carry the canonical id.
- Lint-only findings are never marked fixable.

### Test Scenarios

#### Normal operation

**Findings of several families are returned together in position order**: A document with a heading skip, an unused
definition, a fence without a language, and a table with a short row reports each once in order.

**Every id a check emits is registered**: The ids declared by the checks are lint-only configurable ids of the catalog.

**Alias and id resolve to the same rule**: A suppression by `MDxxx` number silences the rule that the id names.

#### Boundary conditions

**A clean document has no findings**: An empty result for a conforming document.

**Rules switched off by configuration are not asked**: Only the enabled rules report.

#### Error handling

**Unknown names resolve to nothing**: An unknown rule name or a retired alias is not a rule.

**Suppression silences only the named rule**: Other rules still report in the same range.
