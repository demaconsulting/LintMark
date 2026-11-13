# Introduction

This document provides the verification design for the LintMark, a .NET command-line
tool from DEMA Consulting for markdown linting and formatting.

## Purpose

The purpose of this document is to describe how the requirements for the LintMark are
verified. For every software item — system, subsystem, and unit — this document names the
verification approach, identifies the test scenarios (including boundary conditions and error
paths), and describes what is mocked or stubbed. Traceability from each requirement to its tests
is provided by the ReqStream trace matrix, not by these documents. The document does not restate
design; it explains how the design is proven correct.

## Scope

This document covers the verification design for the following software items:

**Local items:**

- **LintMark** — system, subsystem, and unit verification:
  - **Program** — entry point and execution orchestrator
  - **Cli** subsystem
    - **Context** — argument parser and I/O owner
  - **Configuration** subsystem
    - **LintMarkConfig** — validated, immutable settings of one file
    - **ConfigLoader** — discovery, reading, and validation of `.lintmark.yaml` files
    - **EditorConfigResolver** — line ending given by the `.editorconfig` files
  - **Document** subsystem
    - **SourceDocument** — decoding, line-ending normalization, and positions
    - **DocumentParser** — fixed-pipeline parsing into corrected, source-ordered blocks
    - **SuppressionMap** — suppression comment resolution
    - **ReferenceIndex** — link reference definition index
  - **Engine** subsystem
    - **FileDiscovery** — expansion of path, directory, glob, and exclusion arguments
    - **LintMarkEngine** — parallel processing, per-file configuration, safe rewriting, and deterministic reporting
    - **AtomicFileWriter** — all-or-nothing replacement of a file's content
  - **Linting** subsystem
    - **Diagnostic** — one finding with its position and rule identifier
    - **RuleCatalog** — registry of rule identifiers, aliases, and defaults
  - **Formatting** subsystem
    - **EditSet** — validated, ordered collection of text edits
    - **SourcePatcher** — application of an edit set to the source text
    - **DocumentFormatter** — block formatter orchestration and line ending decision
  - **Safety** subsystem
    - **HtmlEquivalenceChecker** — comparison of the rendered HTML of two texts
    - **StructuralTokenChecker** — comparison of structural tokens of two texts
    - **IdempotencyChecker** — proof that formatting the formatted text changes nothing
  - **Reporting** subsystem
    - **TextReporter** — diagnostic output in the documented line format
    - **DiffReporter** — unified diff output of the changes a rewrite would make
  - **SelfTest** subsystem
    - **Validation** — self-validation test runner
  - **Utilities** subsystem
    - **PathHelpers** — safe path combination utilities
    - **TextWidth** — display width of text in terminal columns

**OTS items:**

- **BuildMark** — build-notes documentation tool
- **EditorConfig** — editor settings (`.editorconfig`) evaluation library
- **FileAssert** — document assertion tool
- **FileSystemGlobbing** — glob pattern matching library
- **Markdig** — Markdown parsing library
- **Pandoc** — Markdown-to-HTML conversion tool
- **ReqStream** — requirements traceability tool
- **ReviewMark** — file review enforcement tool
- **SarifMark** — SARIF report conversion tool
- **SonarMark** — SonarCloud quality report tool
- **SysML2Tools** — architecture model validation and diagram rendering tool
- **VersionMark** — tool-version documentation tool
- **WeasyPrint** — HTML-to-PDF conversion tool
- **xUnit** — unit-testing framework
- **YamlDotNet** — YAML parsing library

The following topics are out of scope:

- Verification documents are not produced for the test projects themselves — they are the
  means of verification, not subjects of it
- Build pipeline CI configuration is excluded
- The internal implementation of OTS software items is excluded; only integration and usage
  are verified

## Folder Layout

The test folder structure mirrors the source subsystem breakdown:

```text
test/
├── DemaConsulting.LintMark.Tests/  — unit, subsystem, and system tests
│   ├── Cli/                        — Cli subsystem and Context tests
│   ├── Configuration/              — Configuration subsystem and unit tests
│   ├── Document/                   — Document subsystem and unit tests, with test-support helpers
│   ├── Engine/                     — Engine subsystem and unit tests
│   ├── Formatting/                 — Formatting subsystem and unit tests, with test block formatters
│   ├── Linting/                    — Linting subsystem and unit tests
│   ├── Reporting/                  — Reporting subsystem and unit tests, with a patch applier
│   ├── Safety/                     — Safety subsystem and unit tests
│   ├── SelfTest/                   — SelfTest subsystem and Validation tests
│   └── Utilities/                  — Utilities subsystem, PathHelpers and TextWidth tests
└── OtsSoftwareTests/               — integration tests for the OTS libraries
```

## Companion Artifact Structure

In-house items have corresponding artifacts in parallel directory trees:

- Requirements: `docs/reqstream/{system}/.../{item}.yaml` (kebab-case)
- Design docs: `docs/design/{system}/.../{item}.md` (kebab-case)
- Verification design: `docs/verification/{system}/.../{item}.md` (kebab-case)
- Source code: `src/{System}/.../{Item}.cs` (PascalCase for C#)
- Tests: `test/{System}.Tests/.../{Item}Tests.cs` (PascalCase for C#)

OTS items have parallel artifacts in:

- Requirements: `docs/reqstream/ots/{ots-name}.yaml` (kebab-case)
- Verification: `docs/verification/ots/{ots-name}.md` (kebab-case)
- Tests (when no other evidence exists): `test/OtsSoftwareTests/{OtsName}Tests.cs` (PascalCase for C#)

Review-sets: defined in `.reviewmark.yaml`

## References

- LintMark Software Design Document
- LintMark releases (<https://github.com/demaconsulting/LintMark/releases>)
