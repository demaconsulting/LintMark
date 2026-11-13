### TextReporter

#### Verification Approach

`TextReporter` is verified with unit tests in `TextReporterTests.cs`. The real `Context` is used as the receiver, with
real `Diagnostic` values whose paths lie inside a temporary directory (the `TemporaryDirectory` test helper). The run
helper creates the `Context` with the `--log` option, and with `--silent` when the test needs a quiet console. It
replaces the console error and output streams with string writers, calls `Report`, and returns four things: the exit
code of the context, the lines of the log file, and the text that reached the error console and the standard output
console. This makes every observable effect of the unit visible without a double for the `Context`. The tests give
the reporter diagnostics in controlled orders (unsorted, reversed, rotated, with repeated members, and with ties on
every member in turn), and compare the lines with the exact expected text. The internal `GetDisplayPath` function is
called directly with a base directory inside the temporary directory. The tests change the console streams and are
therefore run sequentially with the other tests that touch process-wide state.

#### Test Environment

Temporary directories are created and removed by each test. The tests restore the console streams before they
finish. Paths are built from the temporary directory with forward slashes, and the expected lines use forward slashes
whatever the platform separator is.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Each distinct diagnostic is written once on its own line as `path:line:col: rule-id message`.
- The path is relative to the base directory with forward slashes, also for nested folders and for a file outside the
  base directory, where it starts with `../`.
- Lines are ordered by path (ordinal), then line (numeric), column (numeric), rule identifier (ordinal), and message
  (ordinal), and the output is the same for every order of the input.
- Nothing is written, and the exit code stays 0, when there are no diagnostics.
- The lines reach the log file and the error console, never standard output, and with `--silent` only the log file,
  while the exit code is 1 for any diagnostic of any kind.
- A null list, a null element, a null base directory, and a null context throw `ArgumentNullException`, and a null
  element is refused before any line is written.

#### Test Scenarios

##### Normal operation

**A diagnostic is written with path, line, column, rule, and message**: One diagnostic for a file in the base
directory is reported; the log holds the single line `a.md:3:5: heading-style Use an ATX heading.`.

**A nested path uses forward slashes**: A diagnostic for a file two folders deep is written with the path
`docs/guide/a.md`.

**A path outside the base directory keeps its parent parts**: A diagnostic for a file next to the base directory is
reported relative to a sibling directory; the path starts with `../`.

**The display path is relative with forward slashes**: The display path function gives `docs/a.md` for a file inside
the base directory and `../other/b.md` for a file beside it.

**Unsorted input is written in path then position order**: Diagnostics for `sub/c.md`, `a.md` at several positions,
and `B.md` are reported out of order; the capital letter sorts before the lowercase ones (ordinal), line 2 sorts
before line 10 (numeric), and columns break ties.

**Findings at one position are ordered by rule identifier**: Three rules at the same position, given out of order,
are written in alphabetical order of the rule identifier.

**Findings that differ only in message are ordered by message**: Three messages that differ in letter and case, at
the same position and rule, are written in ordinal order, so capital letters come first.

**Columns are ordered numerically**: Two findings on one line, at columns 10 and 3, are written with column 3 first.

**The order of the input does not change the output**: A set of diagnostics that includes an equal pair is reported in
the given order, in reverse order, and in a rotated order; the three logs are identical.

**Equal diagnostics are written once**: The same finding given twice, as two separate but equal instances, is written
as one line.

**The lines reach the log and the error console**: Two diagnostics are reported to a context that is not silent; the
log holds both lines in order, the error console shows both, and nothing is written to standard output.

**Any kind of diagnostic makes the exit code one**: A formatting diagnostic and a lint-only diagnostic are each
reported; in both cases the exit code is 1.

##### Boundary conditions

**No diagnostics write nothing**: An empty list is reported to a context that is not silent; the log and the error
console are empty and the exit code is 0.

**A silent run still fails**: One diagnostic is reported to a silent context; nothing reaches the console, the line
is in the log, and the exit code is 1.

##### Error handling

**A null element is refused before anything is written**: A list with a valid diagnostic followed by null throws
`ArgumentNullException`, and the exit code is still 0, which shows that the valid diagnostic was not written either.

**Null arguments are refused**: A null list, a null base directory, and a null context each throw
`ArgumentNullException`.
