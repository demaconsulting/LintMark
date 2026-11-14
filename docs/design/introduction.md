# Introduction

This document provides the detailed design for the LintMark, a .NET command-line
tool from DEMA Consulting for markdown linting and formatting. It covers local
software items (systems, subsystems, and units) and the OTS software items they consume.

## Purpose

The purpose of this document is to describe the internal design of each software unit that
comprises the LintMark. It captures data models, algorithms, key methods, and
inter-unit interactions at a level of detail sufficient for formal code review, compliance
verification, and future maintenance. A reviewer should be able to understand how each item
satisfies its requirements without reading source code. The document does not restate
requirements; it explains how they are realized.

## Scope

Local items:

- **LintMark**: system, subsystem, and unit design for all local components.

OTS items:

- **BuildMark**: integration and usage design.
- **EditorConfig**: integration and usage design.
- **FileAssert**: integration and usage design.
- **FileSystemGlobbing**: integration and usage design.
- **Markdig**: integration and usage design.
- **Pandoc**: integration and usage design.
- **ReqStream**: integration and usage design.
- **ReviewMark**: integration and usage design.
- **SarifMark**: integration and usage design.
- **SonarMark**: integration and usage design.
- **SysML2Tools**: integration and usage design.
- **VersionMark**: integration and usage design.
- **WeasyPrint**: integration and usage design.
- **xUnit**: integration and usage design.
- **YamlDotNet**: integration and usage design.

The following topics are out of scope:

- Design documents are not produced for the test projects or build pipeline CI configuration
- The internal design of OTS software items is excluded; only integration and usage design is documented

## Software Structure

The software structure is modeled in SysML2 under `docs/sysml2/` and rendered to the diagram
below by SysML2Tools as part of the build pipeline. AI agents should query the SysML2 model
directly (see the `sysml2tools-query` skill) rather than parsing this diagram or the prose
elsewhere in this document. The model captures the shipped system's runtime composition only:
the local software items and the OTS libraries the shipped tool consumes (Markdig,
FileSystemGlobbing, YamlDotNet, and EditorConfig, modeled in `docs/sysml2/model/ots.sysml`). The remaining OTS
items listed in Scope above are build-time/pipeline tooling, documented separately (see `docs/design/ots.md`)
rather than modeled as SysML2 parts. The system is made of the entry point `Program` and ten subsystems: `Cli`,
`Configuration`, `Document`, `Engine`, `Formatting`, `Linting`, `Reporting`, `Safety`, `SelfTest`, and `Utilities`.
`Linting` (findings and the rule registry), `Formatting` (edits and the document formatter), `Safety` (the checks that
decide whether a rewrite may be written), and `Reporting` (text and diff output) are the subsystems that make rewriting
files possible and safe; `AtomicFileWriter` is a unit of the `Engine` subsystem and `TextWidth` a unit of the
`Utilities` subsystem. `Formatting` contains two nested subsystems: `Blocks`, which holds the block formatters of the
tool (`HeadingFormatter`, `CodeBlockFormatter`, `ThematicBreakFormatter`, `ListFormatter`, and `BlockQuoteFormatter`),
and `Inline`, which holds the inline normalizer (`InlineNormalizer`); they are the only nested subsystems.

![Software Structure](SoftwareStructureView.svg)

The diagram below is a placeholder that complements the structure view by showing how data flows
at run time: replace its TODO boxes to show how your tool's entry point, Cli, subsystems, inputs,
and outputs fit together.

![Placeholder architecture diagram: replace the TODO boxes for your tool entry point, Cli,
subsystems, support units, dependencies, inputs, and outputs with the real ones](architecture.svg)

## Folder Layout

- **src/** - source files and projects
  - **DemaConsulting.LintMark/** - main application source
    - **Cli/** - command-line interface subsystem
    - **Configuration/** - configuration subsystem
      - `LintMarkConfig.cs`, `LinkRefactorOptions.cs`, and the twelve setting enumerations
        (`WrapMode.cs`, `HeadingStyle.cs`, `BulletMarker.cs`, `OrderedStyle.cs`, `EmphasisMarker.cs`,
        `FenceMarker.cs`, `CodeBlockStyle.cs`, `HardBreakStyle.cs`, `ThematicBreakStyle.cs`, `TableStyle.cs`,
        `BareUrlStyle.cs`, `TrailingWhitespaceStyle.cs`) - the `LintMarkConfig` unit
      - `ConfigLoader.cs`, `ConfigParser.cs`, `ConfigLoadResult.cs`, `ConfigError.cs` - the `ConfigLoader` unit
      - `EditorConfigResolver.cs`, `EditorConfigResult.cs` - the `EditorConfigResolver` unit
    - **Document/** - document loading and parsing subsystem
      - `ParsedDocument.cs` and `GridTableFallback.cs` - supporting types of the `DocumentParser` unit
    - **Engine/** - file discovery, parallel processing, and writing subsystem
      - `FileDiscovery.cs`, `DiscoveredFile.cs` - the `FileDiscovery` unit
      - `LintMarkEngine.cs`, `FileResult.cs`, `FileChange.cs` - the `LintMarkEngine` unit
      - `AtomicFileWriter.cs` - the `AtomicFileWriter` unit
    - **Formatting/** - formatting subsystem (edits, patching, and the document formatter)
      - `TextEdit.cs`, `EditSet.cs` - the `EditSet` unit
      - `SourcePatcher.cs` - the `SourcePatcher` unit
      - `DocumentFormatter.cs`, `IBlockFormatter.cs`, `FormatContext.cs`, `FormatResult.cs` - the `DocumentFormatter`
        unit, with the internal helpers `LineLayout.cs`, `LineOwnership.cs`, `InlineMap.cs`, `LeafWhitespace.cs`,
        `FrontMatterGuard.cs`, `BlankLinePass.cs`, `BlockSpacingPass.cs`, and `TrailerPass.cs`
      - **Blocks/** - the nested `Blocks` subsystem (block formatters)
        - `HeadingFormatter.cs` - the `HeadingFormatter` unit
        - `CodeBlockFormatter.cs` - the `CodeBlockFormatter` unit
        - `ThematicBreakFormatter.cs` - the `ThematicBreakFormatter` unit
        - `ListFormatter.cs` - the `ListFormatter` unit, with the supporting types `ListModel.cs` and `ListPlan.cs`
        - `BlockQuoteFormatter.cs` - the `BlockQuoteFormatter` unit
      - **Inline/** - the nested `Inline` subsystem (inline rewriting)
        - `InlineNormalizer.cs` - the `InlineNormalizer` unit, with the supporting types `InlineCandidates.cs` and
          `ParagraphProof.cs`
    - **Linting/** - findings and rule registry subsystem
      - `Diagnostic.cs`, `DiagnosticKind.cs` - the `Diagnostic` unit
      - `RuleCatalog.cs`, `RuleInfo.cs` - the `RuleCatalog` unit
    - **Reporting/** - text and diff output subsystem
      - `TextReporter.cs` - the `TextReporter` unit
      - `DiffReporter.cs`, `FileDiff.cs` - the `DiffReporter` unit
    - **Safety/** - rewrite safety checks subsystem
      - `HtmlEquivalenceChecker.cs`, `StructuralTokenChecker.cs`, `IdempotencyChecker.cs`, `EquivalenceResult.cs` - one
        unit each
      - `SafetyStatus.cs` - supporting type shared by the checks and the engine
    - **SelfTest/** - self-validation subsystem
    - **Utilities/** - shared utilities subsystem (`PathHelpers.cs` and `TextWidth.cs`)
- **test/** - test projects (not covered by design documents)
  - **DemaConsulting.LintMark.Tests/** - unit, subsystem, and system tests for the local items
  - **OtsSoftwareTests/** - integration tests for the OTS libraries

## Document Conventions

Throughout this document:

- Class names, method names, property names, and file names appear in `monospace` font.
- The word **shall** denotes a design constraint that the implementation must satisfy.
- Section headings within each unit chapter follow a consistent structure: overview, data model,
  methods/algorithms, and interactions with other units.
- Text tables are used in preference to diagrams, which may not render in all PDF viewers.

## Companion Artifact Structure

Local software items have corresponding artifacts in parallel directory trees:

- Requirements: `docs/reqstream/{system}/.../{item}.yaml` (kebab-case)
- Design docs: `docs/design/{system}/.../{item}.md` (kebab-case)
- Verification design: `docs/verification/{system}/.../{item}.md` (kebab-case)
- Source code: `src/{System}/.../{Item}.cs` (PascalCase for C#)
- Tests: `test/{System}.Tests/.../{Item}Tests.cs` (PascalCase for C#)

For example, the `Safety` subsystem and its `HtmlEquivalenceChecker` unit have the design documents
`docs/design/lintmark/safety.md` and `docs/design/lintmark/safety/html-equivalence-checker.md`, the source folder
`src/DemaConsulting.LintMark/Safety/`, and the test files `SafetySubsystemTests.cs` and
`HtmlEquivalenceCheckerTests.cs` under `test/DemaConsulting.LintMark.Tests/Safety/`. A unit that is classified as a
unit of one subsystem keeps the folder of that subsystem in every artifact tree: `AtomicFileWriter` is a unit of the
`Engine` subsystem (`docs/design/lintmark/engine/atomic-file-writer.md`) and `TextWidth` is a unit of the `Utilities`
subsystem (`docs/design/lintmark/utilities/text-width.md`). A supporting type that has no design document of its own
is described in the document of the unit that owns it (for example `TextEdit` in the `EditSet` design, and
`SafetyStatus` in the `Safety` subsystem design). A nested subsystem keeps the folder of its parent in every artifact
tree: the `Blocks` subsystem of `Formatting` has the design document `docs/design/lintmark/formatting/blocks.md`, its
units have `docs/design/lintmark/formatting/blocks/heading-formatter.md` and the like, the source folder
`src/DemaConsulting.LintMark/Formatting/Blocks/`, and the test files `BlocksSubsystemTests.cs` and
`HeadingFormatterTests.cs` under `test/DemaConsulting.LintMark.Tests/Formatting/Blocks/`. The `Inline` subsystem of
`Formatting` follows the same pattern: `docs/design/lintmark/formatting/inline.md`, its unit
`docs/design/lintmark/formatting/inline/inline-normalizer.md`, the source folder
`src/DemaConsulting.LintMark/Formatting/Inline/`, and the test files `InlineSubsystemTests.cs` and
`InlineNormalizerTests.cs` under `test/DemaConsulting.LintMark.Tests/Formatting/Inline/`.

OTS items have integration/usage design documentation parallel to system folders:

- Requirements: `docs/reqstream/ots/{ots-name}.yaml`
- Design: `docs/design/ots/{ots-name}.md`
- Verification: `docs/verification/ots/{ots-name}.md`
- Tests (when no other evidence exists): `test/OtsSoftwareTests/{OtsName}Tests.cs` (PascalCase for C#)

Review-sets: defined in `.reviewmark.yaml`

## References

- LintMark User Guide
- LintMark Repository (<https://github.com/demaconsulting/LintMark>)
