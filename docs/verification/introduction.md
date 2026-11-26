# Introduction

This document provides the verification design for the LintMark, a .NET command-line tool from DEMA Consulting for
markdown linting and formatting.

## Purpose

The purpose of this document is to describe how the requirements for the LintMark are verified. For every software
item (system, subsystem, and unit) it names the verification approach, the test environment, the acceptance criteria,
and the test scenarios, including boundary conditions and error paths, and it states what is mocked or stubbed. The
audience is developers, reviewers, and auditors who need to judge whether the tests prove the design. Traceability from
each requirement to its tests is provided by the ReqStream trace matrix, not by these documents. The document does not
restate design; it explains how the design is proven correct.

## Scope

This document covers the verification design for the following software items.

**Local items:**

- **LintMark** — system verification
  - **Program** — entry point and execution orchestrator
  - **Context** — argument parser and output owner
  - **HelpCommand** — the built-in `help` command, answered from embedded help cards
  - **Validation** — self-validation test runner
  - **Configuration** subsystem
    - **LintMarkConfig** — validated, immutable settings of one file
    - **ConfigLoader** — discovery, reading, and validation of `.lintmark.yaml` files
    - **EditorConfigResolver** — line ending given by the `.editorconfig` files
  - **Document** subsystem
    - **SourceDocument** — decoding, line-ending normalization, and positions
    - **DocumentParser** — parsing into corrected, source-ordered blocks, with the reference index
    - **SuppressionMap** — suppression comment resolution
    - **TextWidth** — display width of text in terminal columns
  - **Engine** subsystem
    - **FileDiscovery** — expansion of path, directory, glob, and exclusion arguments
    - **LintMarkEngine** — parallel processing, per-file configuration, safe rewriting, and ordered reporting
    - **AtomicFileWriter** — all-or-nothing replacement of a file's content
    - **Reporters** — text and unified-diff output
  - **Linting** subsystem
    - **RuleCatalog** — registry of rule identifiers, aliases, defaults, and the finding type
    - **LintRules** — the lint rule engine and the heading, link, structure, and table checks
  - **Formatting** subsystem
    - **DocumentFormatter** — orchestration of the block formatters and the document-wide rules
    - **WhitespacePasses** — whitespace, blank-line, block-spacing, and file-end passes
    - **EditSet** — validated text edits, patching, and rebasing across stages
    - **Blocks** subsystem
      - **HeadingFormatter**, **CodeBlockFormatter**, **ThematicBreakFormatter**, **BlockQuoteFormatter**,
        **ListFormatter**, **TableFormatter**, and **ParagraphFormatter** — one formatter per block family
    - **Inline** subsystem
      - **InlineNormalizer** — emphasis, strong emphasis, bare URLs, and code spans
      - **AtomTokenizer** — division of a paragraph into words and unbreakable constructs
      - **LineWrapper** — fill, sentence, and semantic line layout with the wrap guard
      - **LinkRefactorer** — conversion of long inline links to reference links

**OTS items:**

- **Runtime libraries**: EditorConfig, FileSystemGlobbing, HelpCards, Markdig, TestResults, and YamlDotNet
- **Tools**: BuildMark, FileAssert, Pandoc, ReqStream, ReviewMark, SarifMark, SonarMark, SysML2Tools, VersionMark,
  WeasyPrint, and xUnit

The following topics are out of scope:

- Verification documents are not produced for the test projects themselves; they are the means of verification, not
  subjects of it.
- The build pipeline configuration is excluded.
- The internal implementation of OTS items is excluded; only integration and usage are verified.
- The development tool `tools/LintMark.Verify` and its tests are not software items of the system and have no
  verification document of their own. Its role is described below and in the system verification design.

### Verification of formatting correctness

LintMark does not check its own rewrites at run time. The correctness of the formatting (that a rewrite renders to the
same HTML, keeps the same structure, and is stable) is verified at development time by the corpus harness
`tools/LintMark.Verify`. It is a non-shipping development tool: it formats a curated corpus in memory with the
default configuration and with an option matrix and performs the HTML-equivalence, idempotency, and structure
checks described in the system verification design.

The unit and subsystem tests of the formatters depend on this development tool as a test oracle. The test project
`DemaConsulting.LintMark.Tests` references `tools/LintMark.Verify`. The shared helper `BlockFormatterAssertions` (used
by the `Formatting`, `Blocks`, and `Inline` tests) calls its HTML-equivalence check and its structure check on every
safe rewrite it asserts, and a regression test of `ParagraphFormatter` calls the HTML-equivalence check directly. The
tool is not shipped and is not a software item, so these tests verify the shipped formatters against an independent
oracle and the Test Environment of each affected verification document names the dependency. The checks are
deterministic, in-process, and need neither network access nor a Pandoc installation.

## Folder Layout

The test folder structure mirrors the source subsystem breakdown:

```text
test/
├── DemaConsulting.LintMark.Tests/  — unit, subsystem, and system tests
│   ├── Cli/                        — Context and HelpCommand tests
│   ├── Configuration/              — Configuration subsystem and unit tests
│   ├── Document/                   — Document subsystem and unit tests, with test-support helpers
│   ├── Engine/                     — Engine subsystem and unit tests
│   ├── Formatting/                 — Formatting subsystem and unit tests, with test block formatters
│   │   ├── Blocks/                 — Blocks subsystem and block formatter unit tests
│   │   └── Inline/                 — Inline subsystem and unit tests
│   ├── Linting/                    — Linting subsystem and unit tests
│   ├── Reporting/                  — reporter tests, with a patch applier
│   ├── SelfTest/                   — Validation tests
│   └── TestData/HelpExamples/      — input files of the help examples, one folder per card
└── OtsSoftwareTests/               — integration tests for the OTS libraries
```

The system tests and the `Program` tests are in the root of the test project. The development tool and its own tests
are in `tools/LintMark.Verify` and `tools/LintMark.Verify.Tests`.

## Companion Artifact Structure

In-house items have corresponding artifacts in parallel directory trees:

- Requirements: `docs/reqstream/{system}/.../{item}.yaml` (kebab-case)
- Design docs: `docs/design/{system}/.../{item}.md` (kebab-case)
- Verification design: `docs/verification/{system}/.../{item}.md` (kebab-case)
- Source code: `src/{System}/.../{Item}.cs` (PascalCase for C#)
- Tests: `test/{System}.Tests/.../{Item}Tests.cs` (PascalCase for C#)

A unit that owns several source files or test files (for example `LintMarkEngine`, `ConfigLoader`, and `LintRules`)
has one set of companion documents; its supporting types are verified in the document of their owner. A subsystem
has no source file of its own and is verified by one class `{Subsystem}SubsystemTests`.

OTS items have parallel artifacts in:

- Requirements: `docs/reqstream/ots/{ots-name}.yaml` (kebab-case)
- Verification: `docs/verification/ots/{ots-name}.md` (kebab-case)
- Tests (when no other evidence exists): `test/OtsSoftwareTests/{OtsName}Tests.cs` (PascalCase for C#)

Review-sets: defined in `.reviewmark.yaml`

## References

- LintMark Software Design Document
- LintMark releases (<https://github.com/demaconsulting/LintMark/releases>)
