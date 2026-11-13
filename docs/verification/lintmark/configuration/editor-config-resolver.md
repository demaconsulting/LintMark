### EditorConfigResolver

#### Verification Approach

`EditorConfigResolver` is verified with unit tests in `EditorConfigResolverTests.cs`. The tests use the real
editorconfig library and real `.editorconfig` files in temporary directory trees (the `TemporaryDirectory` test
helper), because the unit's purpose is to turn what that library reports for a Markdown file into a line ending;
mocking the library would remove everything that is worth checking. Two seams that the resolver exposes through its
constructor, one delegate for the library call that resolves the chain of settings files of a directory and one for
the call that evaluates a file against a chain, are used only to provoke failures that cannot be caused reliably on
every platform, and to count lookups: a chain lookup that throws an `IOException` whose message contains a line
break (once with a counter, to show that the failure is determined once per directory), and an evaluation that
throws `UnauthorizedAccessException` while the chain lookup is real. The tests assert on the returned
`EditorConfigResult` (line ending and error message) and never on library types.

Every scenario that reads settings from the file system starts its tree with a root `.editorconfig` that declares
`root = true`, built by one helper in the test class, so the upward search for settings never reaches a file outside
the temporary tree. Section patterns are exercised through one data-driven test whose cases are the pattern forms
of the editor settings specification; each case is a separate test case.

The behavior that is required is the tolerance of settings that cannot be used, the line ending that the settings
give (with the order in which settings files and sections apply), the report of settings that cannot be read, the
handling of paths, and the same results under concurrent use. The editor settings are shared with other tools, so
the scenarios follow the rules of the editor settings format.

#### Test Environment

Temporary directories are created and removed by each test. The tests that resolve a relative path change the
current directory and restore it afterwards, so the class runs sequentially with the other tests that touch
process-wide state. The tests that use many threads allow up to 16 degrees of parallelism over 60 files in three folders
and repeat every lookup ten times through one shared resolver.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- `cr`, `unset`, an unknown word, an empty value, no `end_of_line` setting, and no applicable section all give "not
  specified" without an error message.
- Malformed lines are ignored and no error is reported.
- Many threads get the sequential results.
- Settings that cannot be read give a one-line message and no line ending, never an exception; the same message is
  given for every file of the directory, and a failed lookup is made once per directory.
- A path that cannot be resolved, or has no containing directory, gives "not specified".
- `lf` and `crlf` are reported when a section that applies gives them, with the setting name and the
  value in any letter case; the nearest file that gives a value wins, a value that cannot be used in a nearer file
  hides a farther one, a file that declares `root = true` ends the search, and a later section overrides an
  earlier one; section patterns follow the editor settings format with letter case significant on every platform;
  relative paths, backslash separators, and paths that do not exist are accepted and null and empty paths are
  rejected; the settings chain of a directory is read once.

#### Test Scenarios

##### Settings that cannot be used

**Value `cr` is not specified**: A section with `cr` gives the "not specified" result, with no error.

**Value `unset` is not specified**: The same with `unset`.

**Unknown value is not specified**: `banana`, an empty value, and `lf crlf` each give "not specified" with no
error.

**File without an `end_of_line` setting is not specified**: A section that sets only another property gives the
"not specified" result.

**File that no section matches is not specified**: A section for `*.txt` does not apply to a Markdown file.

**Malformed lines are ignored and later sections still apply**: A file with `[[[`, a bare property name, a bare
equals sign, and a line of text before a valid `[*.md]` section gives `crlf` and no error message.

##### Concurrency

**Many threads get the sequential results**: Nested settings files with different sections and 60 files in three
folders are resolved ten times each from up to 16 threads through one shared resolver; every complete result (line
ending and message) equals the result of a fresh resolver used sequentially, and both line endings occur among
the results.

##### Unreadable settings, paths, and line endings

**Unreadable settings give a one-line message and no line ending**: With the chain lookup throwing an `IOException`
whose message contains a line break, the result has no line ending and the message "Cannot read .editorconfig
settings: disk failure" on one line; with the evaluation throwing `UnauthorizedAccessException`, the message is
"Cannot read .editorconfig settings: denied". Nothing is thrown.

**A failed settings lookup is made once per directory and reported for every file**: With a chain lookup that fails
and counts its calls, three files of one folder get the same message from one lookup, and a file in another folder
causes a second lookup with the same message.

**A path that cannot be resolved has no settings**: A path that contains a null character, and the root of the file
system (which has no containing directory), give "not specified" without consulting the library.

**Section that gives `lf` resolves to `lf`**: A `[*]` section with `end_of_line = lf` gives `lf` for a Markdown file,
with no error message.

**Section that gives `crlf` resolves to `crlf`**: The same with `crlf`.

**Setting name and value are matched in any letter case**: `END_OF_LINE = CRLF`, `End_Of_Line = Crlf`,
`end_of_line=crlf`, and a line with extra spaces all resolve to `crlf`.

**Nearest file wins for the property**: `lf` in the root file and `crlf` in a nested file give `crlf` below the
nested folder and `lf` outside it.

**Farther file is used when the nearer file lacks the property**: A nested file without `root = true` that sets
another property leaves the root file's `crlf` in effect.

**Later section overrides an earlier one**: `[*.md]` with `lf` followed by `[*]` with `crlf` gives `crlf` for a
Markdown file, although the earlier section is more specific.

**Later specific section applies to its own file only**: `[*]` with `crlf` followed by `[a.md]` with `lf` gives `lf`
for `a.md` and `crlf` for `b.md`.

**Section patterns match as the specification says**: Nineteen cases cover `*`, `**`, `*.md` (at any depth), `?`,
character sets and their negation, alternatives, numeric ranges, folder patterns (`docs/*.md` matches in `docs`
only), a leading slash (`/top.md` matches at the top only), `**/deep/**.md`, and an escaped bracket; the section
applies exactly when the pattern is specified to match.

**Pattern with a folder is relative to the folder of its settings file**: A nested file in `docs` with the section
`[guide/*.md]` applies to `docs/guide/a.md` and not to `guide/a.md` at the root.

**Section patterns are case-sensitive on every platform**: With sections `[*.md]` and `[*.TXT]`, the files `A.MD` and
`a.txt` get nothing and `a.TXT` gets `crlf`.

**File that declares `root = true` stops the search**: A nested root file that says nothing about line endings hides
the outer `crlf`, so the line ending is not specified below it.

**`unset` in a nearer file hides a farther value**: `crlf` in the root file and `unset` in a nested file give
nothing below the nested folder, while a file beside the root file keeps `crlf`.

**Unknown value in a nearer file hides a farther valid value**: `banana` in a nested file hides the root file's
`crlf`.

**File in folders that do not exist is still resolved**: A catch-all section applies to a path in missing folders.

**Relative path is resolved against the current directory**: With the temporary directory current, `sub/a.md` gets
the nested file's `crlf`.

**Backslash separators are accepted on every platform**: A path written with backslashes gets the nested file's
`crlf`.

**Chain of settings files is cached per directory**: After one file is resolved, the root settings file is changed on
disk and another file of the same folder is resolved; the earlier line ending is still reported, which shows that
the chain was not read again.

**Null and empty arguments are rejected**: A null path throws `ArgumentNullException` and an empty path throws
`ArgumentException`; a null chain delegate and a null evaluation delegate in the constructor each throw
`ArgumentNullException` naming the argument.

**Result rejects invalid combinations**: A result with both a line ending and an error, with a blank error, or with
an undefined line ending throws `ArgumentException`, and an error message with a line break is stored on one line.
