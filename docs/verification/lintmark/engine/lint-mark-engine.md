### LintMarkEngine

#### Verification Approach

`LintMarkEngine` is verified with unit tests in four files that share one partial test class:
`LintMarkEngineTests.cs` (processing, ordering, configuration, and reporting), `LintMarkEngineFixTests.cs`
(rewriting, `--fix`, `--diff`, and the safety checks), `LintMarkEngineIgnoreTests.cs` (the `ignore` patterns), and
`LintMarkEngineFormattingTests.cs` (the default formatting rules of the tool, with no seam: the engine creates its own
formatter with the three block formatters).
Real temporary files (the `TemporaryDirectory` test helper), the real `FileDiscovery`, `SourceDocument`,
`DocumentParser`, `SourcePatcher`, safety checkers, and reporters, and a real `Context` writing to a log file are
used for the normal scenarios; the run helper `Execute` makes the temporary directory the current directory, runs the
engine, and returns the exit code and the lines written. Constructor seams isolate behavior that cannot be provoked
reliably otherwise:

- The `documentParser` seam makes the parser throw for chosen files (parse failure for one file only), and, in one
  scenario, makes it return a document with a grid table fallback for the proposed text only (the parse check of
  the safety checks), or makes it throw for the proposed text only, or only on the second reading of it (a safety
  check that cannot be completed).
- The `fileProcessor` seam replaces the per-file work to delay chosen files so that completion order is the reverse
  of input order (ordered reporting), to make files wait for one another (proof that files really run at the same
  time), to count how many run at once, and to return diagnostics in scrambled order (ordering within a file).
- The `formatter` seam installs the paragraph formatters from `Formatting/TestBlockFormatters.cs`. The replacing
  formatter makes a harmless text change. The broken formatters each break one property: dropping a hard break
  (rendering changes), joining the lines of a fenced div (Pandoc structure changes), flipping the layout on every
  pass and growing the text on every pass (not idempotent), creating a grid table border, throwing, proposing
  overlapping edits, proposing an edit outside the text, and inserting a carriage return. With them the safety
  checks and the format failures are verified against the real checkers, which are never replaced.
- The `fileWriter` seam makes the write throw (for one file or for all), records the resolved target it is given, and
  changes the file, or points a link to another file, just before it delegates to the real `AtomicFileWriter` (a
  file that changed, or a link that was redirected, after it was read).
- A resolver built from two delegates that throw (so that unreadable editor settings can be provoked, and so that a
  scenario can prove that the resolver was or was not consulted), passed either to the `Process` overload that takes
  a resolver or through the `editorConfigFactory` constructor argument, which lets `Run` use it, or throw if it is
  ever asked to create one (proof that `--no-config` never touches the editor settings).

Determinism is verified by comparing a run with one worker and a run with many workers over many generated
files, some of them failing, and, for `--fix`, by comparing the bytes of every file as well.

The configuration scenarios use real `ConfigLoader` instances (discovery, explicit file, and defaults-only
modes), the real `EditorConfigResolver` over real `.lintmark.yaml` and `.editorconfig` files, and either the
`Process` overloads, to assert on the configuration, the configuration problems, the change, and the safety status
attached to each result, or `Run` through the same `Execute` helper, to assert on the written lines and the bytes of
the files. `--config`, `--no-config`, `--check`, `--fix`, and `--diff` are passed as command-line arguments to the
`Context`. The loader is never replaced by a double, because the engine's job is to combine the real loader's
results with the files. The `Process` overload with discovered files is called with explicit and non-explicit
`DiscoveredFile` values to verify the `ignore` decision without a command line.

#### Test Environment

Temporary directories are created and removed by each test. Each temporary directory holds an empty `.git`
directory, so configuration discovery that starts inside it never reaches a `.lintmark.yaml` or `.editorconfig` file
above the system temporary directory, and every editor-settings file that a scenario writes declares `root = true`.
Scenarios that prove a file was not written give it an old last write time and compare both the content and the
time afterward. Tests that change the current directory or capture console output run in the sequential test
collection.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Valid files and empty selections produce no output and exit code 0.
- Missing files, directories given as files, invalid UTF-8, UTF-16, bare carriage returns, and parser failures
  are reported with the documented rule identifier, position, and message, and exit code 1.
- A failure in one file never prevents the other files from being processed.
- Results and output are ordered by path and then by position, whatever the input order, the degree of
  parallelism, or the completion order.
- Files run at the same time when parallelism allows it, and never when it is limited to one.
- Invalid constructor and method arguments are rejected, and so are a file result or a file change that is missing a
  path, a text, a list, a bytes array, or a resolved target, or that has an undefined line ending.
- A file result cannot be modified through its diagnostic list or its configuration problem list.
- Each file carries the configuration of its own nearest file, the defaults when none applies, and the one named
  with `--config` when there is one; no configuration file and no editor settings are read with `--no-config`.
- A configuration problem is written once, however many files share it, as `path:line:col: config-error message`
  with a forward-slash path relative to the current directory (including `../` paths), merged with file diagnostics
  in path and position order, with exit code 1; the files of an invalid configuration are still read.
- A problem of a named configuration file is reported even when no Markdown file is selected.
- One worker and many workers give identical results, output, and written files.
- The line ending of a file is the configuration's when it gives one (the editor settings are then not consulted),
  otherwise the editor settings', otherwise unspecified, also with an explicit configuration file; `cr` and other
  unusable editor values leave it unspecified; the line ending is what `--fix` writes.
- Editor settings that cannot be read are reported at line 1, column 1 of the Markdown file they were read for,
  and that file is never formatted or rewritten.
- A file with an invalid configuration is read and parsed but never formatted or rewritten.
- With `--fix` a rewrite that passed every check is written with the exact bytes (byte order mark, line endings), a
  file that already complies is not touched, a second run changes nothing, and the diagnostics that the rewrite
  removed are no longer reported; without `--fix` (check mode, `--check`, `--diff`) nothing is written.
- The check mode predicts `--fix`: a rewrite that would be refused is reported as a safety diagnostic in both.
- The default engine proposes the changes of all formatting rules: with `--fix` it rewrites a file that breaks them and
  reports nothing afterwards, a second run leaves the file alone, check mode reports each violation at its place and
  writes nothing, `--diff` prints the change, a suppression by legacy number is honored, the settings of the
  configuration select the written form, and a document with an invalid grid table is never rewritten.
- A rewrite is refused, with a safety diagnostic at the first changed position, the file untouched, and exit code 1,
  when it would change the rendering, change the Pandoc structure, is not stable when formatted again, creates a
  grid table border, or would be read as an invalid grid table; a refusal never stops other files from being
  written.
- A rewrite that only converts the line endings is checked as well: when the document renders differently for the two
  line endings it is refused with a `safety-html-mismatch` diagnostic at 1:1 (in check mode and `--diff` too), the file
  and its directory are untouched, and an ordinary document is still converted.
- A safety check that cannot be completed (for example a parser that throws for the rewritten text) refuses the
  rewrite with a message that says the check failed, leaves the file untouched, and gives exit code 1.
- A formatter that throws, proposes conflicting or out-of-range edits, or inserts a carriage return gives a
  `file-format-error` naming only the exception type (or the carriage return) and leaves the file untouched.
- A failed write is a `file-write-error` for that file, keeps the formatting diagnostics of the file, leaves the file
  as it was, and never stops the other files; a file that changed after it was read, or whose link was redirected to
  another file after it was read, is not overwritten.
- The diff output is the unified diff of the proposed change, including a note for a change of line endings alone,
  and nothing for a file that would not change.
- A document with an invalid grid table is reported as `grid-table-invalid` at the offending line unless the rule is
  switched off or the line is suppressed, and is never rewritten in any case.
- A file matched by an `ignore` pattern of its configuration (relative to the folder of that configuration, or to
  the current directory) is neither read nor reported when it was found, and is processed when named explicitly; a
  file outside that folder is never ignored; a file with an invalid configuration is not ignored.
- Every rule identifier that the engine emits itself is registered in the rule catalog.

#### Test Scenarios

##### Normal operation

**Valid files give no diagnostics**: Valid files give results with empty diagnostic lists.

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

**Diagnostics are written as formatted lines in path order**: Diagnostics are written as
`path:line:col: rule-id message` with relative paths, in path order, and the exit code is 1.

**Glob without matches writes nothing and exits with zero**: A glob that selects no file is not an error.

**Check option behaves like the default**: With `--check` the output and exit code equal those of the default.

**Several diagnostics of one file are ordered by position**: With a file processor that returns four diagnostics in
scrambled order, they are written ordered by line, column, and rule identifier.

**File result diagnostics cannot be changed through a cast**: A result built from an array that the creator later
changes keeps its original diagnostics, and the list is not a `List<Diagnostic>` or an array and refuses every
attempted change through the mutable list interfaces. The same holds for the diagnostics of a result that
`Process` returned.

**Files without a loader get the default configuration**: The `Process` overload that takes only files attaches the
built-in defaults and no configuration problem to every result, although a `.lintmark.yaml` with another line
length exists beside the files.

**Each file gets the settings of its nearest configuration**: A root configuration (line length 100), a nested one
(80), and a folder without its own are processed with four workers; the files get 100 (root), 80 (nested folder and
below), and 100 (plain folder).

**Configuration line ending wins over the editor settings**: A configuration that chooses `lf` and editor settings
that choose `crlf` give `lf`.

**Editor settings are not consulted when the configuration gives a line ending**: A configuration that chooses `crlf`
and a resolver whose two delegates throw give `crlf` and no configuration problem, so the resolver was never asked.

**Editor settings fill in the line ending when the configuration gives none**: A configuration with only a line
length of 90 and editor settings that choose `crlf` give `crlf`, and the line length is kept.

**Explicit configuration still consults the editor settings**: A named configuration without a line ending and
editor settings that choose `crlf` give `crlf` and keep the named file's line length of 70.

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

**Explicit configuration still consults the editor settings during a run**: A valid named configuration and an
engine whose resolver throws when consulted are run with `--config`; the resolver's failure is reported as
`a.md:1:1: config-error Cannot read .editorconfig settings: consulted`, which shows that it was consulted.

**Configuration problems give identical output for any degree of parallelism**: Invalid configurations in several
folders (an invalid value, an unknown setting, and a conflict between a rule's legacy number and its name), forty
valid files, and one undecodable file are run with one worker and with sixteen; the exit code is 1, the lines and
the exit code are identical, no line is repeated, and there are four lines.

**Fix rewrites a CRLF file to the configured line ending**: A CRLF file and a configuration that chooses `lf` are
run with `--fix`; the file holds exactly the LF text, nothing is reported, and the exit code is 0. A CRLF file with a
byte order mark keeps the three bytes of the mark and only the line endings change.

**Fix writes the majority ending, the configured ending, or the editor settings' ending**: A file with two CRLF and
one LF terminator and no configuration is written with CRLF throughout. An LF file with `end-of-line: crlf` in the
configuration, and an LF file with `end_of_line = crlf` in the editor settings, are written with CRLF.

**Fix does not touch a compliant file, and a second run changes nothing**: A compliant file with an old last write
time keeps its bytes and its time after `--fix`. A CRLF file that a first `--fix` rewrote is not written again by a
second one (its time is unchanged) and nothing is reported.

**A written file no longer reports the violation, while the check mode does**: A CRLF file with `end-of-line: lf`
gives `a.md:1:1: line-ending Line endings are CRLF; expected LF` and exit code 1 in the check mode; `--fix` writes it,
reports nothing, and exits with 0.

**Check reports the line ending and writes nothing**: With `--check`, a CRLF file under an LF configuration gives
that one `line-ending` diagnostic, exit code 1, and the file keeps its bytes and its time. A file with two CRLF and
one LF terminator gives `a.md:1:1: line-ending Mixed line endings (2 CRLF, 1 LF); expected CRLF`.

**Diff shows the line ending change and a text change and writes nothing**: With `--diff`, a CRLF file under an LF
configuration gives the `line-ending` diagnostic, the two header lines `--- a/a.md` and `+++ b/a.md`, and the note
`\ Line endings change from CRLF to LF`, and the file is untouched. A file with a double space and the replacing
formatter give the headers, the hunk `@@ -1 +1 @@`, `-a  b`, and `+a b`, exit code 0, and the file is untouched. A
compliant file gives no output and exit code 0.

**Fix writes a safe text change**: A file with a double space and the replacing formatter are run with `--fix`; the
file holds `a b`, nothing is reported, and the exit code is 0.

**A safe file is still written next to a refused one**: One file whose rewrite would lose a hard break (the letter x
and two trailing spaces, then a line with y) and one whose rewrite is harmless (x, two spaces, y on one line) are run
with `--fix` and the replacing formatter; the harmless file
is written, the other is untouched, and exactly one `safety-html-mismatch` diagnostic at 1:3 is reported with exit
code 1.

**Check predicts the refusal of fix**: The hard-break-dropping formatter in check mode gives the same
`safety-html-mismatch` diagnostic at 1:2 as with `--fix`, exit code 1, and the file untouched.

**One write failure does not stop the other files**: Two CRLF files and a writer that throws for `a.md` only are run
with `--fix`; the lines are `a.md:1:1: file-write-error Cannot write file: disk full` followed by the unfixed
`line-ending` diagnostic of `a.md`, the exit code is 1, `a.md` is untouched, and `b.md` holds the LF text.

**A valid grid table is formatted as an ordinary block**: A CRLF document with a valid grid table and a
configuration that chooses LF is run with `--fix`; the line endings are fixed, nothing is reported, and the exit code
is 0.

**Fix gives identical output and files for any degree of parallelism**: Two identical trees of 60 files (CRLF with a
double space, clean, a hard break that the replacing formatter would lose, undecodable, mixed endings, and an invalid
grid table) are fixed with one worker and with eight; the exit code is 1, the lines are identical and in ordinal
order and include a safety, a grid table, and an encoding diagnostic, and every file has the same bytes.

**Processing with a formatter returns the change with the exact new bytes**: A byte order mark file with CRLF
endings and the replacing formatter processed through `Process` give a `FileChange` with the LF text before and
after, the target ending CRLF, the original bytes, and the new bytes (mark kept, text changed, CRLF kept), the
resolved target (the full path of the file), the safety status `Ok`, and no diagnostic.

**The default engine fixes a file that breaks formatting rules**: A file with a setext heading, trailing whitespace, a
hard break, a run of blank lines, and a tilde fence is run with `--fix` and no seam. The file holds the canonical text
(ATX heading, blank lines around the list and the fence, a backtick fence, the hard break kept), nothing is reported,
and the exit code is 0.

**The default engine reports each violation and writes nothing in check mode**: The same file is run without options.
The exit code is 1, each violation is reported under its rule at its place, and the file's bytes are unchanged.

**The default engine shows the change with the diff option**: A file with a closed heading and a trailing space is run
with `--diff`. The lines are the two violations followed by the unified diff of the change, the exit code is 1, and
the file is untouched.

**A second fix run leaves a fixed file alone**: The file of the first scenario is fixed twice. The second run reports
nothing and does not write the file.

**A comment that names a rule by its legacy number disables it**: A file with trailing whitespace between a comment
that disables `MD009` and one that enables it again, and a closed heading after them, is run with `--fix`. The
whitespace in the region stays, the heading is fixed, and nothing is reported.

**The configuration selects the written form**: A configuration with `code-fence: tilde`, `thematic-break: asterisks`,
`max-blank-lines: 2`, and `final-newline: false` and a file with three blank lines, a backtick fence, and a break are
run with `--fix`. The file holds two blank lines, a tilde fence, an asterisk break, and no final line ending.

**Processing with the default formatters returns the change with the formatted bytes and the findings**: The dirty file
is processed through `Process`. The result has a change whose new text is the clean text, and seven findings, all of the
formatting kind.

**A document with an invalid grid table is never rewritten by the default formatters**: A document with an invalid grid
table is run with `--fix`. One `grid-table-invalid` problem is reported at its line and the file is unchanged.

**Ignored files that were found are neither read nor reported**: A file below `node_modules` that is not explicit
(built-in patterns) is marked ignored with no diagnostic and no change, also when the file does not exist, so that
reading it would have been an error. Through `Run` the files of a glob, of a directory argument, and of the
default selection that match the patterns are skipped and the other files are reported, with exit code 1 for the
file that is not skipped.

**Ignore patterns apply per configuration**: A root configuration that ignores `only-root/**` and a nested one that
ignores `only-sub/**` give, with four workers, reports for `only-sub/bad.md` and `sub/only-root/bad.md` only. The
patterns of an explicit configuration file in `conf` are relative to `conf`, so `conf/x/bad.md` is skipped and
`x/bad.md` is reported. With `--no-config` the built-in patterns still skip a `generated` folder and an ignoring
configuration is not read.

**An explicit file is processed even when an ignore pattern matches it, and exclusions still apply**: A file below
`node_modules` named as an explicit `DiscoveredFile` is read and gives its `file-encoding-error`; a file in
`generated` named on the command line is reported; with `generated/bad.md`, `other/bad.md`, and `!other/**` only the
first is reported.

##### Boundary conditions

**Line ending specified nowhere stays unspecified**: Editor settings that only give an indentation
style, with no configuration file, leave the line ending unspecified and report no problem.

**Unsupported editor value leaves the line ending unspecified**: Editor settings that choose `cr` leave the line
ending unspecified, with no problem.

**Without a resolver the editor settings are not read**: Editor settings that choose `crlf` have no effect when no
resolver is passed.

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

**A file outside the ignore root is never ignored**: With a sub folder as the current directory and the glob
`../other/**/*.md`, an undecodable file in `other/generated` lies outside the folder that the built-in patterns are
relative to, so it is read and reported as `../other/generated/bad.md:1:1: file-encoding-error ...`.

**A document with an invalid grid table is untouched whether or not it is reported**: With `--fix` and a CRLF
document with an invalid grid table under an LF configuration, `a.md:2:1: grid-table-invalid Line looks like a grid
table border but the table is not valid; the file is never rewritten` is reported, the exit code is 1, and the file
keeps its bytes and its time. In check mode a border on line 4 is reported at `a.md:4:1`. With
`rules: grid-table-invalid: false`, or with the finding inside a `markdownlint-disable` region for the rule, nothing is
reported and the exit code is 0, but the file is still byte for byte as it was. At the `Process` level a file with an
invalid grid table and a text-changing formatter has no change and one `grid-table-invalid` diagnostic at 2:1.

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
`file-parse-error` naming the exception type for that file, and the other files are unaffected; the same diagnostic
is written as a `file-parse-error` line by `Run`.

**Degree of parallelism below one is rejected**: The constructor throws `ArgumentOutOfRangeException`.

**Null arguments are rejected**: Null arguments to `Run` and `Process` throw `ArgumentNullException`, as does a null
loader passed to the `Process` overload that takes one, a null element of the path list, a default (path-less)
`DiscoveredFile`, and a null list of discovered files.

**A file result without a path or with invalid findings is rejected**: A null path throws `ArgumentNullException`, an
empty path throws `ArgumentException`, a default list of findings throws `ArgumentException`, and a null finding
throws `ArgumentNullException`. A valid result keeps its path and finding and has no configuration.

**A file change with missing or invalid parts is rejected**: A null original or new text throws
`ArgumentNullException`, a default byte array (original or new) throws `ArgumentException`, and a line ending that is
not defined throws `ArgumentOutOfRangeException`. A valid change keeps its texts, line ending, and bytes.

**Invalid configuration is written as one positioned line and exits with one**: A valid file beside
`line-length: abc` gives exactly one line,
`.lintmark.yaml:1:14: config-error Invalid value 'abc' for setting 'line-length'; expected a whole number without
quotes from 0 to 100000`, and exit code 1.

**Configuration problem shared by many files is reported once**: One invalid configuration used by twelve files in
three folders, run with eight workers, gives exactly one line, `.lintmark.yaml:1:1: config-error Unknown setting
'bogus'`, and exit code 1.

**Configuration problems and file diagnostics are merged in path and position order**: A root configuration with two
problems, a nested configuration with one, and two undecodable files give five lines in ordinal path order (the
dotted `.lintmark.yaml` first, then `a.md`, then `docs/.lintmark.yaml`, then `docs/x.md`) and, within a file, in
position order, with exit code 1.

**Files of an invalid configuration are still read and checked**: An invalid configuration and an explicitly named
missing file give the configuration problem and then `missing.md:1:1: file-read-error File not found`. At the
`Process` level, an invalid configuration leaves each result without a configuration but with one configuration
problem, while an undecodable file still reports its `file-encoding-error` and the valid file has no diagnostic. A
file in a folder that the built-in patterns would ignore is read as well, because its `ignore` setting is unknown,
and is not marked ignored.

**A file with an invalid configuration is never formatted or rewritten**: With `bogus: 1` in the configuration and
`--fix`, a file with mixed line endings keeps its bytes and its time and only `.lintmark.yaml:1:1: config-error
Unknown setting 'bogus'` is reported. With `**/*.md` and `--fix`, an undecodable file and a mixed-ending file in
`generated` give the configuration problem and the encoding diagnostic only, and the mixed-ending file is untouched.
At the `Process` level a formatter that throws if it is called is never called: no diagnostic, no change, one
configuration problem, and no configuration on the result.

**Unreadable editor settings are reported at the Markdown file and stop the formatting of that file**: With a
resolver whose lookup throws an `IOException`, `a.md:1:1: config-error Cannot read .editorconfig settings: disk
failure` is written and the exit code is 1; with `--fix` a mixed-ending file keeps its bytes and its time. At the
`Process` level the result keeps its configuration, the line ending stays unspecified, the problem is at line 1,
column 1 of the Markdown file, and a formatter that throws if it is called is never called (no diagnostic, no
change).

**A formatter that throws is a format error for that file**: With the throwing formatter and `--fix`,
`a.md:1:1: file-format-error Formatting failed: InvalidOperationException` is written, the exit code is 1, and the
file is untouched. At the `Process` level the result has the diagnostic, no change, and the safety status `Ok`.

**Conflicting, out-of-range, and carriage return edits are format errors**: With `--fix`, overlapping edits give
`Formatting failed: InvalidOperationException`, an edit beyond the end of the text gives
`Formatting failed: ArgumentException`, and an inserted carriage return gives
`Formatting failed: the result contains a carriage return`; each is a `file-format-error` at 1:1, the exit code is
1, and the file is untouched.

**A rewrite that changes the rendering is refused**: With the hard-break-dropping formatter and `--fix`, one
`safety-html-mismatch` line starts with `a.md:1:2: ... The rewrite would change how the document renders (` and ends
with `); the file is not rewritten`, the exit code is 1, and the file is untouched. At the `Process` level the status
is `HtmlMismatch`, there is no change, and the diagnostic has that rule.

**A conversion of the line endings that changes the rendering is refused**: A document that holds a code span framed by
line breaks (`` ` ``, a line break, `bb`, a line break, `` ` ``) is written with LF line endings under a configuration
that asks for CRLF, and with CRLF line endings under a configuration that asks for LF. With `--fix` the exit code is 1,
the lines are the `line-ending` finding at 1:1 and a `safety-html-mismatch` line at 1:1 that starts with `The rewrite
would change how the document renders because of its line endings (` and ends with `); the file is not rewritten`, the
file keeps its bytes and last write time, and the directory holds no file but the document, the configuration, and the
log. In check mode and with `--diff` the same lines are reported, the exit code is 1, and nothing is written. At the
`Process` level the status is `HtmlMismatch` with no change.

**A harmless conversion of the line endings is still written**: A document with a heading, a one-line code span, a soft
break, a list, and a fenced block is converted from LF to CRLF and from CRLF to LF with `--fix`; the exit code is 0,
nothing is reported, and the file has exactly the converted bytes.

**A rewrite that changes the Pandoc structure is refused**: With the line-joining formatter on a fenced div, one
`safety-structure-mismatch` line at `a.md:1:8` starts with `The rewrite would change the Pandoc structure (`, the
file is untouched, and at the `Process` level the status is `StructureMismatch` with no change.

**A rewrite that is not stable is refused**: With the flip-flop formatter, one `safety-not-idempotent` line at
`a.md:1:6` starts with `Formatting is not stable (`; with the growing formatter the line is at `a.md:1:5`; the
files are untouched, and at the `Process` level the flip-flop gives the status `NotIdempotent` with no change.

**A rewrite that creates a grid table border is refused**: With the formatter that puts `+---` on a line of its own,
one `safety-html-mismatch` or `safety-structure-mismatch` line is reported at line 1, and the file is untouched.

**A rewrite that would be read as an invalid grid table is refused**: A harmless rewrite with a parser seam that
reads the rewritten text as a document with a grid table fallback gives `a.md:1:3: safety-structure-mismatch The
rewrite would not be read as before (`, exit code 1, and an untouched file.

**A safety check that cannot be completed refuses the rewrite**: A harmless rewrite with a parser seam that throws
`InvalidOperationException` for the rewritten text gives one line that starts with
`a.md:1:3: safety-structure-mismatch The rewrite would not be read as before (the check failed: InvalidOperationException)`
and ends with `; the file is not rewritten`, exit code 1, and an untouched file with its old last write time. At the
`Process` level the status is `StructureMismatch`, there is no change, and the message says that the check failed.

**A parser failure during the stability check is reported as not stable**: A harmless rewrite with a parser seam that
reads the rewritten text once and throws the second time (the formatting step of the stability check) gives
`a.md:1:3: safety-not-idempotent Formatting is not stable (`, exit code 1, and an untouched file, because the
stability check treats a formatting step that fails as a change.

**A failed write keeps the formatting diagnostics and the file**: A writer that throws an `IOException` gives
`a.md:1:1: file-write-error Cannot write file: disk full` and then the `line-ending` diagnostic that was not fixed,
with exit code 1 and the file untouched.

**A file that changed after it was read is not overwritten**: A writer that overwrites the file just before the real
atomic write gives two lines, the first being `a.md:1:1: file-write-error Cannot write file: The file changed after
it was read; it was not rewritten.`, exit code 1, and the other writer's content is still in the file.

**The writer is given the resolved target of the file that was read**: A CRLF file is fixed through a writer that
records its target argument and then delegates to the real writer; the recorded value is the full path of the file,
the exit code is 0, nothing is reported, and the file is rewritten. Processing a file through `Process` gives a
`FileChange` whose resolved target is that full path.

**A link redirected after the read is not written through (Unix only)**: A link `a.md` to a CRLF file, a second file
with identical bytes, and a writer that points the link to the second file just before the real atomic write give a
`file-write-error` line that starts `a.md:1:1: file-write-error Cannot write file: The file link now points
elsewhere`, exit code 1, both files with their original bytes, and no temporary file. The test is skipped on
Windows, because creating links there needs privileges that a test run may not have.

**Rule identifiers that the engine emits are registered**: The read, parse, format, write, configuration, and
line-ending rule identifiers are all found in the rule catalog.
