#### ThematicBreakFormatter

##### Verification Approach

`ThematicBreakFormatter` is verified with unit tests in `ThematicBreakFormatterTests.cs`. The real parser,
`LintMarkConfig`, and `EditSet` are used; nothing is replaced by a double. `BlockFormatterAssertions` formats each input
and asserts the exact text and a stable second run. Tests of the guards use real documents in which a changed break
would read as a setext underline or a list marker.

##### Test Environment

N/A - the tests need only the standard test runner and in-memory text.

##### Acceptance Criteria

- All unit tests pass with zero failures.
- Only thematic breaks are accepted, and any break is written with three of the configured character.
- A break that would be read as a setext underline or as a list marker, or as a grid border, is kept as written, with
  only its trailing whitespace removed.
- Indentation and container prefixes are kept.
- A suppressed rule leaves the break as written.

##### Test Scenarios

**Any break becomes the configured style**: Breaks of dashes, asterisks, underscores, and spaced characters become
`---`, or three asterisks or underscores when configured.

**Break under text stays**: A break directly under a text line is a setext underline and stays, also under blocks that
count as text, and a break after a complete block is converted.

**Break next to a list marker stays**: A break after a dash or an asterisk list marker, and after a plus marker when
dashes are wanted, stays. After an ordered marker or with another character it is converted.

**Break on the first line stays**: A break on the first line stays when it could open front matter. It is converted
after front matter or when the configuration makes it safe.

**Break after a border-shaped line stays**: A break after a border-shaped line keeps its text.

**Breaks after a table or other line are converted**: A break after a table, an ordinary line, or a blank line is
converted.

**Container prefix and indentation are kept**: A break in a container is converted in place, and an indented break keeps
its indentation.

**Guarded break loses only whitespace**: A guarded break with trailing whitespace loses only the whitespace.

**Each edit is credited to its rule**: Edits are reported under their own rules.

**Suppressed rule leaves the break**: A suppressed rule keeps the break, and a break after the region is formatted.
