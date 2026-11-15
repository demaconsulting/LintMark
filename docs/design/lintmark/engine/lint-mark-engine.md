### LintMarkEngine

![Engine Structure](EngineView.svg)

#### Purpose

`LintMarkEngine` runs the whole tool for one invocation: it discovers the files, processes each one independently in
parallel, optionally rewrites the files, and reports every finding in a deterministic order. Its supporting types
`FileResult` and `FileChange` are described in this document.

#### Data Model

**Fields**: the maximum degree of parallelism (default the processor count; 1 means sequential), the
`DocumentFormatter` (default from `CreateDefaultFormatter`, with every block formatter), a `LintRuleEngine`, and the
file writer delegate (default `AtomicFileWriter.Write`). The formatter and writer are test and verifier seams. The
instance holds only immutable state, so `Run` may be called concurrently. Constants `ReadErrorRuleId`,
`ParseErrorRuleId`, `FormatErrorRuleId`, and `WriteErrorRuleId` alias the `RuleCatalog` identifiers of failures.

**FileResult**: record `(Path, Diagnostics, Config)` with `ConfigErrors`, `Change`, and `Ignored`. `Config` is null
when the configuration is invalid. `Change` is set only for a rewrite whose bytes differ from the file.

**FileChange**: record `(OriginalLf, NewLf, OriginalEndings, TargetEnding, OriginalBytes, NewBytes, ResolvedTarget)`
with `Remaining`, the findings in the rewritten text, computed only under `--fix`.

#### Key Methods

**Run(Context)**: discover with `FileDiscovery.DiscoverFiles(context.Paths, currentDirectory)`; build the
`ConfigLoader` (`--no-config` defaults only, `--config` that one file, otherwise discovery of the nearest
`.lintmark.yaml`, cached per directory, no merging); create an `EditorConfigResolver` unless `--no-config`; call
`Process`. Then, with `--fix`, `WriteChanges`, otherwise take all diagnostics of all results. Add each distinct
configuration problem (from the files and the loader's initial errors, so a bad `--config` is reported even when no
file matches) as a `LintOnly` diagnostic. Report with `TextReporter`; with `--diff` also call `DiffReporter` for
every result that has a change. `--check` is the default and changes nothing. Without `--fix` nothing is written.

**Process(files, loader, editorConfig, baseDirectory, overrides, fix)**: runs `ProcessFile` with `Parallel.For`,
each worker writing only its own result slot, and returns the results ordered by ordinal path.

**ProcessFile**: load the configuration of the file. If valid, not explicit, and matched by `FileDiscovery.IsIgnored`
(relative to the configuration file's directory, or the base directory for defaults), return an ignored result without
reading the file. Invalid configuration: read and parse the file for its own problems but never format. Otherwise
apply the command-line rule overrides, then if the configuration gives no `end_of_line` consult `.editorconfig`; an
unreadable editor setting adds a problem at 1:1 and prevents formatting, because the target line ending is unknown.

**ReadAndRewrite**: resolve symbolic links, read the bytes from the resolved file, decode and parse with
`SourceDocument` and `DocumentParser`. Failures become diagnostics (`file-read-error`, a decode rule with a precise
position, or `file-parse-error`) and never abort other files. Without a configuration, return only these findings.

**Rewrite**: for a document read with grid tables dropped, report the grid-table finding (unless the rule is off or
suppressed by `SuppressionMap`), run the lint-only rules, and return no change: such a file is never rewritten. Else:
format with `DocumentFormatter`, add the `LintRuleEngine` findings computed on the document as written, and compose
the edits with `SourcePatcher` into the new text. Under `--fix`, when the text changed, parse the new text and compute
`Remaining`: the formatter's non-formatting findings and the lint rules on the new text, so findings are reported at
their post-fix positions. Convert the text to bytes with the target line ending and the original byte order mark;
if the bytes equal the file, return no change; otherwise return a `FileChange`.

**WriteChanges**: after all workers finish, in path order, call the writer for each change. On success report
`Remaining` (or the non-formatting findings when it is null). On failure report all findings plus `file-write-error`
(`Cannot write file: ...`) and continue with the other files.

#### Error Handling

Expected failures (read, decode, parse, formatter exceptions, invalid edits, write) become diagnostics for that file
only; the file stays untouched. Exit codes are 0 and 1 only: every diagnostic, including lint-only findings and
configuration problems, goes through `Context.WriteError` and makes the exit code 1. A blank path argument throws
`ArgumentException` to `Program`. The development-time corpus harness `tools/LintMark.Verify` checks formatter
output; the engine runs no proof or re-check step.

#### Dependencies

`Context`, `FileDiscovery`, `ConfigLoader`, `EditorConfigResolver`, `SourceDocument`, `DocumentParser`,
`SuppressionMap`, `RuleCatalog`, `LintRuleEngine` (Linting), `DocumentFormatter`, `EditSet`, `AtomicFileWriter`,
and `Reporters`. The OTS item FileSystemGlobbing is used through `FileDiscovery`.

#### Callers

- `Program` creates one engine and calls `Run` for every non-validation invocation.
