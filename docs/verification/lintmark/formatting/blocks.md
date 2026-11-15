### Blocks

#### Verification Approach

The `Blocks` subsystem is verified by integration tests in `BlocksSubsystemTests.cs`. The tests drive the block
formatters through `DocumentFormatter` with the production composition: the real `DocumentParser`, the heading, code
block, thematic break, list, block quote, table, and paragraph formatters, the `InlineNormalizer`, the `LinkRefactorer`,
and the real `SourcePatcher`. Nothing is replaced by a double.

The shared helper `BlockFormatterAssertions` formats a text and asserts the properties that every rewrite must have: the
text is as expected, it renders to the same HTML and keeps its structure (the checks of the verification tool in
`tools/LintMark.Verify`, used as oracles), and formatting it again proposes nothing. The tests never relax these
assertions to make a case pass. The units of the subsystem have their own verification documents; this document covers
what the formatters do together on one document.

#### Test Environment

The tests need the standard test runner and in-memory text. The shared helper `BlockFormatterAssertions` calls the
HTML-equivalence and structure checks of the development tool `tools/LintMark.Verify` as a test oracle, so the test
project references that tool. The tool is not shipped and is not a software item; it is built with the tests and needs
no network access and no Pandoc installation.

#### Acceptance Criteria

- All subsystem tests pass with zero failures.
- A document that breaks the rules of every block family is brought into canonical form in one run, and the result is
  stable and renders the same.
- Lists are never merged or split, and the structure of lists and quotes is kept.
- Code, HTML, math, and Pandoc constructs inside containers keep their content.
- Tables are written in the configured layout and are never repaired.
- Paragraphs are wrapped only when they overflow, and the result obeys the guard rules.
- A suppressed rule leaves its block alone, by identifier or by alias.

#### Test Scenarios

**Leaf blocks are brought into canonical form**: A document with setext and closed headings, an indented code block, a
tilde fence, and a long thematic break is formatted. Headings are ATX, code is fenced, breaks are `---`, and a second
run changes nothing.

**Containers are brought into canonical form**: A document with an asterisk list, repeated ordered numbers, wide marker
gaps, and quote markers with extra spaces is formatted. Lists use the default marker and counting numbers, quotes have
one space after the marker, and nothing is merged or split.

**Containers nest leaf blocks**: A quote that holds a list, a list that holds code and a heading, and a quote in a list
item are formatted. Every block keeps its prefix and content.

**Tables are written in the configured layout**: A pipe table without outer pipes is formatted with the compact and with
the aligned layout, and a table that cannot fit falls back to compact.

**Paragraphs are filled up to the width**: A long paragraph in a list item and in a quote is wrapped with the prefix
repeated, and a paragraph that fits is unchanged.

**Protected content is kept**: Code, HTML, display math, front matter, and Pandoc constructs inside containers are not
changed.

**Suppression leaves a block alone**: A block under a disable comment, by identifier or alias, keeps its text, and the
block after the region is formatted.

**Line endings and byte order mark do not change the edits**: The same document read as LF, CRLF, mixed, and BOM bytes
gives the same edits.
