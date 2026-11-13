# OTS Software Integration Design

This document provides the integration and usage design for all Off-The-Shelf (OTS) software
items used by the LintMark.

## Scope

OTS items are third-party tools and libraries consumed by the project: build and documentation
tooling used by the pipeline, and runtime libraries (Markdig, FileSystemGlobbing, YamlDotNet, and EditorConfig)
that are part of
the shipped tool. This document covers the integration pattern and usage design for each item. The
internal design of OTS items is out of scope; only how this project integrates and uses each item is
documented.

## OTS Items

The following OTS items have integration design documentation:

- BuildMark (*buildmark.md*) — build-notes documentation tool
- EditorConfig (*editorconfig.md*) — editor settings (`.editorconfig`) library (runtime)
- FileAssert (*fileassert.md*) — document assertion tool
- FileSystemGlobbing (*filesystemglobbing.md*) — glob pattern matching library (runtime)
- Markdig (*markdig.md*) — Markdown parsing library (runtime)
- Pandoc (*pandoc.md*) — Markdown-to-HTML conversion tool
- ReqStream (*reqstream.md*) — requirements traceability tool
- ReviewMark (*reviewmark.md*) — file review enforcement tool
- SarifMark (*sarifmark.md*) — SARIF report conversion tool
- SonarMark (*sonarmark.md*) — SonarCloud quality report tool
- SysML2Tools (*sysml2tools.md*) — architecture model validation and diagram rendering tool
- VersionMark (*versionmark.md*) — tool-version documentation tool
- WeasyPrint (*weasyprint.md*) — HTML-to-PDF conversion tool
- xUnit (*xunit.md*) — unit-testing framework
- YamlDotNet (*yamldotnet.md*) — YAML parsing library (runtime)

## References

N/A
