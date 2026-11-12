## Document

### Verification Approach

The `Document` subsystem is verified by integration tests in `DocumentSubsystemTests.cs` that exercise
`SourceDocument`, `DocumentParser`, `SuppressionMap`, and `ReferenceIndex` together on real and generated
Markdown. No mocking is used. Four kinds of evidence are produced:

- **Repository corpus tiling**: every Markdown file in the repository is loaded and parsed in three variants
  built in memory (LF, CRLF, and mixed line endings), and the block list is checked for structural
  properties (below).
- **Synthetic nesting**: every chain of up to three containers from a fixed set (block quote, bullet list,
  ordered list, two-digit ordered list, and tab-indented bullet list) around a set of leaf bodies, in
  explicit-prefix and lazy-continuation form, in LF and CRLF, is generated and checked for the same
  properties. The bodies include paragraphs, headings, fenced code, tables, HTML, link reference definitions
  (alone, followed by text, and stacked), and a footnote definition. The depth and enclosing blocks of a
  definition are compared with the container chain, and the lines of generated paragraphs are checked against
  the fixture.
- **Synthetic footnotes and definitions**: top-level documents with footnote definitions in the shapes that the
  parser treats differently (referenced, unreferenced, repeated label with the same and with another letter
  case, adjacent definitions, several paragraphs, lazy continuation, body on the next line, indented marker,
  an indented line that looks like a definition, a definition-like line in code, and definitions mixed with
  link reference definitions) are checked for the same properties, in LF and CRLF.
- **Workflow and contracts**: documents built from bytes show that invalid content is rejected with its
  position, that block offsets convert to line and column, that the whole dialect is recognized, and that
  suppression comments and link reference definitions are resolved consistently with the block list.

The "tiling" properties checked by the assertions are the ones a span-patching formatter depends on: block
ranges are valid and ordered; siblings are disjoint; every block lies inside its parent; a block with no
parent is top level and lies in no container, and a block with a parent is exactly one level deeper than
that parent; only container prefix text (quote markers and list markers) and whitespace precede a block on
its first line, with the footnote marker allowed only before the first child of a footnote; a block ends at
the end of a line; list items start on their marker and setext headings end on their underline; footnote
definitions start on their marker; and every character outside the block ranges is whitespace or container
prefix text. There is no exception for unclosed fences or footnote labels: both are covered by block ranges.

The support helpers `SpanTilingAssertions` (the tiling checks), `SyntheticDocuments` (the generated documents),
and `RepositoryMarkdownFiles` (corpus discovery) are test-support code and have no requirements of their own.
The tab-indented container chains are the one place where the generated fixtures are only partly asserted: a
tab stop can leave too little indentation for an inner item, which makes a continuation line genuinely lazy,
or turn a quote marker into literal text. For those chains the lines of a generated paragraph are compared at
their ends only and laziness is not asserted; the other properties are still checked.

### Test Environment

The tests locate the repository root by walking up from the test output directory to the solution file and
read every Markdown file beneath it, excluding `node_modules`, `bin`, `obj`, `.git`, `.venv`, `generated`,
`.agent-logs`, and `TestResults`. A repository root that cannot be found fails the test rather than
skipping it, so coverage cannot be lost silently. Otherwise the standard test environment applies.

### Acceptance Criteria

- All subsystem tests pass with zero failures on every supported runtime and platform.
- No tiling violation is reported for any repository file in any of the three line-ending variants, or for any
  synthetic nesting or footnote document.
- The normalized text and block structure of the LF, CRLF, and mixed variants of every file are identical, and
  `ToBytes` restores the original bytes for LF and CRLF.
- Parsing the repository corpus concurrently gives results identical to sequential parsing.
- A link reference definition in any container chain reports the depth and enclosing blocks of that chain.
- Paragraph line content and laziness match the fixture, except for the tab-indented chains described above.
- Invalid content is rejected with the rule and the position of the first problem.
- Block offsets convert to the expected 1-based line and column and back.
- Every construct of the dialect is recognized from bytes, and nothing that is absent from the file is added.
- Suppression, reference lookup, and line-ending facts of one document agree with its content.

### Test Scenarios

#### Normal operation

**Repository Markdown tiles correctly with LF line endings**: Every repository Markdown file is parsed with LF
line endings and the tiling assertions report no violations.

**Line-ending variants give identical blocks and restore the original bytes**: The normalized text and block
structure of the three variants of each file are equal, and writing the normalized text back with the
original style restores the original bytes for LF and CRLF.

**Parallel parsing of the corpus matches sequential parsing**: The repository corpus is parsed on many threads
with the shared pipeline, and the results equal those of a sequential run.

**Container chains tile correctly in LF**: Generated container chains around each body are parsed and no
violation is reported.

**Footnote and definition documents tile correctly in LF**: Every generated top-level footnote and definition
document is parsed and no violation is reported.

**Definition in a container chain reports the chain's depth and enclosing blocks**: For every chain, the single
link reference definition has a depth equal to the number of enclosing containers and a parent chain whose
kinds are those of the containers.

**Paragraph lines of nested fixtures are exact and lazy only where the fixture is lazy**: The content ranges and
lazy flags returned by `GetLines` match the fixture (see the tab-indented caveat above).

**Block offsets convert to line and column**: The blocks of a document loaded from CRLF bytes with a byte order
mark report the expected 1-based positions, and every position converts back to the same offset.

**Every construct of the dialect is recognized from bytes**: A document that uses each construct of the dialect
is loaded from bytes; the expected blocks and inline elements are found, and the heading has no generated
attributes and the document no generated definitions.

**Suppression, references, and line-ending facts agree for one document**: A document with a byte order mark,
CRLF endings, a suppression region, link reference definitions (the last repeating the first label in
another letter case), and an unreferenced footnote is loaded and parsed; the line-ending facts, the suppressed
rules at known offsets, the definitions with their lines, the first-definition-wins lookup by a label that
differs in case and whitespace, the footnote block, and the tiling all give the expected results.

#### Boundary conditions

**Repository Markdown tiles correctly with CRLF line endings**: The same files are converted to CRLF and parsed;
no violations are reported.

**Repository Markdown tiles correctly with mixed line endings**: The same files are converted to mixed line
endings and parsed; no violations are reported.

**Container chains tile correctly in CRLF**: The same chains given as CRLF bytes are parsed and no violation is
reported.

**Footnote and definition documents tile correctly in CRLF**: The same documents given as CRLF bytes are parsed
and no violation is reported.

The other boundary values (unterminated regions, empty input, repeated labels, tab stops) are verified by the
unit verification designs of the units.

#### Error handling

**Invalid content is rejected with its position**: Invalid UTF-8 on line 2, a UTF-16 byte order mark, and a bare
carriage return on line 3 each raise the typed failure with the documented rule and the position of the first
offending byte or character.
