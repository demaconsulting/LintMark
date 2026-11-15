### WhitespacePasses

#### Verification Approach

`WhitespacePasses` (the leaf whitespace rules, `BlankLinePass`, `BlockSpacingPass`, and `TrailerPass`) is verified with
unit tests in `DocumentFormatterPassTests.cs` and the helper tests in `DocumentFormatterSupportTests.cs`
(`LeafWhitespace` and `InlineMap`). The passes run inside the real `DocumentFormatter` with the real parser and no block
formatters, so the passes are tested on exactly the lines that no formatter claims. Each test formats a small document
and asserts the exact text and the violations by rule and line. Nothing is replaced by a double.

#### Test Environment

The tests need the standard test runner and in-memory text. The shared helper `BlockFormatterAssertions` calls the
HTML-equivalence and structure checks of the development tool `tools/LintMark.Verify` as a test oracle, so the test
project references that tool. The tool is not shipped and is not a software item; it is built with the tests and needs
no network access and no Pandoc installation.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Trailing whitespace and tabs are removed only where they are not part of the content: code, HTML, math, front matter,
  grid tables, link titles and destinations, reference labels, and exact hard breaks stay.
- Blank-line runs shrink to the configured maximum, and blank lines inside blocks stay.
- Headings, fences, lists, and tables get one blank line from their neighbors, except where a line would then change the
  meaning of the text.
- The file ends with exactly one line ending, or with none when the setting says so, and an unclosed fence or HTML block
  is left alone.
- A rule that is suppressed leaves its part alone, and every edit of an always-applied rule is reported at its place.

#### Test Scenarios

**Trailing whitespace is removed from text**: Paragraphs, table rows, definitions, marker lines, and claimed or
unclaimed headings and breaks lose trailing whitespace. Literal blocks, literal inline spans, front matter, link titles,
and grid tables keep it.

**Hard break keeps exactly two spaces**: A hard break with several spaces is written with two. A backslash break and
mixed breaks are kept.

**Tabs expand to the tab stop**: A tab in text expands, a tab after a code span expands, a tab in the text of an inline
link or of a reference link expands in the first run (`DocumentFormatter_Format_TabInText_ExpandsToTheTabStop`,
`LinkRefactorer_Format_TabInLinkText_IsExpandedInTheFirstRun`), and tabs in code or protected spans, a link
destination or title, and the text of a collapsed reference link (its label) stay
(`DocumentFormatter_Format_TabsThatMustStay_AreKept`).

**Blank-line runs collapse**: Runs collapse to the configured maximum. Runs inside containers keep their prefix without
whitespace, and runs inside blocks stay.

**Special blank runs settle in one run**: A run after an empty list item, after a thematic break in an item, or after a
border-shaped last line settles in one pass.

**Blocks get blank lines around them**: A heading, a fenced block, a list, and a table without blank lines get them.
Blocks inside containers, lazy continuations, footnotes next to headings, and lines that only look like a list or a grid
border are not separated. A setext heading that starts with a list marker right after a link reference definition, and
a list shaped like a thematic break (`- --`) right after a list, are not separated either (issues 24 and 26).

**Suppression is respected**: Each part is left alone when its rule is suppressed, by identifier, by alias, or by a next
line directive; an unsuppressed neighbor is still handled.

**File end is normalized**: A missing line ending is added, several are reduced to one, and a closed comment as the last
line reports the final newline. The setting for none removes it. An unclosed fence or HTML block is left alone.

**Whitespace-only lines become empty**: Whitespace lines become empty, but a wide whitespace line that ends indented
code stays part of the code.

**Edits are reported by rule and line**: The edits are reported per rule at their place, and an edit of a configurable
rule is not reported twice.
