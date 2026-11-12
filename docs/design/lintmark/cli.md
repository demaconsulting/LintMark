## Cli

![Cli Structure](CliView.svg)

### Overview

The `Cli` subsystem is the boundary between the host environment's command-line invocation and
the tool's internal logic. It owns argument parsing, output channel management, and exit-code
tracking. All other subsystems receive a `Context` object from this subsystem to read parsed
flags and write output. The `Cli` subsystem contains one unit: `Context`.

### Interfaces

**Context.Create**: Factory method that constructs a `Context` from a command-line argument array.

- *Type*: In-process .NET static method.
- *Role*: Provider.
- *Contract*: Parses `string[] args` into flag properties and, if `--log` is present, opens the log file for
  writing (an existing file is truncated). Returns a fully
  initialized `Context`. Accepts `--result` as a legacy alias for
  `--results`, `--check`, `--config <file>`, `--no-config`, and positional path arguments. Every argument that
  does not start with a dash is a path; they are exposed in order through `Paths`.
- *Constraints*: Throws `ArgumentException` for unknown or malformed arguments (every unrecognized
  dash-leading token, including a lone `-`, and an empty argument), for `--config` with a missing or blank
  filename, for `--config` combined with `--no-config`, and for `--validate` combined with a path, `--check`,
  `--config`, or `--no-config`; the combinations are raised before the log file is opened. Throws
  `InvalidOperationException` when the log file cannot be opened.

**Context.WriteLine**: Writes a message to stdout and to the log file.

- *Type*: In-process .NET instance method.
- *Role*: Provider.
- *Contract*: Writes `message` to `Console.Out` and to the log file if logging is enabled. Stdout
  output is suppressed when `Silent` is true; the log always receives the message.
- *Constraints*: Thread-safe: concurrent calls are serialized so lines are never interleaved.

**Context.WriteError**: Writes an error message and sets the error exit code.

- *Type*: In-process .NET instance method.
- *Role*: Provider.
- *Contract*: Sets `_hasErrors` to true, writes `message` in red to `Console.Error`, and writes
  to the log file if one is open. Stderr output is suppressed when `Silent` is true, but
  `ExitCode` is set to 1 regardless.
- *Constraints*: Once set, `ExitCode` cannot return to 0 within the same invocation. Thread-safe:
  concurrent calls are serialized so lines and console color changes are never interleaved.

**Context.ExitCode**: Derived property returning 0 or 1.

- *Type*: In-process .NET property.
- *Role*: Provider.
- *Contract*: Returns 1 if `WriteError` has been called at least once; returns 0 otherwise.
- *Constraints*: Read-only; reads the error flag under the output lock.

**Context.Paths** and **Context.Check**: Parsed positional arguments and the `--check` flag.

- *Type*: In-process .NET properties.
- *Role*: Provider.
- *Contract*: `Paths` lists the positional arguments in the order given (empty when none were
  given), as an immutable list; `Check` is true when `--check` was present. Both are fixed after
  `Create`.
- *Constraints*: Read-only; a caller cannot change `Paths` even by casting it. Phase 1 processing is
  identical with and without `Check`.

**Context.ConfigFile** and **Context.NoConfig**: Parsed configuration options.

- *Type*: In-process .NET properties.
- *Role*: Provider.
- *Contract*: `ConfigFile` is the filename given with `--config`, as written on the command line (a relative path
  is relative to the current directory), or `null` when the option was not given; when `--config` is repeated
  the last value wins. `NoConfig` is true when `--no-config` was present. Both are fixed after `Create`. The
  `Engine` subsystem uses them to choose between discovery, one explicit file, and the built-in defaults.
- *Constraints*: Read-only. `ConfigFile` and `NoConfig` are never both set, because `Create` rejects the
  combination.

**Context.Dispose**: Closes the log file.

- *Type*: In-process .NET method (`IDisposable`).
- *Role*: Provider.
- *Contract*: Disposes `_logWriter` (if logging is enabled) and sets it to null, which closes the log file.
  Callers must use a `using` statement to guarantee disposal.
- *Constraints*: Safe to call multiple times (idempotent after first call); takes the output lock so
  it never disposes the writer while another thread is writing.

### Design

The `Cli` subsystem contains only the `Context` unit; there is no subsystem-level code of its
own. All behavior is provided by `Context`. The `Program` unit creates a `Context` at the start
of each invocation and passes it to all other units that produce output or need the path
arguments.

The subsystem has no dependencies on other tool subsystems; it uses only .NET BCL types
(`Console`, `StreamWriter`, `System.Threading.Lock`). The configuration options (`--config`, `--no-config`) are
only parsed and validated here; selecting and reading configuration files belongs to the `Configuration` and
`Engine` subsystems.

Error handling flows from `Context.Create` to `Program.Main`: argument parsing errors propagate
as `ArgumentException`; log-file errors propagate as `InvalidOperationException`. Both are
caught and handled in `Program.Main`, which writes the error to stderr and returns exit code 1.

Because files are processed in parallel by the `Engine` subsystem, the output members are guarded
by a single lock. The engine itself reports results from one thread after its workers finish, so
the lock is a defensive guarantee for any caller that writes concurrently rather than a
requirement of the current flow. `ExitCode` logic is unchanged: 1 after any `WriteError`.

When `--log` is active, `Context` opens the log file with a `StreamWriter` (`append: false`, `AutoFlush` on) when
it is created, so an existing file at that path is truncated, exactly as in the other DEMA Consulting tools, and
holds the writer for the rest of the invocation. The `Program.Main` call site wraps the `Context` in a `using`
statement to ensure the file handle is released on exit.
