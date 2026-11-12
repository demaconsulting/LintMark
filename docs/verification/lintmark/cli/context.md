### Context

#### Verification Approach

`Context` is verified with unit tests defined in `ContextTests.cs`. Because `Context` depends
only on .NET BCL types (`Console`, `StreamWriter`), no mocking or test doubles are required.
Tests call `Context.Create` with controlled argument arrays, inspect the resulting properties
and exit codes, and verify output written to captured streams and to log files in temporary locations.
The tests that capture the console run in the sequential test collection.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- All flag properties are correctly set for each recognized argument.
- `ArgumentException` is thrown for all unknown or malformed arguments.
- `ExitCode` is 1 after `WriteError` is called and 0 otherwise.
- Silent mode suppresses console output but does not affect the log file.
- Positional arguments are collected in order as paths; dash-leading tokens that are not options, and empty
  arguments, are rejected.
- `--validate` combined with paths or `--check` is rejected before the log file is opened.
- `--config` exposes the file name exactly as written (the last one when repeated) and is never taken for a path;
  a missing or blank value is rejected; `--no-config` exposes a flag; both default to "not given".
- `--config` combined with `--no-config`, and `--validate` combined with either configuration option, are rejected;
  `--help` and `--version` are accepted together with them.
- The log file is opened (and so created or truncated) when the context is created, written by `WriteLine` and
  `WriteError`, and closed by disposal; a log file that cannot be opened is reported as a wrapped
  `InvalidOperationException`.
- `Paths` cannot be modified by a caller that casts it.
- Concurrent writers never interleave lines and never lose the error state.

#### Test Scenarios

##### Normal operation

**No arguments give the default context**: All boolean flags are false, `ResultsFile` is null, `HeadingDepth` is
1, and the exit code is 0.

**Long and short version flags set the version flag**: `--version` and `-v` set `Version`.

**Help flags set the help flag**: `--help`, `-h`, and `-?` set `Help`.

**Silent flag sets silent mode**: `--silent` sets `Silent`.

**Validate flag sets validation**: `--validate` sets `Validate`.

**Check flag sets check mode**: `--check` sets `Check` and changes no other flag.

**Results flag sets the results file**: `--results output.trx` sets `ResultsFile`.

**Legacy result alias sets the results file**: `--result output.trx` sets `ResultsFile` exactly as `--results`
does.

**Depth flag sets the heading depth**: `--depth 3` sets `HeadingDepth` to 3.

**Heading depth defaults to one**: Without `--depth`, `HeadingDepth` is 1.

**Log flag opens the log file**: `--log <tmp>.log` followed by `WriteLine` creates the file with the message.

**Existing log file is truncated when the context is created**: With `--log` naming an existing file that has
content, the file is empty as soon as `Context.Create` returns (the same behavior as the other DEMA Consulting
tools), and it then holds only what the context writes.

**Log file is closed by disposal**: After `Dispose`, a further `WriteLine` leaves the log as it was, and the
log file can be deleted because its handle has been released.

**Positional arguments are collected in order**: Several non-dash arguments, including a `!` exclusion pattern,
appear in `Paths` in the order given and no flag is set.

**Paths cannot be changed through a cast**: `Paths` is not a `List<string>` or an array, and every attempt to add,
replace, remove, or clear an element through `IList<string>` throws `NotSupportedException`.

**Paths and options can be interleaved**: Options (including a `--log` value that must not be taken as a path) and
paths mixed in one list give only the positional arguments in `Paths` and the parsed options.

**Help and version are accepted together with paths**: `--help` and `--version` combined with paths do not
throw.

**Validate with its own options is accepted**: `--validate` combined with `--results`, `--depth`, and `--silent`
parses as before.

**Config option exposes the file name as written**: `--config conf/lint.yaml a.md` gives `ConfigFile` equal to
`conf/lint.yaml`, `NoConfig` false, and `Paths` holding only `a.md`, so the value is not taken for a path.

**No-config flag sets no-config**: `--no-config` sets `NoConfig`, leaves `ConfigFile` null, and adds no path.

**Repeated config option keeps the last value**: `--config first.yaml --config second.yaml` gives `ConfigFile` equal
to `second.yaml`.

**Config option and paths can be mixed in any order**: `a.md --config c.yaml --silent b.md` gives `ConfigFile` equal
to `c.yaml`, `Paths` holding `a.md` and `b.md` in that order, and `Silent` set.

**Help and version are accepted together with the configuration options**: `--help --config a.yaml` and
`--no-config --version` do not throw, and the flags and the config file are all exposed.

**Configuration options default to not given**: With no arguments `ConfigFile` is null and `NoConfig` is false.

**Output goes to the console unless silent**: `WriteLine` and `WriteError` on a non-silent context put the
message on standard output and standard error, respectively.

**Silent mode suppresses console output**: `WriteLine` and `WriteError` on a silent context write nothing to
the console.

**Errors are always written to the log file**: With `--silent --log`, `WriteError` still writes the message
to the log file.

##### Boundary conditions

**No paths give an empty path list**: Without positional arguments `Paths` is empty.

**Depth below the minimum is rejected**: `--depth 0` throws `ArgumentException`.

**Depth above the maximum is rejected**: `--depth 7` throws `ArgumentException`.

**Empty argument is rejected as unsupported**: An empty string throws `ArgumentException` with the message
"Unsupported argument ''"; it is never taken as a path.

**Lone dash and dash-leading tokens are rejected**: `-`, `-x`, and `--unknown` each throw `ArgumentException`,
because a dash-leading token is never a path.

**Error state makes the exit code one**: After `WriteError`, `ExitCode` is 1.

**Concurrent writers keep every error line intact**: Many threads call `WriteError` with long lines; every line
appears exactly once and intact in the log file, and the exit code is 1.

**Concurrent writers keep every line intact**: Many threads call `WriteLine` with long lines; every line appears
exactly once and intact in the log file, and no error is recorded.

##### Error handling

**Unknown argument is rejected**: `--unknown` throws `ArgumentException` that contains "Unsupported argument".

**Option without a value is rejected**: `--log`, `--results`, `--result`, `--depth`, and `--config` without a value
each throw `ArgumentException`; for `--config` the message says that a configuration filename argument is required.

**Blank config value is rejected like a missing one**: `--config` followed by an empty string or by spaces throws
`ArgumentException` with the same message.

**Option after the config option is rejected**: `--config --no-config`, `--config --validate`, and `--config -v` throw
`ArgumentException` with the same message instead of taking the option as the file name.

**Config combined with no-config is rejected**: `--config a.yaml --no-config` throws `ArgumentException` whose message
names both options.

**Validate combined with the config option is rejected**: `--validate --config a.yaml` throws `ArgumentException`
whose message names `--config`.

**Validate combined with the no-config flag is rejected**: `--no-config --validate` throws `ArgumentException` whose
message names `--no-config`.

**Non-integer depth is rejected**: `--depth abc` throws `ArgumentException`.

**Validate combined with a path is rejected**: `--validate` and a path throw `ArgumentException` whose message
names the flag.

**Validate combined with check is rejected**: `--validate` and `--check` throw `ArgumentException`.

**Rejected validate combination creates no log file**: With `--log` pointing at a file that does not exist, the
`--validate` conflict is raised and the log file is never created.

**Log file that cannot be opened is rejected**: `--log` with a directory, or a path in a directory that does not
exist, throws `InvalidOperationException` whose message starts with "Failed to open log file" and names the
path, and nothing is created.
