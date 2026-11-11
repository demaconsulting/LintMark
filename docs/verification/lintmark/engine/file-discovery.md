### FileDiscovery

#### Verification Approach

`FileDiscovery` is verified with unit tests in `FileDiscoveryTests.cs`. The real FileSystemGlobbing library
and a real temporary directory tree are used (a shared tree containing top-level and nested Markdown files,
a non-Markdown file, a `generated` folder, and files below `node_modules` and `.git`). Tests call `Discover`
with controlled arguments and compare the result with the expected sorted full paths. No mocking is used
because the unit's behavior is its interaction with the file system.

#### Test Environment

Temporary directories are created and removed by each test. Case-dependent behavior is asserted according to
the platform's comparison rule, so the tests pass on case-sensitive and case-insensitive systems.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Files, directories, relative, parent-relative, absolute, and backslash globs select the documented files.
- Missing literal paths pass through; patterns without matches return an empty list without error.
- Exclusions (relative, absolute, directory name, explicit file) remove the files they match.
- Built-in excludes apply to directory, glob, and default expansion but not to explicit files.
- The result is distinct, sorted, and independent of argument order.

#### Test Scenarios

**FileDiscovery_Discover_ExistingFile_ReturnsFullPath**: An existing file argument returns its full path.
Tested by `FileDiscovery_Discover_ExistingFile_ReturnsFullPath`.

**FileDiscovery_Discover_ExplicitFile_IsNotSubjectToDefaultExcludesOrExtension**: A non-Markdown file and a
file below `node_modules`, named explicitly, are returned. Tested by
`FileDiscovery_Discover_ExplicitFile_IsNotSubjectToDefaultExcludesOrExtension`.

**FileDiscovery_Discover_Directory_ReturnsMarkdownFilesBeneathIt**: A directory argument returns its
Markdown files at every depth. Tested by `FileDiscovery_Discover_Directory_ReturnsMarkdownFilesBeneathIt`.

**FileDiscovery_Discover_RecursiveGlob_ReturnsMatchingFiles**: `docs/**/*.md` returns the matches below
`docs`. Tested by `FileDiscovery_Discover_RecursiveGlob_ReturnsMatchingFiles`.

**FileDiscovery_Discover_SingleSegmentGlob_DoesNotDescend**: `*.md` returns only top-level files. Tested by
`FileDiscovery_Discover_SingleSegmentGlob_DoesNotDescend`.

**FileDiscovery_Discover_BackslashGlob_IsAccepted**: A glob written with backslashes works. Tested by
`FileDiscovery_Discover_BackslashGlob_IsAccepted`.

**FileDiscovery_Discover_AbsoluteGlob_ReturnsMatchingFiles**: An absolute glob returns the matches. Tested by
`FileDiscovery_Discover_AbsoluteGlob_ReturnsMatchingFiles`.

**FileDiscovery_Discover_ParentRelativeGlob_ReturnsMatchingFiles**: A glob starting with `..` relative to the
base directory returns the matches. Tested by `FileDiscovery_Discover_ParentRelativeGlob_ReturnsMatchingFiles`.

**FileDiscovery_Discover_NoArguments_UsesDefaultPatternAndAppliesBuiltInExcludes**: No arguments select every
Markdown file under the base directory except below `node_modules` and `.git`. Tested by
`FileDiscovery_Discover_NoArguments_UsesDefaultPatternAndAppliesBuiltInExcludes`.

**FileDiscovery_Discover_DirectoryAndGlob_SkipNodeModulesAndGit**: Directory and glob expansion skip
`node_modules` and `.git`. Tested by `FileDiscovery_Discover_DirectoryAndGlob_SkipNodeModulesAndGit`.

**FileDiscovery_Discover_ExcludeGlob_RemovesMatches**: A `!` glob removes the files it matches from a
directory selection. Tested by `FileDiscovery_Discover_ExcludeGlob_RemovesMatches`.

**FileDiscovery_Discover_ExcludeDirectoryName_RemovesEverythingBeneath**: A list with only `!docs/generated`
selects the default set minus everything beneath that directory. Tested by
`FileDiscovery_Discover_ExcludeDirectoryName_RemovesEverythingBeneath`.

**FileDiscovery_Discover_AbsoluteExclude_RemovesMatches**: An absolute `!` pattern removes matches. Tested by
`FileDiscovery_Discover_AbsoluteExclude_RemovesMatches`.

**FileDiscovery_Discover_ExcludeMatchingExplicitFile_RemovesIt**: A `!` pattern removes a file that was
named explicitly. Tested by `FileDiscovery_Discover_ExcludeMatchingExplicitFile_RemovesIt`.

**FileDiscovery_Discover_MissingLiteralPath_PassesThrough**: A literal path that does not exist is returned
as a full path. Tested by `FileDiscovery_Discover_MissingLiteralPath_PassesThrough`.

**FileDiscovery_Discover_GlobWithoutMatches_ReturnsEmpty**: A glob without matches returns an empty list.
Tested by `FileDiscovery_Discover_GlobWithoutMatches_ReturnsEmpty`.

**FileDiscovery_Discover_OverlappingArguments_ReturnsDistinctFiles**: A file selected by several arguments is
returned once. Tested by `FileDiscovery_Discover_OverlappingArguments_ReturnsDistinctFiles`.

**FileDiscovery_Discover_ArgumentOrder_DoesNotAffectSortedResult**: Reordering the arguments does not change
the result. Tested by `FileDiscovery_Discover_ArgumentOrder_DoesNotAffectSortedResult`.

**FileDiscovery_Discover_GlobCase_FollowsPlatformComparison**: A glob that differs in letter case matches
only on platforms with case-insensitive comparison. Tested by
`FileDiscovery_Discover_GlobCase_FollowsPlatformComparison`.

**FileDiscovery_Discover_BlankPattern_ThrowsArgumentException**: Empty, whitespace, and lone `!` arguments
raise `ArgumentException` (theory). Tested by `FileDiscovery_Discover_BlankPattern_ThrowsArgumentException`.

**FileDiscovery_Discover_NullArguments_ThrowArgumentNullException**: Null arguments are rejected. Tested by
`FileDiscovery_Discover_NullArguments_ThrowArgumentNullException`.
