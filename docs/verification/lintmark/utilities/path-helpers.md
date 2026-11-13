### PathHelpers

#### Verification Approach

`PathHelpers` is verified with unit tests defined in `PathHelpersTests.cs`. Because `PathHelpers`
performs pure path manipulation using only .NET BCL types, no mocking or test doubles are
required. Tests call `PathHelpers.SafePathCombine` directly with controlled base and relative
path arguments and assert on the returned string or the thrown exception type and message.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Valid relative paths are combined correctly and the returned path equals the expected result.
- Path traversal patterns cause `ArgumentException` with message containing "Invalid path
  component".
- Absolute paths supplied as the relative argument cause `ArgumentException`.
- Null inputs cause `ArgumentNullException`.
- A filename beginning with `".."` that is not a traversal sequence is accepted correctly.

#### Test Scenarios

##### Normal operation

**Valid relative path is combined with the base path**: A relative path (`"subfolder/file.txt"`)
is combined with a base path; the returned path equals the expected combined result and no
exception is thrown, confirming basic path combination works.

**Current directory reference is combined with the base path**: A relative path starting with
`"./"` (e.g., `"./subfolder/file.txt"`) is combined with a base path; the returned path equals
the expected combined result and no exception is thrown.

**Deeply nested relative path is combined with the base path**: A deeply nested relative path
(`"a/b/c/d/file.txt"`) is combined with a base path; the returned path equals the expected
combined result and no exception is thrown.

##### Boundary conditions

**Empty relative path returns the base path**: An empty string is passed as the relative path;
the returned path equals the base path and no exception is thrown, confirming the empty-string
edge case is handled correctly.

**File name starting with two dots is accepted**: A relative path whose filename starts with
`".."` but is not a traversal sequence (e.g., `"..data/file.txt"`) is combined with a base path;
the returned path equals the expected combined result and no exception is thrown, confirming such
filenames are not misidentified as traversal.

##### Error handling

**Leading traversal sequence is rejected**: A relative path starting with `"../"` is passed; an
`ArgumentException` containing "Invalid path component" is thrown, rejecting the leading
traversal attempt.

**Embedded traversal sequence is rejected**: A relative path containing
`"subfolder/../../../etc/passwd"` is passed; an `ArgumentException` is thrown, rejecting the
embedded traversal sequence.

**Absolute path as the relative argument is rejected**: An absolute path is passed as the
relative argument — Unix-style `/etc/passwd` on all platforms and Windows-style
`C:\Windows\System32\file.txt` when running on Windows; an `ArgumentException` is thrown for each
sub-case.

**Null base path is rejected**: `null` is passed as the `basePath` argument; an
`ArgumentNullException` is thrown, confirming the null guard on `basePath`.

**Null relative path is rejected**: `null` is passed as the `relativePath` argument; an
`ArgumentNullException` is thrown, confirming the null guard on `relativePath`.
