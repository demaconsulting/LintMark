### FileDiscovery

#### Verification Approach

`FileDiscovery` is verified with unit tests in `FileDiscoveryTests.cs`. The real FileSystemGlobbing library
and a real temporary directory tree (the `TemporaryDirectory` test helper) are used. Most tests share a tree
that contains top-level and nested Markdown files, a non-Markdown file, a `generated` folder, and files below
`node_modules` and `.git`; the tests for third-party folders build their own trees, including one whose base
directory lies inside `node_modules`. Tests call `Discover` with controlled arguments and compare the result with
the expected sorted full paths. The tests of `DiscoverFiles` use the same trees and check, for each file, the full
path and whether the command line named it (the explicit flag). The tests of `IsIgnored` need no files at all,
because the method judges paths only: they pass a root and a path built with the `TemporaryDirectory` helper (so that
the paths are valid on the platform) together with patterns, and compare the answer; the built-in patterns are taken
from the real `LintMarkConfig.DefaultIgnore` list, so the test follows the list that the configuration uses. No
mocking is used because the unit's behavior is its interaction with the file system and the globbing library.

#### Test Environment

Temporary directories are created and removed by each test. Case-dependent behavior is asserted according to
the platform's comparison rule, so the tests pass on case-sensitive and case-insensitive systems.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Files, directories, relative, parent-relative, absolute, and backslash globs select the documented files.
- Missing literal paths pass through; patterns without matches return an empty list without error.
- Exclusions (relative, absolute, directory name, explicit file) remove the files they match.
- Directory, glob, and default expansion never select a file from a `node_modules` or `.git` folder: not when
  the folder is found beneath the searched directory, and not when the argument names the folder or a glob
  starts inside it.
- A file named explicitly is selected even inside such a folder, and a base directory that itself lies inside
  such a folder still finds its own files, while a root that climbs out of it into or beside a third-party
  folder selects nothing.
- The result is distinct, sorted, and independent of argument order; where names are compared without regard
  to case, one file named with different spellings is returned with the same (smallest-in-ordinal-order) spelling
  whatever the argument order.
- Literal paths, directories, and exclusions written with backslashes select the same files as with forward slashes.
- Blank and null arguments are rejected, and a null element is reported as a null argument even when a blank
  pattern precedes it.
- `DiscoverFiles` returns the same files as `Discover`, in ordinal order of the full path, and marks a file explicit
  exactly when the command line named it as an existing file or as a path without wildcards (also when the file does
  not exist); files found through a directory, a glob, or the default pattern are not explicit; a file that is both
  named and found, also under a spelling that differs only in case, is explicit; a `!` exclusion still removes an
  explicit file.
- `IsIgnored` is true exactly when the file lies below the root and its root-relative path matches a pattern: patterns
  are relative to the root, `**` matches at any depth, `*` stays within one folder, backslashes in patterns work like
  slashes, case follows the platform, an empty pattern list ignores nothing, and a file outside the root (beside
  it or above it) is never ignored.
- The built-in ignore patterns match the `.git`, `node_modules`, `.venv`, `generated`, `thirdparty`, and `.agent-logs`
  folders at the documented depth and nothing else.
- A discovered file without a path is rejected, and a valid one keeps its path and its provenance flag.

#### Test Scenarios

##### Normal operation

**Existing file is returned as its full path**: An existing file argument returns its full path.

**Explicit file is exempt from the extension and the third-party exclusion**: A non-Markdown file and a file
below `node_modules`, each named explicitly, are returned.

**Directory selects its Markdown files at every depth**: A directory argument returns the `*.md` files beneath
it.

**Recursive glob selects nested files**: `docs/**/*.md` returns the matches below `docs` and ignores other
extensions.

**Single-segment glob does not descend**: `*.md` returns only top-level files.

**Glob written with backslashes is accepted**: Backslash separators give the same result as slashes.

**Literal file and directory paths written with backslashes are found**: `docs\guide.md` and `docs\deep` select
the same files as `docs/guide.md` and `docs/deep`, on every platform (the case that fails on Unix when
separators are normalized only for globs).

**Exclusions written with backslashes remove the same files**: `!docs\deep` and `!docs\guide.md` remove the
directory contents and the file respectively.

**Absolute glob is resolved from its wildcard-free prefix**: An absolute glob returns the matches.

**Parent-relative glob is resolved against the base directory**: A glob that starts with `..` returns the matches.

**No arguments select the default pattern minus third-party folders**: No arguments select every Markdown file
under the base directory except below `node_modules` and `.git`.

**Directory and glob expansion skip third-party folders beneath the root**: The files below `node_modules` and
`.git` found under a searched directory are not selected.

**Exclusion glob removes the files it matches**: A `!` glob removes files from a directory selection.

**Excluding a directory name removes everything beneath it**: A list with only `!docs/generated` selects the
default set minus everything beneath that directory.

**Absolute exclusion removes matches**: An absolute `!` pattern removes the files it matches.

**Exclusion also removes an explicitly named file**: A `!` pattern removes a file that was named explicitly.

**Overlapping arguments give each file once**: A file selected by several arguments is returned once.

**Argument order does not change the result**: Reordering the arguments gives the same sorted result.

**Case variants of one name give one spelling whatever the order**: `docs/guide.md` and `docs/GUIDE.md` given in
either order, or with a glob, give the same result. Where names ignore case, that is the single
smallest-in-ordinal-order spelling `docs/GUIDE.md`; where names are case-sensitive, the two spellings are two
different files (the second does not exist) and both are returned.

**A third-party folder named together with a file inside it selects only the file**: The arguments
`node_modules` and `node_modules/pkg/readme.md` select only the file.

**A file named as an existing path is explicit, also when it is inside a third-party folder**: `DiscoverFiles` with
`docs/guide.md` returns that full path with the explicit flag set; `node_modules/pkg/readme.md` is kept and explicit.

**Files found through a glob, a directory, or the default pattern are not explicit**: `docs/*.md` returns the file
without the flag, the directory `docs` returns three files none of which is explicit, and no argument returns four
files none of which is explicit.

**A file that is both named and globbed is explicit and only that file**: `docs/**/*.md` together with `docs/guide.md`
returns three files, each once, and only `docs/guide.md` is explicit.

**A named file that a user exclusion removes is gone, and the other named file stays explicit**: `README.md`,
`docs/guide.md`, and `!README.md` return only `docs/guide.md`, explicit.

**The result is in ordinal order of the full path whatever the arguments**: A named file, a second named file, and a
directory given in unsorted order return distinct paths sorted with the ordinal comparer (four files).

**Case variants of one file collapse to the smallest spelling and the file is explicit**: Where names ignore case, a
glob that finds `docs/guide.md` and the argument `docs/GUIDE.md` give one explicit file under the spelling
`docs/GUIDE.md`, in either argument order; where names are case-sensitive the scenario does nothing, because the two
spellings are different files.

**An ignore pattern matches a file below the root**: `docs/generated/**` ignores `docs/generated/api.md`, and
`docs/generated/**` together with `*.txt` does not ignore `docs/guide.md`.

**Patterns are relative to the ignore root**: With the root `sub`, the pattern `x/**` ignores `sub/x/a.md` but not
`x/a.md` (below the base, outside the root), and with the base as root it does not ignore `sub/x/a.md` (wrong depth).

**A pattern written with backslashes matches like its forward-slash form**: `docs\generated\**` ignores
`docs/generated/api.md`.

**A recursive wildcard in the middle of a pattern matches at any depth**: `docs/**/x.md` ignores `docs/x.md`,
`docs/a/x.md`, and `docs/a/b/c/x.md`, but not `other/x.md` or `docs/a/y.md`.

**A single-segment pattern matches only directly under the root**: `*.generated.md` ignores `a.generated.md` but
neither `a.md` nor `docs/a.generated.md`.

**The built-in ignore patterns match the version-control, dependency, and generated folders**: With the real default
list, `.git/info.md`, `.git/hooks/h.md`, `node_modules/pkg/readme.md`, `tools/.venv/lib/readme.md`,
`docs/generated/api.md`, `generated/api.md`, `a/thirdparty/lib/readme.md`, and `.agent-logs/report.md` are ignored;
`README.md`, `docs/guide.md`, `docs/generated.md`, and `src/agent-logs/report.md` are not.

##### Boundary conditions

**Directory argument naming a third-party folder selects nothing**: The arguments `node_modules` and `.git`
each select nothing.

**Nested third-party folder selects nothing**: The argument `docs/node_modules` selects nothing.

**Absolute path to a third-party folder selects nothing**: An absolute argument naming `node_modules` or `.git`
selects nothing.

**Glob rooted in a third-party folder selects nothing**: Globs whose wildcard-free prefix is or lies inside such
a folder select nothing: `node_modules/*/*.md`, `node_modules/**/*.md`, `node_modules/**`,
`node_modules/pkg/*.md`, `.git/**`, `.git/*.md`, `docs/node_modules/**`, `docs/node_modules/**/*.md`, and
`node_modules\**\*.md`.

**Base directory inside a third-party folder still finds its own files**: With the base directory
`node_modules/pkg`, no argument, `.`, and `**/*.md` select the files of that directory, a subdirectory argument
selects its files, and a `node_modules` folder beneath the base directory is still skipped.

**Climbing out of a base directory inside a third-party folder selects nothing**: With the base directory
`node_modules/pkg`, the arguments `..`, `../other` (a sibling package), and `../**/*.md` select nothing, while
`../..` selects the Markdown files of the project folder above `node_modules` and skips `node_modules`.

**Glob case follows the platform**: A glob that differs in letter case matches only on platforms with
case-insensitive comparison.

**Glob without matches returns an empty list**: A glob that matches nothing, or whose directory does not exist,
returns nothing and is not an error.

**A file outside the ignore root is never ignored**: With the root `sub`, a match-everything pattern `**` ignores
`sub/a.md` but neither `other/a.md` (a sibling) nor `a.md` (above the root).

**Ignore pattern case follows the platform**: The pattern `docs/**` ignores `Docs/Api.md` only on platforms that
compare names without regard to case.

**An empty pattern list ignores nothing**: `docs/guide.md` is not ignored when there are no patterns.

**A missing literal path is explicit**: `DiscoverFiles` with `missing.md` returns its full path with the explicit
flag set, so that the read problem is reported for a deliberate request.

##### Error handling

**Missing literal path passes through**: A literal path that does not exist is returned as a full path, so that
reading it reports the problem.

**Blank patterns are rejected**: Empty, whitespace-only, and lone `!` arguments throw `ArgumentException`.

**Null arguments are rejected**: Null arguments throw `ArgumentNullException`.

**A null element is reported as null**: A null element first, after a blank pattern, or after valid patterns
throws `ArgumentNullException` for `patterns`, never `ArgumentException`.

**Null arguments to DiscoverFiles are rejected**: A null list, a null base directory, and a null element of the list
throw `ArgumentNullException`.

**Blank patterns to DiscoverFiles are rejected**: Empty, whitespace-only, and lone `!` arguments throw
`ArgumentException`.

**Null arguments to IsIgnored are rejected**: A null path, a null root, a null pattern list, and a null element of
the pattern list throw `ArgumentNullException`.

**A discovered file without a path is rejected**: A null path throws `ArgumentNullException` and an empty path throws
`ArgumentException`. A valid discovered file keeps its path and its provenance flag, whichever value the flag has.
