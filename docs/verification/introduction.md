# Introduction

This document provides the verification design for the LintMark, a .NET command-line
tool from DEMA Consulting for markdown linting and formatting.

## Purpose

The purpose of this document is to describe how each requirement for the LintMark is
verified. For every software item — system, subsystem, and unit — this document names the
verification approach, identifies the test scenarios (including boundary conditions and error
paths), describes what is mocked or stubbed, and maps each requirement to at least one named
test scenario. The document does not restate design; it explains how the design is proven correct.

## Scope

This document covers the verification design for the following software items:

**Local items:**

- **LintMark** — system, subsystem, and unit verification:
  - **Program** — entry point and execution orchestrator
  - **Cli** subsystem
    - **Context** — argument parser and I/O owner
  - **Document** subsystem
    - **SourceDocument** — decoding, line-ending normalization, and positions
    - **DocumentParser** — fixed-pipeline parsing into corrected, source-ordered blocks
    - **SuppressionMap** — suppression comment resolution
    - **ReferenceIndex** — link reference definition index
  - **Engine** subsystem
    - **FileDiscovery** — expansion of path, directory, glob, and exclusion arguments
    - **LintMarkEngine** — parallel processing and deterministic failure reporting
  - **SelfTest** subsystem
    - **Validation** — self-validation test runner
  - **Utilities** subsystem
    - **PathHelpers** — safe path combination utilities

**OTS items:**

- **BuildMark** — build-notes documentation tool
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
│   ├── Document/                   — Document subsystem and unit tests, with test-support helpers
│   ├── Engine/                     — Engine subsystem and unit tests
│   ├── SelfTest/                   — SelfTest subsystem and Validation tests
│   └── Utilities/                  — Utilities subsystem and PathHelpers tests
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
