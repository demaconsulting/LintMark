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
LintMark project. Each scenario describes a behavior that LintMark relies on and that the tests confirm. The
scenarios that record known deviations of the library (an unclosed fence reporting only its opening line, a
list reporting its trailing line feed, reference definitions grouped in a shell block) pin the behavior that
the `Document` subsystem compensates for, so that a library update that changes it is noticed. The
behaviors specific to footnote definitions (where a footnote's range starts, which definitions the library
drops, how a definition inside a quote or list item is read) are pinned through the `DocumentParser` unit
tests, because the compensating logic is verified there.

### Test Scenarios

#### Block and inline ranges match the source text

**Scenario**: A document is parsed with precise source locations and the ranges of blocks and inlines are
compared with the source text.

**Expected**: Every reported range covers exactly the text of its element.

**Requirement coverage**: `LintMark-OTS-Markdig-Spans`.

#### Link reference definitions expose label, destination, title, and range

**Scenario**: A document with link reference definitions is parsed.

**Expected**: Each definition exposes its label, destination, title, and its own source range; the definitions
are held in a shell block whose own range is not meaningful, the behavior the `Document` subsystem corrects.

**Requirement coverage**: `LintMark-OTS-Markdig-Spans`.

#### Unclosed fence range covers only the opening line

**Scenario**: A fenced code block without a closing fence is parsed.

**Expected**: The reported range covers only the opening line, the behavior the `Document` subsystem
corrects.

**Requirement coverage**: `LintMark-OTS-Markdig-Spans`.

#### List range includes the trailing line feed

**Scenario**: A list is parsed and its range is compared with the text.

**Expected**: The range includes the trailing newline, the behavior the `Document` subsystem corrects.

**Requirement coverage**: `LintMark-OTS-Markdig-Spans`.

#### Every required extension is recognized

**Scenario**: A document using each required extension is parsed with the pipeline.

**Expected**: Pipe tables, grid tables, front matter, footnotes, task lists, strikethrough, alerts, math,
and automatic links are all recognized.

**Requirement coverage**: `LintMark-OTS-Markdig-Extensions`.

#### Escaped pipe in a code span stays in its table cell

**Scenario**: A pipe table cell contains a code span with an escaped pipe, parsed with the GFM rules.

**Expected**: The cell is not split at the escaped pipe.

**Requirement coverage**: `LintMark-OTS-Markdig-Extensions`.

#### Block and inline HTML comments are recognized

**Scenario**: Block-level and inline HTML comments are parsed.

**Expected**: Block comments are HTML blocks of the comment type and inline comments are inline HTML.

**Requirement coverage**: `LintMark-OTS-Markdig-Extensions`.

#### One shared pipeline parses on many threads with identical results

**Scenario**: Many threads parse documents at the same time with one shared pipeline.

**Expected**: Every result equals the result of a sequential parse.

**Requirement coverage**: `LintMark-OTS-Markdig-Concurrency`.
