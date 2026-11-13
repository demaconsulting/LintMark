## Linting

### Verification Approach

The `Linting` subsystem is verified by integration tests in `LintingSubsystemTests.cs`. The subsystem holds two
units, `Diagnostic` and `RuleCatalog`, and the tests show that they work together and with the units that report
findings or accept rule names. No test doubles are used: the real `RuleCatalog` and `Diagnostic` are combined, the
real `ConfigLoader` parses the `rules` setting of small configuration texts, and the identifier constants of the
real `LintMarkEngine`, `SourceDocumentException`, `ConfigLoader`, and `DocumentFormatter` are read directly. A
reflection scan over the public constants of `RuleCatalog` makes sure that a constant added later is checked too.
Details of each unit, such as the exact argument checks of `Diagnostic` and the lookup rules of `RuleCatalog`, are
verified in the unit documents.

### Test Environment

N/A - standard test environment. The tests use only in-memory text, make no file-system access, and do not change
process-wide state.

### Acceptance Criteria

- All integration tests pass with zero failures.
- A diagnostic created from the registered kind of a rule is fixable for a formatting rule and not fixable for a
  lint-only rule.
- Every rule identifier that the engine, the document, the configuration, and the formatter emit, and every
  identifier constant of the catalog, is registered under its own canonical identifier.
- A legacy rule number in the `rules` setting switches the canonical rule, and the number of a removed rule is
  accepted and changes nothing.
- The always-applied rule and the identifiers of failures are reported as unknown rules when a configuration names
  them.

### Test Scenarios

#### Normal operation

**The kind of a registered rule decides whether its diagnostic is fixable**: The registered entries of
`line-length` and `link-empty` are looked up, and one diagnostic is created from each entry. The diagnostic of the
formatting rule is fixable, and the diagnostic of the lint-only rule is not.

**Every emitted rule identifier is registered**: The identifiers named by the engine (read, parse, format, and write
errors), the document (encoding and line ending errors), the configuration loader, and the formatter are collected,
together with every public string constant of `RuleCatalog` whose name ends in `RuleId`. Reflection must find at
least one constant. Each identifier is found in the catalog and the entry carries the same identifier, so no
reporting unit can use an identifier that the registry does not know.

**A legacy rule number is accepted in the rules setting**: A configuration with `MD013: false` is parsed. It has no
errors, and the canonical rule `line-length` is switched off.

#### Boundary conditions

**The number of a removed rule is accepted and changes nothing**: A configuration with `MD033: false` is parsed. It
has no errors, and its rule states equal the defaults, so an older configuration file still loads.

#### Error handling

**Rules that cannot be configured are reported as unknown rules**: Configurations that name `line-ending` and
`config-error` are parsed. Neither produces a configuration. Each gives exactly one error at line 2, column 3, with
the message `Unknown rule` followed by the quoted name, so the protection of the files of the user cannot be
switched off in a configuration.
