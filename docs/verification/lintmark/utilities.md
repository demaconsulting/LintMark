## Utilities

### Verification Approach

The `Utilities` subsystem is verified by integration tests defined in `UtilitiesSubsystemTests.cs`. The subsystem
holds two units, `PathHelpers` and `TextWidth`, that do not depend on each other or on other tool units, so no
mocking is required and both are used for real.

The path tests exercise `PathHelpers` through realistic path-combination workflows to confirm that valid paths are
resolved correctly, traversal attacks are rejected, and the resulting paths can be used for actual directory
creation. The text width test measures a line that mixes accented letters, a combining accent, East Asian
characters, and an emoji with `TextWidth`, and then pads the line to a fixed number of columns, as a table layout
would, to show that the measurement is usable for layout. The detailed behavior of each unit is verified in the
unit documents.

### Test Environment

N/A - standard test environment. The path test that creates a directory uses a temporary location that it removes
afterward.

### Acceptance Criteria

- All integration tests pass with zero failures.
- Valid relative paths are combined correctly with no exception.
- Path traversal patterns (e.g., `../`) cause `ArgumentException` to be thrown.
- Absolute paths supplied as the relative argument cause `ArgumentException` to be thrown.
- Paths produced by `SafePathCombine` can be passed directly to `Directory.CreateDirectory`.
- A line that mixes scripts is measured in display columns, not in UTF-16 code units, and a line that is padded to
  a number of columns by that measure measures exactly that number of columns.

### Test Scenarios

#### Normal operation

**Valid relative paths are resolved within the base directory**: Multiple valid relative path arguments are combined
with a base path using `PathHelpers.SafePathCombine`; all results are correctly combined paths that remain within the
base directory and no exception is thrown.

**A safely combined path can be used to create a directory**: `PathHelpers.SafePathCombine` is used to compute a
nested path, and the resulting path is passed to `Directory.CreateDirectory`; the directory is created at the expected
location within the base directory.

**A line of mixed scripts is measured in display columns**: The line `Café é 漢字 😀 ok` is built with an
accented letter, a letter with a combining accent, two East Asian characters, and an emoji. `TextWidth.Of` returns 17
columns, which differs from the number of UTF-16 code units of the line. The line is then padded with spaces up to 20
columns using the measured width, and measuring the padded line gives exactly 20.

#### Error handling

**Path traversal patterns are rejected**: Multiple path traversal patterns (e.g., `../`, `subfolder/../../../`) are
passed to `SafePathCombine`; an `ArgumentException` is thrown for each traversal pattern and no traversal succeeds.

**Absolute paths are rejected**: Absolute paths (e.g., `/etc/passwd` and on Windows `C:\Windows\System32\file.txt`)
are passed as the relative path argument to `SafePathCombine`; an `ArgumentException` is thrown for each and no
injection succeeds.
