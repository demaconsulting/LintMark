### AtomicFileWriter

#### Verification Approach

`AtomicFileWriter` is verified with unit tests in `AtomicFileWriterTests.cs`. The unit replaces a file's content
through a temporary file in the same folder, and it works on the real file system, so the tests create real files
and links in a `TemporaryDirectory` and assert on the resulting bytes, the remaining files, and the exceptions. A
test hook that runs before the replacement simulates a failure or a link that changes after the check. No mocks
are used. Scenarios that need symbolic links or permission bits run where the platform provides them.

#### Test Environment

Each test creates and removes a temporary directory. Symbolic-link and file-mode scenarios depend on platform
support and are skipped where it is missing.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- A write replaces the content completely and leaves no temporary file.
- A file that differs from the bytes the caller read, even with equal length, is never replaced.
- A read-only or missing file is refused and left unchanged.
- A symbolic link is written through to its final target; a link that is redirected after the read or before the
  replacement is refused and neither file is changed.
- The mode of an executable file is kept.

#### Test Scenarios

##### Normal operation

**New content replaces the file**: The file holds the new bytes afterward.

**Symbolic link target is written through**: The link stays and its target changes.

**Executable mode is kept**: The permission bits survive.

**Resolving a regular file or a link returns the final target**: The resolved path is the full path or the target.

##### Boundary conditions

**Original bytes equal but different length are refused**: The comparison is of the full content.

##### Error handling

**A failure before replacement leaves the original and removes the temporary file**: The hook throws.

**A file changed since the read is refused**: The file keeps its content.

**A read-only or missing file is refused**: The exception is thrown and the file is untouched.

**A target that differs from the expected one is refused**: Both files are left unchanged.

**A link redirected after the read or before the replacement is refused**: The temporary file is removed.
