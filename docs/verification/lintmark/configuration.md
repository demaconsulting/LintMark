## Configuration

### Verification Approach

The `Configuration` subsystem is verified by integration tests in `ConfigurationSubsystemTests.cs`. Each test
builds a real temporary directory tree (using the `TemporaryDirectory` test helper) and drives the real
`ConfigLoader`, `LintMarkConfig`, and `EditorConfigResolver` together, as the `Engine` subsystem does: it loads the
configuration for Markdown files at several depths, or resolves the line ending that the editor settings give, and
asserts on the returned configuration, the positioned problems, and the line ending. The editorconfig library
behind `EditorConfigResolver` is the real library; the injected seams of the resolver are not used at this level,
because they are verified in the `EditorConfigResolver` unit. No mocking is used.

The subsystem does not lint or format anything and writes no output, so the observable interface is the values
that its units return. The scenarios therefore check the combined behavior that no single unit can show: that
discovery, validation, and defaults produce one coherent configuration per file; that every problem of an invalid
file is returned together, in position order; and that the loader and the resolver stay correct when they are
shared by many threads.

### Test Environment

Temporary directories are created and removed by each test. By default each directory holds an empty `.git`
directory, so configuration discovery that starts inside it never reaches a `.lintmark.yaml` file that happens to
exist above the system temporary directory, and the tests do not depend on the machine they run on. A scenario
about the repository boundary creates a directory without that marker and builds the boundary itself. Every tree
used with editor settings starts with an `.editorconfig` file that declares `root = true`, so the upward search for
editor settings never reaches a file outside the tree. The tests change the current directory in one scenario and
are therefore run sequentially with the other tests that touch process-wide state.

### Acceptance Criteria

- All integration tests pass with zero failures.
- The nearest `.lintmark.yaml` file up to and including the repository root is used and nothing above the
  repository root is read; no configuration is merged.
- An explicitly named file is used instead of discovery, also when it is given as a relative path.
- A tree without a configuration file and a tree with an empty file both give the documented defaults.
- The documented full sample file applies every setting, including a rule given by its legacy number.
- An invalid file gives no configuration and one problem per fault, each at its line and column in the file, ordered
  by position.
- The editor settings give the line ending per file, honoring sections and `root = true`; values that cannot be
  used (`cr`, `unset`, unknown, malformed lines, unrelated settings) are "not specified" and never an error.
- Many threads that use one loader and one resolver get results identical to sequential use.
- Configurations and problem lists cannot be changed by their holders.

### Test Scenarios

#### Normal operation

**Nearest configuration wins within the repository and nothing above it is read**: An invalid `.lintmark.yaml`
above a folder that holds a `.git` directory, a configuration in the repository root, and a nested configuration
in `docs` are created. A file at the repository root gets the root settings, a file below `docs` gets only the
nested settings (the line length and wrap mode of the root file are not inherited), a file in a folder without its
own configuration shares the root file's result instance, and the file above the repository is never used.

**An explicit file replaces discovery**: A discoverable file and a second file, named with a relative path, are
created; with the named file loaded through the explicit-file loader while the temporary directory is the current
directory, the named file's settings apply.

**Absent and empty configuration files give the documented defaults**: A tree without a file and a tree with an empty
file both give a configuration equal to the built-in defaults (line length 120, fill wrapping, compact tables,
link conversion off).

**The full sample file applies every setting and rule alias**: The documented sample that sets every setting to a
non-default value is loaded; there are no problems, the settings have the sample's values, the rule named by its
legacy number is switched off under its rule name, and the ignore patterns replace the defaults.

**Editor settings give the line ending per file**: A root `.editorconfig` gives `lf` to all files and `crlf` to
Markdown files, and a second file in a sub folder declares `root = true` without any line-ending setting. A
Markdown file gets `crlf`, a text file gets `lf`, and a Markdown file in the sub folder gets no line ending,
because the nested root hides the outer file.

#### Boundary conditions

**Many threads get the sequential results**: Configuration and editor settings in several folders (one
configuration invalid) are used by 16 threads over 48 files, each looked up ten times through one shared loader and
one shared resolver; every result (line length, problem messages, line ending, and editor-settings problem) equals
the result of sequential use with fresh instances.

**Problems of one file are ordered by position**: A file whose faults come from different checks (an unknown rule,
an empty ignore pattern, an unknown setting, an invalid value) gives problems on strictly ascending lines.

**Unsupported editor values and unrelated settings are not specified**: Sections that give `cr`, `unset`, an
unrelated setting, no section at all, and a malformed `[[[` line are resolved; every result equals the "not
specified" result and none carries an error message.

#### Error handling

**An invalid file reports every problem at its position and yields no configuration**: A file with an unknown
setting, a wrong type, and an invalid value gives no configuration and three problems at line 1 column 1, line 2
column 14, and line 3 column 7 of that file.

**Results cannot be changed through casts**: The rule table and the ignore patterns of a valid configuration, and the
problem list of an invalid one, refuse every change attempted through the mutable collection interfaces.
