## Context

![LintMark Structure](LintMarkView.svg)

### Purpose

`Context` parses the command-line arguments of one invocation and owns the program output. Its single responsibility is
to turn the argument list into immutable, validated settings, to route every line of output to the console and the
optional log file, and to remember whether an error was written so that the process can return the right exit code.

### Data Model

Settings are read-only after `Create`:

- Flags: `Version`, `Help`, `Silent`, `Validate`, `Check`, `Fix`, `Diff`, `NoConfig`.
- Values: `Paths` (ordered positional arguments), `ConfigFile` (nullable), `ResultsFile` (nullable), `HeadingDepth`
  (default 1), and `RuleOverrides` (default `RuleOverrides.None`).
- Private state: the raw `--enable` and `--disable` values, the log file name, a `StreamWriter` for the log, an error
  flag, and one lock that guards console output, log output, the error flag and disposal.

`ExitCode` is derived: 1 when an error was ever written, otherwise 0. The context produces no other value; only an
unexpected exception, which `Program.Main` re-throws, ends the process with another code.

### Key Methods

**Create(args)**: builds the context.

1. Parses the arguments left to right (see the option list below); the first invalid argument throws.
2. Rejects contradictory combinations: `--validate` with paths, `--check`, `--fix`, `--diff`, `--config`,
   `--no-config`, `--enable` or `--disable`; `--fix` with `--check` or `--diff`; `--config` with `--no-config`.
3. Calls `RuleOverrides.Create` on the collected `--enable` and `--disable` values (see *RuleCatalog Design*).
4. Opens the log file, if one was named, truncating it and flushing after every line.

**Options as parsed**:

- `-v`, `--version`; `-?`, `-h`, `--help`; `--silent`; `--validate`: flags.
- `--check`: report without writing; it is the default, so it only states the default.
- `--fix`: rewrite files and report what remains. `--diff`: print a unified diff and write nothing.
- `--config <file>`: the next token must be non-blank and must not start with `-`. `--no-config`: ignore the
  configuration and the editor settings.
- `--enable <ids>` and `--disable <ids>`: repeatable; each takes the next token, a comma-separated list of rule ids or
  legacy rule-number aliases. `--log <file>`: takes the next token. `--results <file>` (also spelled `--result`): the
  next token.
- `--depth <n>`: an integer from 1 to 6, the heading depth of the validation report.
- Any token that does not start with `-` is a path (file, directory, glob or `!` exclude). Every other dashed
  token, including a lone `-` and an empty string, throws `Unsupported argument`.

**WriteLine(message)**: under the lock, writes to standard output unless `--silent`, and always to the log file.
**WriteError(message)**: under the lock, sets the error flag (so the exit code becomes 1 even when silent), writes to
standard error in red unless `--silent`, and always to the log file. **Dispose** closes the log writer under the lock.

### Error Handling

`Create` throws `ArgumentException` for every invalid argument or combination (a missing or invalid option value,
an unsupported argument, a contradictory pair, an invalid rule list) and `InvalidOperationException`, wrapping the
original exception, when the log file cannot be opened. It never writes output itself; `Program.Main` prints these
as `Error: {message}` and returns 1. After `Create`, `WriteLine` and `WriteError` do not throw for output reasons the
unit can foresee; the lock keeps lines from different threads from interleaving.

### Dependencies

`RuleOverrides` from the `Linting` subsystem (the `RuleCatalog` unit). It uses the console and the file system only for
output and the log file.

### Callers

`Program` (creates it, dispatches on it, reads `ExitCode`), `LintMarkEngine` (paths, `ConfigFile`, `NoConfig`,
`RuleOverrides`, `Fix`, `Diff`, and the report output), the reporters (`WriteError` and `WriteLine`), and `Validation`
(`HeadingDepth`, `ResultsFile`, output, and new contexts for its own tests).
