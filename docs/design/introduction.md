# Introduction

This document provides the detailed design for LintMark, a .NET command-line tool from DEMA Consulting for Markdown
linting and formatting. It covers the local software items (system, subsystems, and units) and the OTS software items
they consume.

## Purpose

The purpose of this document is to describe the internal design of each software item that comprises LintMark. It
captures data models, algorithms, key methods, and inter-item interactions at a level of detail sufficient for formal
code review, compliance verification, and future maintenance. A reviewer should be able to understand how each item
satisfies its requirements without reading source code. The document does not restate requirements; it explains how
they are realized.

## Scope

Local items:

- **LintMark**: the system, its 5 subsystems (`Configuration`, `Document`, `Engine`, `Linting`, `Formatting`), the 2
  nested subsystems of `Formatting` (`Blocks` and `Inline`), and its 30 units.

OTS items:

- **Runtime libraries** shipped with the tool: Markdig, FileSystemGlobbing, YamlDotNet, EditorConfig, and TestResults.
- **Build and pipeline tools**: BuildMark, FileAssert, Pandoc, ReqStream, ReviewMark, SarifMark, SonarMark,
  SysML2Tools, VersionMark, WeasyPrint, and xUnit.

For OTS items only the integration and usage design is documented; the internal design of an OTS item is out of scope.

The following are explicitly out of scope:

- Test projects (`test/`) and the build pipeline (CI configuration).
- Build-time-only package references without behavior (for example SourceLink and analyzers).
- The development tool `tools/LintMark.Verify` and its test project. It is not part of the shipped system and is not a
  software item: it has no requirements, design, or review set of its own.

Verification tooling: the correctness of the tool's rewrites is verified at development time, not by run-time checks.
The shipped tool contains no run-time proof, re-check, or equivalence step. The corpus harness `tools/LintMark.Verify`
is a non-shipping development tool: it runs the tool over a corpus of Markdown documents and checks that the output
keeps the document structure, renders to equivalent HTML, and is stable when formatted again, using deterministic
rules. It is described in the verification documents, and its own tests run in the normal test pipeline.

## Software Structure

The software structure is modeled in SysML2 under `docs/sysml2/` and rendered to the diagram below by SysML2Tools as
part of the build pipeline. AI agents should query the SysML2 model directly (see the `sysml2tools-query` skill) rather
than parsing this diagram or the prose elsewhere in this document. The model captures the shipped system's runtime
composition: the local software items and the OTS libraries the shipped tool consumes (Markdig, FileSystemGlobbing,
YamlDotNet, EditorConfig, and TestResults, modeled in `docs/sysml2/model/ots.sysml`). The remaining OTS items listed in
Scope are build-time or pipeline tools, documented in `docs/design/ots.md` and not modeled as parts.

The system consists of three units that sit directly under it (`Program`, `Context`, and `Validation`) and five
subsystems: `Configuration` (settings, `.lintmark.yaml`, `.editorconfig`), `Document` (decoding, parsing, suppression
comments, and text width), `Linting` (rule registry, diagnostics, and lint-only rules), `Formatting` (the canonical
form and edit composition), and `Engine` (discovery, per-file pipeline, writing, and reporting). `Formatting` contains
two nested subsystems: `Blocks` (the heading, code block, thematic break, block quote, list, table, and paragraph
formatters) and `Inline` (inline normalization, atom tokenizing, line wrapping, and link refactoring). They are the only
nested subsystems.

![Software Structure](SoftwareStructureView.svg)

## Runtime Data Flow

The structure view above shows what the system is made of, not how a run moves data between those parts. The diagram
below follows one `lintmark` run: `Program` and `Context` hand the command line to the `Engine`, which discovers the
files and, for each file in parallel, resolves the configuration and parses the document. The formatter and the
lint-only rules then both read that parsed document as written, so what the lint-only rules report does not depend on
what the formatter changes; their findings are combined. Only with `--fix` is the rewritten text parsed again and
checked again, so that the remaining findings are reported at the positions of the new file. The `Engine` then reports
the findings and, with `--fix`, writes the result. Look for the OTS libraries that each unit consumes and for the
inputs and outputs at the edges. The `Validation` unit (`--validate`) is a separate path and is not shown. The SysML2
model remains the authority on structure; this diagram only adds the data flow.

![Runtime data flow of a lintmark run: the command line, Markdown files, .lintmark.yaml, and .editorconfig enter
Program, Context, and the Engine. FileDiscovery expands the paths, then each file passes through Configuration and
Document, and Formatting and Linting both read the parsed document. The Reporters write findings to stderr and a unified
diff to stdout,
AtomicFileWriter rewrites files for --fix, and Context sets the exit code. Markdig, YamlDotNet, EditorConfig, and
FileSystemGlobbing are the OTS libraries used.](architecture.svg)

## Folder Layout

- **src/** - source files and projects
  - **DemaConsulting.LintMark/** - main application source
    - `Program.cs` - the `Program` unit
    - **Cli/** - `Context.cs`: the `Context` unit
    - **SelfTest/** - `Validation.cs`: the `Validation` unit
    - **Configuration/** - `Configuration` subsystem
      - `LintMarkConfig.cs` - the `LintMarkConfig` unit (the settings record, the setting enumerations, and
        `LinkRefactorOptions`)
      - `ConfigLoader.cs`, `ConfigParser.cs` - the `ConfigLoader` unit (with `ConfigLoadResult` and `ConfigError`)
      - `EditorConfigResolver.cs` - the `EditorConfigResolver` unit
    - **Document/** - `Document` subsystem
      - `SourceDocument.cs` - the `SourceDocument` unit
      - `DocumentParser.cs`, `ParsedDocument.cs`, `ReferenceIndex.cs` - the `DocumentParser` unit
      - `SuppressionMap.cs` - the `SuppressionMap` unit
      - `TextWidth.cs` - the `TextWidth` unit
    - **Engine/** - `Engine` subsystem
      - `FileDiscovery.cs`, `LintMarkEngine.cs`, `AtomicFileWriter.cs` - one unit each
    - **Reporting/** - `TextReporter.cs`, `DiffReporter.cs`: the `Reporters` unit of the `Engine` subsystem
    - **Linting/** - `Linting` subsystem
      - `RuleCatalog.cs`, `Diagnostic.cs` - the `RuleCatalog` unit
      - `LintRuleEngine.cs`, `HeadingRules.cs`, `LinkRules.cs`, `StructureRules.cs`, `TableRules.cs` - the `LintRules`
        unit
    - **Formatting/** - `Formatting` subsystem
      - `DocumentFormatter.cs`, `LineLayout.cs`, `LineOwnership.cs` - the `DocumentFormatter` unit
      - `LeafWhitespace.cs`, `SpacingPasses.cs` - the `WhitespacePasses` unit
      - `Edits.cs`, `EditRebaser.cs` - the `EditSet` unit
      - **Blocks/** - nested `Blocks` subsystem: `HeadingFormatter.cs`, `CodeBlockFormatter.cs`,
        `ThematicBreakFormatter.cs`, `BlockQuoteFormatter.cs`, `ListFormatter.cs` (with `ListModel.cs`),
        `TableFormatter.cs`, `ParagraphFormatter.cs` - one unit each
      - **Inline/** - nested `Inline` subsystem: `InlineNormalizer.cs`, `AtomTokenizer.cs`, `LineWrapper.cs` (with
        `WrapGuard.cs`), `LinkRefactorer.cs` - one unit each
- **test/** - test projects (not covered by design documents)
  - **DemaConsulting.LintMark.Tests/** - unit, subsystem, and system tests for the local items
  - **OtsSoftwareTests/** - integration tests for the OTS libraries
- **tools/** - non-shipping development tools (`LintMark.Verify` and its tests); not software items

## Companion Artifact Structure

Local software items have corresponding artifacts in parallel directory trees:

- Requirements: `docs/reqstream/{system}/.../{item}.yaml` (kebab-case)
- Design docs: `docs/design/{system}/.../{item}.md` (kebab-case)
- Verification design: `docs/verification/{system}/.../{item}.md` (kebab-case)
- SysML2 model: `docs/sysml2/model/{system}/.../{item}.sysml` (kebab-case)
- Source code: `src/{System}/.../{Item}.cs` (PascalCase for C#)
- Tests: `test/{System}.Tests/.../{Item}Tests.cs` (PascalCase for C#)

A unit keeps the folder of its parent subsystem in every artifact tree, whatever the source folder is. For example, the
`LintMarkEngine` unit of the `Engine` subsystem has the design document
`docs/design/lintmark/engine/lint-mark-engine.md`, the requirements file
`docs/reqstream/lintmark/engine/lint-mark-engine.yaml`, the verification document
`docs/verification/lintmark/engine/lint-mark-engine.md`, and the source file `Engine/LintMarkEngine.cs`; the `Reporters`
unit has the same artifact folders but its source is in `Reporting/`. A nested subsystem keeps the folder of its parent:
the `Blocks` subsystem has `docs/design/lintmark/formatting/blocks.md` and its units are under
`docs/design/lintmark/formatting/blocks/`.

A unit may own several source files and its tests may be spread over several test files (for example the `LintRules`
unit owns four rule families). A supporting type, such as a record, enumeration, or interface, has no document of its
own: it is described in the design of the unit that owns it (for example `Diagnostic` in the `RuleCatalog` design,
`ReferenceIndex` in the `DocumentParser` design, and `TextReporter` in the `Reporters` design). The Folder Layout above
and the SysML2 model name the files of each unit.

OTS items have integration and usage design documentation parallel to the system folders:

- Requirements: `docs/reqstream/ots/{ots-name}.yaml`
- Design: `docs/design/ots/{ots-name}.md`
- Verification: `docs/verification/ots/{ots-name}.md`
- Tests (when no other evidence exists): `test/OtsSoftwareTests/{OtsName}Tests.cs` (PascalCase for C#)

Review-sets are defined in `.reviewmark.yaml`.

## References

- LintMark User Guide
- LintMark Repository (<https://github.com/demaconsulting/LintMark>)
- CommonMark Specification (<https://spec.commonmark.org/>)
