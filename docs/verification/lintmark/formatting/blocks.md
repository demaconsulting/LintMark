### Blocks

#### Verification Approach

The `Blocks` subsystem is verified by integration tests in `BlocksSubsystemTests.cs` and a corpus test in
`BlocksCorpusTests.cs`. Both use the production composition: the real `DocumentParser`, the real `DocumentFormatter`
created with `HeadingFormatter`, `CodeBlockFormatter`, and `ThematicBreakFormatter`, and the real `SourcePatcher`. No unit
is replaced by a double. A shared helper, `BlockFormatterAssertions` (the file `BlockFormatterAssertions.cs`), formats a
text and asserts every property that a rewrite of the tool must have, and the tests never relax an assertion to make a
case pass:

- the formatted text is the expected text;
- it renders to the same HTML, both as LF text and as the CRLF text that would be written to disk (`HtmlEquivalenceChecker`);
- it keeps its Pandoc structure (`StructuralTokenChecker`) and its grid table reading;
- formatting it again proposes no edit and reports no problem (`IdempotencyChecker` and a direct second run);
- the edits are ordered and inside the text, and a problem is reported exactly when the text changed;
- the same result is computed when the formatter is called from several threads.

These three checks are the ones that the `Engine` subsystem runs before it writes a file; they are collaborators of the
tests and are verified in the `Safety` subsystem.

`BlocksSubsystemTests` covers the interaction of the three block formatters with the document passes on hand-written
documents: a document that breaks every rule, its canonical form, nested containers, Pandoc constructs, front matter,
HTML, hard breaks, tabs, indented code in many forms, tilde fences, thematic breaks, setext headings, spacing, and the
configured settings. `BlocksCorpusTests` runs the same assertion helper over every Markdown file of the repository as
LF, CRLF, and mixed-ending text with a byte order mark, over generated nested documents (several thousand container
chains, with and without trailing whitespace), and over seeded random hostile documents of seven families (with and
without injected tabs and trailing whitespace). The repository's own files are asserted to need no edit and to give no
problem, which documents that the repository follows its own style.

#### Test Environment

N/A - standard test environment. The corpus test reads the Markdown files of the repository from the working copy and
writes nothing.

#### Acceptance Criteria

- All integration and corpus tests pass with zero failures.
- A document that breaks every rule is rewritten to the canonical document, each rule is reported once at the place of
  its change, and the canonical document produces no edit and no problem.
- Every rewrite renders to the same HTML, keeps its Pandoc structure, and is stable when formatted again.
- Quote and list structure survives every change, code and HTML content is never changed, and a fence that is never
  closed stays as written.
- A document with CRLF line endings and a byte order mark is rewritten in the same way as the LF text without a mark,
  and mixed line endings are written with the majority ending.
- Where a conversion could change the meaning of the text (a setext heading that is nested or multi-line, a braced
  fence, a thematic break under text or at the start of the file), the text stays as written.
- The configured settings select the fence character, the break style, the maximum of blank lines, and the final newline.
- The same document gives the same result on many threads.
- Every Markdown file of the repository, and every generated document, passes the assertions of the helper.

#### Test Scenarios

##### Normal operation

**A document that breaks every rule becomes canonical**: A document with trailing whitespace, a tab, a long run of
blank lines, setext and closed headings, an indented heading, a tilde fence, indented code, a thematic break written
with asterisks, a list and a table that touch their neighbors, and no final line ending is formatted. The result is the
canonical document, and it passes every assertion of the helper.

**A canonical document is left alone**: The canonical document is formatted. No edit and no problem result.

**Every rule is reported once**: The document that breaks every rule is formatted. One problem is reported for each of
the 14 rules, each at the place of its change and with the description of its rule.

**The configured settings select the canonical form**: The same documents are formatted with `code-fence: tilde`,
`thematic-break: asterisks` and `underscores`, `max-blank-lines: 2` and `3`, and `final-newline: false`. Each rule
writes the configured form.

**Formatting twice changes nothing the second time**: The result of every document above is formatted again. No edit and
no problem result.

##### Boundary conditions

**Line endings and the byte order mark do not matter**: The document that breaks every rule is formatted from bytes with
CRLF line endings and a byte order mark. The edits equal those for the LF text, the rewrite keeps the mark, and writing
it with the target line ending gives CRLF throughout. A file with mixed line endings is written with the majority ending.

**Nested containers keep their structure**: Headings, code, thematic breaks, and lists inside quotes, lists, and each
other are formatted. The containers, indentation, and prefixes stay, and no blank line is added inside a container.

**Hard breaks keep their meaning**: Two spaces stay, more spaces become two, one space is removed, and a backslash break
stays.

**Tabs are expanded only in plain text**: A tab in text and in a heading becomes spaces; a tab in indentation, code,
a link title, a table, or a code span stays.

**Pandoc constructs are kept**: Fenced divs, page break commands, display math, attributes, citations, raw blocks, and
grid tables are formatted together with the lines around them. The constructs are unchanged, trailing whitespace next to
them is removed, and a braced fence keeps its backticks even when the configuration asks for tildes.

**A grid table that is valid is left alone and an invalid one is never rewritten**: A valid grid table next to text is
formatted and stays. A document with an invalid grid table is returned without a rewrite.

**Front matter and leading dashes are kept**: Front matter keeps its whitespace, a first line of dashes is not turned
into front matter, and a heading directly after front matter gets a blank line.

**HTML blocks keep their content**: HTML blocks and comments keep their content and whitespace, and the headings next
to them get blank lines.

**A fence that is never closed stays as written**: An unclosed fence, with the blank lines after it and without a final
line ending, is formatted. Nothing changes.

**Indented code in its many forms becomes fenced code**: Indented blocks with tabs, internal blank lines, content that
looks like a fence, and directly after a heading are formatted. Each becomes a fenced block that its own content cannot
close.

**Tilde fences are converted correctly or left alone**: Tilde fences that contain backtick fences, and an info string
with a backtick, are formatted. A fence is lengthened where its content needs it, and the fence whose info string has
a backtick stays tilde.

**Thematic breaks keep the meaning of the document**: Breaks in every written form, under text, at the start of the
file, after front matter, and after list markers are formatted. Each is converted or left as written according to the
guards, and the document renders as before.

**Setext headings are converted only where that is safe**: Single-line, multi-line, nested, and lookalike headings are
formatted. Only the safe ones become ATX headings.

**Closing number signs are removed from headings**: Headings with closing number signs, with extra blanks before them,
and with an empty text are formatted, and so are headings whose text ends in a number sign or an escaped one. Only a
closing sequence is removed.

**Spacing is added only for real block boundaries**: Lists, tables, and fences that touch their neighbors get blank
lines where that is a violation, and text that only looks like a boundary is not separated.

##### Robustness

**The same document gives the same result on many threads**: One formatter instance and one document are shared by many
threads. Every thread gets the result of a single call.

**The repository and generated documents pass every assertion**: Every Markdown file of the repository (as LF, CRLF, and
mixed endings with a byte order mark), the generated nested documents, and the random hostile documents are formatted.
Each result is safe and stable, and the repository's own files need no edit.
