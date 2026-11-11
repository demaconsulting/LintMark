## Engine

### Verification Approach

The `Engine` subsystem is verified by integration tests in `EngineSubsystemTests.cs`. Each test builds a
temporary directory tree of Markdown and non-Markdown files, runs the real `LintMarkEngine` (with the real
`FileDiscovery`, `SourceDocument`, and `DocumentParser`) against a real `Context` that writes to a log file,
and asserts on the failure lines and the exit code. No mocking is used. The tests change the current
directory when they exercise the default pattern and are therefore run sequentially with the other tests that
touch process-wide state.

### Test Environment

Temporary directories are created and removed by each test. The directory trees avoid names that differ
only by case, so the tests behave the same on case-insensitive and case-sensitive file systems.

### Acceptance Criteria

- All integration tests pass with zero failures.
- A clean tree reports nothing and exits with 0; files below `node_modules` are not read.
- Failures are reported in the documented format, in path order, with exit code 1.
- One worker and eight workers produce identical output and exit code.
- A file selected by several arguments is reported once, and exclusions keep files from being read.

### Test Scenarios

**EngineSubsystem_DiscoveryAndParse_DirectoryWithMarkdown_ReportsNothing**: A directory with valid
Markdown in nested folders, an undecodable file under `node_modules`, and a non-Markdown file is processed;
nothing is reported, exit code is 0, and the `node_modules` file is never read. This scenario is tested by
`EngineSubsystem_DiscoveryAndParse_DirectoryWithMarkdown_ReportsNothing`.

**EngineSubsystem_ParseFailure_ReportedWithPositionAndRule**: A valid file, a file with invalid UTF-8, and a
file with a bare carriage return are processed; the two failures are reported in path order in the format
`path:line:col: rule-id message` and exit code is 1. This scenario is tested by
`EngineSubsystem_ParseFailure_ReportedWithPositionAndRule`.

**EngineSubsystem_ParallelAndSequential_ProduceIdenticalOutput**: 150 files in several directories, some
failing, are processed with one worker and with eight workers; the reported lines (in the same order) and
exit codes are identical, and the lines are in ordinal order. This scenario is tested by
`EngineSubsystem_ParallelAndSequential_ProduceIdenticalOutput`.

**EngineSubsystem_DefaultPattern_UsesCurrentDirectory**: With no path argument the current directory is
searched; a failing file in a subfolder is found and reported with a relative path. This scenario is tested
by `EngineSubsystem_DefaultPattern_UsesCurrentDirectory`.

**EngineSubsystem_ExcludePattern_SkipsExcludedFiles**: `**/*.md` with `!generated/**` is processed; the
failing file under `generated` is never read and exit code is 0. This scenario is tested by
`EngineSubsystem_ExcludePattern_SkipsExcludedFiles`.

**EngineSubsystem_OverlappingArguments_ReportEachFailureOnce**: One failing file is selected by file name,
by directory, and by glob; the failure is reported exactly once. This scenario is tested by
`EngineSubsystem_OverlappingArguments_ReportEachFailureOnce`.
