### LintMarkEngine

![Engine Structure](EngineView.svg)

#### Purpose

`LintMarkEngine` discovers the files selected on the command line, processes them in parallel, determines the
effective configuration of each file, and reports read, decoding, parse, and configuration failures through the
`Context` in a deterministic order.

#### Data Model

**ReadErrorRuleId**: `file-read-error`. **ParseErrorRuleId**: `file-parse-error`. The encoding and
line-ending identifiers come from `SourceDocumentException`; the configuration identifier is
`ConfigLoader.ErrorRuleId` (`config-error`).

**_maxDegreeOfParallelism**: `int` — at least one; defaults to the processor count.

**_fileProcessor**, **_documentParser**, and **_editorConfigFactory**: the replaceable per-file work, parse step,
and editor-settings resolver creation (test seams supplied through the internal constructor parameters
`fileProcessor`, `documentParser`, and `editorConfigFactory`; the last defaults to creating a resolver that reads
the real file system).

**FileFailure**: A record `(string RuleId, int Line, int Column, string Message)`.

**FileResult**: A record `(string Path, ImmutableArray<FileFailure> Failures, LintMarkConfig? Config = null)` with
an init-only property `ConfigErrors` (`ImmutableArray<ConfigError>`, empty by default). The failure list is empty
for a file that was read and parsed, and cannot be changed by its holders. `Config` is the effective
configuration of the file (the configuration that applies to it with the line ending filled in from the editor
settings when the configuration gives none), or `null` when the file's configuration is invalid or has not been
attached. `ConfigErrors` holds the configuration problems that apply to the file and is empty when its
configuration is valid. The added members are optional, so existing two-argument construction is unchanged.

**ReportLine**: A private record `(Path, Line, Column, RuleId, Message)` used to merge file failures and
configuration problems before they are sorted and written.

The instance holds only immutable configuration, so `Run` and `Process` can be called concurrently.

Failure catalog:

- **`file-read-error`** at 1:1: `File not found` (file or directory missing), `Access to the file is denied`,
  or `Cannot read file: {detail}`.
- **`file-encoding-error`** at the first bad byte, or at 1:1 for a UTF-16 or UTF-32 byte order mark: the
  message comes from `SourceDocumentException`.
- **`file-line-ending-error`** at the first bare carriage return: the message comes from
  `SourceDocumentException`.
- **`file-parse-error`** at 1:1: `Markdown parser failed: {ExceptionType}`.
- **`config-error`** at the position of the problem in the configuration file, with the message of the
  `ConfigError` (see *ConfigLoader Design*), or at 1:1 of the Markdown file with the message
  `Cannot read .editorconfig settings: {detail}` (see *EditorConfigResolver Design*).

Line breaks in messages are replaced by spaces so that a failure is always one line.

#### Key Methods

**Run**: Discovers, processes, and reports.

- *Parameters*: `Context context`.
- *Returns*: `void`.
- *Preconditions*: `context` is not null.
- *Postconditions*: every failure and every distinct configuration problem has been written with
  `Context.WriteError`; the exit code is 1 when at least one was reported, and unchanged otherwise.

Steps: (1) discover the files with `FileDiscovery.Discover(context.Paths, current directory)`; (2) select the
configuration sources: a defaults-only loader and no resolver for `--no-config`, a loader for exactly the named
file for `--config`, otherwise a discovering loader; a resolver is created (through the factory) unless
`--no-config` was given; (3) call the `Process` overload that takes the loader and resolver; (4) build one report
line for every failure of every file, and one for every distinct configuration problem (the `ConfigErrors` of all
files together with the loader's initial errors, de-duplicated by value, with the rule identifier `config-error`);
(5) pair each line with its display path (relative to the current directory, `/` separators) and sort by
display path (ordinal), line, column, rule identifier (ordinal), and message (ordinal); (6) write
`{display-path}:{line}:{col}: {rule-id} {message}` for each line. No summary line is written. Zero matched files
is not an error, and the problems of an explicit configuration file are reported even then.

**Process (files)**: Processes files with the built-in defaults.

- *Parameters*: `IReadOnlyList<string> files`.
- *Returns*: `IReadOnlyList<FileResult>` sorted by path (ordinal).
- *Preconditions*: `files` is not null.

Equivalent to the overload below with a defaults-only loader and no resolver, so no configuration source is
read. This keeps the behavior and the signature that existed before configuration was added.

**Process (files, loader, editorConfig)**: Processes files and attaches the effective configuration of each.

- *Parameters*: `IReadOnlyList<string> files`; `ConfigLoader loader`; `EditorConfigResolver? editorConfig` (null
  ignores the editor settings).
- *Returns*: `IReadOnlyList<FileResult>` sorted by path (ordinal).
- *Preconditions*: `files` and `loader` are not null.

Allocates an array of results, runs `Parallel.For` with `MaxDegreeOfParallelism` set to the configured
limit, and lets every worker write only its own index: the worker processes the file and then attaches its
configuration. The results are then ordered by path. A replaced
file processor that throws propagates its exception (wrapped in an `AggregateException` when run in
parallel).

**ProcessFile** (private): Reads one file and converts every expected failure into a `FileFailure`. Reading
the bytes catches `IOException`, `UnauthorizedAccessException`, `NotSupportedException`, and
`SecurityException` as read failures. Creating the `SourceDocument` and parsing catch
`SourceDocumentException` (precise position) and any other exception as a parse failure that names only the
exception type, so one file's failure never stops the others.

**AttachConfiguration** (private): Attaches the effective configuration to a file's result.

- *Parameters*: the result of `ProcessFile`, the loader, and the optional resolver.
- *Returns*: the result with `Config` or `ConfigErrors` set.

Steps: (1) `loader.Load(path)`; when it returns errors the result gets `Config` null and those errors, and the
editor settings are not consulted; (2) when the configuration has no `EndOfLine` and a resolver is present,
`Resolve(path)`; (3) when the resolver returns a message, the result keeps the configuration without a line ending
and gets one `ConfigError` at line 1, column 1 of the Markdown file; (4) when it returns a line ending, the
configuration is copied with that line ending using a `with` expression on the immutable record; (5) otherwise the
configuration is attached unchanged. The precedence is therefore: the configuration's `end-of-line`, then the
editor settings, then unspecified. Nothing consumes the line ending before the formatter exists, so this
precedence is verified by tests that have no requirement in this phase; the formatting phase adds the requirements
that make it observable.

#### Error Handling

Per-file failures are data, not exceptions, and are reported through `Context.WriteError`. Configuration
problems are data too: the loader and the resolver never throw for environment or content problems, so an invalid
configuration never stops the other files, and never stops the file it applies to from being read and parsed.
Argument errors raise `ArgumentNullException`, and a degree of parallelism below one raises
`ArgumentOutOfRangeException`.
An empty path argument raises `ArgumentException` from discovery; because `Context` writes happen only
after processing, nothing has been written when that happens, and `Program.Main` reports it as an error.
An explicit `--config` file is read when the loader is created, before processing starts; its problems do not
raise but are reported with the others.

Workers never write output and never touch the `Context`; this is the thread-safety rule that makes the
output deterministic (see the *LintMark* system design).

#### Dependencies

- **Context** — path and configuration arguments in, failures out.
- **FileDiscovery** — file selection.
- **SourceDocument** and **DocumentParser** — decoding and parsing of each file.
- **ConfigLoader** and **ConfigLoadResult** — the configuration that applies to each file and its problems.
- **EditorConfigResolver** and **EditorConfigResult** — the line ending from the editor settings.
- **LintMarkConfig** and **ConfigError** — the effective configuration and the problem entries it carries.

#### Callers

- **Program** — `Run` calls `Run(context)` on an engine with the default settings.
