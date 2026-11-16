# OTS Software Integration Design

This document provides the integration and usage design for all Off-The-Shelf (OTS) software items used by LintMark.

## Scope

OTS items are third-party tools and libraries consumed by the project. This document covers the integration pattern and
usage design for each item. The internal design of OTS items is out of scope; only how this project integrates and
uses each item is documented. The OTS items fall into two groups:

- **Runtime libraries** that are part of the shipped tool: Markdig, FileSystemGlobbing, YamlDotNet, EditorConfig, and
  TestResults. They are modeled as parts in the SysML2 model and are referenced by the local software items that use
  them.
- **Build and pipeline tools** used to build, verify, and document the tool: BuildMark, FileAssert, Pandoc, ReqStream,
  ReviewMark, SarifMark, SonarMark, SysML2Tools, VersionMark, WeasyPrint, and xUnit. They are not part of the shipped
  tool.

## OTS Items

The following OTS items have integration design documentation:

- BuildMark - build-notes documentation tool
- EditorConfig - editor settings (`.editorconfig`) library (runtime)
- FileAssert - document assertion tool
- FileSystemGlobbing - glob pattern matching library (runtime)
- Markdig - Markdown parsing library (runtime)
- Pandoc - Markdown-to-HTML conversion tool
- ReqStream - requirements traceability tool
- ReviewMark - file review enforcement tool
- SarifMark - SARIF report conversion tool
- SonarMark - SonarCloud quality report tool
- SysML2Tools - architecture model validation and diagram rendering tool
- TestResults - TRX and JUnit result file library (runtime)
- VersionMark - tool-version documentation tool
- WeasyPrint - HTML-to-PDF conversion tool
- xUnit - unit-testing framework
- YamlDotNet - YAML parsing library (runtime)

## Selection Criteria

An OTS item is adopted only when its license is compatible with the MIT license of LintMark and its redistribution
terms allow it to be shipped (runtime libraries) or used in the pipeline (tools). The item must be actively maintained
by a community or vendor with a track record of fixing defects and security issues, and it must do one well-defined job
that the project would otherwise have to build and verify itself. Preference goes to items that supply their own
self-validation evidence (`--validate` results) or whose behavior is straightforward to exercise with focused
integration tests. Runtime libraries are chosen for a small, stable API surface, because each one is part of the
shipped tool.

## Version Management Policy

Upgrades are initiated by Dependabot, which proposes NuGet package and GitHub Actions updates every week. The pinned
.NET tools in the local tool manifest (`.config/dotnet-tools.json`) are updated by hand. Every upgrade goes through a
pull request, and the normal CI run (build, tests, OTS self-validation, linting, and document generation) must pass
before it is merged. A major version change, or a release note that mentions a behavior change, triggers a review of
the matching integration design and verification pages. Builds are reproducible because package versions are pinned
in the project files and tool versions are pinned in the tool manifest; the tool versions used by a release are
captured with VersionMark and recorded in the build notes. Version numbers are deliberately not repeated in this
document.

## General Integration Approach

Runtime libraries are consumed directly through their public API by the unit that needs them, and each library is
used by one subsystem or a few units so that an upgrade has a small blast radius. Build and pipeline tools are
consumed as command-line programs, either as pinned .NET tools run through `dotnet`, or as GitHub Actions steps, and
their outputs (reports, documents, TRX files) are the interface. Failures of an OTS item surface as a failed build or
a failed lint step; the tool never continues silently with a missing or broken OTS result.

## Qualification Strategy

Runtime libraries are qualified by integration tests in the dedicated OTS test project, which exercise every feature
listed in the item's integration design against the packages directly. Pipeline tools are qualified by their own
self-validation results, saved as TRX files and traced to the OTS requirements by ReqStream, and by FileAssert
assertions on the documents they produce. Before a version upgrade is accepted, the vendor release notes are read, the
OTS tests and self-validations are re-run, and the integration design is updated when a feature it lists has changed.

## References

N/A
