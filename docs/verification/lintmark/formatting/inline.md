### Inline

#### Verification Approach

The `Inline` subsystem is verified by integration tests in `InlineSubsystemTests.cs`. The tests drive the inline units
through their composition with the real `DocumentParser`, `DocumentFormatter`, `InlineNormalizer`, `ParagraphFormatter`
(which uses `AtomTokenizer` and `LineWrapper`), and `LinkRefactorer`, and apply the edits with `SourcePatcher`. Nothing
is replaced by a double. Each test builds a small document inline and asserts the exact text and the reported
violations. The units of the subsystem have their own verification documents; this document covers what they do
together.

#### Test Environment

The tests need the standard test runner and in-memory text. The shared helper `BlockFormatterAssertions` calls the
HTML-equivalence and structure checks of the development tool `tools/LintMark.Verify` as a test oracle, so the test
project references that tool. The tool is not shipped and is not a software item; it is built with the tests and needs
no network access and no Pandoc installation.

#### Acceptance Criteria

- All subsystem tests pass with zero failures.
- Emphasis, strong emphasis, bare URLs, and code-span padding are normalized in paragraphs, and never in code, HTML,
  math, links, headings, or tables.
- Wrapping keeps links, code spans, HTML, and hard breaks whole, never starts a line with block syntax, and reports what
  cannot be fixed.
- Long inline links become reference links with one definitions block, and a second run changes nothing.
- A suppressed rule leaves its part alone.

#### Test Scenarios

**Inline styles are normalized**: A paragraph with underscore emphasis, asterisk strong emphasis, a bare URL, and a
padded code span is written with the configured delimiters, angle brackets, and no padding.

**Protected inline content is kept**: Delimiters inside code, HTML, math, and link destinations, and URLs already
linked, are not changed.

**Paragraph is wrapped without splitting atoms**: A long paragraph with a link, a code span, an inline HTML tag, and a
hard break is wrapped, and each of them stays whole on one line.

**Sentence and semantic layouts**: The same paragraph is laid out one sentence per line and by clauses, and a second run
changes nothing.

**Unfixable overflow is reported**: A paragraph with a single atom wider than the width is kept and the overflow is
reported.

**Long links become reference links**: A paragraph with long inline links and an image is converted with the labels and
the definitions block at the end of the document, and a link that is short, in a heading, or in code is not touched.

**Wrap and link refactor compose**: A long link in an overlong paragraph is converted before the wrap, and the result is
stable.

**Suppression leaves a part alone**: A suppressed rule, by identifier or alias, keeps its part, and the other rules
still apply.
