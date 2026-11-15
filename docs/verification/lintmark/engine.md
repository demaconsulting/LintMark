## Engine

### Verification Approach

The `Engine` subsystem is verified by integration tests in `EngineSubsystemTests.cs`. Each test creates real files in a
`TemporaryDirectory`, runs the real `LintMarkEngine` through its run entry point with a real `Context`, and so drives
file discovery, configuration, parsing, linting, formatting, atomic writing, and the text and diff reporters
together, as `Program` does. The tests assert on the files on disk, the captured console output, and the exit
status. The formatting and linting subsystems are real collaborators and no mocks are used; the only seam used is
the injectable file writer of the engine in the scenarios that need a write failure. Details of each stage are
verified in the unit tests.

### Test Environment

Each test creates and removes a temporary directory that contains an empty `.git` folder, so no configuration above
the tree is read. Console output is captured through the context. The tests change no process-wide state apart from
the current directory in scenarios that use relative paths, so they run sequentially.

### Acceptance Criteria

- All integration tests pass with zero failures.
- Files are selected from paths, directories, globs, and exclusions; each file is processed once.
- In check mode the engine reports findings and writes nothing; in fix mode it writes only changed files; in diff
  mode it prints the change and writes nothing.
- Reports list findings in path then position order, regardless of the degree of parallelism.
- A write never replaces a file that changed after it was read and leaves no temporary file behind.
- A failure of one file never stops the others.

### Test Scenarios

#### Normal operation

**Selected files are processed and reported in path order**: A directory and a glob select the files; two files with
findings are reported sorted by path.

**Check reports and does not write**: A file with formatting violations is reported and left byte-identical.

**Fix rewrites to canonical form**: The file is rewritten, a second run reports nothing, and no temporary file
remains.

**Diff prints the change and does not write**: A unified diff of the change is printed.

**Each file uses its nearest configuration and ignore patterns**: Files in folders with different settings are
formatted differently; ignored files are skipped; an explicitly named file is processed.

#### Boundary conditions

**No matching files is a success**: A glob without matches writes nothing and exits with 0.

**Parallel and sequential runs agree**: The same files give the same output and the same file contents.

#### Error handling

**Unreadable and undecodable files are findings, not crashes**: A missing file and a file with invalid UTF-8 are
reported and the other files are still processed.

**A file that changed after it was read is not overwritten**: The write failure is reported and the file keeps its
new content.

**An invalid configuration is reported once**: The file is parsed and linted but never rewritten.
