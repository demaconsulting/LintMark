## Formatting

### Verification Approach

The `Formatting` subsystem is verified by integration tests in `FormattingSubsystemTests.cs`. The tests drive the
subsystem through its outer boundary, `DocumentFormatter.Format`, with the production composition: the real
`DocumentParser`, the real block formatters, `InlineNormalizer`, `ParagraphFormatter`, `LinkRefactorer`, and the real
document passes and `EditSet`. Nothing is replaced by a double. Each test builds a small Markdown document inline,
formats it, applies the returned edits with `SourcePatcher`, and asserts the exact resulting text, the reported
violations, and the line ending decision.

The units of the subsystem (`DocumentFormatter`, `WhitespacePasses`, and `EditSet`) and the nested subsystems `Blocks`
and `Inline` have their own verification documents; this document covers only what the units do together.

### Test Environment

N/A - the tests need only the standard test runner. They use in-memory text and no files.

### Acceptance Criteria

- All subsystem tests pass with zero failures.
- A document that breaks the rules of every family is brought into canonical form in one run, and formatting the result
  again proposes no edit.
- Every block is handled by one formatter or by the passes, never by both, and the edits of all stages compose without
  conflict.
- Code, HTML, math, front matter, grid tables, and blocks under a suppression comment keep their text.
- A format-unsafe document gets no edit and no violation.
- The line ending written is the configured one, else the majority one of the file.

### Test Scenarios

**Default formatter composes the edits of all rule families**: A document with a setext heading, an asterisk list, a
padded code span, a pipe table, a long link, trailing whitespace, and a missing final newline is formatted. Each family
changes its part, and a second run proposes nothing.

**Each block is claimed once**: A document with headings, lists, quotes, tables, and paragraphs is formatted. The
formatted text equals the text produced when each block is formatted on its own.

**Passes handle what no block formatter claims**: A document with whitespace-only lines, blank-line runs, and a missing
blank line around a fence is formatted with only the passes registered. The passes fix exactly those parts.

**Edits of different stages compose**: A list that moves and overflows its width is formatted. The final text is the
same as formatting it in two steps, and one run reaches it.

**Protected regions are untouched**: A document with fenced code, an HTML block, display math, and front matter that all
contain trailing whitespace and tabs is formatted. Their text is unchanged.

**Suppression comments leave a block alone**: A block under a disable comment, by rule identifier or by alias, keeps its
text, and the next block is formatted.

**Format-unsafe document is not touched**: A document with a grid table that could not be parsed, in CRLF text with
violations, is formatted. No edit and no violation is returned.

**Line ending follows the configuration or the majority**: An LF file with a CRLF setting and a mixed file without a
setting are formatted. The first reports a conversion to CRLF, and the second targets the majority ending.
