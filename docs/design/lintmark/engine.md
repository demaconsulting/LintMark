## Engine

![Engine Structure](EngineView.svg)

### Overview

The `Engine` subsystem selects the Markdown files named on the command line, processes them in
parallel, determines the effective configuration of each file, and reports the files it cannot process and
the configuration problems it finds. `FileDiscovery` expands the positional arguments of
the command line into a list of files; `LintMarkEngine` reads and parses each file with the units of the
`Document` subsystem, asks the units of the `Configuration` subsystem for the configuration that applies to it,
and writes failures through the `Context`. The subsystem owns the file system
access of the tool (directory enumeration and file reading) and the format and order of the report. It
contains two units: `FileDiscovery` and `LintMarkEngine`.

### Interfaces

**FileDiscovery.Discover**: Expands path arguments into files.

- *Type*: In-process .NET static method.
- *Role*: Provider.
- *Contract*: Takes the positional arguments and a base directory and returns distinct full paths in
  ordinal order. See *FileDiscovery Design* for the argument rules.
- *Constraints*: Throws `ArgumentNullException` for a null argument or a null element, and `ArgumentException`
  for an empty or whitespace-only argument. A pattern that matches nothing is not an error. Either separator
  style is accepted on every platform.

**LintMarkEngine.Run**: Discovers, processes, and reports.

- *Type*: In-process .NET instance method.
- *Role*: Provider.
- *Contract*: Discovers the files named by `Context.Paths` against the current directory, selects the
  configuration sources from `Context.NoConfig` and `Context.ConfigFile`, processes the files, and writes one
  `Context.WriteError` line per failure and per distinct configuration problem in a deterministic order.
- *Constraints*: Writes nothing for files that are processed successfully with a valid configuration; never
  writes any file.

**LintMarkEngine.Process**: Processes a list of files.

- *Type*: In-process .NET instance methods (two overloads).
- *Role*: Provider.
- *Contract*: Returns one `FileResult` per input, sorted by path (ordinal), independent of completion order.
  `Process(files)` attaches the built-in default configuration and reads no configuration source. The internal
  overload `Process(files, loader, editorConfig)` attaches the effective configuration of each file, supplied by
  a `ConfigLoader` and an optional `EditorConfigResolver`.
- *Constraints*: Workers never write output.

The subsystem consumes the `Context` of the `Cli` subsystem (paths and configuration options in, failures out),
the `Document` subsystem (`SourceDocument` and `DocumentParser`), the `Configuration` subsystem (`ConfigLoader`,
`EditorConfigResolver`, `LintMarkConfig`, `ConfigLoadResult`, `ConfigError`, and `EditorConfigResult`), and the
FileSystemGlobbing library.

### Design

`LintMarkEngine.Run` performs four steps: discovery, selection of the configuration sources, parallel
processing, and sequential reporting.
Discovery returns the full paths of the files. The configuration sources are chosen from the command line:
`--no-config` selects a defaults-only loader and no editor-settings resolver (nothing is probed); `--config <file>`
selects a loader that reads exactly that file; otherwise a discovering loader is used. The editor-settings
resolver is created unless `--no-config` was given. Processing runs `Parallel.For` bounded by a maximum degree
of parallelism (the processor count by default; one means sequential, and values below one are rejected).
Each worker handles a whole file and writes only its own slot of an array indexed by the input position; no
worker touches the `Context` or writes any output.

The worker first reads and parses the file, then attaches the effective configuration of the file:

1. The loader returns the configuration that applies to the file. If it returns problems, the file has no
   configuration (`FileResult.Config` is null), the problems are stored in `FileResult.ConfigErrors`, and the
   editor settings are not consulted. The file has still been read and parsed, so its own failures are still
   reported.
2. If the configuration gives an `end-of-line`, it is final.
3. Otherwise, when a resolver is present, the line ending from the editor settings is copied into the
   configuration with a `with` expression, so a shared configuration is never changed. When the editor settings
   cannot be read, one `ConfigError` is stored at line 1, column 1 of the Markdown file and the configuration is kept
   without a line ending. A line ending that is not specified stays unset. This report is verified by tests without a
   requirement until the formatting phase re-adds a requirement for it together with an observable system-level
   behavior.

When all workers have finished, the results are sorted by path. The failures of every file and the
distinct configuration problems (those of all files plus the initial problems of an explicit `--config` file,
which are reported even when no Markdown file is selected) are merged into one list of report lines. Each
line has the display path (relative to the current directory, with `/` separators; a configuration file outside
the current directory starts with `../`), line, column, rule identifier, and message. The list is sorted by
display path (ordinal), line, column, rule identifier (ordinal), and message (ordinal) and written one line per
problem. Configuration problems that are equal (same path, position, and message), however many files reach
them, are reported once. As a result, the console and log output and the exit code do not depend on scheduling.

The report line is `display-path:line:col: rule-id message`. The rule identifiers are
`file-read-error`, `file-encoding-error`, `file-line-ending-error`, `file-parse-error`, and `config-error`.
A configuration problem uses `config-error` with the configuration file as its path. There is no
summary line and nothing is written for a clean run.

The engine has three internal test seams, constructor parameters `fileProcessor` (replaces the per-file work,
for example to invert completion order), `documentParser` (replaces the parse step, for example to make
the parser fail), and `editorConfigFactory` (replaces the creation of the editor-settings resolver used by `Run`,
for example to simulate unreadable editor settings). They are not user options. The degree of parallelism is
also not a user option.

In this phase nothing is ever written to a file by the engine, so the `--check` option is accepted and behaves
exactly like the default.

Files from `node_modules` and `.git` folders belong to other parties and are never selected by a directory,
glob, or default expansion, including when such a folder is itself the argument or the root of a glob. The
rule lives in `FileDiscovery`; only a file that is named explicitly reaches the engine from such a folder,
and the engine processes it like any other.
