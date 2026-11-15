## Context

### Verification Approach

`Context` is verified with unit tests in `ContextTests.cs`. The class parses an argument array and owns the console
and log-file output, and it depends only on the file system for the log file, so no mocks are used. The tests build
contexts from argument arrays and assert on the parsed properties and on the exceptions thrown for invalid input.
Output scenarios redirect the console writers to capture the text; log scenarios use real files in a
`TemporaryDirectory`. Rule switches given by `--enable` and `--disable` are resolved with the real rule catalog,
because the context stores canonical rule identifiers.

### Test Environment

Log-file scenarios create and remove temporary directories. Console redirection changes process-wide state, so
the tests run sequentially with other tests that capture the console.

### Acceptance Criteria

- All unit tests pass with zero failures.
- Every option, long and short form, sets exactly the property it names, and positional arguments are kept in
  order, also when mixed with options.
- Every invalid combination or value is refused with an `ArgumentException` whose message names the problem.
- Output is written to the console unless silent, always to the log file when one is open, and an error output
  sets the exit status to 1.
- The log file is created or truncated, closed on disposal, and a log path that cannot be opened is refused.

### Test Scenarios

#### Normal operation

**No arguments give the default context**: Nothing is requested, nothing is silent, no paths, exit status 0.

**Flags set their properties**: `--version`, `--help`, `--silent`, `--validate`, `--check`, `--fix`, `--diff`, and
`--no-config` each set the matching property, and the short forms (`-v`, `-h`, `-?`, and the other short flags) behave
like the long forms.

**Valued options keep their values**: `--results` and its alias `--result`, `--log`, `--config`, and `--depth` store
their values; a repeated `--config` keeps the last one.

**Positional paths keep their order**: Paths given alone and mixed with flags are stored in order.

**Rule switches resolve to canonical identifiers**: `--enable` and `--disable` accept kebab-case ids and `MDxxx`
aliases, in comma-separated lists that may be repeated, and store the canonical ids; a retired alias is accepted
and ignored.

**`--diff` with `--check` is accepted**: The two modes may be combined.

**Output goes to the console and the log**: A normal line is written to the console and the log; a silent context
writes nothing to the console but still writes to the log.

**The log file is truncated on open**: An existing log file starts empty.

**Disposal closes the log file**: After disposal the file can be read and deleted.

**Concurrent error writers keep their lines intact**: Many threads writing errors produce complete lines only.

#### Boundary conditions

**A value out of range is refused**: A `--depth` outside its range throws.

**A repeated `--config` is accepted**: The last value wins.

#### Error handling

**An unknown argument is refused**: It throws `ArgumentException`.

**A valued option without a value is refused**: `--log`, `--results`, `--result`, `--depth`, and `--config` at the end
of the arguments throw; `--config` followed by another option also throws.

**A non-integer `--depth` is refused**: It throws.

**Conflicting options are refused**: `--fix` with `--check`, `--config` with `--no-config`, and `--validate` with
paths, `--check`, or a rule switch throw.

**An invalid rule list is refused**: An unknown rule name, an always-applied formatting rule, an empty item, a
missing value, or a rule named in both lists throws.

**A log file that cannot be opened is refused**: An invalid path or a missing directory throws
`InvalidOperationException`.

**An error line sets the exit status**: Writing an error sets exit status 1, also when silent.
