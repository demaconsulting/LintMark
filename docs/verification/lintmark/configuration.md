## Configuration

### Verification Approach

The `Configuration` subsystem is verified by integration tests in `ConfigurationSubsystemTests.cs`. Each test builds
a small directory tree with the `TemporaryDirectory` helper and drives the real `ConfigLoader`, `LintMarkConfig`,
and `EditorConfigResolver` together, from the outer boundary that the `Engine` subsystem uses: load the settings
of a Markdown file, or resolve the line ending that the editor settings give it. The tests assert on the returned
configuration, the positioned problems, and the resolved line ending. The rule identifiers that the `rules`
setting accepts come from the real rule catalog, and the YAML and editor-settings libraries are the real ones. No
mocks are used. Parsing details, individual validation messages, and input limits are verified in the unit tests;
the subsystem tests verify what no single unit can show: that discovery, parsing, defaults, and the editor settings
combine into one coherent result per file.

### Test Environment

Each test creates and removes a temporary directory that contains an empty `.git` folder, so the upward search
for `.lintmark.yaml` never reaches a file outside the tree. Trees used with editor settings start with an
`.editorconfig` that declares `root = true`. No other setup is needed.

### Acceptance Criteria

- All integration tests pass with zero failures.
- The nearest `.lintmark.yaml` is used for each file, files at different depths get their own settings, and no
  settings are merged between files.
- A tree without a settings file gives the documented defaults, and every documented setting in a sample file is
  applied.
- Every problem of an invalid file is returned together, each at its line and column, and the file gives no settings.
- The line ending is taken from `.editorconfig` per file when the configuration does not give one, and unusable
  editor settings are "not specified" and never an error.
- Ignore patterns from the settings file are returned as given.

### Test Scenarios

#### Normal operation

**Nearest configuration applies per file**: Two Markdown files in folders with their own `.lintmark.yaml` and one in
a folder without a settings file each get the settings of their nearest file or the defaults.

**Sample file applies every setting**: A file that sets each documented option, with rules by id and by `MDxxx`
number, loads without problems and the values read back.

**Explicit configuration replaces discovery**: A file given by path is used for every Markdown file instead of
the discovered ones.

**Line ending comes from the editor settings per file**: Sections of one `.editorconfig` give different endings to
different files, and `root = true` stops the search.

**Configuration line ending wins over the editor settings**: A configured line ending is returned without
consulting `.editorconfig`.

**Ignore patterns are returned**: The patterns of the settings file replace the defaults in the given order.

#### Boundary conditions

**Empty settings file gives the defaults**: A file with no content loads the same settings as no file at all.

**Repository root bounds the search**: A `.lintmark.yaml` above the folder that contains `.git` is never read.

#### Error handling

**All problems of an invalid file are returned in position order**: A file with an unknown key, a bad value, and
a duplicate key reports each at its position, and no settings are returned.

**Malformed YAML and a missing explicit file are reported at a position**: Both give a problem at a line and column
and no settings.

**Unusable editor settings are not an error**: `cr`, `unset`, and an unknown value give "not specified".
