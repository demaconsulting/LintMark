### LintMarkEngine

#### Verification Approach

`LintMarkEngine` is verified with unit tests in `LintMarkEngineTests.cs` and its topic files
(`LintMarkEngineFixTests.cs`, `...FormattingTests.cs`, `...IgnoreTests.cs`, `...LinkRefactorTests.cs`,
`...LintTests.cs`, `...ScalingTests.cs`, `...TableTests.cs`, `...WrapTests.cs`). The engine composes the real
configuration loader, parser, lint rules, formatter, and reporters, so the tests use real files in a
`TemporaryDirectory` and the real components. The only seams used are the engine's injectable file writer and
formatter, replaced in scenarios that need a failure (a write that throws, a formatter that throws, a file that
changes after it was read). Tests call the per-file entry point and assert on the result, and call the run entry
point with a real `Context` and assert on the captured output, the exit status, and the files on disk. The support
extension `LintMarkEngineTestExtensions` simplifies those calls.

#### Test Environment

Each test creates and removes temporary directories with a `.git` marker. Some scenarios change the current directory
and so run sequentially. The scaling scenario uses a generated large document and compares time against size.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Each file gets its nearest configuration; a configuration error is reported once per file that shares it, and the
  file is parsed but never rewritten.
- Read, decode, and line-ending failures are findings of the file and never stop the run.
- Findings from configuration, files, and rules are merged in path then position order and are identical for
  parallel and sequential processing.
- In fix mode only formatting changes are written, the written text keeps BOM and the decided line ending, lint-only
  findings are reported at their positions in the written text, and a second fix changes nothing.
- A failed formatting or write is reported for that file only, other files are still written, and a file changed
  after it was read is never overwritten.
- Grid-table fallback documents and documents with an invalid configuration are never rewritten.
- Ignored files are neither read nor changed; explicitly named files are processed.
- The ignore list of the configuration that applies at each scan root prunes discovery for everything beneath it,
  nested repositories included, while each file keeps the settings of its own nearest configuration.
- Processing time grows in proportion to document size.

#### Test Scenarios

##### Normal operation

**Valid files give no output and exit 0**: Check mode writes nothing.

**Failures are written as formatted lines in path order**: Each line has path, position, rule, and message.

**Fix writes only formatting changes**: BOM and CRLF files keep their marks and endings, mixed files get the majority
ending, an `.editorconfig` ending is honored, and a second fix leaves the file and reports nothing.

**Fix re-reports remaining findings at written positions**: Lint-only findings after lines moved appear at their new
line, also for CRLF files.

**Check and diff never write**: A CRLF file is reported or diffed and left byte-identical.

**Nearest configuration, explicit configuration, and ignore patterns apply per file**: The line ending from the
configuration wins over `.editorconfig`; ignored files are not read.

**Ignore lists prune at the scan root, nested repositories included**: A folder of clones, each with its own `.git` and
no configuration, is skipped when the root configuration ignores it (`...IgnoredFolderHoldsNestedRepository_Pruned`); a
nested repository that is not ignored still uses its own configuration (`...NestedRepositoryNotIgnored_UsesOwnConfig`);
a file named on the command line inside an ignored nested repository is processed
(`...ExplicitFileInIgnoredNestedRepository_IsProcessed`); a directory argument prunes by the configuration at that
directory (`...DirectoryArgumentBelowConfig_PrunesIgnoredNestedRepository`); `--no-config` prunes with the built-in
defaults (`...NoConfig_BuiltInIgnorePrunesNestedRepository`); `--config` prunes relative to the given file's folder
(`...ExplicitConfig_PrunesNestedRepositoryBelowItsFolder`).

**Enable and disable options win over the configuration**: Rules can be switched on or off by option.

**Suppression comments, link refactor, tables, and wrapping behave through the engine**: A long link is converted
on request, a short table row is reported unless switched off, and a paragraph that cannot be wrapped is reported
as remaining.

##### Boundary conditions

**Glob without matches writes nothing and exits 0**: The run succeeds.

**Large document time is proportional to size**: Doubling the size roughly doubles the time.

**Parallel and sequential runs are identical**: Output and file contents match.

##### Error handling

**Invalid UTF-8, UTF-16, bare CR, missing file, and directory in place of a file are reported**: The matching finding
is produced and the run continues.

**A file the parser rejects is reported and the run continues**: Over-deeply nested emphasis gives `file-parse-error`
for that file while other files are still processed
(`LintMarkEngine_Process_DeeplyNestedEmphasis_ReportsParseErrorAndKeepsGoing`).

**A formatter or write failure is reported for that file**: The file is left as it was, other files are still
written, and the diagnostics of the failed file are kept.

**A file changed after it was read or a redirected link is not overwritten**: A write error is reported.

**Configuration problems are reported once and still allow file failures**: An invalid configuration, a missing
explicit configuration, and a configuration outside the current directory are reported with their path.

**Grid-table fallback is left byte-identical and reported**: It is silent when the rule is disabled or suppressed.
