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

**_logWriter**: `StreamWriter?` — Writer of the log file, opened by `Create` with `AutoFlush` on; `null` when
logging is not active and after disposal.

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

**ConfigFile**: `string?` — Filename supplied after `--config`, as written on the command line, or `null` if the
option was not present; when the option is repeated, the last value wins. A relative path is relative to the
current directory. Never blank: a missing or blank filename is rejected by `Create`.

**NoConfig**: `bool` — `true` when `--no-config` was present: both `.lintmark.yaml` and `.editorconfig` are ignored
and the built-in defaults apply.

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
- *Postconditions*: All flag properties reflect the parsed argument state; if `--log` was supplied, the log
  file has been opened for writing (an existing file is truncated).

Delegates to the private `ArgumentParser` helper to parse flags, then rejects `--validate` combined
with `--check`, `--config`, `--no-config`, or any path, rejects `--config` combined with `--no-config`, and only
then, if `--log` was present, opens the log file with
`OpenLogFile` (`StreamWriter` with `append: false` and `AutoFlush` on, so an existing file is truncated, as in
the other DEMA Consulting tools). `--config` takes the next argument as its value (a missing or blank value is an
error) and a repeated `--config` replaces the earlier value; `--no-config` is a plain flag. The parser
treats a token that does not start with a dash as a positional
path. Every other token (including a lone `-` and the empty string) that is not a recognized
option is an unsupported argument. `-v`/`--version` and `-?`/`-h`/`--help` may appear together
with paths, `--config`, and `--no-config`; the dispatcher then ignores them. Throws `ArgumentException` for
unknown or malformed arguments and for the `--validate` and `--config`/`--no-config` combinations; throws
`InvalidOperationException` if
the log file cannot be opened.

**WriteLine**: Writes a message to standard output and to the log file.

- *Parameters*: `string message` — the message to write.
- *Returns*: `void`.
- *Preconditions*: None.
- *Postconditions*: Message is on stdout (unless `Silent`) and in the log file (if logging is enabled).

Holds `_outputLock` while writing, so concurrent calls produce whole lines in some order. The log line
is written to `_logWriter` when it is not null.

**WriteError**: Writes an error message, sets the error state, and records to the log file.

- *Parameters*: `string message` — the error message.
- *Returns*: `void`.
- *Preconditions*: None.
- *Postconditions*: `_hasErrors` is true; message is on stderr in red (unless `Silent`) and in
  the log file (if logging is enabled).

Holds `_outputLock` for the error flag, the console color change, the console write, and the log
write, so concurrent calls neither interleave lines nor tear the color change.

**Dispose**: Closes the log file.

- *Parameters*: None.
- *Returns*: `void`.
- *Preconditions*: None.
- *Postconditions*: `_logWriter` is disposed and set to null, so the log file is closed and later writes do not
  reach it.

Takes `_outputLock` so the writer is never disposed while another thread is writing to it.

#### Error Handling

`Create` throws `ArgumentException` ("Unsupported argument '{arg}'") for any unrecognized
dash-leading argument (including a lone `-`) and for an empty argument (the message is then
"Unsupported argument ''"; an empty string is never a path), ("{arg} requires {description}") for an
option whose required value is missing or not an accepted integer ("--config requires a configuration filename
argument" when the value of `--config` is missing or blank), ("--validate cannot be combined with paths, --check,
--config, or --no-config") for the self-validation conflict, and
("--config cannot be combined with --no-config") for the contradictory configuration options. It throws
`InvalidOperationException` ("Failed to open log file '{path}': {detail}", with the underlying exception as
the inner exception) when the `--log` file cannot be opened. Both exceptions propagate to
`Program.Main`, which reports them as `Error: {message}`.

`WriteLine` and `WriteError` write to the open log writer without catching its exceptions, so an I/O failure after
the log was opened (for example, a full disk) propagates as the `IOException` the writer throws, as in the other DEMA
Consulting tools. `Dispose` closes the writer and can likewise throw if the final flush fails.

#### Dependencies

- **.NET BCL** — `Console`, `StreamWriter`, and `System.Threading.Lock` are the only
  dependencies. No other tool units are used.

#### Callers

- **Program** — creates `Context` via `Context.Create` and calls `WriteLine` and `WriteError`.
- **Validation** — receives `Context` from `Program` and calls `WriteLine` and `WriteError`.
- **LintMarkEngine** — reads `Paths`, `ConfigFile`, and `NoConfig`, and reports failures with `WriteError`.
