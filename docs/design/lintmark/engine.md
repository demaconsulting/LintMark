## Engine

![Engine Structure](EngineView.svg)

### Overview

The `Engine` subsystem orchestrates a run. It expands the positional arguments of the command line into Markdown files,
processes the files in parallel (configuration, read, parse, lint, format, patch), optionally rewrites them, and
reports every finding in a deterministic order. It owns all file system access of the tool: directory enumeration,
file reading, file writing, and the order of the whole run. It contains four units:

- `FileDiscovery` expands path arguments into files and applies the `ignore` patterns of a configuration.
- `LintMarkEngine` drives the per-file pipeline using units of the `Configuration`, `Document`, `Linting`, and
  `Formatting` subsystems, and owns the `FileResult` and `FileChange` types.
- `AtomicFileWriter` replaces the content of a file in one step and refuses to overwrite a changed file.
- `Reporters` writes diagnostics as text lines (`TextReporter`) and proposed rewrites as unified diffs (`DiffReporter`).

### Interfaces

- **FileDiscovery.DiscoverFiles**: static; arguments and a base directory in, distinct `DiscoveredFile` values in
  ordinal path order out. **FileDiscovery.IsIgnored**: static and pure; says whether `ignore` patterns match a path.
- **LintMarkEngine.Run(Context)**: the only entry point used by `Program`. Reads `Context.Paths`, `Fix`, `Diff`,
  `NoConfig`, `ConfigFile`, and `RuleOverrides`; reports through `Context.WriteError` and `Context.WriteLine`.
- **AtomicFileWriter.Write**: static; path, resolved target, expected original bytes, and new bytes in; throws
  `IOException` or `UnauthorizedAccessException` and leaves the target untouched on any failure.
- **TextReporter.Report and DiffReporter.Report**: static; diagnostics or `FileDiff` values, a base directory, and the
  `Context` in; lines out.

The subsystem consumes `Context`, `ConfigLoader`, `EditorConfigResolver`, `DocumentParser`, `SourceDocument`,
`SuppressionMap`, `RuleCatalog`, `LintRuleEngine`, `DocumentFormatter`, `EditSet`, and the Microsoft FileSystemGlobbing
package (see the OTS design *FileSystemGlobbing*).

### Design

`LintMarkEngine.Run` is a three-phase pipeline. First it discovers files against the current directory. Then workers
process the files in parallel, each worker returning an immutable `FileResult` stored by input index; workers never
write output or files. Finally, on the calling thread and after all workers finish, the engine writes the rewrites
(with `--fix`, one file at a time in path order), collects the diagnostics, and reports them. Results are sorted by path
(ordinal) and the reporters sort diagnostics, so console output, files written, and the exit code do not depend on
scheduling.

Per file, a worker resolves the configuration first (nearest `.lintmark.yaml`, no merging, cached per directory;
`--config` selects one explicit file; `--no-config` selects the defaults and also skips `.editorconfig`), so that
`ignore` patterns can skip the file before it is read. It then reads and parses the file, runs the lint rules and the
formatter, and composes the formatter edits into the proposed text. A document the parser could only read by dropping
grid tables is never formatted. The default mode and `--check` only report; `--diff` also prints a unified diff and
writes nothing; `--fix` writes through `AtomicFileWriter` and reports what remains at post-fix positions.

Every reported diagnostic goes through `Context.WriteError`, so any finding, formatting or lint-only, makes the exit
code 1; otherwise it is 0. The correctness of the formatter output is checked during development by the non-shipping
corpus harness `tools/LintMark.Verify`, not at run time.
