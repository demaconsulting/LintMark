#### InlineNormalizer

##### Verification Approach

`InlineNormalizer` (with `InlineCandidates`) is verified with unit tests in `InlineNormalizerTests.cs`. The real parser
(Markdig through `DocumentParser`), `LintMarkConfig`, `EditSet`, and `SourcePatcher` are used; nothing is replaced by a
double. `InlineCandidates` is tested directly for the order and uniqueness of the candidate edits, and the normalizer is
tested on whole paragraphs. Tests assert the exact text, the credited rule, and stability on a second run.

##### Test Environment

The tests need the standard test runner and in-memory text. The shared helper `BlockFormatterAssertions` calls the
HTML-equivalence and structure checks of the development tool `tools/LintMark.Verify` as a test oracle, so the test
project references that tool. The tool is not shipped and is not a software item; it is built with the tests and needs
no network access and no Pandoc installation.

##### Acceptance Criteria

- All unit tests pass with zero failures.
- Emphasis and strong emphasis use the configured delimiters, within-word emphasis keeps its asterisks, and unsafe
  emphasis keeps the paragraph.
- Bare URLs are wrapped in angle brackets unless they are already linked, attached to other text, in HTML, with an
  unsupported scheme or address kind, or when the setting says to keep them.
- A code span loses symmetric single padding and keeps padding that is asymmetric, multiple, at a backtick edge, across
  lines, with a tab, or only spaces.
- Headings, tables, code, HTML, math, and link destinations are never normalized.
- Candidates are returned in start order and once each, and nothing is returned when the rules are off.
- Each edit is credited to its rule, and a suppressed rule leaves only its part.

##### Test Scenarios

**Emphasis delimiters are normalized**: Underscores become asterisks and asterisks become underscores as configured,
next to links, code, and breaks, and in containers and footnotes.

**Within-word emphasis keeps asterisks**: Emphasis inside a word is not changed.

**Unsafe emphasis keeps the paragraph**: A paragraph that may render differently after the change is left, and the rest
is normalized.

**Bare URLs are wrapped**: A bare URL gets angle brackets, also at the start of a block after a heading.

**URLs that are not rewritten**: Linked, attached, HTML, brace, mail, `www`, and upper-case-scheme URLs are kept. The
`leave` setting keeps URLs and still normalizes emphasis.

**Code-span padding is removed**: A symmetric padded code span loses its padding and is stable afterward.

**Code spans that keep padding**: Asymmetric or multiple padding, a backtick at an edge, a span across lines or with a
tab, and a span of spaces are kept. Code spans in headings and tables are kept.

**Delimiters in protected content are kept**: Delimiters in code spans, HTML, math, and link destinations are not
changed.

**Headings and tables are not normalized**: Paragraph rules do not apply to them.

**Candidates are ordered and unique**: Emphasis, strong, and a URL come in start order, the same emphasis in two places
gives one edit each, and disabled rules give none.

**Suppressed or disabled rule leaves only its part**: A suppressed or disabled rule keeps its part, and the other rules
still apply.

**Edits are credited to their rules**: A non-canonical paragraph reports each edit under its rule.
