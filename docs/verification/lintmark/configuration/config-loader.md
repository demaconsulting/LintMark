### ConfigLoader

#### Verification Approach

`ConfigLoader` is verified with unit tests in two classes. `ConfigLoaderTests.cs` covers everything that touches the
file system: discovery of the nearest `.lintmark.yaml` file, the repository boundary, path forms, an explicitly
named file, the defaults-only mode, caching, and concurrent use. `ConfigLoaderParseTests.cs` covers the reading and
validation of a configuration file's content by calling the uncached `Parse` method with UTF-8 bytes and a fixed
reporting path (`conf/.lintmark.yaml`), so that every rule of the file format can be exercised, with exact line and
column assertions, without creating files. The real YamlDotNet parser and the real `LintMarkConfig` are used at
every level; nothing in the unit is mocked, because the unit's value is the combination of the file-system probing,
the library's events, and the validation that follows them. The behavior of the YAML library that the loader depends
on is pinned separately by the YamlDotNet OTS tests.

Discovery and caching scenarios use real temporary directory trees (the `TemporaryDirectory` test helper). Most
parse scenarios are data-driven: one test method covers many inputs of one kind (for example every allowed token of
every choice setting) and each input is a separate case of that test. Equality of results is checked on projected
values (source path, line length, and problem positions and messages), and the instance identity of results is
checked where the design promises that one result is shared.

#### Test Environment

Temporary directories are created and removed by each test. By default each directory holds an empty `.git`
directory, so discovery started inside it stops there and never reaches a `.lintmark.yaml` file above the system
temporary directory; the scenarios about the repository boundary create their own `.git` directory or file, or build
a tree without any marker. Tests that change the current directory (relative paths) restore it afterwards and run
sequentially with the other tests that touch process-wide state. The scenario about a file that is locked against
reading runs on Windows only, where exclusive locks are enforced for every reader; it is skipped on other platforms
and no requirement depends on it. The tests that run many threads use 16 degrees of parallelism over many files and
repeat every lookup ten times.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- The nearest configuration file in the file's directory or a parent directory up to the repository root is used,
  including for a file or directory that does not exist; a farther file is never read, merged, or reported on;
  a directory named like the file is skipped; a configuration above a `.git` directory or `.git` file is never used.
- A relative path, and a path written with backslashes, find the same configuration on every platform.
- An explicitly named file is used for every Markdown file instead of discovery; a missing, unreadable, directory,
  or oversized file is a problem at line 1, column 1, available before any Markdown file is processed.
- Defaults apply without a file, for an empty or comment-only file, and for every setting that a file omits; the
  defaults-only loader never reads or probes the file system.
- Every documented value of every setting is accepted, written with or without quotes, in block or flow style, in
  any order, with LF or CRLF line endings, and with or without a UTF-8 marker at the start of the file.
- Unknown settings (including wrong-case names), wrong kinds of values, out-of-range numbers, null values,
  duplicates, rule conflicts, invalid YAML, unsupported structure, implausibly deep nesting, an implausibly large
  file, and invalid or unsupported encodings are each reported as a problem at the right position; none is ignored
  or partially applied, and a pathological file is rejected promptly.
- Problems carry the file path and a 1-based line and column, are ordered by line, column, and message, and are the
  same on every run, for LF and CRLF files alike.
- Many threads get results identical to sequential use.
- Problem lists cannot be changed through casts.

The scenarios headed "supporting" pin behavior that is described in the design (a result is never both a
configuration and problems, argument validation, and the caching of results per directory). They are tests without
a requirement, because that behavior is construction and not an observable need.

#### Test Scenarios

##### Discovery and the repository boundary

**Tree without a configuration file gives the defaults**: Loading a file in a repository root that has no
`.lintmark.yaml` file returns the built-in defaults and no source file.

**Configuration beside the Markdown file is used**: A `.lintmark.yaml` in `docs` with line length 90 is used for
`docs/a.md`, and the result names that file as its source.

**Configuration in an ancestor directory is used**: A file in `docs` is used for `docs/a/b/c.md`.

**Nearest configuration wins and a farther one is never read**: An invalid file in the root and a valid file in a
nearer folder are created; loading a file in the nearer folder gives the nearer file's settings and no problem, so
the root file was never read.

**Nearer partial configuration does not inherit from a farther one**: A root file sets the line length and a nearer
file sets only the wrap mode; the line length of the file in the nearer folder is the default, not the root file's
value.

**Configuration in the repository root is found**: A file in the directory that holds the `.git` directory is used
for a file three levels below it.

**Configuration above a `.git` directory is ignored**: An invalid file above a folder that holds a `.git`
directory is not used for a file below that folder; the result is the defaults without a source.

**A `.git` file stops the search like a `.git` directory**: A `.git` file (as used by linked working trees and
submodules)
with an invalid configuration above it gives the defaults.

**A `.git` directory in a nearer folder stops the search there**: A root configuration with a nested folder that
holds its own `.git` directory is not used for a file in the nested folder.

**Configuration beside a `.git` entry is found before the boundary applies**: A nested repository's own file wins
over the outer repository's file, because the boundary directory is searched too.

**Search continues to the file-system root without a repository marker**: In a tree without any `.git` entry, a
configuration at its top is found for a deeply nested file.

**Directory named like the configuration file is skipped**: A directory named `.lintmark.yaml` nearer the file is
not treated as a configuration, and the real file above it is used.

**File in a folder that does not exist still finds the configuration above it**: Loading a path whose folders do
not exist searches upward and uses the root file.

**Relative Markdown path is resolved against the current directory**: With the temporary directory made current,
`sub/a.md` finds the `sub` configuration.

**Backslash separators are accepted on every platform**: A path written with backslashes finds the configuration of
its folder, on Windows and elsewhere.

**Discovery walk returns the expected file for several layouts**: For a repository with nested files and a nested
repository, the walk gives the nearest file for a folder, for a deeper folder, for a path with a trailing
separator, and for the repository root itself; it passes through a folder without a file, and gives nothing for a
folder inside a nested repository that has no file.

**Defaults-only loader never reads or probes the file system**: With an invalid configuration beside the file and
a path in folders that do not exist, both loads return the defaults without a source, the two results are the same
instance, and there are no initial problems.

##### Explicit configuration file

**Named file is used for any Markdown file**: A file outside the Markdown files' folders gives its settings to
files in unrelated places through one shared result, and has no initial problems.

**Relative file name is resolved against the current directory**: A file named with backslashes and a relative path
is found with the temporary directory current, and its full path is the result's source.

**Named file replaces a discoverable one**: A discoverable file with one line length and a named file with another
give the named file's value for a file beside the discoverable one.

**Valid file is loaded with its full path**: Loading a file directly gives its settings and its full path as the
source.

**Missing file is a problem at line 1, column 1**: The loader holds exactly one initial problem, "Configuration file
not found", at 1:1 of the missing path, available before any file is loaded, and loading a Markdown file gives no
configuration.

**Directory given as the file is a problem**: The single problem says that the configuration file cannot be read,
at 1:1.

**File locked against reading is a problem (Windows only)**: A file held open exclusively gives one problem that
says the file cannot be read, instead of an exception. The scenario is skipped on other platforms.

**Problems of an invalid file are known up front and returned for every file**: A file with two faults gives two
initial problems, and loading any file returns the same problems; the discovery and defaults-only loaders have no
initial problems.

**File larger than 1 MiB is reported without being read to the end**: A file one byte over the limit gives one
problem at 1:1 that says it is larger than 1 MiB; direct loading and parsing of the same size behave the same.

**File of exactly 1 MiB is accepted**: Content of exactly the limit (a long comment) is a valid, empty
configuration.

**Null and blank arguments are rejected (supporting)**: The explicit-file factory throws `ArgumentNullException`
for null and `ArgumentException` for a blank name; direct loading throws for null and for an empty name; the
discovery and defaults-only loaders throw for a null or empty Markdown path; the discovery walk throws for a null
or empty directory.

##### Defaults and omitted settings

**Empty file gives the defaults**: Zero bytes give a configuration equal to the built-in default.

**File without settings gives the defaults**: Whitespace only, comments only, a lone document marker, a document
marker with an end marker, a lone `~`, and a lone `null` each give the defaults.

**Omitted settings keep their defaults**: A file that sets the line length and the bullet equals the default with
just those two changed.

**Omitted link conversion options keep their defaults**: A group that sets only `enabled` equals the disabled
options with only that change.

**Rules that are not mentioned keep their defaults**: Switching one rule on and another off changes exactly those
two rules from the defaults.

**Explicit empty collections are valid**: An empty ignore list, an empty rules mapping, and an empty link
conversion mapping are accepted; nothing is ignored and rules and link conversion keep their defaults.

##### Settings that are accepted

**Documented full sample applies every setting**: The sample that sets every setting, nested link conversion
options, rule toggles by name and legacy number, and ignore patterns gives the sample's values everywhere.

**Every token of every choice setting is accepted**: Each of the 28 allowed tokens (wrap, heading style, bullet,
ordered style, emphasis, strong, code fence, code block style, hard break, thematic break, bare URLs, tables,
trailing whitespace, and line ending) is accepted.

**Quoted values and keys are accepted for choice settings**: A double-quoted value, a single-quoted value, and a
double-quoted key all select the wrap mode `semantic`.

**Line length accepts zero, one, and the maximum**: `0` (unlimited), `1`, and `100000` are stored.

**List indent accepts two and four**: Both values are stored.

**Booleans accept true and false**: Plain `true` and `false` are accepted for an on-or-off setting, for link
conversion flags, and for rule toggles.

**Flow-style collections are accepted**: A link conversion mapping, a rules mapping, and an ignore list written
in flow style are applied.

**Order of settings does not matter**: The same settings in two orders give equal configurations.

**Rule is switched off by name**: A rule given by its name changes only that rule.

**Legacy rule number is stored under the rule name**: `MD013` switches off the line-length rule, no alias key
appears, and the table still has 22 entries.

**Rule names and numbers are matched in any letter case**: `md013`, `Md013`, `Line-Length`, and `LINE-LENGTH` all
switch off the line-length rule.

**Legacy numbers of removed rules are accepted and ignored**: `MD025`, `md033`, and `MD041` change nothing.

**Reordered rule entries give equal rules**: The same toggles in opposite orders give equal tables.

**Ignore list replaces the default patterns**: A list of three patterns in non-alphabetical order is kept exactly as
given, in that order, and no default pattern remains.

**Empty ignore list clears the default patterns**: Nothing remains ignored.

**Backslashes in ignore patterns become forward slashes**: `docs\generated\**` is stored with forward slashes.

**Exact duplicate patterns are removed keeping the first and the order**: A list that repeats a pattern (also after
normalizing a backslash) keeps the first occurrences in their original order.

**Patterns that differ in letter case are distinct**: `Docs/**` and `docs/**` are both kept.

**UTF-8 marker at the start of the file is ignored**: A file that starts with the three-byte marker has its first
key recognized.

**CRLF line endings parse and number lines like LF**: The same three-line content with LF and with CRLF gives
the same problems at the same positions, on lines 2 and 3.

**A valid file with CRLF line endings gives the same configuration as with LF**: A valid file with several settings,
a rule toggle, and an ignore list is parsed with LF and with CRLF line endings; the two configurations are equal
and differ from the defaults.

##### Unknown settings

**Unknown setting is reported at its key**: An unknown name on line 3 (after a blank line made of spaces) is
reported at 3:1 as "Unknown setting 'unknown-setting'".

**Several unknown settings are all reported in position order**: Three unknown names and one invalid value are
reported on lines 1, 3, 4, and 5, in position order and not in name order.

**Wrong-case names and tokens are unknown**: `Line-Length`, `Fill`, `LF`, and `Crlf` are each rejected; names and
tokens are case-sensitive.

**Unknown link conversion option is reported with its dotted name**: An unknown option under the link conversion
group is reported at 2:3 with the name prefixed by `link-refactor.`.

**Unknown rule is reported at its key**: `no-such-rule` is reported at 2:3 as "Unknown rule".

**Legacy number of a rule that the formatter always applies is unknown**: `MD009` is reported as an unknown rule.

##### Invalid values

**Token that is not allowed lists the allowed tokens**: For each of 16 invalid tokens (for example `reflow` for
wrap, `star` for bullet, and `cr`, `auto`, and `consistent` for the line ending), the problem is at the value,
names the setting, and lists the allowed tokens in order.

**List indent other than two or four is reported**: `0`, `1`, `3`, `8`, a quoted `"2"`, and `two` are reported at
the value.

**Numbers that are not plain decimal whole numbers are reported**: `abc`, quoted numbers, `1.5`, `0x10`, `+5`,
`1_000`, `007`, `1e3`, and `true` are each reported with "expected a whole number without quotes from 0 to 100000".

**Numbers outside the range are reported**: `-1`, `100001`, `2147483648`, and a number beyond the integer range are
reported with the range.

**Maximum blank lines of zero is reported**: The range starts at one.

**Only plain true and false are accepted as booleans**: `yes`, `no`, `on`, `off`, `True`, `FALSE`, quoted `"true"`
and `'false'`, and `1` are each reported with "expected true or false without quotes".

**Mapping or list where a single value is expected is reported**: A mapping or list for the wrap mode, the line
length, and a boolean is reported at the collection with "must be a single value".

**Scalar where a collection is expected is reported**: A scalar for the rules, the link conversion group, or the
ignore list (and a mapping or list of the wrong kind) is reported at the first character of the value.

**Link conversion numbers outside their range are reported**: A threshold of `0`, `-4`, or `100001` and a label
length of `0` or `-1` are reported with the dotted setting name and the range 1 to 100000.

**Link conversion flag that is not a boolean is reported**: `yes` for `enabled` is reported at 2:12.

**Definitions marker that is not an HTML comment is reported**: Plain text, an unclosed comment, `<!--->`, and an
unopened comment are reported as "expected a single-line HTML comment".

**Definitions marker that spans lines is reported**: A marker with an escaped line break is reported.

**Rule value that is not a boolean is reported**: `yes`, a quoted `"true"`, `1`, and an empty value are reported
against the dotted rule name.

**Invalid ignore element is reported**: An empty element, a blank element, a null element, a missing element, a
nested list or mapping, and a multi-line element each give one problem at the element's line, naming what is
wrong, while the valid element before it causes none.

##### Null values

**Null in any spelling is reported, not defaulted**: An empty value, `~`, `null`, `Null`, and `NULL` for a setting
are each reported at 1:1 as "Setting 'wrap' must not be empty".

**Empty rules, link conversion, ignore, and number settings are reported**: Empty or null values for `rules`,
`link-refactor`, `ignore`, and `line-length` are each reported at the key.

##### Duplicates and rule conflicts

**Duplicate setting is reported at its second occurrence**: A repeated `line-length` is reported at 3:1 and names
the line of the first.

**Duplicate nested setting is reported with its dotted name**: A repeated `threshold` under the link conversion
group is reported at 3:3.

**Duplicate with an invalid first value reports both problems**: `wrap: nope` followed by `wrap: fill` gives the
invalid value and the duplicate.

**Alias and name of one rule conflict at the later entry**: `MD013: false` followed by `line-length: true` is
reported at the second entry and names the first.

**Name then alias conflicts at the alias**: `line-length` followed by `MD013` is reported at the alias.

**Alias then name with reversed values conflicts at the name**: `MD013: true` followed by `line-length: false` is
reported at the name, so the outcome follows the content of the file and not the order in which entries are
examined.

**Spellings that differ in case conflict**: `MD013` and `md013` are reported at the second.

**Removed rule numbers still take part in conflict and value checks**: A repeated `MD025` in another case and an
invalid value for `MD033` are both reported.

##### Invalid syntax

**Unclosed flow sequence is reported as invalid YAML**: An unclosed `[a, b` inside a file gives one problem that
starts with "Invalid YAML", at a valid position, and no configuration, even though the parser fails with an
unusual exception type.

**Tab indentation is reported as invalid YAML**: A nested mapping indented with a tab gives one syntax problem.

**Unterminated quote is reported as invalid YAML**: The problem is on or after the line of the quote.

**Positions of syntax problems are never before line 1, column 1**: Ten malformed inputs (unclosed braces and
brackets, an unterminated quote, a doubled colon, `a: b: c`, a lone tab, a directive, and a complex key with an
unclosed bracket) never throw, any problem has a position of at least 1:1, and a result has no configuration
exactly when it has problems.

##### Unsupported structure

**Root that is not a mapping is reported**: A list, plain text, a number, and a flow list as the whole file are
reported at 1:1 as "The configuration must be a mapping of settings".

**Second document is reported at its marker**: Two documents give a problem at 2:1 saying that only one is
allowed.

**Anchors, aliases, and tags are rejected**: Five inputs (an anchor on a value, an anchor used by an alias, two tags,
and an anchor on a list) are each reported at the construct with "Anchors, aliases and tags are not supported".

**Complex key is reported**: A key that is a list is reported at 1:3.

##### Implausibly deep nesting

**Block nesting below the limit inside an unknown setting gives one problem**: Fifty levels inside an unknown
setting give only the unknown-setting problem and no crash.

**Flow nesting beyond the limit is reported at the offending bracket**: Sixty-five nested `[` and sixty-five nested
`{` are each reported at the first bracket beyond 64 levels, at column 3 + 64 + 1, instead of being parsed.

**Flow nesting exactly at the limit is parsed**: Sixty-four nested brackets are accepted, so only the unknown
setting is reported.

##### Encoding

**Invalid UTF-8 is reported at the offending position**: An invalid byte after two characters on line 2 is
reported at 2:3 as "Invalid UTF-8 byte sequence", with no configuration.

**UTF-16 and UTF-32 encodings are rejected**: A UTF-16 little endian mark, a UTF-16 big endian mark, and a UTF-32
mark at the start are each reported at 1:1 as an unsupported byte order mark.

**Position of an invalid byte does not count the UTF-8 marker**: A UTF-8 marker followed by an invalid byte is
reported at 1:1, as an editor would show it.

##### Problem positions, ordering, and consistency

**Problems are sorted by position and then by message**: A file whose faults arise from different checks gives
four problems in ascending line and column order.

**Problems are the same on every run (supporting)**: Parsing the same faulty content 21 times gives identical
positions and messages every time.

**Every problem carries the path it was given**: Parsing under another path names that path in every problem and
in the result's source.

**A result holds a configuration or problems, never both and never neither (supporting)**: Constructing a result
with a configuration and problems, with neither, with an uninitialized problem list, or with an empty source name
throws `ArgumentException`.

**A problem rejects invalid arguments and flattens line breaks (supporting)**: A null path or a null message throws
`ArgumentNullException`; an empty path or a blank message throws `ArgumentException`; a line or column of zero
throws `ArgumentOutOfRangeException`; a message with a line break is stored on one line.

**Parse rejects a null or empty path (supporting)**: Null throws `ArgumentNullException` and empty throws
`ArgumentException`.

##### Concurrency and caching

**Many threads get the sequential results**: A root file, a nested file, and an invalid nested file serve 60 files
in many folders plus 20 files elsewhere; 16 threads load every file ten times; every result equals the sequential
one.

**Files of one directory share one result instance (supporting)**: Two files in the same folder get the same
instance, which shows that a configuration file is read once per directory.

**A file changed after the first load keeps its cached result (supporting)**: After the file is rewritten, a second
file in the same folder still gets the first result with the original settings.

##### Immutability

**Problem lists cannot be changed through casts**: The problem list of a result refuses every change attempted
through the mutable list interfaces.
