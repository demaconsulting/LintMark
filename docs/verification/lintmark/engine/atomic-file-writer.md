### AtomicFileWriter

#### Verification Approach

`AtomicFileWriter` is verified with unit tests in `AtomicFileWriterTests.cs`. The real file system is used, in a
temporary directory created with the `TemporaryDirectory` test helper, because the unit's behavior is its
interaction with the file system: the tests write real files and then read back their bytes, attributes, and
neighbors. No double replaces the .NET file classes. The one seam is the internal overload of `Write` that takes a
hook that runs just before the replacement; a hook that throws simulates a crash at the critical moment, after the
temporary file is complete and before it replaces the target. A helper searches the whole temporary tree for files
that end with the temporary suffix `.lintmark-tmp`, so that every scenario can prove that no leftover remains. The
bytes passed as the original and as the new content are written out in each test, so that a test can make the
file differ from the original on purpose (a file that changed after it was read).

Two scenarios depend on the platform and are skipped, with a stated reason, where they cannot run. Writing through a
symbolic link is skipped on Windows, because creating links there needs privileges that a test run may not have.
Keeping the permission bits of an executable file is skipped on Windows, because Unix file modes do not exist
there; on Windows the replacement operation keeps the attributes of the replaced file instead, which the
scenario about a read-only file and the successful replacement exercise indirectly. A skipped test is reported by the
test framework as skipped and not as passed, so a run on Windows does not claim coverage of the Unix-only scenarios;
they run on the Linux and macOS builds.

#### Test Environment

Temporary directories are created and removed by each test. The scenario about a read-only file sets the read-only
attribute on a file in the directory and clears it again in a `finally` block, so that the directory can be deleted.
The Unix-only scenarios need a Unix-like operating system and a file system with permission bits and symbolic links.
The large-content scenario writes five megabytes into the temporary directory.

#### Acceptance Criteria

- All unit tests pass with zero failures; the Unix-only scenarios are skipped on Windows and pass elsewhere.
- A successful write leaves exactly the new content in the target, byte for byte (byte order mark, line endings,
  non-ASCII text, empty content, and large content included), and no temporary file next to it.
- A failure at the moment before the replacement leaves the original bytes in place, removes the temporary file, and
  lets the original exception propagate.
- A file whose current bytes differ from the bytes that the rewrite was computed from, including bytes that only
  begin like them or that extend them, is refused with an `IOException`, left as the other writer made it, and no
  temporary file remains.
- A read-only file is refused with `UnauthorizedAccessException` and left as it was.
- A missing file is refused with `FileNotFoundException`, and a path in a missing directory with an `IOException`;
  neither the file nor the directory is created.
- Writing through a symbolic link replaces the file that the link points to and keeps the link (Unix).
- The permission bits of the replaced file are kept (Unix).
- A second write that expects the bytes of the first write succeeds.
- A null path is rejected with `ArgumentNullException`, and an empty or blank path with `ArgumentException`.

#### Test Scenarios

##### Normal operation

**New content replaces the file**: A file holding `old` is replaced with `new` while the expected original is `old`;
the file then holds exactly `new`.

**A successful write leaves no temporary file**: After a replacement the target is the only file in the directory and
no file with the temporary suffix exists anywhere under it.

**Empty content produces an empty file**: Replacing a file with no bytes leaves an existing, empty file.

**A byte order mark, CRLF line endings, and non-ASCII text are written byte for byte**: Content that starts with the
three bytes of a UTF-8 byte order mark, contains an accented letter and CRLF terminators, is read back identical.

**Large content is written completely**: Five megabytes of patterned bytes are read back identical.

**A second write expects the bytes of the first**: A file replaced once and then replaced again with the first
write's content as the expected original ends with the last content.

**Writing through a symbolic link replaces the target and keeps the link (Unix only)**: With a link to a real file,
writing to the link changes the real file, the link is still a link, and reading through it gives the new content.
The test is skipped on Windows.

**An executable file stays executable (Unix only)**: A file that is readable and executable for everyone, and also
writable for its owner, has the same permission bits after the replacement. The test is skipped on Windows.

##### Boundary conditions

**A file that changed since it was read is refused**: A file that now holds other bytes than the expected original
causes an `IOException`; the other writer's bytes are still in the file and no temporary file remains.

**An expected original that only matches a prefix or an extension is refused**: A file holding `abc` followed by a
line feed is written with the expected original `abc`, and again with `abc` followed by two line feeds; both calls
throw `IOException` and the file keeps its bytes.

**A failure before the replacement leaves the original and removes the temporary file**: With a hook that throws an
`IOException` named `simulated crash`, the exception propagates with that message, the original bytes are intact, and
no temporary file remains.

##### Error handling

**A read-only file is refused**: A file with the read-only attribute causes `UnauthorizedAccessException`; the
content is unchanged, no temporary file remains, and the attribute is cleared afterward by the test.

**A missing file is refused**: A path without a file in an existing directory causes `FileNotFoundException` and no
file is created.

**A missing directory is refused**: A path in a directory that does not exist causes an `IOException` and the
directory is not created.

**A null path is rejected**: A null path causes `ArgumentNullException`.

**An empty or blank path is rejected**: An empty string and a string of spaces cause `ArgumentException`.
