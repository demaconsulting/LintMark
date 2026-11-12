### LintMarkEngine

![Engine Structure](EngineView.svg)

#### Purpose

`LintMarkEngine` discovers the files selected on the command line, processes them in parallel, and
reports read, decoding, and parse failures through the `Context` in a deterministic order.

#### Data Model

**ReadErrorRuleId**: `file-read-error`. **ParseErrorRuleId**: `file-parse-error`. The encoding and
line-ending identifiers come from `SourceDocumentException`.

**_maxDegreeOfParallelism**: `int` — at least one; defaults to the processor count.

**_fileProcessor** and **_documentParser**: the replaceable per-file work and parse step (test seams
supplied through the internal constructor parameters `fileProcessor` and `documentParser`).

**FileFailure**: A record `(string RuleId, int Line, int Column, string Message)`.

**FileResult**: A record `(string Path, ImmutableArray<FileFailure> Failures)`; the failure list is empty for
a file that was read and parsed, and cannot be changed by its holders.

The instance holds only immutable configuration, so `Run` and `Process` can be called concurrently.

Failure catalog:

- **`file-read-error`** at 1:1: `File not found` (file or directory missing), `Access to the file is denied`,
  or `Cannot read file: {detail}`.
- **`file-encoding-error`** at the first bad byte, or at 1:1 for a UTF-16 or UTF-32 byte order mark: the
  message comes from `SourceDocumentException`.
- **`file-line-ending-error`** at the first bare carriage return: the message comes from
  `SourceDocumentException`.
- **`file-parse-error`** at 1:1: `Markdown parser failed: {ExceptionType}`.

Line breaks in messages are replaced by spaces so that a failure is always one line.

#### Key Methods

**Run**: Discovers, processes, and reports. Two overloads: `Run(Context)` selects the files itself and
`Run(Context, IReadOnlyList<string>)` reports on files that the caller has already selected.

- *Parameters*: `Context context`; optionally the selected `files`.
- *Returns*: `void`.
- *Preconditions*: arguments are not null.
- *Postconditions*: every failure has been written with `Context.WriteError`; the exit code is 1 when at
  least one failure was reported, and unchanged otherwise.

Steps: (1) `Run(Context)` calls `Select`; (2) call `Process`; (3) pair
each result with its display path (relative to the current directory, `/` separators) and sort by display
path (ordinal); (4) for each file, order its failures by line, column, and rule identifier (ordinal) and
write `{display-path}:{line}:{col}: {rule-id} {message}`. No summary line is written. Zero matched files
is not an error.

**Select** (static): Selects the files and protects the log file.

- *Parameters*: `Context context`.
- *Returns*: `IReadOnlyList<string>` — the distinct full paths from `FileDiscovery.Discover(context.Paths,
  current directory)`.
- *Preconditions*: `context` is not null.
- *Postconditions*: nothing has been read or written, and the log file has not been touched.

When `context.LogFile` is among the selected files the method throws
`InvalidOperationException` ("Log file '{path}' is also selected as an input file; refusing to overwrite it.
Choose a different --log path or exclude the file with a ! pattern."). Paths are compared by real path
(`ResolveLinks`): every symbolic link or junction along each path, including the last segment, is replaced by its
target (directories are resolved once and cached), and the results are compared with
`FileDiscovery.PathComparison`. This
matters because the same file can be named through a link: macOS temporary folders live below `/var`, a link to
`/private/var`, and the current directory is reported by its real path, so a log named through `/var` would not
equal the discovered path of the same file. Hard links cannot be recognized this way and are not detected; they
are made harmless by `Context`, which never truncates the log file in place but moves a temporary file over it,
so the other name of a hard-linked file keeps its content. A context that has written anything replaces the log
file when it is disposed, which would destroy an input that is also the log; refusing is the only outcome that
cannot lose data, and an
exclusion pattern lets the user keep the log inside the selected folders. `Program` calls `Select` before any
output for this reason.

**Process**: Processes files, possibly in parallel.

- *Parameters*: `IReadOnlyList<string> files`.
- *Returns*: `IReadOnlyList<FileResult>` sorted by path (ordinal).
- *Preconditions*: `files` is not null.

Allocates an array of results, runs `Parallel.For` with `MaxDegreeOfParallelism` set to the configured
limit, and lets every worker write only its own index. The results are then ordered by path. A replaced
file processor that throws propagates its exception (wrapped in an `AggregateException` when run in
parallel).

**ProcessFile** (private): Reads one file and converts every expected failure into a `FileFailure`. Reading
the bytes catches `IOException`, `UnauthorizedAccessException`, `NotSupportedException`, and
`SecurityException` as read failures. Creating the `SourceDocument` and parsing catch
`SourceDocumentException` (precise position) and any other exception as a parse failure that names only the
exception type, so one file's failure never stops the others.

#### Error Handling

Per-file failures are data, not exceptions, and are reported through `Context.WriteError`. Argument errors
raise `ArgumentNullException`, and a degree of parallelism below one raises `ArgumentOutOfRangeException`.
An empty path argument raises `ArgumentException` from discovery; because `Context` writes happen only
after processing, nothing has been written when that happens, and `Program.Main` reports it as an error.
A log file that is one of the selected files raises `InvalidOperationException` from `Select`, likewise
before anything is written.

Workers never write output and never touch the `Context`; this is the thread-safety rule that makes the
output deterministic (see the *LintMark* system design).

#### Dependencies

- **Context** — path arguments in, failures out.
- **FileDiscovery** — file selection.
- **SourceDocument** and **DocumentParser** — decoding and parsing of each file.

#### Callers

- **Program** — `Run` calls the static `Select` before any output and then `Run(context, files)` on an engine
  with the default settings.
