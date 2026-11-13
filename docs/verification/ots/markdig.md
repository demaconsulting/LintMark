## Markdig Verification

This document provides the verification evidence for the Markdig OTS software item. Requirements for this
OTS item are defined in the Markdig OTS Software Requirements document.

### Required Functionality

Markdig is the Markdown parsing library used by the `Document` subsystem and the rendering library used by the
`Safety` subsystem. LintMark depends on it to report exact source ranges for every block and inline element, to
recognize the fixed set of Markdown extensions, to parse on many threads with one shared pipeline, and to render the
same text to the same HTML every time, also on many threads, so that the HTML before and after a rewrite can be
compared.

### Verification Approach

Markdig has no self-validation mode. It is verified by integration tests in the dedicated OTS test project
`test/OtsSoftwareTests` (`MarkdigTests.cs`), which reference the Markdig package directly and not the
LintMark project. Each scenario describes a behavior that LintMark relies on and that the tests confirm. The
scenarios that record known deviations of the library (an unclosed fence reporting only its opening line, a
list reporting its trailing line feed, reference definitions grouped in a shell block, a rejected next-line
title kept in a definition's span, a definition that survives inside a dropped footnote, and a code span that renders
differently for CRLF and LF text) pin the behavior that the `Document` and `Safety` subsystems compensate for, so
that a library update that changes it is noticed. The
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

#### Definition with a rejected next-line title keeps the title in its span

**Scenario**: A definition followed by a line that starts like a title but has text after it, a definition with
an empty title, and a definition without a title are parsed.

**Expected**: The rejected title is null but still inside the definition's span (and its title span is not
empty), an empty title is an empty string, and a definition without a title has an empty title span; the
behavior the `Document` subsystem corrects by ending the definition at its destination.

**Requirement coverage**: `LintMark-OTS-Markdig-Spans`.

#### A definition inside a dropped footnote is still a definition

**Scenario**: A footnote definition that nothing refers to, with a link reference definition behind its marker,
is parsed.

**Expected**: The parser produces no footnote block, but the inner definition exists with its label and its own
range; the behavior the `Document` subsystem relies on when it ends the dropped footnote before the definition.

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

#### The same text and pipeline render to identical HTML

**Scenario**: A document with a heading, emphasis, a code span, a link with a title, a list, and a pipe table is
rendered repeatedly with one pipeline and once with a second pipeline that is built the same way.

**Expected**: All renderings are identical.

**Requirement coverage**: `LintMark-OTS-Markdig-HtmlRendering`.

#### A multi-line code span renders differently for CRLF and LF text

**Scenario**: A code span whose content is framed by line breaks is rendered once with LF line endings and once
with CRLF line endings.

**Expected**: With LF the line breaks become spaces that are stripped and the HTML is `<p><code>bb</code></p>`;
with CRLF the spaces stay and the HTML is `<p><code> bb </code></p>`. The two differ, which is why LintMark
compares the rendering of a rewrite with the line endings it has on disk as well as with normalized line endings, and a
library update that removes the difference is noticed.

**Requirement coverage**: `LintMark-OTS-Markdig-HtmlRendering`.

#### One shared pipeline renders on many threads with identical results

**Scenario**: Five different documents (a heading with a code span and emphasis, a list and a quote, a pipe table, a
grid table, and a footnote) are rendered once on one thread to get the expected HTML, then 128 renderings of them
are made from up to 16 threads through the same shared pipeline.

**Expected**: Every result equals the single-threaded HTML of its document.

**Requirement coverage**: `LintMark-OTS-Markdig-HtmlRendering`.

#### Grid tables and pipe tables render as tables

**Scenario**: A pipeline with only the pipe table and grid table extensions renders a grid table followed by a pipe
table.

**Expected**: The HTML holds two tables, the grid table with plain cells (`a`, `b`, `1`, `2`) and the pipe table
with a header row (`c`, `d`) and body cells (`3`, `4`).

**Requirement coverage**: `LintMark-OTS-Markdig-HtmlRendering`.
