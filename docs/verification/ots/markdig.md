## Markdig Verification

This document provides the verification evidence for the Markdig OTS software item. Requirements for this
OTS item are defined in the Markdig OTS Software Requirements document.

### Required Functionality

Markdig is the Markdown parsing library used by the `Document` subsystem. LintMark depends on it to report
exact source ranges for every block and inline element, to recognize the fixed set of Markdown extensions,
and to parse on many threads with one shared pipeline.

### Verification Approach

Markdig has no self-validation mode. It is verified by integration tests in the dedicated OTS test project
`test/OtsSoftwareTests` (`MarkdigTests.cs`), which reference the Markdig package directly and not the
LintMark project. Each scenario names a test that passes only when the library behaves as LintMark relies
on. Tests that record known inaccuracies of the library (unclosed fences, trailing newlines) pin the
behavior the `Document` subsystem compensates for, so that a library update that changes it is noticed.

### Test Scenarios

#### Markdig_SourceSpans_BlocksAndInlines_AreSourceAccurate

**Scenario**: A document is parsed with precise source locations and the ranges of blocks and inlines are
compared with the source text.

**Expected**: Every reported range covers exactly the text of its element.

**Requirement coverage**: `LintMark-OTS-Markdig-Spans`.

#### Markdig_LinkReferenceDefinitions_ExposeLabelUrlTitleAndSpan

**Scenario**: A document with link reference definitions is parsed.

**Expected**: Each definition exposes its label, destination, title, and its own source range.

**Requirement coverage**: `LintMark-OTS-Markdig-Spans`.

#### Markdig_UnclosedFence_SpanCoversOnlyOpeningLine

**Scenario**: A fenced code block without a closing fence is parsed.

**Expected**: The reported range covers only the opening line, the behavior the `Document` subsystem
corrects.

**Requirement coverage**: `LintMark-OTS-Markdig-Spans`.

#### Markdig_ListSpan_IncludesTrailingNewline

**Scenario**: A list is parsed and its range is compared with the text.

**Expected**: The range includes the trailing newline, the behavior the `Document` subsystem corrects.

**Requirement coverage**: `LintMark-OTS-Markdig-Spans`.

#### Markdig_Pipeline_RequiredExtensions_RecognizedBlocks

**Scenario**: A document using each required extension is parsed with the pipeline.

**Expected**: Pipe tables, grid tables, front matter, footnotes, task lists, strikethrough, alerts, math,
and automatic links are all recognized.

**Requirement coverage**: `LintMark-OTS-Markdig-Extensions`.

#### Markdig_PipeTables_GfmRules_EscapedPipeInCodeSpanStaysInCell

**Scenario**: A pipe table cell contains a code span with an escaped pipe, parsed with the GFM rules.

**Expected**: The cell is not split at the escaped pipe.

**Requirement coverage**: `LintMark-OTS-Markdig-Extensions`.

#### Markdig_HtmlComments_BlockAndInline_AreRecognized

**Scenario**: Block-level and inline HTML comments are parsed.

**Expected**: Block comments are HTML blocks of the comment type and inline comments are inline HTML.

**Requirement coverage**: `LintMark-OTS-Markdig-Extensions`.

#### Markdig_SharedPipeline_ParallelParse_IdenticalResults

**Scenario**: Many threads parse documents at the same time with one shared pipeline.

**Expected**: Every result equals the result of a sequential parse.

**Requirement coverage**: `LintMark-OTS-Markdig-Concurrency`.
