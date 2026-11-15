#### LinkRefactorer

##### Verification Approach

`LinkRefactorer` (with `LinkLabelAllocator` and `InlineLinkTail`) is verified with unit tests in
`LinkRefactorerTests.cs`. The real parser, `DocumentFormatter`, `ParagraphFormatter`, `LintMarkConfig`, `EditSet`, and
`SourcePatcher` are used; nothing is replaced by a double. The tests format whole documents with the link refactor rule
switched on through the configuration and assert the exact text, the reported positions, and stability. The label
allocator is tested directly for the labels it derives.

##### Test Environment

The tests need the standard test runner and in-memory text. The shared helper `BlockFormatterAssertions` calls the
HTML-equivalence and structure checks of the development tool `tools/LintMark.Verify` as a test oracle, so the test
project references that tool. The tool is not shipped and is not a software item; it is built with the tests and needs
no network access and no Pandoc installation.

##### Acceptance Criteria

- All unit tests pass with zero failures.
- Nothing changes when the rule is off, which is the default, or switched off, and an override can switch it on.
- A long inline link or image becomes a reference link or image, with its definition in one block at the end of the
  document, after an existing link block when there is one. Short links, links that are already references, and links in
  headings, code, HTML, or footnotes are not touched.
- Labels derive from the link text, avoid collisions with other labels and bracketed text, take the first free number
  when needed, and use the destination when the text has no letters or digits.
- Equal destination and title share one definition, and another title gets another definition.
- Destinations that cannot be read with certainty are left as written.
- Links in lists, quotes, and over-wide table rows are converted, and a table that cannot fit or is excluded stays.
- The conversion happens before the wrap, and the result is stable on a second run.

##### Test Scenarios

**Disabled by default**: Long links stay when the rule is off by default or switched off, and an override switches the
feature on.

**Long inline link becomes a reference**: A long link or image becomes a reference with the definition at the end of the
document, and each conversion is reported at its position.

**Short and other links are not touched**: Short links, reference links, and links in headings, code, HTML, or footnotes
stay.

**Labels are derived and kept unique**: A label comes from the text, a taken label gets the first free number, a label
that equals bracketed text elsewhere is avoided, and a text without letters uses the destination. A configured marker is
written.

**Definitions are shared or separate**: The same destination twice shares one definition, another title gets another,
and an existing definition is reused only when destination and title agree.

**Definitions block is placed after existing ones**: New definitions follow the last definition of an existing block or
a badge block, and front matter is kept.

**Destination forms are copied**: Titles and destination forms are copied as written, and a destination that is not read
with certainty is left.

**Link text stays stable when a link is converted**: A long inline link whose text holds a tab is expanded and
converted in one run, and a second run changes nothing (`LinkRefactorer_Format_TabInLinkText_IsExpandedInTheFirstRun`).

**Link text with other content is handled**: Link text with brackets, emphasis, and code stays whole, text split across
lines is converted, and the break-text option breaks the text and keeps the destination whole.

**Lists, quotes, and tables are converted**: Links in lists and quotes are converted, and in a table only the row that
is too wide is converted. A table that cannot fit or is excluded stays, and an aligned table that fits is aligned in one
run.

**Conversion happens before the wrap**: A long link in an overlong paragraph is converted and then wrapped, and a
suppressed paragraph stays while the next one is converted.

**Unclosed fence at the end changes nothing**: A plan for a document ending in an unclosed fence proposes no edit.
