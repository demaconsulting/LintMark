#### HeadingFormatter

##### Verification Approach

`HeadingFormatter` is verified with unit tests in `HeadingFormatterTests.cs`. The real `DocumentParser`, `SourceDocument`,
`LintMarkConfig`, `EditSet`, and `SourcePatcher` are used; nothing is replaced by a double, because the formatter is
only meaningful for the blocks that the real parser produces. A helper of the test project
(`BlockFormatterAssertions`) applies the formatter alone, without the document passes, to every block it accepts,
patches the text, and asserts that the result is the expected text, that it renders to the same HTML as the input, that
it keeps its Pandoc structure, and that the formatter has nothing more to do with the result. Most scenarios are
theories with several inputs. Suppression is driven by real suppression comments (by identifier, by legacy number, and
for all rules). The test for concurrent use shares one formatter instance and one document among many threads and
compares every result with the result of a single call.

##### Test Environment

N/A - standard test environment.

##### Acceptance Criteria

- All unit tests pass with zero failures.
- ATX and setext headings are accepted and nothing else; null arguments and a block of another kind are rejected; the
  list of rules is immutable.
- A setext heading of one line, not nested, with safe text, becomes an ATX heading of the same level; every other setext
  heading keeps its form.
- Closing number signs, extra blanks after the marker, indentation of a top-level heading, trailing whitespace, and tabs
  in plain text are removed, and number signs that belong to the text, tabs in literal text, and text that is not a
  heading (`#Title`) are never changed.
- A heading inside a quote or list keeps its prefix and indentation.
- Each edit is attributed to the rule that it enforces, and a canonical heading produces no edit.
- A heading in which a rule is suppressed is left as written, and the headings after a comment that enables the rule are
  formatted.
- CRLF line endings and a byte order mark do not change the edits.
- One shared instance gives the same result on many threads.

##### Test Scenarios

###### Normal operation

**Setext headings become ATX headings**: One-line setext headings with `=` and `-` underlines, a short underline,
indentation, inline markup, and a heading after another are formatted. Each becomes an ATX heading of level 1 or 2 with
its text; a heading after a link reference definition is converted without changing the definition.

**Closed ATX headings lose the closing number signs**: `# Title #`, `## Title ##`, a closing run after extra spaces,
a long closing run, and a closing run of another length are formatted. The closing signs and the spaces before them go,
and an empty closed heading (`### ###`) keeps its number signs only.

**Blanks after the marker become one space**: Several spaces, a tab, and a mix of both after the number signs are
formatted. Exactly one space remains, and spaces inside the text are kept.

**Indentation of a heading is removed**: Headings indented by one, three, and two spaces (with a closing run) are
formatted. They start at the left margin.

**Trailing whitespace of a heading is removed**: Spaces and a tab at the end of a heading are removed, and a backslash
before a space at the end is kept.

**A tab in the text becomes spaces**: A tab after one, two, and three characters, and two tabs in a row, are formatted.
Each becomes the spaces that reach the next tab stop of four columns.

**A heading that is already canonical is left alone**: Headings of several levels, with emphasis, and an empty heading
are formatted. No edit results.

**Each edit is attributed to its rule**: A heading that is closed, has extra blanks, and is indented, with trailing
whitespace, is formatted. The edits are credited to `heading-style`, `heading-space`, `heading-start-left`, and
`trailing-whitespace`.

###### Boundary conditions

**A multi-line setext heading stays setext**: A heading of two lines, with a hard break, is formatted. It keeps its form
and the hard break, and only its whitespace is cleaned.

**A setext heading inside a quote or list stays setext**: A setext heading in a quote and in a list item is formatted. It
keeps its form, and trailing whitespace goes.

**A setext heading whose text would read differently stays setext**: Texts that end with `#`, with an escaped `#`, or
with a backslash, and texts that start with a grid table border, a fenced div, a LaTeX command, or display math are
formatted. Each keeps its setext form.

**A tab in a setext heading is expanded in the conversion, and a tab in a code span is kept**: A setext heading with a
tab in its text becomes an ATX heading with the tab expanded from the column of the new line, and one with a tab in a
code span keeps that tab.

**Number signs that belong to the text are kept**: `# C#`, `# foo#`, `# foo \#`, and `# foo #bar` are formatted. No
closing sequence is removed.

**An empty heading keeps its marker only**: `#` followed by spaces or a tab is formatted. The number signs remain alone.

**A heading in a container keeps its prefix**: Headings in a quote, a list item, and an ordered list item, some closed or
with extra blanks, are formatted. The prefix and indentation of the container stay and the heading is cleaned.

**A tab in literal text of a heading is kept**: A tab in a code span and in a link destination of a heading is formatted.
It stays.

**Text without a space after the number signs is not a heading**: `#Title`, `##Title##`, and `#hashtag and text` are
formatted. Nothing changes, because they are paragraphs.

**A suppressed rule leaves the heading alone**: A comment that disables `heading-style`, `MD003`, `MD021`, `MD019`,
`MD023`, `MD009`, `hard-tabs`, or all rules precedes a heading that breaks that rule. The heading is unchanged, and a
later comment that enables the rule again lets the next heading be formatted.

**CRLF line endings and a byte order mark do not change the edits**: A document parsed from bytes with CRLF line endings
and a byte order mark gives the same edits as the LF text.

###### Error handling

**A missing block, context, or edit set is rejected**: `CanFormat` with a null block, and `Format` with each null
argument, throw `ArgumentNullException`.

**A block that is not a heading is rejected**: `Format` for a paragraph block throws `ArgumentException`.

**The rule list is immutable**: The `RuleIds` property lists the five heading rules, and a cast to a mutable type cannot
change it.

**One shared instance gives one result on many threads**: One formatter and one document are used by many threads. Every
result equals the result of a single call.
