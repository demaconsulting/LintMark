## Reporting

![Reporting Structure](ReportingView.svg)

### Overview

The `Reporting` subsystem turns the results of a run into output: the findings as text lines and, on request, the
proposed rewrites as a unified diff. It owns the format and the order of everything the tool prints about files, so
that the output never depends on the order in which files were processed or findings were produced. It does no
file-system access and holds no state. The subsystem contains two units: `TextReporter` and `DiffReporter`.

- `TextReporter` writes diagnostics, one per line, to the error channel of the `Context`.
- `DiffReporter` writes what the fix option would change as a unified diff to the standard output channel. Its
  supporting type `FileDiff` is described in *DiffReporter Design*.

### Interfaces

**TextReporter.Report**: Writes diagnostics as text.

- *Type*: In-process .NET static method.
- *Role*: Provider.
- *Contract*: Takes the diagnostics in any order, a base directory, and a `Context`; removes repeated diagnostics and
  writes each remaining one as `path:line:col: rule-id message` with `Context.WriteError`, ordered by display path,
  line, column, rule identifier, and message. The display path is relative to the base directory with `/` separators
  (a path outside it keeps its `../` parts).
- *Constraints*: Throws `ArgumentNullException` for a null argument or a null element. Writes nothing, and leaves the
  exit code unchanged, when there are no diagnostics; every line written makes the exit code 1.

**DiffReporter.Report**: Writes unified diffs.

- *Type*: In-process .NET static method.
- *Role*: Provider.
- *Contract*: Takes a `FileDiff` for each changed file in any order, a base directory, and a `Context`, and writes the
  diffs to standard output through `Context.WriteLine`, files in display-path order. A file with no change writes
  nothing. The output applies cleanly with a standard patch tool.
- *Constraints*: Throws `ArgumentNullException` for a null argument and `ArgumentException` for a default `FileDiff`.
  Writing a diff never changes the exit code.

The subsystem consumes the `Cli` subsystem (the `Context` that receives the lines), the `Linting` subsystem
(`Diagnostic`), and the `Document` subsystem (`LineEnding` and `LineEndingInfo`, for the line-ending note). It is
consumed by the `Engine` subsystem.

### Design

The two reporters share one rule: **the report is a pure function of its input**. Neither keeps state, neither reads
the file system, and both order what they print by values that the reader sees (display path, position, rule
identifier, message) rather than by processing order. The engine reports from a single thread after every worker has
finished, so the console and the log receive the same lines in the same order for any degree of parallelism.

The two channels have different purposes. Findings are errors: they go to the error channel and set the exit code,
so a run that finds anything fails. A diff is information: it goes to standard output, is suppressed by `--silent`
like all console output (the log file still receives it), and never changes the exit code. A diff run therefore fails
because of the findings it reports, and not because it printed a diff. `DiffReporter` reuses the display path
function of `TextReporter`, so both reporters name a file the same way.

Neither reporter decides whether a file is written; the diff is for display only. The decision to write a file is made
by the engine from the safety checks and never depends on a diff.

Thread safety: both reporters are static and stateless; the `Context` serializes its own output.
