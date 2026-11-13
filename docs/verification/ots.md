# OTS Software Verification

This section provides verification evidence for all Off-The-Shelf (OTS) software items
used by the LintMark.

## Scope

Each OTS item is verified by a combination of:

- The OTS tool's own self-validation suite (where the tool supports `--validate`)
- Pipeline output assertions performed by FileAssert on documents produced by the tool
- Integration tests in the dedicated OTS test project `test/OtsSoftwareTests` for libraries that have no
  self-validation mode (Markdig, FileSystemGlobbing, YamlDotNet, and EditorConfig)

Internal OTS tool design is out of scope; only integration and usage evidence is documented.

## OTS Items

The following OTS items have verification evidence in this section:

- BuildMark (*buildmark.md*) — build-notes documentation tool
- EditorConfig (*editorconfig.md*) — editor settings (`.editorconfig`) evaluation library
- FileAssert (*fileassert.md*) — document assertion tool
- FileSystemGlobbing (*filesystemglobbing.md*) — glob pattern matching library
- Markdig (*markdig.md*) — Markdown parsing library
- Pandoc (*pandoc.md*) — Markdown-to-HTML conversion tool
- ReqStream (*reqstream.md*) — requirements traceability tool
- ReviewMark (*reviewmark.md*) — file review enforcement tool
- SarifMark (*sarifmark.md*) — SARIF report conversion tool
- SonarMark (*sonarmark.md*) — SonarCloud quality report tool
- SysML2Tools (*sysml2tools.md*) — architecture model validation and diagram rendering tool
- VersionMark (*versionmark.md*) — tool-version documentation tool
- WeasyPrint (*weasyprint.md*) — HTML-to-PDF conversion tool
- xUnit (*xunit.md*) — unit-testing framework
- YamlDotNet (*yamldotnet.md*) — YAML parsing library
