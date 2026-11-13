## Program

![LintMark Structure](LintMarkView.svg)

### Purpose

`Program` is the entry point and execution orchestrator for the LintMark. Its single
responsibility is to create a `Context` from the command-line arguments, dispatch to the
appropriate handler based on the parsed flags, and return the exit code.

### Data Model

**Version**: `string` (static property) — The tool version read from
`AssemblyInformationalVersionAttribute` on every access, falling back to `AssemblyVersion`, then
`"0.0.0"`. No caching is applied; callers that need the value more than once should store it
locally.

### Key Methods

**Main**: Entry point for the tool process.

- *Parameters*: `string[] args` — command-line arguments from the host environment.
- *Returns*: `int` — exit code; 0 for success, 1 for expected errors.
- *Preconditions*: None.
- *Postconditions*: Exit code reflects whether any errors were reported during execution.

Creates a `Context` using `Context.Create(args)`, calls `Run(context)`, and returns
`context.ExitCode`. Catches `ArgumentException` and `InvalidOperationException` — writes
`"Error: {message}"` to stderr and returns 1. Catches any other `Exception` — writes
`"Unexpected error: {message}"` to stderr and re-throws so the runtime can record it.

**Run**: Dispatches execution based on parsed flags.

- *Parameters*: `Context context` — the parsed context.
- *Returns*: `void`.
- *Preconditions*: `context` is not null.
- *Postconditions*: Exactly one handler has been called.

Inspects flags in priority
order: (1) if `context.Version` is true, calls `context.WriteLine(Version)` and returns; (2) calls
`PrintBanner`; (3) if `context.Help` is true, calls `PrintHelp` and returns; (4) if `context.Validate` is
true, calls `Validation.Run(context)`; (5) otherwise calls `LintMarkEngine.Run(context)`, which selects,
processes, optionally rewrites (`--fix`), and reports the files.

**PrintBanner**: Writes the tool name, version, and copyright line to `context`.

- *Parameters*: `Context context` — output target.
- *Returns*: `void`.

**PrintHelp**: Writes the usage synopsis (`Usage: lintmark [options] [paths...]`), the description
of the path arguments (including the `!` exclusion prefix and the `**/*.md` default), and the
options table (including `--check`, which reports problems without writing any file (the default); `--fix`, which
rewrites files into canonical form when every safety check passes and then reports the problems that remain (not with
`--check` or `--diff`); `--diff`, which prints a unified diff of what `--fix` would change and writes no file;
`--config <file>`, which uses a named configuration file instead of
discovering `.lintmark.yaml`, and `--no-config`, which ignores `.lintmark.yaml` and `.editorconfig` and uses the
built-in defaults) to `context`.

- *Parameters*: `Context context` — output target.
- *Returns*: `void`.

### Error Handling

`Main` handles errors at two levels. Expected errors (`ArgumentException` and
`InvalidOperationException`) are written to stderr as `"Error: {message}"` and cause exit
code 1 without a stack trace; this includes the usage errors raised by `Context.Create` when
`--validate` is combined with paths, `--check`, `--fix`, `--diff`, `--config`, or `--no-config`, when `--fix` is
combined with `--check` or `--diff`, when `--config` is combined with
`--no-config`, or when `--config` has no filename. Unexpected errors (`Exception`) are written to
stderr as `"Unexpected error: {message}"` and re-thrown so the runtime can record them in event logs.
`Run`, `PrintBanner`, and `PrintHelp` do not catch exceptions; all errors
propagate to `Main`. Failures of individual files are not exceptions: `LintMarkEngine` reports them
through `Context.WriteError`, which makes the exit code 1; so are configuration problems, which are also reported by
the engine, and so are the safety refusals and write failures of the fix option.

### Dependencies

- **Context** — `Program` reads parsed flags from `Context` and calls `Context.WriteLine` and
  `Context.WriteError` for all output.
- **Validation** — `Program.Run` calls `Validation.Run(context)` when the `--validate` flag is
  set.
- **LintMarkEngine** — `Run` calls `LintMarkEngine.Run(context)` to process the selected Markdown files; the
  engine chooses the configuration sources from the `--config` and `--no-config` options and acts on the `--fix`
  and `--diff` options.

### Callers

N/A - entry point, called by the host environment.
