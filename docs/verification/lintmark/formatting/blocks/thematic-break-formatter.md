#### ThematicBreakFormatter

##### Verification Approach

`ThematicBreakFormatter` is verified with unit tests in `ThematicBreakFormatterTests.cs`. The real `DocumentParser`,
`SourceDocument`, `LintMarkConfig`, `EditSet`, and `SourcePatcher` are used; nothing is replaced by a double. A helper of
the test project (`BlockFormatterAssertions`) applies the formatter alone, without the document passes, patches the
text, and asserts that the result is the expected text, that it renders to the same HTML as the input, that it keeps its
Pandoc structure, and that the formatter has nothing more to do with the result. Most scenarios are theories with several
inputs, and the break style is varied through the configuration. Suppression is driven by real suppression comments. The
test for concurrent use shares one formatter instance and one document among many threads.

##### Test Environment

N/A - standard test environment.

##### Acceptance Criteria

- All unit tests pass with zero failures.
- A thematic break is accepted and nothing else, in particular not a setext underline; null arguments and a block that is
  not a break are rejected; the list of rules is immutable.
- Every written form of a break becomes three characters of the configured style, the indentation and the container prefix
  stay, and trailing whitespace is removed.
- A break is left as written where the conversion would underline text, start or close front matter, or join a list marker
  of the same character; trailing whitespace is still removed there.
- Each edit is attributed to the rule that it enforces, and a canonical break produces no edit.
- A break in which a rule is suppressed is left as written.
- CRLF line endings and a byte order mark do not change the edits, and one shared instance gives the same result on many
  threads.

##### Test Scenarios

###### Normal operation

**Every form of a break becomes three dashes**: Breaks written with three and more asterisks, underscores, and dashes,
with spaces and tabs between the characters, are formatted. Each becomes `---`.

**The configuration selects the style**: The same breaks are formatted with `thematic-break: asterisks` and with
`underscores`. Each becomes `***` or `___`, and a break of the configured style is unchanged.

**A break on the first line follows the configuration**: A break on the first line of the file, with asterisks as the
configured style, is converted, because the guard concerns the dash form only.

**The indentation of a break is kept**: Breaks indented by two and three spaces are formatted. The indentation stays.

**Trailing whitespace is removed**: Breaks with trailing spaces and a tab are formatted. The whitespace goes and the
break becomes canonical.

**A break in a container is converted in place**: Breaks in a quote and a list item are formatted. The prefix stays.

**A break after a complete block is converted**: A break directly after a heading, fenced code, indented code, a list, and
a quote is formatted. It becomes `---`, because the blocks are complete in themselves.

**Each edit is attributed to its rule**: A break written with asterisks and trailing whitespace is formatted. The edits
are credited to `thematic-break` and `trailing-whitespace`.

**A canonical break is left alone**: `---` on its own line is formatted. No edit results.

###### Boundary conditions

**A break under text stays as written**: Breaks directly under a paragraph, a list item, a quote line, and a link
reference definition are formatted. They are unchanged, because dashes would underline the text, and trailing whitespace
after such a break is still removed.

**A break on the first line of a file stays as written**: A break with asterisks, underscores, or spaced dashes on the
first line is formatted with the default configuration. It is unchanged, because dashes could open front matter.

**A dashes line after a first line of dashes stays as written**: A document that starts with dashes and has a second
dashes line is formatted. The second line is unchanged, because converting it could close front matter that the document
never had.

**A break after front matter is converted**: A break after real front matter is formatted. It becomes `---`.

**A break after a list marker stays as written**: `- ***` with the default configuration and `* ---` with the asterisk
configuration are formatted. Each is unchanged, because the converted line would be a thematic break of its own and the
list would be lost.

**A suppressed rule leaves the break alone**: A comment that disables `thematic-break`, `MD035`, `MD009`, or all rules
precedes a break. The break is unchanged.

**CRLF line endings and a byte order mark do not change the edits**: A document parsed from bytes with CRLF line endings
and a byte order mark gives the same edits as the LF text.

###### Error handling

**A missing block, context, or edit set is rejected**: `CanFormat` with a null block, and `Format` with each null
argument, throw `ArgumentNullException`.

**A block that is not a break is rejected**: `Format` for a paragraph block throws `ArgumentException`. A setext
underline, a list item, and fenced content that looks like a break are not accepted by `CanFormat`.

**The rule list is immutable**: The `RuleIds` property lists `thematic-break` and `trailing-whitespace`, and a cast to a
mutable type cannot change it.

**One shared instance gives one result on many threads**: One formatter and one document are used by many threads. Every
result equals the result of a single call.
