## Engine

![Engine Structure](EngineView.svg)

### Overview

The `Engine` subsystem selects the Markdown files named on the command line, processes them in
parallel, and reports the files it cannot process. `FileDiscovery` expands the positional arguments of
the command line into a list of files; `LintMarkEngine` reads and parses each file with the units of the
`Document` subsystem and writes failures through the `Context`. The subsystem owns the file system
access of the tool (directory enumeration and file reading) and the format and order of the failure
report. It contains two units: `FileDiscovery` and `LintMarkEngine`.

### Interfaces

**FileDiscovery.Discover**: Expands path arguments into files.

- *Type*: In-process .NET static method.
- *Role*: Provider.
- *Contract*: Takes the positional arguments and a base directory and returns distinct full paths in
  ordinal order. See *FileDiscovery Design* for the argument rules.
- *Constraints*: Throws `ArgumentException` for an empty or whitespace-only argument. A pattern that
  matches nothing is not an error.

**LintMarkEngine.Run**: Discovers, processes, and reports.

- *Type*: In-process .NET instance method.
- *Role*: Provider.
- *Contract*: Reads `Context.Paths`, resolves them against the current directory, processes the files, and
  writes one `Context.WriteError` line per failure in a deterministic order.
- *Constraints*: Writes nothing for files that are processed successfully; never writes any file.

**LintMarkEngine.Process**: Processes a list of files.

- *Type*: In-process .NET instance method.
- *Role*: Provider.
- *Contract*: Returns one `FileResult` per input, sorted by path (ordinal), independent of completion order.
- *Constraints*: Workers never write output.

The subsystem consumes the `Context` of the `Cli` subsystem (paths in, failures out), the `Document`
subsystem (`SourceDocument` and `DocumentParser`), and the FileSystemGlobbing library.

### Design

`LintMarkEngine.Run` performs three steps: discovery, parallel processing, and sequential reporting.
Discovery returns the full paths of the files. Processing runs `Parallel.For` bounded by a maximum degree
of parallelism (the processor count by default; one means sequential, and values below one are rejected).
Each worker handles a whole file and writes only its own slot of an array indexed by the input position; no
worker touches the `Context` or writes any output. When all workers have finished, the results are sorted
by path, converted to display paths (relative to the current directory, with `/` separators), sorted by
display path (ordinal), and written one failure per line in the order line, column, rule identifier. As a
result, the console and log output and the exit code do not depend on scheduling.

The failure report line is `display-path:line:col: rule-id message`. The rule identifiers are
`file-read-error`, `file-encoding-error`, `file-line-ending-error`, and `file-parse-error`. There is no
summary line and nothing is written for a clean run.

The engine has two internal test seams, constructor parameters `fileProcessor` (replaces the per-file work,
for example to invert completion order) and `documentParser` (replaces the parse step, for example to make
the parser fail). They are not user options. The degree of parallelism is also not a user option.

In this phase nothing is ever written to a file, so the `--check` option is accepted and behaves exactly
like the default.
