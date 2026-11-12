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

The configuration scenarios use real `ConfigLoader` instances (discovery, explicit file, and defaults-only
modes), the real `EditorConfigResolver` over real `.lintmark.yaml` and `.editorconfig` files, and either the
`Process` overload that takes a loader and a resolver, to assert on the configuration and the configuration
problems attached to each result, or `Run` through the same `Execute` helper, to assert on the written lines.
`--config` and `--no-config` are passed as command-line arguments to the `Context`. Three seams make failure
behavior reproducible on every platform: a resolver built from two delegates that throw (so that unreadable editor
settings can be provoked, and so that a scenario can prove that the resolver was or was not consulted), and the
`editorConfigFactory` constructor argument, which lets `Run` use such a resolver, or throw if it is ever asked to
create one (proof that `--no-config` never touches the editor settings). The loader is never replaced by a double,
because the engine's job is to combine the real loader's results with the files.

#### Test Environment

Temporary directories are created and removed by each test. Each temporary directory holds an empty `.git`
directory, so configuration discovery that starts inside it never reaches a `.lintmark.yaml` or `.editorconfig` file
above the system temporary directory, and every editor-settings file that a scenario writes declares `root = true`.
Tests that change the current directory or capture console output run in the sequential test collection.

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
- A file result cannot be modified through its failure list or its configuration problem list.
- Each file carries the configuration of its own nearest file, the defaults when none applies, and the one named
  with `--config` when there is one; no configuration file and no editor settings are read with `--no-config`.
- A configuration problem is written once, however many files share it, as `path:line:col: config-error message`
  with a forward-slash path relative to the current directory (including `../` paths), merged with file failures in
  path and position order, with exit code 1; the files of an invalid configuration are still read.
- A problem of a named configuration file is reported even when no Markdown file is selected.
- One worker and many workers give identical results and output.
- Supporting: editor settings that cannot be read are reported at line 1, column 1 of the Markdown file they were
  read for; this is verified without a requirement until the formatting phase re-adds one.
- Supporting: the line ending of a file is the configuration's when it gives one (the editor settings are then not
  consulted), otherwise the editor settings', otherwise unspecified, also with an explicit configuration file; `cr`
  and other unusable editor values leave it unspecified. The line ending is not observable until files are
  rewritten, so these scenarios are tests without a requirement until the formatting phase.

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

**File result failures cannot be changed through a cast**: A result built from an array that the creator later
changes keeps its original failures, and the list is not a `List<FileFailure>` or an array and throws
`NotSupportedException` for every attempted change.

**Files without a loader get the default configuration**: The `Process` overload that takes only files attaches the
built-in defaults and no configuration problem to every result, although a `.lintmark.yaml` with another line
length exists beside the files.

**Each file gets the settings of its nearest configuration**: A root configuration (line length 100), a nested one
(80), and a folder without its own are processed with four workers; the files get 100 (root), 80 (nested folder and
below), and 100 (plain folder).

**Configuration line ending wins over the editor settings (supporting)**: A configuration that chooses `lf` and
editor settings that choose `crlf` give `lf`.

**Editor settings are not consulted when the configuration gives a line ending (supporting)**: A configuration that
chooses `crlf` and a resolver whose two delegates throw give `crlf` and no configuration problem, so the resolver
was never asked.

**Editor settings fill in the line ending when the configuration gives none (supporting)**: A configuration with
only a line length of 90 and editor settings that choose `crlf` give `crlf`, and the line length is kept.

**Explicit configuration still consults the editor settings (supporting)**: A named configuration without a line
ending and editor settings that choose `crlf` give `crlf` and keep the named file's line length of 70.

**Explicit configuration is used instead of a discovered one**: An invalid discoverable file and a valid named file
give the named file's line length (77) and no configuration problem.

**Parallel processing with configurations equals sequential processing**: 60 files in four folders, below a root
configuration, a folder configuration that chooses `lf`, an invalid folder configuration, and editor settings that
choose `crlf`, are processed with one worker and with eight; the path, line length, line ending, and problem
messages of every result are identical and in the same order.

**Configuration problems of a result cannot be changed through a cast**: The problem list refuses every attempted
change through the mutable list interfaces.

**No-config option reads neither a configuration nor editor settings**: An invalid `.lintmark.yaml` and an engine
whose editor-settings factory throws when it is asked for a resolver are run with `--no-config`; the exit code is
0 and nothing is written.

**Explicit configuration still consults the editor settings during a run (supporting)**: A valid named
configuration and an engine whose resolver throws when consulted are run with `--config`; the resolver's failure is
reported as `a.md:1:1: config-error Cannot read .editorconfig settings: consulted`, which shows that it was
consulted.

**Configuration problems give identical output for any degree of parallelism (supports the ordered results)**:
Invalid configurations in several
folders (an invalid value, an unknown setting, and a conflict between a rule's legacy number and its name), forty
valid files, and one undecodable file are run with one worker and with sixteen; the exit code is 1, the lines and
the exit code are identical, no line is repeated, and there are four lines.

##### Boundary conditions

**Line ending specified nowhere stays unspecified (supporting)**: Editor settings that only give an indentation
style, with no configuration file, leave the line ending unspecified and report no problem.

**Unsupported editor value leaves the line ending unspecified (supporting)**: Editor settings that choose `cr` leave
the line ending unspecified, with no problem.

**Without a resolver the editor settings are not read (supporting)**: Editor settings that choose `crlf` have no
effect when no resolver is passed.

**Named configuration with a relative path is reported by its relative display path**: An invalid file named with
backslashes, `conf\custom.yaml`, is reported as `conf/custom.yaml:1:1: config-error Unknown setting 'bogus'` and the
exit code is 1.

**Named configuration outside the current directory is reported with a parent-relative path**: With a sub folder as
the current directory and `../other.yaml` named, the line starts with `../other.yaml:1:1`.

**Named configuration that is missing is reported even with no Markdown file selected**: `--config missing.yaml` in
an empty directory writes `missing.yaml:1:1: config-error Configuration file not found` and exits with 1.

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

**Null arguments are rejected**: Null arguments to `Run` and `Process` throw `ArgumentNullException`, and a null
loader passed to the `Process` overload that takes one throws as well (supporting for the loader).

**Invalid configuration is written as one positioned line and exits with one**: A valid file beside
`line-length: abc` gives exactly one line,
`.lintmark.yaml:1:14: config-error Invalid value 'abc' for setting 'line-length'; expected a whole number without
quotes from 0 to 100000`, and exit code 1.

**Configuration problem shared by many files is reported once**: One invalid configuration used by twelve files in
three folders, run with eight workers, gives exactly one line, `.lintmark.yaml:1:1: config-error Unknown setting
'bogus'`, and exit code 1.

**Configuration problems and file failures are merged in path and position order**: A root configuration with two
problems, a nested configuration with one, and two undecodable files give five lines in ordinal path order (the
dotted `.lintmark.yaml` first, then `a.md`, then `docs/.lintmark.yaml`, then `docs/x.md`) and, within a file, in
position order, with exit code 1.

**Files of an invalid configuration are still read and checked**: An invalid configuration and an explicitly named
missing file give the configuration problem and then `missing.md:1:1: file-read-error File not found`. At the
`Process` level, an invalid configuration leaves each result without a configuration but with one configuration
problem, while an undecodable file still reports its `file-encoding-error` and the valid file has no failure.

**Unreadable editor settings are reported at the Markdown file (supporting)**: With a resolver whose lookup throws an
`IOException`, `a.md:1:1: config-error Cannot read .editorconfig settings: disk failure` is written and the exit
code is 1; at the `Process` level the result keeps its configuration, the line ending stays unspecified, and the
problem is at line 1, column 1 of the Markdown file. These scenarios have no requirement; the formatting phase
re-adds a requirement for them together with an observable system-level behavior.
