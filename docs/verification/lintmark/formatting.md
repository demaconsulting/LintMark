## Formatting

### Verification Approach

The `Formatting` subsystem is verified by integration tests in `FormattingSubsystemTests.cs`. Each test parses a small
Markdown text with the real `DocumentParser`, runs a real `DocumentFormatter` that is built with block formatters
from the test helpers, and applies the result with the real `SourcePatcher`. A private helper formats a document and
patches it in one step. The tool as shipped has no block formatter that changes text, so the tests use test doubles
that exist only in the test project:

- `ReplacingFormatter` (from `TestBlockFormatters.cs`) is a simple and correct paragraph formatter that replaces one
  text by another and is registered under the `paragraph-wrap` rule, so suppression comments for that rule apply.
- `SubsystemHeadingUppercaseFormatter` (defined in the subsystem test file) writes headings in upper case and is
  registered under the `heading-style` rule. It lets a test combine formatters for different kinds of block.
- `OverlappingEditsFormatter` (from `TestBlockFormatters.cs`) is a deliberately broken formatter that proposes two
  edits of the same characters.

The units are not replaced by doubles, so the tests show that the edit set, the formatter, and the patcher agree with
each other. Per-unit details are verified in the unit documents.

### Test Environment

N/A - standard test environment. The tests use only in-memory text and bytes; no file is read or written.

### Acceptance Criteria

- All integration tests pass with zero failures.
- Edits that two formatters propose for different blocks are both applied, in document order, and the text that no
  edit covers is unchanged.
- Without a block formatter the patched text is the very same string as the source text.
- Two edits of the same characters make formatting fail instead of choosing one of them.
- A block in which the rule of its formatter is suppressed stays as written, and other blocks are still formatted.
- A document that is flagged as unsafe to rewrite yields no edit and no problem.
- A file that uses CRLF is patched as LF text, the `line-ending` problem is reported, and the bytes that are written
  use the line ending that the configuration asks for.

### Test Scenarios

#### Normal operation

**Edits of several formatters are all applied in order**: A document with a heading and a paragraph that contains a
double space is formatted by a `ReplacingFormatter` (double space to single space) and the heading formatter. The
result holds two edits, the heading edit (rule `heading-style`) first and the paragraph edit (rule `paragraph-wrap`)
second, and the patched text is `# TITLE`, a blank line, and `body text`.

**A document without formatters stays identical**: A document with a double space is formatted by a
`DocumentFormatter` that has no block formatters. The result has no edit, and the patched text is the same string
instance as the source text.

**A suppressed block stays verbatim while other blocks are formatted**: A paragraph inside a region that disables
`paragraph-wrap`, and one paragraph after the region, are formatted with a `ReplacingFormatter`. Only the paragraph
after the region is changed, and the paragraph inside the region and both comments are unchanged.

**A CRLF file is patched as LF text and reported**: A file with CRLF line endings and two double spaces is parsed
from bytes and formatted with a configuration that asks for LF. The patched text has the double spaces removed and no
carriage return. The result holds one `line-ending` problem and the line ending LF, and the bytes produced from the
patched text with that line ending are the LF text.

#### Boundary conditions

**A document that is unsafe to rewrite produces nothing**: A document with a line that looks like the border of a grid
table, but is not a valid grid table, is flagged as unsafe to rewrite. Formatting it with a `ReplacingFormatter`
yields no edit and no problem, and the patched text equals the source.

#### Error handling

**Overlapping edits are rejected**: A `DocumentFormatter` with an `OverlappingEditsFormatter` formats a one-paragraph
document. The call throws `InvalidOperationException` instead of choosing one of the two edits.
