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

## References

N/A
