### ConfigLoader

#### Verification Approach

`ConfigLoader` is verified with unit tests in `ConfigLoaderTests.cs` (discovery and file access) and
`ConfigLoaderParseTests.cs` (parsing and validation of the content). The loader depends only on the file system and
the YAML library, so no mocks are used. Discovery and file-access scenarios build real directory trees in a
`TemporaryDirectory`, including a `.git` marker so the upward search stops inside the tree. Parsing scenarios call the
parse entry point with text or bytes and assert on the returned settings and on the problem list, comparing
configurations by content with a shared comparer. Rule identifiers come from the real rule catalog. The sample file
that sets every option is a shared fixture (`ConfigurationSamples`).

#### Test Environment

Discovery scenarios create and remove temporary directories and change the current directory in one scenario, so the
tests run sequentially with other tests that use process-wide state. Locked and denied files are created through
the file system as far as the platform allows.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- The nearest `.lintmark.yaml` up to the repository root wins, farther files are never read, and a nearer partial file
  does not inherit from a farther one.
- An explicit file replaces discovery; a missing, unreadable, or oversized file is a problem at line 1, column 1.
- Every setting is accepted in every documented spelling and refused otherwise with a message that names the
  allowed values.
- Every fault is reported at its line and column, once, in position order; repeated keys are reported at the later
  entry.
- Rules are accepted by id or `MDxxx` alias in any letter case and stored as canonical ids; retired aliases are
  accepted and not stored; unknown and formatting-only names are refused.
- Input beyond the size limit or the nesting limit is reported promptly and never crashes the loader.

#### Test Scenarios

##### Normal operation

**No settings file gives the defaults**: A tree without `.lintmark.yaml` returns the default configuration.

**Settings in an ancestor folder are used**: A file in a parent folder applies to files below it.

**Nearest file wins and the farther one is never read**: Two files at different depths give the nearer values only.

**Partial nearer file does not inherit**: Settings missing from the nearer file are defaults, not the farther
file's values.

**The repository root file is found and the search stops at `.git`**: A file above the root is ignored.

**Full sample file applies every setting**: Each option reads back its configured value, including link-refactor
sub-options.

**Equivalent spellings give equal settings**: Block style, flow style, quoted values, a UTF-8 byte order mark, and
CRLF line endings load the same.

**Rules by id, alias, or any letter case are stored canonically**: `MDxxx` numbers and kebab-case ids both end up as
ids; retired aliases are accepted and dropped.

**Ignore patterns replace the defaults**: The list keeps the given order, converts backslashes, and removes exact
duplicates while keeping case variants.

##### Boundary conditions

**List indent two and four are accepted**: Other values are refused.

**Input at the size limit and at the nesting limit is accepted**: One byte or level more is reported.

**Discovery for odd paths still works**: A path with a null character or the file-system root returns the
defaults; a file in a missing folder still searches upward.

##### Error handling

**Invalid enumeration, case, integer, range, boolean, null, and shape values are reported**: Each message names the
key and, for enumerations, lists the allowed values.

**Unknown and repeated keys are reported at their position**: Several unknown keys are all reported in order; a
repeated key is reported at the second occurrence, also with an invalid first value.

**Invalid link-refactor sub-options are reported with a dotted name**: An unknown sub-key, a number out of range, a
non-boolean flag, and a marker that is not one comment each give a problem.

**Unknown, formatting-only, and conflicting rules are refused**: An alias and the id of the same rule together are
reported at the later one.

**Malformed YAML, root sequences, several documents, anchors, aliases, tags, and complex keys are reported**: Each gives
a problem at a position of at least line 1, column 1.

**Invalid or UTF-16 text is reported at the offending position**: Positions account for a byte order mark and for
each line break once.

**Missing, unreadable, locked, denied, or too large files are reported**: A directory in place of the
file, a locked file, access denied, an invalid path, and a file over the limit each give one problem; an unreadable
nearest file is reported without falling back to a farther one.

**Deep nesting is reported, not crashed on**: Nesting beyond the limit reports the position of the offending bracket.
