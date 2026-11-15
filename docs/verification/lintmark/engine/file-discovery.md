### FileDiscovery

#### Verification Approach

`FileDiscovery` is verified with unit tests in `FileDiscoveryTests.cs`. The unit expands path, directory, glob, and
exclusion arguments over the real file system with the real file-globbing library, so the tests create real
directory trees in a `TemporaryDirectory` and assert on the returned paths and on whether each was named
explicitly. No mocks are used. The ignore-pattern check is verified through the same class with in-memory
patterns and paths.

#### Test Environment

Each test creates and removes a temporary directory tree, including folders named like the skipped ones
(`node_modules`, `.git`). Case-sensitivity scenarios follow the comparison rule of the platform that runs them.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- A named file is always returned and marked explicit, regardless of extension and default exclusions.
- A directory returns the Markdown files beneath it; globs match as written; the default pattern `**/*.md` applies
  when no argument is given.
- Folders named `node_modules` and `.git` are skipped unless the root of the search lies inside them or the file is
  named explicitly.
- Exclusion arguments remove matches, also named files, and everything beneath an excluded directory name.
- Overlapping arguments and case variants return each file once, in a stable order.
- A directory link or junction back into a folder the search is inside is skipped, so each file is found once and
  the search ends; a link to another folder is followed; a broken link is skipped.
- A blank pattern is refused; a missing literal path passes through for the engine to report.
- Ignore patterns match relative to the configuration folder at any depth with the platform's case rule.

#### Test Scenarios

##### Normal operation

**Explicit file is not subject to default excludes**: A file in `node_modules` named on the command line is returned.

**Directory returns the Markdown files beneath it**: Only `.md` files are returned.

**Recursive and single-segment globs match as written**: `**` descends; `*` does not.

**No arguments use the default pattern**: The built-in excludes apply.

**Exclusions remove matches**: A glob, a directory name, and an explicit file are removed.

**Overlapping arguments return distinct files**: Case variants return one spelling in any order.

**Ignore patterns match**: Pattern matches, patterns relative to the config folder, `**` at any depth, and default
patterns for `.git`, `node_modules`, and `generated`.

##### Boundary conditions

**Search root inside `node_modules` still finds its files**: The folder is skipped only below the root.

**Glob without matches returns empty**: No exception.

**A directory link back into its own tree is skipped**: A link to an ancestor
(`FileDiscovery_Discover_DirectoryLinkCycle_IsSkippedAndFilesFoundOnce`) and two folders that link to each other
(`FileDiscovery_Discover_DirectoryLinksToEachOther_Terminates`) end after one lap and return each file once. The
same holds when the search folder is itself reached through a link, as on macOS where `/var` is a link
(`FileDiscovery_Discover_SearchRootBehindALink_StillRecognizesTheCycle`).

**A directory link elsewhere is followed**: The Markdown files behind it are returned
(`FileDiscovery_Discover_DirectoryLinkElsewhere_IsFollowed`).

**A broken directory link is skipped**: No exception, the other files are returned
(`FileDiscovery_Discover_BrokenDirectoryLink_IsSkipped`). The link tests create a symbolic link, fall back to a Windows
junction, and pass without a check when neither can be created.

**A missing literal path passes through**: It is marked explicit.

**File outside the root is not ignored**: Ignore matching returns false.

##### Error handling

**Blank pattern is refused**: `ArgumentException`.

**Null arguments are refused**: `ArgumentNullException`.
