# OTS Software Verification

This section provides verification evidence for all Off-The-Shelf (OTS) software items used by the LintMark.

## Scope

Each OTS item is verified by a combination of:

- The OTS tool's own self-validation suite (where the tool supports `--validate`)
- Pipeline output assertions performed by FileAssert on documents produced by the tool
- Integration tests in the dedicated OTS test project `test/OtsSoftwareTests` for runtime libraries that have no
  self-validation mode (Markdig, FileSystemGlobbing, YamlDotNet, EditorConfig, and TestResults)

Internal OTS tool design is out of scope; only integration and usage evidence is documented. The OTS test project
references the packages directly and not the LintMark project, so a library update that changes a behavior that
LintMark relies on is noticed even when LintMark itself still passes.

## OTS Items

Runtime libraries (shipped with the tool):

- EditorConfig (*editorconfig.md*) — editor settings (`.editorconfig`) evaluation library
- FileSystemGlobbing (*filesystemglobbing.md*) — glob pattern matching library
- Markdig (*markdig.md*) — Markdown parsing library
- TestResults (*testresults.md*) — TRX and JUnit result file writer
- YamlDotNet (*yamldotnet.md*) — YAML parsing library

Development and build tools (not shipped):

- BuildMark (*buildmark.md*) — build-notes documentation tool
- FileAssert (*fileassert.md*) — document assertion tool
- Pandoc (*pandoc.md*) — Markdown-to-HTML conversion tool
- ReqStream (*reqstream.md*) — requirements traceability tool
- ReviewMark (*reviewmark.md*) — file review enforcement tool
- SarifMark (*sarifmark.md*) — SARIF report conversion tool
- SonarMark (*sonarmark.md*) — SonarCloud quality report tool
- SysML2Tools (*sysml2tools.md*) — architecture model validation and diagram rendering tool
- VersionMark (*versionmark.md*) — tool-version documentation tool
- WeasyPrint (*weasyprint.md*) — HTML-to-PDF conversion tool
- xUnit (*xunit.md*) — unit-testing framework
