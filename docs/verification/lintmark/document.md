## Document

### Verification Approach

The `Document` subsystem is verified by integration tests in `DocumentSubsystemTests.cs` that exercise
`SourceDocument`, `DocumentParser`, `SuppressionMap`, and `ReferenceIndex` together on real and generated
markdown. No mocking is used. Five kinds of evidence are produced:

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
  an indented line that looks like a definition, a definition-like line in code, a footnote followed by an
  indented HTML block alone, followed by a paragraph, and after a nested list, and definitions mixed with
  link reference definitions) are checked for the same properties, in LF and CRLF.
- **Workflow and contracts**: documents built from bytes show that invalid content is rejected with its
  position, that block offsets convert to line and column, that the whole dialect is recognized, and that
  suppression comments and link reference definitions are resolved consistently with the block list. Three
  scenarios cover the grid table fallback through the real loader: an invalid border next to a valid grid table
  (CRLF bytes), a valid grid table alone, and a repeated parse of one source, asserting the `GridFallback` location
  and `IsFormatUnsafe` of the parsed document.
- **Randomized adversarial documents**: a fixed seed (20260511) generates 10500 documents in seven families
  (footnote-heavy mixtures of quotes, lists, and look-alike definitions; footnote definitions with children of
  every kind; look-alike definitions at assorted indents inside a quote or list; a broad mixture of block
  constructs that includes lines that look like grid table rows; footnote markers followed on the same line by
  a definition or another marker, in quotes and lists; a mixture rich in such lines; and definitions followed
  by lines that start like a title), each parsed from LF and from CRLF bytes. A further 1500 documents of valid
  grid tables and such same-line shapes must keep every grid table. The documents are checked for the nesting
  properties only (valid and ordered spans, disjoint siblings, any two blocks disjoint or nested, the innermost
  enclosing block is the parent, consistent parentage and depth). The tiling properties about text between
  blocks are **not** applied
  to these documents: adversarial input legitimately leaves text between blocks (for example text that the
  parser swallows after a dropped footnote), and that is outside the guarantee.

The "tiling" properties checked by the assertions are the ones a span-patching formatter depends on: block
ranges are valid and ordered; siblings are disjoint; every block lies inside its parent; a block with no
parent is top level and lies in no container, and a block with a parent is exactly one level deeper than
that parent; only container prefix text (quote markers and list markers) and whitespace precede a block on
its first line, with the footnote marker allowed only before the first child of a footnote; a block ends at
the end of a line; list items start on their marker and setext headings end on their underline; footnote
definitions start on their marker; and every character outside the block ranges is whitespace or container
prefix text. For the repository corpus and the generated fixtures there is no exception for unclosed fences or
footnote labels: both are covered by block ranges.

The support helpers `SpanTilingAssertions` (the tiling and nesting checks), `SyntheticDocuments` (the generated
documents), `RandomDocuments` (the randomized adversarial documents), and `RepositoryMarkdownFiles` (corpus
discovery) are test-support code and have no requirements of their own.
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
- No sibling overlap or parent violation is reported for any generated adversarial document, in LF or CRLF, and
  every grid table of the valid grid table documents is kept.
- The normalized text and block structure of the LF, CRLF, and mixed variants of every file are identical (also for
  a code span that ends a line), and
  `ToBytes` restores the original bytes for LF and CRLF.
- Parsing the repository corpus concurrently gives results identical to sequential parsing.
- A link reference definition in any container chain reports the depth and enclosing blocks of that chain.
- Paragraph line content and laziness match the fixture, except for the tab-indented chains described above.
- Invalid content is rejected with the rule and the position of the first problem.
- Block offsets convert to the expected 1-based line and column and back.
- Every construct of the dialect is recognized from bytes, and nothing that is absent from the file is added.
- A document with an invalid grid table is marked format-unsafe, with the 1-based line and column of the offending
  line, whether loaded from LF or CRLF bytes; a document with only a valid grid table is not marked and keeps the
  grid table; the same document parsed again reports the same location.
- Suppression, reference lookup, and line-ending facts of one document agree with its content.
- No collection exposed by a parse result can be modified by a caller that casts it.

### Test Scenarios

#### Normal operation

**Parse result collections cannot be changed through a cast**: A document with blocks, a disable region that
names a rule, and a definition is parsed; the block list, the line table, the suppression ranges and the first
range's rule collection, and the definition list each throw `NotSupportedException` for every attempted change
through the mutable interfaces and are not `List<T>`, `HashSet<T>`, or array instances.

**Repository Markdown tiles correctly with LF line endings**: Every repository Markdown file is parsed with LF
line endings and the tiling assertions report no violations.

**Line-ending variants give identical blocks and restore the original bytes**: The normalized text and block
structure of the three variants of each file are equal, and writing the normalized text back with the
original style restores the original bytes for LF and CRLF.

**Multi-line code span gives the same blocks for LF and CRLF**: A document whose paragraph ends a line inside a
code span (which the Markdown parser reads differently for CRLF text) is loaded from LF bytes and from CRLF bytes;
the normalized text and the block lists are equal, so the normalization hides the difference.

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

**A valid grid table is kept and the document is not marked**: CRLF bytes with a valid grid table between two
paragraphs are loaded and parsed; there is no fallback location, the document is not format-unsafe, exactly one block
is a grid table, and the tiling checks report no violation.

**Parsing the same text again reports the same fallback location**: A document in which an invalid border stands on
line 2 and a second one inside a block quote is parsed twice from one source and once from an equal source built from
the same text; every result reports the first offending line, 2:1.

#### Boundary conditions

**Repository Markdown tiles correctly with CRLF line endings**: The same files are converted to CRLF and parsed;
no violations are reported.

**Repository Markdown tiles correctly with mixed line endings**: The same files are converted to mixed line
endings and parsed; no violations are reported.

**Container chains tile correctly in CRLF**: The same chains given as CRLF bytes are parsed and no violation is
reported.

**Footnote and definition documents tile correctly in CRLF**: The same documents given as CRLF bytes are parsed
and no violation is reported.

**Random adversarial documents nest without overlap in LF and CRLF**: The 10500 generated documents are parsed
from LF bytes and, in a second test, from CRLF bytes; the number of documents is as expected and the nesting
checks report no violation. In addition 1500 documents with valid grid tables are parsed in both variants and
keep all their grid tables (no spurious fallback). The failure message names the escaped document.

The other boundary values (unterminated regions, empty input, repeated labels, tab stops) are verified by the
unit verification designs of the units.

#### Error handling

**Invalid content is rejected with its position**: Invalid UTF-8 on line 2, a UTF-16 byte order mark, and a bare
carriage return on line 3 each raise the typed failure with the documented rule and the position of the first
offending byte or character.

**A document with an invalid grid table is marked unsafe at the offending line**: CRLF bytes with a valid grid table
(lines 1 to 3) and an invalid border on line 5 are loaded and parsed; the document is format-unsafe with the location
5:1, no block is a grid table (the document is read without grid tables), and the nesting checks report no violation.
