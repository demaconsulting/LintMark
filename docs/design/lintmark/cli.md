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
- *Contract*: Parses `string[] args` into flag properties and, if `--log` is present, checks the
  log file path and exposes its full path as `LogFile` without opening the file. Returns a fully
  initialized `Context`. Accepts `--result` as a legacy alias for
  `--results`, `--check`, and positional path arguments. Every argument that does not start with a
  dash is a path; they are exposed in order through `Paths`.
- *Constraints*: Throws `ArgumentException` for unknown or malformed arguments (every unrecognized
  dash-leading token, including a lone `-`, and an empty argument) and for `--validate` combined with
  a path or `--check`; the latter is raised before the log path is checked. Throws
  `InvalidOperationException` when the log file path is unusable (invalid, a directory, or in a
  missing directory). The log file is neither created nor changed by `Create`.

**Context.WriteLine**: Writes a message to stdout and to the log file.

- *Type*: In-process .NET instance method.
- *Role*: Provider.
- *Contract*: Writes `message` to `Console.Out` and to the log file if logging is enabled. Stdout
  output is suppressed when `Silent` is true; the log always receives the message. The first
  write creates a temporary file next to the log file to hold the log; the log file itself is not touched.
- *Constraints*: Thread-safe: concurrent calls are serialized so lines are never interleaved. Throws
  `InvalidOperationException` when the first write cannot create the temporary log file.

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
  `Create`. `LogFile` is the full path of the log file, or null when `--log` was not given.
- *Constraints*: Read-only; a caller cannot change `Paths` even by casting it. Phase 1 processing is
  identical with and without `Check`.

**Context.Dispose**: Closes the log and replaces the log file with it.

- *Type*: In-process .NET method (`IDisposable`).
- *Role*: Provider.
- *Contract*: Disposes `_logWriter` (if the first write opened it), sets it to null, and moves the temporary
  log file over the log file (`File.Move` with overwrite, which replaces the directory entry rather than
  truncating the old file; a link is replaced, not followed). After disposal nothing is opened again. Callers
  must use a `using` statement to guarantee disposal.
- *Constraints*: Safe to call multiple times (idempotent after first call); takes the output lock so
  it never disposes the writer while another thread is writing. Throws `InvalidOperationException` when the
  log file cannot be replaced; the log then stays in the temporary file, which the message names.

### Design

The `Cli` subsystem contains only the `Context` unit; there is no subsystem-level code of its
own. All behavior is provided by `Context`. The `Program` unit creates a `Context` at the start
of each invocation and passes it to all other units that produce output or need the path
arguments.

The subsystem has no dependencies on other tool subsystems; it uses only .NET BCL types
(`Console`, `StreamWriter`, `System.Threading.Lock`).

Error handling flows from `Context.Create` to `Program.Main`: argument parsing errors propagate
as `ArgumentException`; log-file errors propagate as `InvalidOperationException`. Both are
caught and handled in `Program.Main`, which writes the error to stderr and returns exit code 1.

Because files are processed in parallel by the `Engine` subsystem, the output members are guarded
by a single lock. The engine itself reports results from one thread after its workers finish, so
the lock is a defensive guarantee for any caller that writes concurrently rather than a
requirement of the current flow. `ExitCode` logic is unchanged: 1 after any `WriteError`.

When `--log` is active, `Context` opens a temporary file next to the log file with a `StreamWriter` at the first
write and holds it for the rest of the invocation; `Dispose` moves it over the log file. The log file itself is
therefore never truncated in place (which would destroy any input reached through a name that cannot be
recognized, a hard link for example), and `Create` only checks the
path and exposes it as `LogFile`: the caller can then compare it with the files it is about to read and
refuse the run before a source file that was also named as the log is replaced (`Program` does this
through `LintMarkEngine.Select`, before any output, because a context that has written anything replaces the
log file when it is disposed). The `Program.Main` call site wraps the `Context` in a `using`
statement to ensure the log is published and the file handle is released on exit. The log is not visible
in its final place before then; a process that is killed leaves the temporary file behind.
