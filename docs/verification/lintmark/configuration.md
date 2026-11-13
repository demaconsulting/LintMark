## Configuration

### Verification Approach

The `Configuration` subsystem is verified by integration tests in `ConfigurationSubsystemTests.cs`. Each test
builds a real temporary directory tree (using the `TemporaryDirectory` test helper) and drives the real
`ConfigLoader`, `LintMarkConfig`, and `EditorConfigResolver` together, as the `Engine` subsystem does: it loads the
configuration for Markdown files at several depths, or resolves the line ending that the editor settings give, and
asserts on the returned configuration, the positioned problems, and the line ending. The rule identifiers that the
`rules` setting accepts come from the real rule catalog of the `Linting` subsystem. The editorconfig library
behind `EditorConfigResolver` is the real library. The injected seams of the resolver are not used at this level,
with one exception: the scenario about editor settings that cannot be read has no portable way to make a real file
unreadable, so it injects a failing settings lookup, which is the failure the real lookup produces. (The system
tests make a real file unreadable, as far as the platform allows.) No mocking is used.

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
  repository root is read; no other configuration file influences the result, even when it is invalid and sits
  between the repository root and the nearest file.
- An explicitly named file is used instead of discovery, also when it is given as a relative path; a named file
  that is missing or cannot be read is reported at line 1, column 1, before any Markdown file is processed.
- A tree without a configuration file and a tree with an empty file both give the documented defaults.
- The sample file that gives every setting is accepted without problems, and representative values of it are applied,
  including a rule given by its legacy number.
- A rule is accepted by its name or legacy number, the legacy number of a removed rule is accepted and changes
  nothing, and the name of a rule that is registered but not configurable (`line-ending`) is reported as unknown.
- Equivalent files in block style, in flow style, with the settings in another order, with CRLF line endings, and
  with a UTF-8 marker give equal settings.
- A file with malformed YAML, with an unsupported structure (a list, two configurations, a reference, or a
  non-text name), with invalid UTF-8, or saved as UTF-16 gives no configuration and a problem at the position of
  the fault.
- An invalid file gives no configuration and one problem per fault, each at its line and column in the file, ordered
  by position.
- A setting given twice and a rule named twice are reported at the later entry.
- A file larger than the documented size limit (1 MiB) is reported at line 1, column 1, and a file nested far beyond
  the documented nesting limit (a file nested 100,000 levels deep) is reported with a position within 10 seconds.
- Editor settings that cannot be used (`cr`, `unset`, an unknown value such as `banana`, malformed lines, unrelated
  settings) are "not specified" and never an error.
- The line ending that the editor settings give is resolved per file, honoring sections and `root = true`.
- Editor settings that cannot be read are reported as a one-line message and give no line ending.
- Many threads that use one loader and one resolver get results identical to sequential use.
- Configurations and problem lists cannot be changed by their holders.

### Test Scenarios

#### Normal operation

**Nearest configuration wins within the repository and nothing above it is read**: An invalid `.lintmark.yaml`
above a folder that holds a `.git` directory, a configuration in the repository root, and a nested configuration
in `docs` are created. A file at the repository root gets the root settings, a file below `docs` gets only the
nested settings (the line length and wrap mode of the root file are not inherited), a file in a folder without its
own configuration shares the root file's result instance, and the file above the repository is never used.

**A farther configuration file has no influence, even when it is invalid**: Inside one repository, an invalid
configuration file in a folder, with a valid nearer file below it that sets only the line length, and a valid
farther file that sets other settings with a valid nearer file below it that sets only the line length. A file below
each nearer file gets no problems and a configuration that equals the defaults with only the nearer file's line
length; the invalid file's problems are not reported and the valid farther file's settings are not inherited.

**An explicit file replaces discovery**: A discoverable file and a second file, named with a relative path, are
created; with the named file loaded through the explicit-file loader while the temporary directory is the current
directory, the named file's settings apply.

**Absent and empty configuration files give the documented defaults**: A tree without a file and a tree with an empty
file both give a configuration equal to the built-in defaults (line length 120, fill wrapping, compact tables,
link conversion off).

**The sample file that gives every setting is accepted and representative values are applied**: The sample that gives
every setting, every link conversion option, three rules (one by its legacy number), and two ignore patterns is
loaded; there are no problems, and the line length, line ending, table style, link conversion flag, the rule named
by its legacy number (switched off under its rule name), and the ignore patterns (which replace the defaults) have
the sample's values. The sample is shared with the unit and system tests through a neutral test helper.

**Editor settings give the line ending per file**: A root
`.editorconfig` gives `lf` to all files and `crlf` to Markdown files, and a second file in a sub folder declares
`root = true` without any line-ending setting. A Markdown file gets `crlf`, a text file gets `lf`, and a Markdown
file in the sub folder gets no line ending, because the nested root hides the outer file.

**Equivalent files give equal settings whatever their layout**: The same settings are written in block style, in
flow style (using a legacy rule number), with the settings in the opposite order, with CRLF line endings, and with a
UTF-8 marker at the start; all five give no problems and a configuration that equals the first and differs from the
defaults.

#### Boundary conditions

**A file larger than the limit is reported at its first line**: A file one byte larger than the documented limit of 1
MiB gives no configuration and one problem at line 1, column 1 that names the limit.

**A deeply nested file is reported promptly**: A file whose value is nested 100,000 levels deep, far beyond the
documented limit of 64 levels, gives no configuration and one problem on line 1 that starts `Nesting is too deep`,
and loading takes less than 10 seconds. The time bound is part of the scenario because the reason for the limit
is that parsing such a file unguarded takes unbounded time.

**Many threads get the sequential results**: Configuration and editor settings in several folders (one
configuration invalid) are used by up to 16 threads over 48 files, each looked up ten times through one shared
loader and one shared resolver; every result (line length, problem messages, line ending, and editor-settings
problem) equals the result of sequential use with fresh instances.

**Problems of one file are ordered by position**: A file whose problems are found at different stages (an unknown
rule, an empty ignore pattern and an unknown setting while the settings are checked, and an anchor on its last line
while the file is read) gives the problems in ascending position order, although the anchor was found first.

**Unsupported editor values and unrelated settings are not specified**: Sections that give `cr`, `unset`, an unknown
value (`banana`), an unrelated setting, no section at all, and a malformed `[[[` line are resolved; every result
equals the "not specified" result and none carries an error message.

#### Error handling

**A missing or unreadable explicit file is reported at line 1**: A path that does not exist and a directory where a
file is expected are each given to the explicit-file loader; each reports one problem at line 1, column 1 as soon as
the loader is created, and a Markdown file loaded through it gets no configuration.

**An unknown setting, rule, and link option are reported at the name**: A file with an unknown setting `color` on its
first line, an unknown rule `no-such-rule` under `rules`, and an unknown option `color` under `link-refactor` gives no
configuration and three problems, each at the position of the unknown name (1:1, 3:3, and 5:3) with the messages
`Unknown setting 'color'`, `Unknown rule 'no-such-rule'`, and `Unknown setting 'link-refactor.color'`.

**An invalid value is reported at the value**: A file with `wrap: sideways` gives no configuration and one problem at
the value (1:7) with the message `Invalid value 'sideways' for setting 'wrap'; expected one of: fill, sentence,
semantic`.

**An invalid file reports every problem at its position and yields no configuration**: A file with an unknown
setting, a wrong type, and an invalid value gives no configuration and three problems at line 1 column 1, line 2
column 14, and line 3 column 7 of that file. The scenario shows that all problems are returned together, each at its
own position.

**Malformed YAML is reported with a position**: A file with an unterminated quote gives no configuration and one
problem that starts `Invalid YAML`, at the opening quote (2:7).

**Unsupported structure is reported at the construct**: A list, two configurations, a reference to another part of
the file, and a non-text name, each in its own folder, give no configuration and one problem each, at the position of
the construct (1:1, 2:1, 1:14, and 1:3).

**Unsupported text is reported at the fault**: A file with an invalid byte after two characters of its second line
is reported at 2:3 as invalid UTF-8; a file saved as UTF-16 is reported at 1:1 as an unsupported encoding.

**A setting given twice and a rule named twice are reported at the later entry**: A rule named by its legacy number
and then by its name, and a setting given again at the end of the file, give no configuration and problems at the
rule's second entry (4:3) and at the repeated setting (5:1), each naming the earlier line.

**Editor settings that cannot be read are reported as a message**: A resolver whose lookup of the
settings fails with a line break in its message gives no line ending and the one-line message
`Cannot read .editorconfig settings: disk failure`.

**Rule aliases, dropped aliases, and non-configurable rules are handled by the rule catalog**: One configuration
switches a rule off by its legacy number (`MD013`) and also names the legacy number of a removed rule (`MD033`);
it gives no problem, the rule `line-length` is off, and every other rule keeps its default state, so the removed
rule changed nothing. Another configuration names the rule `line-ending`, which the catalog registers but does not
let a configuration change; it gives one problem at 2:3, `Unknown rule 'line-ending'`. The rule identifiers
themselves are verified with the rule catalog unit.

**Results cannot be changed through casts**: The rule table and the ignore patterns of a valid configuration, and the
problem list of an invalid one, refuse every change attempted through the mutable collection interfaces.
