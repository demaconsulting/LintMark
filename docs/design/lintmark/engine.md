## Engine

![Engine Structure](EngineView.svg)

### Overview

The `Engine` subsystem is the orchestrator of a run. It selects the Markdown files named on the command line, processes
them in parallel (configuration first, then read, parse, format, and safety checks), optionally rewrites the files that
passed every check, and reports the findings. `FileDiscovery` expands the positional arguments of the command line into
a list of files and decides which of them the `ignore` setting may skip; `LintMarkEngine` drives the per-file work with
the units of the `Document`, `Configuration`, `Formatting`, and `Safety` subsystems, writes the files, and reports
through the `Reporting` subsystem; `AtomicFileWriter` replaces the content of a file in one step. The subsystem owns the
file system access of the tool (directory enumeration, file reading, and file writing) and the order of the whole run.
It contains three units: `FileDiscovery`, `LintMarkEngine`, and `AtomicFileWriter`.

Its supporting types are described with the unit that owns them: `DiscoveredFile` with `FileDiscovery`, and
`FileResult` and `FileChange` with `LintMarkEngine`.

### Interfaces

**FileDiscovery.Discover and FileDiscovery.DiscoverFiles**: Expand path arguments into files.

- *Type*: In-process .NET static methods.
- *Role*: Provider.
- *Contract*: Take the positional arguments and a base directory and return distinct files in ordinal order of the
  full path. `Discover` returns the paths; `DiscoverFiles` returns `DiscoveredFile` values that also say whether the
  command line named the file (an explicit file). See *FileDiscovery Design* for the argument rules.
- *Constraints*: Throw `ArgumentNullException` for a null argument or a null element, and `ArgumentException` for an
  empty or whitespace-only argument. A pattern that matches nothing is not an error. Either separator style is
  accepted on every platform.

**FileDiscovery.IsIgnored**: Applies the `ignore` patterns of a configuration to a file.

- *Type*: In-process .NET static method.
- *Role*: Provider.
- *Contract*: Takes the full path of a file, the directory the patterns are relative to, and the patterns, and says
  whether the file lies below that directory and matches a pattern. Pure: no file-system access.
- *Constraints*: Throws `ArgumentNullException` for a null argument or a null element.

**LintMarkEngine.Run**: Discovers, processes, optionally rewrites, and reports.

- *Type*: In-process .NET instance method.
- *Role*: Provider.
- *Contract*: Discovers the files named by `Context.Paths` against the current directory, selects the configuration
  sources from `Context.NoConfig` and `Context.ConfigFile`, processes the files, and then: with `Context.Fix` writes
  every rewrite that passed every safety check, one file after the other, atomically; reports every diagnostic and
  every distinct configuration problem in a deterministic order through `Context.WriteError`; with `Context.Diff`
  writes a unified diff of the proposed rewrites to standard output.
- *Constraints*: Without `Context.Fix` nothing is written to disk. Writes nothing for files that are compliant with a
  valid configuration. Never writes a file that failed a safety check, that has an invalid configuration or
  unreadable editor settings, or that the parser could only read by dropping grid tables.

**LintMarkEngine.Process**: Processes a list of files.

- *Type*: In-process .NET instance methods (three overloads).
- *Role*: Provider.
- *Contract*: Return one `FileResult` per input, sorted by path (ordinal), independent of completion order.
  `Process(files)` treats every file as named on the command line and attaches the built-in default configuration
  without reading any configuration source. The internal overloads attach the effective configuration of each file,
  supplied by a `ConfigLoader` and an optional `EditorConfigResolver`; the one that takes `DiscoveredFile` values also
  applies the `ignore` patterns.
- *Constraints*: Workers never write output and never write files.

**AtomicFileWriter.Write**: Replaces the content of a file.

- *Type*: In-process .NET static method.
- *Role*: Provider.
- *Contract*: Takes the path, the resolved target that was captured when the file was read, the bytes that the new
  content was computed from, and the new content, and replaces the file in one step.
- *Constraints*: Throws when the file changed or the link was redirected after the file was read, the file is
  read-only, or it cannot be written; the file is then as it was. See *AtomicFileWriter Design*.

The subsystem consumes the `Cli` subsystem (`Context`: paths and options in, reports out), the `Document` subsystem
(`SourceDocument` and `DocumentParser`), the `Configuration` subsystem (`ConfigLoader`, `EditorConfigResolver`,
`LintMarkConfig`, `ConfigLoadResult`, `ConfigError`, and `EditorConfigResult`), the `Formatting` subsystem
(`DocumentFormatter` and `SourcePatcher`, and the three block formatters of its `Blocks` subsystem that the default
formatter is created with), the `Safety` subsystem (the three checkers, `EquivalenceResult`, and
`SafetyStatus`), the `Reporting` subsystem (`TextReporter` and `DiffReporter`), the `Linting` subsystem (`Diagnostic`
and the rule identifiers), and the FileSystemGlobbing library.

### Design

`LintMarkEngine.Run` performs five steps: discovery, selection of the configuration sources, parallel processing,
sequential writing, and sequential reporting.

Discovery returns the files, each marked as explicit or not. The configuration sources are chosen from the command
line: `--no-config` selects a defaults-only loader and no editor-settings resolver (nothing is probed); `--config
<file>` selects a loader that reads exactly that file; otherwise a discovering loader is used. The editor-settings
resolver is created unless `--no-config` was given. Processing runs `Parallel.For` bounded by a maximum degree of
parallelism (the processor count by default; one means sequential, and values below one are rejected). Each worker
handles a whole file and writes only its own slot of an array indexed by the input position; no worker touches the
`Context`, writes any output, or writes any file.

**Configuration comes first.** The worker asks the loader for the configuration of the file before it reads the
file, because the `ignore` patterns decide whether the file is read at all:

1. When the configuration is valid, the file was not named on the command line, and `FileDiscovery.IsIgnored` matches
   it, the result is marked ignored and the file is neither read nor reported nor rewritten. The patterns are relative
   to the directory of the configuration file that applies to the file (the current directory when none does). A file
   named on the command line is never ignored, the same rule that exempts it from the built-in exclusion of
   `node_modules` and `.git`; a `!` exclude on the command line still removes it.
2. When the configuration is invalid, the file has no configuration (`FileResult.Config` is null) and carries the
   problems in `FileResult.ConfigErrors`. The file is still read and parsed so its own failures are reported, but it is
   never formatted, because its `ignore` and every other setting are unknown.
3. When the configuration gives an `end-of-line`, it is final. Otherwise, when a resolver is present, the line ending
   from the editor settings is copied into the file's configuration with a `with` expression, so a shared
   configuration is never changed. Editor settings that cannot be read add one `ConfigError` at line 1, column 1 of the
   Markdown file, and the file is not formatted either, because the line ending it should get is not known.

The worker then reads, decodes, and parses the file (the engine's `documentParser` seam), formats it with
`DocumentFormatter`, applies the edits with `SourcePatcher`, and runs the safety checks on a proposed change of the
text or of the bytes: every rewrite that changes bytes is checked, also one that only changes the line endings, because
the parser renders some text differently for CRLF and LF. A read, encoding, line-ending, parse, or format failure
becomes a diagnostic for that file only. The details of
the pipeline are in *LintMarkEngine Design*.

When all workers have finished, the results are sorted by path. With `--fix` the engine writes the rewrites in path
order, one at a time from the calling thread, through `AtomicFileWriter`. Sequential writing also means that two paths
that name the same file (a hard link, for example) can never be written by two threads at once. A failed write becomes
a `file-write-error` diagnostic, leaves that file as it was, and never stops the other files. The formatting
diagnostics of a file that was written are dropped, because the rewrite removed them; those of a file that was refused
or could not be written stay.

All diagnostics and the distinct configuration problems (those of all files plus the initial problems of an explicit
`--config` file, which are reported even when no Markdown file is selected) go to `TextReporter`, which writes them in
one deterministic order. With `--diff`, `DiffReporter` then writes the unified diff of the proposed rewrites to
standard output. As a result, the console and log output, the files written, and the exit code do not depend on
scheduling.

The check mode runs the whole pipeline, including the safety checks, and only the write is gated by `--fix`, so what
the check mode reports predicts what `--fix` does. `--check` is an explicit statement of that default; `--diff` implies
that nothing is written.

The engine has five internal test seams, constructor parameters: `fileProcessor` (replaces the per-file work, for
example to invert completion order; it receives the path and the configuration the file may be formatted under),
`documentParser` (replaces the parse step, for example to make the parser fail), `editorConfigFactory` (replaces the
creation of the editor-settings resolver, for example to simulate unreadable editor settings), `formatter` (installs
block formatters, including broken ones), and `fileWriter` (replaces the atomic write, for example to simulate a failed
write or a file that changed after it was read). They are not user options. The degree of parallelism is also not a
user option.

Files from `node_modules` and `.git` folders belong to other parties and are never selected by a directory, glob, or
default expansion, including when such a folder is itself the argument or the root of a glob. The rule lives in
`FileDiscovery`; only a file that is named explicitly reaches the engine from such a folder, and the engine processes
it like any other.
