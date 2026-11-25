## Program

![LintMark Structure](LintMarkView.svg)

### Purpose

`Program` is the entry point of the tool. Its single responsibility is to hand the `help` command to `HelpCommand`, and
otherwise to create a `Context` from the command-line arguments, dispatch to the one action those arguments select
(version, help, self-validation or linting), turn expected errors into a message and exit code 1, and return the exit
code that the context accumulated.

### Data Model

**Version**: a static string property. It reads `AssemblyInformationalVersionAttribute` of the executing assembly on
every access, falls back to the assembly version, and finally to the placeholder `0.0.0`. Nothing is cached.

`Program` holds no other state: it is a static class, and everything that varies per run lives in the `Context`.

### Key Methods

**Main(args)**: the process entry point; returns an `int` exit code.

1. Offers the arguments to `HelpCommand.Default.TryRun`. When the first argument is `help`, the help command answers
   and `Main` returns its exit code (see *Help command* in the system design); `help` is a command and not an option,
   so `Context.Create` would take it for a path. Any other first argument continues with the next step.
2. Creates the context with `Context.Create(args)` inside a `using`, so the log file is closed on every path.
3. Calls `Run(context)`.
4. Returns `context.ExitCode`, which is 0 unless something was written as an error, and then 1.

**Run(context)**: dispatches in priority order and executes only the first match.

1. `--version`: writes the version string alone, without the banner, and returns.
2. Otherwise prints the banner (tool name with version, the copyright line, a blank line).
3. `--help`: prints the usage text (the usage lines `lintmark [options] [paths...]` and `lintmark help [topic]`, the
   paths, then every option, and finally `HelpCommand.Default.Footer`, the pointer to the help command) and returns.
4. `--validate`: calls `Validation.Run(context)` and returns.
5. Otherwise creates a `LintMarkEngine` and calls its `Run(context)`, which lints, fixes or diffs the selected files.

**PrintBanner** and **PrintHelp** write only through `context.WriteLine`, so `--silent` and `--log` apply to them. The
usage text is the user-visible description of the options parsed by `Context`; the two must be kept in step.

### Error Handling

`Main` distinguishes four cases:

- The `help` command: its exit code is returned as is. The help command writes its own answers and errors to the
  console, so `--silent` and the log file do not apply to it.
- `ArgumentException` (an invalid command line, including an invalid rule list for `--enable` or `--disable`) and
  `InvalidOperationException` (the log file cannot be opened): the message is written to standard error as
  `Error: {message}` and `Main` returns 1, with no stack trace.
- Any other exception is unexpected: `Unexpected error: {message}` is written to standard error and the exception is
  re-thrown so that the runtime records it in the event log; the process then ends with the runtime's failure code.
- Findings of a lint run are not exceptions. They are written through `Context.WriteError`, which sets the exit code
  to 1, so a run with any finding returns 1 and a clean run returns 0.

Because the console messages of `Main` go straight to standard error, they ignore `--silent` and the log file.

### Dependencies

`HelpCommand` (the `help` command, and the footer of the usage text), `Context` (arguments and output),
`LintMarkEngine` (the linting run) and `Validation` (self-validation). The version comes from the assembly metadata
through reflection.

### Callers

The .NET runtime calls `Main`. `Validation` calls `Run` and `Version` to exercise the version and help actions of the
tool itself, and `HelpCommand` reads `Version` for the version that the help shows.
