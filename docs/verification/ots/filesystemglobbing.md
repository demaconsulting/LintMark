## FileSystemGlobbing Verification

This document provides the verification evidence for the FileSystemGlobbing OTS software item (the
`Microsoft.Extensions.FileSystemGlobbing` package). Requirements for this OTS item are defined in the
FileSystemGlobbing OTS Software Requirements document.

### Required Functionality

FileSystemGlobbing provides include and exclude glob matching. LintMark depends on it to expand the glob
arguments of the command line, to apply built-in and user exclusions, and to honor a caller-selected case
sensitivity.

### Verification Approach

FileSystemGlobbing has no self-validation mode. It is verified by integration tests in the dedicated OTS test
project `test/OtsSoftwareTests` (`FileSystemGlobbingTests.cs`), which reference the package directly and not
the LintMark project. The tests use a temporary directory tree and in-memory path lists.

### Test Scenarios

#### FileSystemGlobbing_Matcher_RecursiveInclude_MatchesNestedFiles

**Scenario**: A `**` include is executed against a directory tree.

**Expected**: Files at every depth that match the pattern are returned.

**Requirement coverage**: `LintMark-OTS-FileSystemGlobbing-Match`.

#### FileSystemGlobbing_Matcher_Exclude_RemovesMatches

**Scenario**: An include and an exclude pattern are executed against a tree.

**Expected**: Files matched by the exclude are removed from the result.

**Requirement coverage**: `LintMark-OTS-FileSystemGlobbing-Match`.

#### FileSystemGlobbing_Matcher_DirectoryExcludeAnyDepth_RemovesRootAndNestedDirectories

**Scenario**: An exclude of the form `**/name/**` is executed against a tree with the directory at the root
and nested.

**Expected**: Files below the directory are removed at every depth.

**Requirement coverage**: `LintMark-OTS-FileSystemGlobbing-Match`.

#### FileSystemGlobbing_Matcher_NoMatches_ReturnsEmpty

**Scenario**: A pattern that matches nothing is executed.

**Expected**: The result is empty and no error occurs.

**Requirement coverage**: `LintMark-OTS-FileSystemGlobbing-Match`.

#### FileSystemGlobbing_Matcher_InMemoryMatch_AppliesIncludeAndExclude

**Scenario**: A matcher is applied to an in-memory list of relative paths.

**Expected**: The include and exclude patterns are applied to the list.

**Requirement coverage**: `LintMark-OTS-FileSystemGlobbing-Match`.

#### FileSystemGlobbing_Matcher_Wildcards_StarMatchesWithinSegment

**Scenario**: A single-segment `*` pattern is applied to nested files.

**Expected**: The wildcard matches within one path segment and does not descend.

**Requirement coverage**: `LintMark-OTS-FileSystemGlobbing-Match`.

#### FileSystemGlobbing_Matcher_CaseSensitivityMode_ControlsMatching

**Scenario**: The same pattern is executed with case-sensitive and case-insensitive comparison.

**Expected**: The match result follows the comparison given to the matcher.

**Requirement coverage**: `LintMark-OTS-FileSystemGlobbing-CaseSensitivity`.
