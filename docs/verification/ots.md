# OTS Software Verification

This section provides verification evidence for all Off-The-Shelf (OTS) software items used by the LintMark.

## Scope

Each OTS item is verified by a combination of:

- The OTS tool's own self-validation suite (where the tool supports `--validate`)
- Pipeline output assertions performed by FileAssert on documents produced by the tool
- Integration tests in the dedicated OTS test project `test/OtsSoftwareTests` for runtime libraries that have no
  self-validation mode (Markdig, FileSystemGlobbing, YamlDotNet, EditorConfig, and TestResults)
- The help tests of the tool itself for HelpCards, a runtime library whose features are only used with the help cards
  that the tool ships

Internal OTS tool design is out of scope; only integration and usage evidence is documented. The OTS test project
references the packages directly and not the LintMark project, so a library update that changes a behavior that
LintMark relies on is noticed even when LintMark itself still passes.

## OTS Items

Runtime libraries (shipped with the tool):

- EditorConfig (*editorconfig.md*) — editor settings (`.editorconfig`) evaluation library
- FileSystemGlobbing (*filesystemglobbing.md*) — glob pattern matching library
- HelpCards (*help-cards.md*) — built-in help library
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

## Verification Strategy

OTS items are verified in three ways, chosen by how the item is used. Runtime libraries have no self-validation mode,
so they are exercised by integration tests in the dedicated OTS test project, which call the same library features that
the integration design lists; HelpCards is the exception and is exercised by the help tests of the tool, with the cards
that the tool ships. Pipeline tools that support `--validate` rely on that self-validation, run in CI and saved
as TRX results. Tools that produce documents are also checked by FileAssert assertions on their output, which show that
the tool ran and produced usable content.

## Qualification Evidence

Evidence is collected on every CI run: the OTS test project results, the results of the help tests of the tool (for
HelpCards), the self-validation TRX files of the pipeline
tools, and the FileAssert results for the generated documents. ReqStream traces each OTS requirement to this evidence
and fails the build when an OTS requirement has no passing test. The evidence for each item is named in its own page
below.

## Regression Approach

A version upgrade of an OTS item, proposed by Dependabot or made by hand in the tool manifest, runs through the same CI
pipeline as any change, so all OTS tests, self-validations, and document assertions are re-run against the new
version. The vendor release notes are reviewed for behavior changes; when a feature that the integration design lists
has changed, the OTS test or the design and verification pages are updated in the same pull request before it is
merged.
