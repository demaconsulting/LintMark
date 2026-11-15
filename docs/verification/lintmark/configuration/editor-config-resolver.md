### EditorConfigResolver

#### Verification Approach

`EditorConfigResolver` is verified with unit tests in `EditorConfigResolverTests.cs`. The resolver reads real
`.editorconfig` files through the editorconfig library, so the tests build directory trees in a `TemporaryDirectory`
whose root file declares `root = true`, resolve the line ending of a file path, and assert on the result. No mocks
are used. The library's own behavior (pattern matching, layering, raw values) is verified as OTS evidence; here only
LintMark's interpretation of the raw `end_of_line` text is verified: `lf` and `crlf` are accepted in any letter case,
everything else means "not specified".

#### Test Environment

Each test creates and removes a temporary directory tree. The tests use relative paths in one scenario and so run
sequentially with other tests that change the current directory.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- `end_of_line = lf` and `crlf` resolve to those endings, in any letter case of key and value.
- `cr`, `unset`, an unknown value, a missing property, and a file outside every section resolve to "not specified".
- The nearest file that mentions the property decides; `unset` or an invalid value in a nearer file hides a farther
  value; `root = true` stops the search.
- Section patterns follow the specification, are relative to the file that holds them, and are case-sensitive on
  every platform.
- Malformed lines are ignored, a missing file in a missing folder still resolves, and paths with null characters or
  the file-system root give "not specified".

#### Test Scenarios

##### Normal operation

**`lf` and `crlf` sections resolve**: Each value gives its line ending, also in upper case.

**Nearest file wins per property**: A nearer file overrides a farther one; a nearer file without the property
defers to the farther one.

**Later section overrides an earlier one**: A less specific later section wins over a more specific earlier one.

**Section patterns match per specification**: Patterns for extensions, folders, and nested folders match the
expected files and no others; the pattern case is significant.

**The chain of one directory is reused**: Many files in one directory resolve from the cached chain.

**Relative and backslash paths are normalized**: Both resolve against the current directory and the folder separator.

##### Boundary conditions

**`root = true` stops the search**: A farther `crlf` is not seen.

**Nonexistent file in a missing folder still resolves**: The sections of the existing parents apply.

##### Error handling

**`cr`, `unset`, and unknown values are not specified**: `unset` in a nearer file also hides a farther value.

**Missing property or file outside every section is not specified**: No exception is thrown.

**Malformed lines are ignored**: Later valid sections still apply.

**Null characters in the path and the file-system root are not specified**: No exception is thrown.
