## Markdig Verification

This document provides the verification evidence for the Markdig OTS software item. Requirements for this OTS item
are defined in the Markdig OTS Software Requirements document.

### Required Functionality

Markdig is the Markdown parsing library used by the `Document` subsystem. LintMark depends on it to report exact
source ranges for every block and inline element, to recognize the fixed set of Markdown extensions, and to parse on
many threads with one shared pipeline. Its HTML renderer is also used by the development-time corpus harness
`tools/LintMark.Verify` to compare the rendering of a text before and after formatting; the renderer is deterministic
and thread-safe for that purpose.

### Verification Approach

Markdig has no self-validation mode. It is verified by integration tests in the dedicated OTS test project
`test/OtsSoftwareTests` (`MarkdigTests.cs`), which reference the Markdig package directly and not the LintMark
project. Each scenario describes a behavior that LintMark relies on and that the tests confirm. Scenarios that record
known deviations of the library (an unclosed fence reporting only its opening line, a list reporting its trailing line
feed, reference definitions grouped in a shell block, a rejected next-line title kept in a definition's span, a
definition that survives inside a dropped footnote, and a code span that renders differently for CRLF and LF text) pin
the behavior that the `DocumentParser` unit compensates for, so that a library update that changes it is noticed. The
corrections themselves are verified in the `DocumentParser` unit tests.

### Test Scenarios

#### Block and inline ranges match the source text

**Scenario**: A document is parsed with precise source locations and the ranges of blocks and inlines are compared
with the source text.

**Expected**: Every range covers exactly the text of its element.

#### Word-level inlines expose hard breaks and exact source ranges

**Scenario**: A paragraph with a line end made of two spaces, one made of a backslash, and a plain line end, and with
an entity, an inline math span, and a footnote reference, is parsed together with a task list item and a footnote
definition.

**Expected**: The line ends of spaces and of a backslash are hard breaks (the second a backslash break) and the plain
one is a soft break. The entity, the math span, the footnote reference, and the task marker each report a range that
covers exactly their source text.

#### Link reference definitions expose label, destination, title, and range

**Scenario**: A document with link reference definitions is parsed.

**Expected**: Each definition exposes its label, destination, title, and its own source range; the definitions are
held in a shell block whose own range is not meaningful.

#### Unclosed fence range covers only the opening line

**Scenario**: A fenced code block without a closing fence is parsed.

**Expected**: The reported range covers only the opening line.

#### List range includes the trailing line feed

**Scenario**: A list is parsed and its range is compared with the text.

**Expected**: The range includes the trailing newline.

#### Definition with a rejected next-line title keeps the title in its span

**Scenario**: A definition followed by a line that starts like a title but has text after it, a definition with an
empty title, and a definition without a title are parsed.

**Expected**: The rejected title is null but still inside the definition's span, an empty title is an empty string,
and a definition without a title has an empty title span.

#### A definition inside a dropped footnote is still a definition

**Scenario**: A footnote definition that nothing refers to, with a link reference definition behind its marker, is
parsed.

**Expected**: The parser produces no footnote block, but the inner definition exists with its label and its own range.

#### Every required extension is recognized

**Scenario**: A document using each required extension is parsed with the pipeline.

**Expected**: Pipe tables, grid tables, front matter, footnotes, task lists, strikethrough, alerts, math, and
automatic links are all recognized.

#### Escaped pipe in a code span stays in its table cell

**Scenario**: A pipe table cell contains a code span with an escaped pipe, parsed with the GFM rules.

**Expected**: The cell is not split at the escaped pipe.

#### Block and inline HTML comments are recognized

**Scenario**: Block-level and inline HTML comments are parsed.

**Expected**: Block comments are HTML blocks of the comment type and inline comments are inline HTML.

#### One shared pipeline parses on many threads with identical results

**Scenario**: Many threads parse documents at the same time with one shared pipeline.

**Expected**: Every result equals the result of a sequential parse.

#### The same text and pipeline render to identical HTML

**Scenario**: A document with a heading, emphasis, a code span, a link with a title, a list, and a pipe table is
rendered repeatedly with one pipeline and once with a second pipeline built the same way.

**Expected**: All renderings are identical.

#### A multi-line code span renders differently for CRLF and LF text

**Scenario**: A code span whose content is framed by line breaks is rendered with LF and with CRLF line endings.

**Expected**: With LF the line breaks become stripped spaces (`<p><code>bb</code></p>`); with CRLF the spaces stay
(`<p><code> bb </code></p>`). The corpus harness therefore compares renderings with the real line endings as well as
with normalized ones.

#### One shared pipeline renders on many threads with identical results

**Scenario**: Five different documents are rendered once on one thread, then 128 renderings of them are made from up
to 16 threads through the same shared pipeline.

**Expected**: Every result equals the single-threaded HTML of its document.

#### Grid tables and pipe tables render as tables

**Scenario**: A pipeline with only the pipe table and grid table extensions renders a grid table followed by a pipe
table.

**Expected**: The HTML holds two tables, the grid table with plain cells and the pipe table with a header row and body
cells.
