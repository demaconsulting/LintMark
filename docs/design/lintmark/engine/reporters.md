### Reporters

![Engine Structure](EngineView.svg)

#### Purpose

`Reporters` turns the results of a run into output. `TextReporter` writes diagnostics as text lines on the error
channel; `DiffReporter` writes what `--fix` would change as a unified diff on standard output. Both are
deterministic. The supporting type `FileDiff` is described in this document.

#### Data Model

Both reporters are static and stateless. **FileDiff**: readonly record struct
`(Path, BeforeLf, AfterLf, OriginalEndings, TargetEnding)`. Both texts are LF-normalized, so a line-ending change
alone leaves them equal; the counts of original terminators and the target ending let the reporter say so.
`DiffReporter` uses three lines of context (`ContextLines`) and a search limit `MaxEditDistance` of 2000.

#### Key Methods

**TextReporter.Report(diagnostics, baseDirectory, context)**: remove equal diagnostics (`Distinct`), compute the
display path, order by display path, line, column, rule identifier, and message (all ordinal), and write
`path:line:col: rule-id message` through `Context.WriteError`, which sets the exit code to 1. Nothing is written for
no diagnostics. **GetDisplayPath(path, baseDirectory)**: relative path with forward slashes; a path outside the base
directory keeps its `../` parts, and one on another Windows drive stays absolute.

**DiffReporter.Report(diffs, baseDirectory, context)**: orders files by display path and writes each block of
`Format` through `Context.WriteLine`.

**DiffReporter.Format(diff, displayPath)**: writes nothing when neither the text nor the line endings change. Else
`--- a/path` and `+++ b/path`, then a note `\ Line endings change from X to Y` when a terminator differs from the
target ending (X may be `mixed (n CRLF, m LF)`), then hunks. The lines are split keeping terminators. `Compare` strips
the common head and tail, then runs the Myers shortest-edit-script search on whole lines. When the edit distance
exceeds the limit the middle is shown as one removal block and one addition block, still correct but not minimal.
`AddHunks` groups changes whose gaps are at most six lines into one hunk with three lines of context. Headers are
`@@ -start,count +start,count @@`; a count of one drops the comma and an empty range starts at the line before it
(GNU style). A final line without a terminator is followed by `\ No newline at end of file`.

The diff is for display only: whether a file is written never depends on it.

#### Error Handling

N/A - the reporters perform no fallible work. Null arguments are programming errors and throw; the context serializes
its own output. `GetDisplayPath` throws `ArgumentException` for an empty base directory.

#### Dependencies

`Context` (`WriteError`, `WriteLine`), `Diagnostic` (Linting), and the `LineEndingInfo` and `LineEnding` types of the
Document subsystem.

#### Callers

- `LintMarkEngine.Run` calls `TextReporter.Report` for every run and `DiffReporter.Report` when `--diff` is given.
