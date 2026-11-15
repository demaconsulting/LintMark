### RuleCatalog

#### Verification Approach

`RuleCatalog` (with its `Diagnostic` finding type, `RuleInfo`, and `RuleOverrides`) is verified with unit tests in
`RuleCatalogTests.cs`. The catalog is static registry data with no dependencies, so the tests query it directly and
build rule overrides from lists of names; no mocks, files, or process-wide state are used. The finding type is
exercised throughout the lint-rule and reporter tests.

#### Test Environment

N/A - standard test environment. One scenario changes the current culture to Turkish for the duration of the test.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Every kebab-case id, every alias, and any letter case resolves to the canonical rule; unknown names and retired
  aliases do not resolve, and the Turkish culture does not change that.
- The rule `line-ending` and the formatting-only rules resolve but are not configurable.
- Rule overrides turn configurable rules on and off by id or number, canonicalized, and refuse invalid lists and a
  rule named in both lists.

#### Test Scenarios

##### Normal operation

**Kebab id resolves to the canonical rule**: The id returns itself.

**Every alias resolves to its rule**: Each `MDxxx` number returns its rule.

**Any letter case resolves**: Upper- and lower-case spellings agree.

**Spacing lint-only rule by number is configurable and enabled**: Its default state is on.

**Overrides force rules on and off**: Applying overrides to a configuration changes only the named rules.

**Names and numbers become canonical ids in overrides**: Both forms give the same entry.

##### Boundary conditions

**The Turkish culture still matches**: Case folding is invariant.

**Line ending resolves but is not configurable**: The registry lists it as always applied.

**Formatting rule alias resolves but is not configurable**: The alias names a formatting rule.

##### Error handling

**Unknown names and unknown ids do not resolve**: The lookup returns false.

**A retired alias does not resolve and is reported as dropped**: It is accepted by the loader but not stored.

**An invalid override list is refused**: An empty or unknown entry throws.

**A rule named in both lists is refused**: Enable and disable cannot overlap.
