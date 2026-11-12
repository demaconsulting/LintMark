### LintMarkEngine

#### Verification Approach

`LintMarkEngine` is verified with unit tests in `LintMarkEngineTests.cs`. Real temporary files (the
`TemporaryDirectory` test helper), the real `FileDiscovery`, `SourceDocument`, and `DocumentParser`, and a real
`Context` writing to a log file are used for the normal scenarios; the run helper `Execute` makes the temporary
directory the current directory, runs the engine, and returns the exit code and the lines written. Constructor
seams isolate behavior that cannot be provoked reliably otherwise: the `documentParser` seam makes the parser
throw for chosen files (parse failure for one file only), and the `fileProcessor` seam replaces the per-file work
to delay chosen files so that completion order is the reverse of input order (ordered reporting), to make files
wait for one another (proof that files really run at the same time), and to count how many run at once.
Determinism is verified by comparing a run with one worker and a run with many workers over many generated
files, some of them failing.

#### Test Environment

Temporary directories are created and removed by each test. Tests that change the current directory or capture
console output run in the sequential test collection.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Valid files and empty selections produce no output and exit code 0.
- Missing files, directories given as files, invalid UTF-8, UTF-16, bare carriage returns, and parser failures
  are reported with the documented rule identifier, position, and message, and exit code 1.
- A failure in one file never prevents the other files from being processed.
- Results and output are ordered by path and then by position, whatever the input order, the degree of
  parallelism, or the completion order.
- Files run at the same time when parallelism allows it, and never when it is limited to one.
- Invalid constructor and method arguments are rejected.
- A log file that is among the selected files is refused before it is touched, whether it is named, found by
  discovery, or missing; excluding it with a `!` pattern, or placing it outside the selection, allows the run.
- A file result cannot be modified through its failure list.

#### Test Scenarios

##### Normal operation

**Valid files give no failures**: Valid files give results with empty failure lists.

**Empty file list gives an empty result**: Processing no files returns no results.

**Input order does not change the result order**: Shuffling the input gives the same result order.

**Results stay path-sorted when later files finish first**: With the completion order inverted by the file
processor seam, the results are still sorted by path.

**Files run at the same time when workers are available**: With four workers, files that wait for one another
complete without timing out and at least two are in flight together.

**Parallel and sequential runs of a large set give identical results**: Over at least 200 generated files,
including failing ones, one worker and many workers give identical results.

**Clean run writes nothing and exits with zero**: A run over valid files writes no line and leaves the exit code
at 0.

**Failures are written as formatted lines in path order**: Failures are written as
`path:line:col: rule-id message` with relative paths, in path order, and the exit code is 1.

**Glob without matches writes nothing and exits with zero**: A glob that selects no file is not an error.

**Check option behaves like the default**: With `--check` the output and exit code equal those of the default.

**Several failures of one file are ordered by position**: They are written ordered by line, column, and rule
identifier.

**Excluded log file lets the run proceed**: With `--log` naming a Markdown file in the selected tree and the
same file excluded with a `!` pattern, `Select` returns the other files and leaves the log file's content
intact until it is written.

**Log file outside the selection does not prevent a run**: A log whose name the selection does not match is
accepted, and `Select` does not create it.

**File result failures cannot be changed through a cast**: A result built from an array that the creator later
changes keeps its original failures, and the list is not a `List<FileFailure>` or an array and throws
`NotSupportedException` for every attempted change.

##### Boundary conditions

**One worker never runs two files at once**: A limit of one gives a maximum concurrency of one.

**Missing explicit file is reported with its relative path**: A file named on the command line that does not
exist is reported as a read failure.

##### Error handling

**Invalid UTF-8 is reported as an encoding error**: A file with invalid UTF-8 gives a `file-encoding-error` at
the position of the first bad byte.

**Bare carriage return is reported as a line-ending error**: A file with a bare carriage return gives a
`file-line-ending-error` at its position.

**UTF-16 file is reported as an encoding error**: A UTF-16 file gives a `file-encoding-error` at 1:1.

**Missing file is reported as a read error**: A missing file gives a `file-read-error` at 1:1 with the message
`File not found`.

**Directory given as a file is reported as a read error**: A path that cannot be read as a file gives a
`file-read-error`.

**Parser exception is reported for that file only**: A parser that throws for one file gives a
`file-parse-error` naming the exception type for that file, and the other files are unaffected; the same failure
is written as a `file-parse-error` line by `Run`.

**Degree of parallelism below one is rejected**: The constructor throws `ArgumentOutOfRangeException`.

**Null arguments are rejected**: Null arguments to `Run`, `Select`, and `Process` throw `ArgumentNullException`.

**Log file named as an input is refused**: With the log file named as the input, `Select` throws
`InvalidOperationException` that names the file, and the file's content is unchanged.

**Log file found by discovery is refused**: With the log file a Markdown file under the current directory and
no path arguments, `Run` throws `InvalidOperationException`, reports no failure, and leaves the file intact.

**Log file reached through a link is refused**: A log named through a symbolic link to an input file, and a log
named through a linked directory that holds the input, each make `Select` throw `InvalidOperationException` with
the input intact (skipped where the environment cannot create symbolic links). The same comparison by real path
makes the other log tests pass on macOS, where temporary folders are below the `/var` link.
