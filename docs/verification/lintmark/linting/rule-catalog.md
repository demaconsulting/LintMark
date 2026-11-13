### RuleCatalog

#### Verification Approach

`RuleCatalog` is verified with unit tests in `RuleCatalogTests.cs`. The catalog is static and immutable and depends
only on `DiagnosticKind`, so no test doubles are used: the tests call the real lookup methods and read the real
collections. The test for the Turkish culture sets `CultureInfo.CurrentCulture` to `tr-TR` for the duration of the
test and restores it afterward. The test for immutability uses the `ReadOnlyAssertions` helper, which attempts every
modification that a cast to a mutable interface would allow. Where the registry must agree with the `Diagnostic`
record, the tests create a real `Diagnostic` for each registered identifier.

#### Test Environment

N/A - standard test environment. The culture test changes the culture of the current thread only and restores it
in a `finally` block, so other tests are not affected.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- A canonical identifier, a legacy alias, and any letter case of either resolve to the canonical identifier, also
  when the current culture is Turkish.
- An unknown name and the number of a removed rule do not resolve, and removed numbers are recognized as dropped.
- A configurable lookup accepts only the 22 configurable rules and rejects the always-applied rule and the failure
  identifiers.
- Exactly three configurable rules are off by default, and the 22 defaults cover exactly the 22 configurable rules.
- Every entry has a description that ends in a period and a unique identifier, the entries are in ordinal order, and
  every identifier is accepted by `Diagnostic`.
- Exactly the line ending, line length, and paragraph wrap rules are formatting rules.
- The counts are 22 configurable rules, 11 other entries, 16 aliases, and 9 dropped aliases.
- Every exposed collection refuses modification, also through a cast to a mutable interface.
- A null name or identifier throws `ArgumentNullException` that names the parameter.

#### Test Scenarios

##### Normal operation

**A canonical identifier resolves to itself**: `heading-increment` is resolved. The result is true and the identifier
is returned unchanged.

**Every legacy alias resolves to its rule**: All 16 documented aliases (for example `MD001`, `MD013`, and `MD059`)
are resolved and each gives the documented rule. The alias map holds exactly the same number of entries.

**The legacy number of the reversed link rule reaches the rule**: `MD011` is read from the alias map and resolved
as a configurable name. Both give `link-reversed`, and that rule is on by default.

**Names are matched in any letter case**: `LINE-LENGTH`, `Line-Length`, `md013`, and `Md013` are resolved. Each gives
`line-length`.

**Matching does not depend on the culture of the machine**: The current culture is set to Turkish, where the letter
i has special case rules, and `LINE-LENGTH` and `LINK-EMPTY` are resolved. Both resolve to the same rules as in any
other culture.

**A configurable lookup returns the canonical identifier**: The names `line-length`, `LINE-LENGTH`, `MD013`, `md013`,
`link-reversed`, `MD011`, and `paragraph-wrap` are resolved with the configurable lookup. Each returns the expected
canonical identifier.

**A registered identifier returns its entry**: `line-length` and `LINE-LENGTH` are looked up. The entry has the
identifier `line-length`, the formatting kind, the alias `MD013`, is on by default, and is configurable.

**The always-applied rule is registered but not configurable**: `line-ending` is resolved and looked up. It resolves
to itself and its entry is not configurable, and it is absent from the list of configurable rules and from the
defaults.

**The configurable rules are sorted, unique, and covered by the defaults**: The list of configurable rules holds 22
identifiers, in ordinal order, without duplicates. Each identifier has a default, and the defaults hold no other
keys.

**The aliases are unique and lead to configurable rules**: Every alias maps to a configurable rule, no two aliases map
to the same rule, and no alias is also a dropped alias.

**Exactly three rules are off by default**: The disabled defaults are `descriptive-link-text`,
`dollar-sign-in-shell-code`, and `table-grid-unformatted`, in that order.

**Entries are described and unique**: Every entry has a description that is not blank and ends in a period. The
identifiers and aliases of all entries together contain no duplicate, ignoring letter case.

**Entries are sorted by identifier**: The identifiers of the entries are in ordinal order.

**Only three rules are formatting rules**: The entries of the formatting kind are `line-ending`, `line-length`, and
`paragraph-wrap`, in that order.

**The non-configurable entries are the always-applied rule and the failure identifiers**: The entries that are not
configurable are `line-ending`, the three safety identifiers, the six file identifiers, and `config-error`. All are
enabled and none has an alias.

**Every registered identifier is a valid diagnostic identifier**: A `Diagnostic` is created for each entry using its
identifier and kind. Each creation succeeds and keeps the identifier.

**The counts of the registry agree**: The entries are 22 configurable and 11 not configurable, 33 in all. There are
22 configurable identifiers, 22 defaults, 16 aliases, and 9 dropped aliases, and the configurable entries match the
list of configurable identifiers.

##### Boundary conditions

**Unknown names do not resolve**: An empty name, `no-such-rule`, `MD009` (a rule that the formatter always applies),
and `MD999` are resolved. Each returns false with no identifier, and none is a dropped alias.

**The number of a removed rule is recognized but is not a rule**: The nine removed numbers `MD002`, `MD006`,
`MD008`, `MD025`, `MD033`, `MD041`, `MD043`, `MD044`, and `MD057`, and `md025` in lower case, are resolved. Each
returns false, and each is reported as a dropped alias.

**Rules that are not configurable are rejected by the configurable lookup**: The names `line-ending`, `LINE-ENDING`,
`config-error`, and `safety-html-mismatch` are resolved as configurable. Each returns false with no identifier, as
for an unknown name.

**Removed numbers and unknown names are not configurable**: `MD033`, `md025`, `no-such-rule`, `MD009`, and an empty
name are resolved as configurable. Each returns false with no identifier, and none is found by `TryGet`.

**Only canonical identifiers are found by a lookup**: `no-such-rule`, an empty name, the alias `MD013`, and the
removed number `MD033` are looked up. Each returns false and no entry.

##### Error handling

**A null name is rejected by the resolving methods**: `TryResolve` and `IsDroppedAlias` are called with null, and
each throws `ArgumentNullException`. The configurable lookup does the same and names the parameter `name`.

**A null identifier is rejected by the lookup**: `TryGet` is called with null. It throws `ArgumentNullException` and
names the parameter `id`.

**The exposed collections cannot be modified**: The configurable rules, the dropped aliases, and the entries are cast
to read-only list views, and the defaults and aliases are cast to dictionary views. Every attempted modification
fails, and the mutable list and dictionary interfaces report that they are read-only.
