### TextReporter

![Reporting Structure](ReportingView.svg)

#### Purpose

`TextReporter` writes diagnostics as text, one per line, in a deterministic order. It is the single place where the
format of a finding line is defined, and it replaces the ad hoc merge that the engine did before the diagnostic model
existed.

#### Data Model

`TextReporter` is a static class with no state.

**Line format**: `display-path:line:col: rule-id message`, where `display-path` is the path relative to the base
directory with `/` separators (it may start with `../` for a file outside the base directory), and line and column are
the 1-based values of the diagnostic. There is no summary line.

**Order**: display path (ordinal), then line, column, rule identifier (ordinal), and message (ordinal).

**De-duplication**: diagnostics with equal members (value equality of `Diagnostic`) are written once. A
configuration problem reached through many Markdown files, or a finding reported twice, therefore appears once.

#### Key Methods

**Report**: Writes diagnostics to the error channel of a context.

- *Parameters*: `IEnumerable<Diagnostic> diagnostics`; `string baseDirectory`; `Context context`.
- *Returns*: `void`.
- *Preconditions*: no argument is null and no element of `diagnostics` is null.
- *Postconditions*: each distinct diagnostic has been written once with `Context.WriteError`, in the order above, from
  the calling thread; when there were none, nothing was written and the exit code is unchanged.

Algorithm: (1) validate the arguments and materialize the diagnostics, checking the elements before anything is
written; (2) remove equal diagnostics; (3) pair each with its display path and sort by the order above; (4) write each
line.

**GetDisplayPath** (internal): Converts a path to the form used in reports.

- *Parameters*: `string path`; `string baseDirectory`.
- *Returns*: the path relative to the base directory with `/` as the separator on every platform.

It calls `Path.GetRelativePath` and replaces the platform's directory separator with `/`. `DiffReporter` calls it too.

#### Error Handling

`ArgumentNullException` for a null argument and for a null element, raised before any line is written, so a bad call
never produces partial output. The reporter catches nothing else: an I/O failure of the log writer propagates from the
`Context` as it does for any other output.

#### Dependencies

- **Diagnostic** (`Linting` subsystem) — the data that is reported.
- **Context** (`Cli` subsystem) — the receiver; `WriteError` also sets the exit code.
- **.NET BCL** — `Path` and LINQ ordering.

#### Callers

- **LintMarkEngine** — reports every diagnostic of a run (file findings and configuration problems) once, after all
  workers have finished and after any files have been written.
- **DiffReporter** — uses `GetDisplayPath`.
