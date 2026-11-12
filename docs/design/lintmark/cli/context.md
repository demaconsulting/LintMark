### Context

![Cli Structure](CliView.svg)

#### Purpose

`Context` handles command-line argument parsing and program output for one tool invocation. Its
single responsibility is to parse the argument list, expose the parsed flags and positional paths as
read-only properties, own the two output channels (console and log file), and derive the exit code
from whether any errors were reported. Its output members are safe to call from several threads.

#### Data Model

**_outputLock**: `System.Threading.Lock` — Guards console output, log file output, the error flag,
and disposal so that concurrent callers never interleave lines.

**_logWriter**: `StreamWriter?` — Writer of the temporary log file; `null` until the first write opens it, after
disposal, and when logging is not active.

**_logTemporaryFile**: `string?` — Path of the temporary file (`.{log file name}.{guid}.tmp` in the log file's
directory) that holds the log until `Dispose` moves it over the log file.

**_disposed**: `bool` — Set by `Dispose`; once set, nothing is opened again. Guarded by
`_outputLock`.

**_hasErrors**: `bool` — Set to `true` on the first `WriteError` call; once set, cannot return
to `false` within the same invocation. Always read and written under `_outputLock`.

**Version**: `bool` — `true` when `-v` or `--version` was present in the argument list.

**Help**: `bool` — `true` when `-?`, `-h`, or `--help` was present in the argument list.

**Silent**: `bool` — `true` when `--silent` was present in the argument list.

**Validate**: `bool` — `true` when `--validate` was present in the argument list.

**Check**: `bool` — `true` when `--check` was present in the argument list. In this phase it is an
explicit statement of the default behavior and does not change processing.

**Paths**: `IReadOnlyList<string>` — The positional arguments (files, directories, globs, and
`!` exclusions) in the order given; empty when none were supplied. Backed by an immutable array, so a caller
cannot change it by casting.

**LogFile**: `string?` — The full path of the log file, or `null` when `--log` was not present. The file
itself is not created or changed until `Dispose`, and then only if a line was written.

**ResultsFile**: `string?` — Path supplied after `--results` or `--result`, or `null` if
neither flag was present.

**HeadingDepth**: `int` — Heading depth for markdown output; valid range 1–6, default 1;
supplied via `--depth`.

**ExitCode**: `int` (derived) — Returns 1 if `_hasErrors` is true; returns 0 otherwise. The flag
is read under `_outputLock`.

#### Key Methods

**Create**: Factory method that parses arguments and returns a fully initialized `Context`.

- *Parameters*: `string[] args` — raw command-line argument array.
- *Returns*: `Context` — a new instance with all flags set.
- *Preconditions*: `args` is not null.
- *Postconditions*: All flag properties reflect the parsed argument state; `LogFile` is the full log
  path if `--log` was supplied. The log file has not been created or changed.

Delegates to the private `ArgumentParser` helper to parse flags, then rejects `--validate` combined
with `--check` or with any path, and only then, if `--log` was present, resolves the log path with
`ResolveLogFile`: the path must be valid, must not name a directory, and its directory must exist.
The file is deliberately not opened here, because replacing it would destroy an input that is also the log, and the
caller must be able to refuse such a run first (`LintMarkEngine.Select`). The parser
treats a token that does not start with a dash as a positional
path. Every other token (including a lone `-` and the empty string) that is not a recognized
option is an unsupported argument. `-v`/`--version` and `-?`/`-h`/`--help` may appear together
with paths; the dispatcher then ignores the paths. Throws `ArgumentException` for unknown or
malformed arguments and for the `--validate` combination; throws `InvalidOperationException` if
the log file path is unusable.

**WriteLine**: Writes a message to standard output and to the log file.

- *Parameters*: `string message` — the message to write.
- *Returns*: `void`.
- *Preconditions*: None.
- *Postconditions*: Message is on stdout (unless `Silent`) and in the log file (if logging is enabled).

Holds `_outputLock` while writing, so concurrent calls produce whole lines in some order. The log line
goes through the private `WriteToLog`, which on first use opens the temporary file (`OpenLogFile`, `AutoFlush`
on) next to the log file, and does nothing when logging is not enabled or after disposal. The log file itself is
not opened, so it is never truncated in place.

**WriteError**: Writes an error message, sets the error state, and records to the log file.

- *Parameters*: `string message` — the error message.
- *Returns*: `void`.
- *Preconditions*: None.
- *Postconditions*: `_hasErrors` is true; message is on stderr in red (unless `Silent`) and in
  the log file (if logging is enabled).

Holds `_outputLock` for the error flag, the console color change, the console write, and the log
write, so concurrent calls neither interleave lines nor tear the color change.

**Dispose**: Closes the log and replaces the log file with it.

- *Parameters*: None.
- *Returns*: `void`.
- *Preconditions*: None.
- *Postconditions*: `_disposed` is true; `_logWriter` is disposed and set to null; if a line was written, the
  temporary file has been moved over the log file (`CommitLog`: `File.Move` with overwrite). Moving replaces the
  directory entry instead of truncating the old file, so another name for that file (a hard link) is unchanged
  and a symbolic link is replaced, not followed. Later writes do nothing.

Takes `_outputLock` so the writer is never disposed while another thread is writing to it.

#### Error Handling

`Create` throws `ArgumentException` ("Unsupported argument '{arg}'") for any unrecognized
dash-leading argument (including a lone `-`) and for an empty argument (the message is then
"Unsupported argument ''"; an empty string is never a path), ("{arg} requires {description}") for an
option whose required value is missing or not an accepted integer, and
("--validate cannot be combined with paths or --check") for the self-validation conflict. It throws
`InvalidOperationException` ("Failed to open log file '{path}': {detail}") when the `--log` path is
unusable (invalid, a directory, or in a missing directory). Both exceptions propagate to
`Program.Main`, which reports them as `Error: {message}`.

`WriteLine` and `WriteError` write to whichever output channels are available. The one exception is
the first write to the log: if the temporary log file cannot be created (for example, access is denied) it throws
`InvalidOperationException` with the same message, which likewise propagates to `Program.Main`.

`Dispose` throws `InvalidOperationException` ("Failed to write log file '{path}': {detail} The log was left in
'{temporary path}'.") when the log file cannot be replaced; the log is then still in the temporary file.
Otherwise it does not throw.

#### Dependencies

- **.NET BCL** — `Console`, `StreamWriter`, `Path`, and `System.Threading.Lock` are the only
  dependencies. No other tool units are used.

#### Callers

- **Program** — creates `Context` via `Context.Create` and calls `WriteLine` and `WriteError`.
- **Validation** — receives `Context` from `Program` and calls `WriteLine` and `WriteError`.
- **LintMarkEngine** — reads `Paths` and `LogFile` (to refuse a log file that is also an input) and
  reports failures with `WriteError`.
