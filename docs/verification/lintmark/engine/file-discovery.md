### FileDiscovery

#### Verification Approach

`FileDiscovery` is verified with unit tests in `FileDiscoveryTests.cs`. The real FileSystemGlobbing library
and a real temporary directory tree (the `TemporaryDirectory` test helper) are used. Most tests share a tree
that contains top-level and nested Markdown files, a non-Markdown file, a `generated` folder, and files below
`node_modules` and `.git`; the tests for third-party folders build their own trees, including one whose base
directory lies inside `node_modules`. Tests call `Discover` with controlled arguments and compare the result with
the expected sorted full paths. No mocking is used because the unit's behavior is its interaction with the
file system.

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
- The result is distinct, sorted, and independent of argument order.
- Blank and null arguments are rejected.

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

**A third-party folder named together with a file inside it selects only the file**: The arguments
`node_modules` and `node_modules/pkg/readme.md` select only the file.

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

##### Error handling

**Missing literal path passes through**: A literal path that does not exist is returned as a full path, so that
reading it reports the problem.

**Blank patterns are rejected**: Empty, whitespace-only, and lone `!` arguments throw `ArgumentException`.

**Null arguments are rejected**: Null arguments throw `ArgumentNullException`.
