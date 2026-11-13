## Cli

### Verification Approach

The `Cli` subsystem is verified by integration tests defined in `CliSubsystemTests.cs`. Each
test exercises `Context.Create` and `Program.Run` together, treating the pair as the observable
subsystem interface. Tests pass controlled argument arrays and assert on captured console output,
file-system side-effects, and exit codes. `Validation` (part of the `SelfTest` subsystem)
executes its real logic in scenarios that exercise the `--validate` path, and the real `LintMarkEngine`
processes the files in the scenarios that pass paths; temporary files come from the `TemporaryDirectory` test
helper. No mocking is applied at any level.

The configuration scenarios follow the same pattern. The configuration options are passed through
`Context.Create`, the scenarios assert that `Context` exposes them (`ConfigFile` and `NoConfig`), and `Program.Run`
passes them on to the real engine, which loads the real configuration files that the scenario wrote to a temporary
directory; the assertions are on the lines in the log file and on the exit code. The scenarios that reject a
combination of options go through `Program.Main` and assert on the captured standard error.

The fix and diff options follow the same pattern: the flags are passed through `Context.Create`, the scenario asserts
that `Context` exposes them (`Fix` and `Diff`) together with the paths, and the rejected combination of `--fix` with
`--check` is also sent through `Program.Main` to show the usage error and the exit code. The writing behavior of
`--fix` and `--diff` is not repeated here, because the `Engine` subsystem verification covers it with the real
writer; the `Cli` scenarios stop at the flags and the usage errors.

### Test Environment

The scenarios that pass paths or configuration files use temporary directories from the `TemporaryDirectory` test
helper. Each holds an empty `.git` directory, so configuration discovery that starts inside it never reaches a
`.lintmark.yaml` file above the system temporary directory. Otherwise the standard test environment applies.

### Acceptance Criteria

- All integration tests pass with zero failures.
- All supported flags produce the correct observable behavior when passed through `Context.Create`
  and `Program.Run`.
- Invalid arguments produce exit code 1 and an error message.
- Log files and results files are created at the specified paths when the corresponding flags
  are used.
- Positional paths flow from `Context.Create` to the engine; an undecodable file gives a diagnostic and
  exit code 1, and `--check` leaves files unchanged.
- `--validate` combined with paths, `--check`, `--fix`, `--diff`, or `--no-config` is rejected with exit code 1.
- `--config` names the configuration file that governs the run and `--no-config` makes the run ignore every
  configuration file; both flow from `Context.Create` through `Program.Run` to the engine, and a problem in the
  named file gives a positioned `config-error` line and exit code 1.
- `--config` combined with `--no-config`, and `--validate` combined with `--config`, are rejected with exit code 1
  and an error on standard error that names the option.
- `--fix` and `--diff` flow from the command line to `Context` next to the paths and set only their own flag;
  `--fix` combined with `--check` is rejected by `Context` and, through `Program.Main`, with exit code 1 and an error
  on standard error that says that `--fix` cannot be combined with `--check` or `--diff`.
- Concurrent writers to one `Context` produce intact lines and exit code 1.

### Test Scenarios

#### Normal operation

**Long version flag displays the version**: `["--version"]` is passed through `Context.Create` and `Program.Run`;
standard output contains the version string and the exit code is 0.

**Short version flag displays the version**: `["-v"]` gives the same result.

**Long help flag displays the help text**: `["--help"]` gives help text on standard output and exit code 0.

**Short help flags display the help text**: `["-?"]` and `["-h"]` give help text and exit code 0.

**Validate flag runs the self-validation**: `["--validate"]` gives output that contains `"Total Tests:"` and exit
code 0.

**Silent flag suppresses console output**: `["--version", "--silent"]` gives empty standard output and exit
code 0.

**Results flag writes a results file**: `["--validate", "--results", "<tmp>.trx"]` creates a results file at the
specified path and exits with 0.

**Legacy result alias writes a results file**: `["--validate", "--result", "<tmp>.trx"]` behaves the same.

**Log flag writes a log file**: `["--log", "<tmp>.log"]` creates a log file at the specified path and exits
with 0.

**Depth flag sets the heading depth**: `["--depth", "2"]` is parsed and propagated through `Context`, and the exit
code is 0.

**Paths flow to the engine and a valid file exits with zero**: A valid temporary Markdown file is passed as a
positional argument with `--silent` and `--log`; `Context.Paths` holds the file, the real engine processes it, the
log contains no diagnostic, and the exit code is 0.

**Check flag leaves files unchanged**: `--check` and a file path are passed; `Context.Check` is true, the exit
code is 0, and the file's bytes and last-write time are unchanged.

**Fix and diff flags are parsed next to paths**: `["--silent", "--fix", "a.md"]` and `["--silent", "--diff",
"a.md"]` are each passed through `Context.Create`; the first context has `Fix` set and `Diff` not, the second has
`Diff` set and `Fix` not, and both keep `a.md` as their only path.

**Config flag names the configuration file and its problems are reported**: A valid Markdown file and a configuration
file with an unknown setting name is created; `--silent --log <log> --config <file> <path>` makes
`Context.ConfigFile` equal the named file, and after `Program.Run` the log contains
`custom.yaml:1:1: config-error Unknown setting` followed by the offending name, and the exit code is 1.

**No-config flag ignores an invalid configuration file**: An invalid `.lintmark.yaml` sits beside a valid Markdown
file; `--no-config` makes `Context.NoConfig` true, and after `Program.Run` the exit code is 0 and the log contains
no `config-error`.

#### Boundary conditions

**Concurrent writers keep lines intact**: Many threads write lines and errors to one silent `Context` with a log
file; all 200 lines are present and intact in the log and the exit code is 1.

#### Error handling

**Error output goes to standard error and sets the exit code**: A `Context` created with no arguments writes a
known message with `WriteError`; standard error receives it and the exit code is non-zero.

**Unknown flag is rejected with exit code 1**: `["--unknown-flag"]` passed to `Program.Main` gives exit code 1
and an error on standard error that names the flag.

**Undecodable file is reported and exits non-zero**: A file that is not valid UTF-8 is passed with `--silent` and
`--log`; the log contains `bad.md:1:2: file-encoding-error Invalid UTF-8 byte sequence` and the exit code is 1.

**Validate combined with paths is rejected**: `["--validate", "docs"]` passed to `Program.Main` gives exit code 1
and an error on standard error that names `--validate`.

**Validate combined with the config option is rejected**: `["--validate", "--config", "a.yaml"]` passed to
`Program.Main` gives exit code 1 and an error on standard error that names `--config`.

**Validate combined with check is rejected**: `["--validate", "--check"]` passed to `Program.Main` gives exit code 1
and an error on standard error that names `--check`.

**Validate combined with fix is rejected**: `["--validate", "--fix"]` passed to `Program.Main` gives exit code 1 and
an error on standard error that names `--fix`.

**Validate combined with diff is rejected**: `["--validate", "--diff"]` passed to `Program.Main` gives exit code 1 and
an error on standard error that names `--diff`.

**Validate combined with the no-config flag is rejected**: `["--validate", "--no-config"]` passed to `Program.Main`
gives exit code 1 and an error on standard error that names `--no-config`.

**Config option combined with the no-config flag is rejected**: `["--config", "a.yaml", "--no-config"]` passed to
`Program.Main` gives exit code 1 and an error on standard error that names `--no-config`.

**Fix combined with check is rejected**: `["--fix", "--check"]` is rejected by `Context.Create` with
`ArgumentException`, and passed to `Program.Main` gives exit code 1 and an error on standard error that contains
`--fix cannot be combined with --check or --diff`.
