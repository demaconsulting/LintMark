## Document

### Verification Approach

The `Document` subsystem is verified by integration tests in `DocumentSubsystemTests.cs` that exercise
`SourceDocument`, `DocumentParser`, `SuppressionMap`, and `ReferenceIndex` together on real and generated
Markdown. No mocking is used. Three kinds of evidence are produced:

- **Repository corpus tiling**: every Markdown file in the repository is loaded and parsed in three variants
  built in memory (LF, CRLF, and mixed line endings), and the block list is checked for structural
  properties (below).
- **Synthetic nesting**: every chain of up to three containers from a fixed set (block quote, bullet list,
  ordered list, two-digit ordered list, and tab-indented bullet list) around a set of leaf bodies, in
  explicit-prefix and lazy-continuation form, in LF and CRLF, is generated and checked for the same
  properties; paragraph lines are checked against the fixture.
- **Workflow**: a document with suppression comments and reference definitions is parsed and the
  `SuppressionMap` and `ReferenceIndex` results are compared with the known content.

The "tiling" properties checked by the assertions are the ones a span-patching formatter depends on: block
ranges are valid and ordered, siblings are disjoint, every block lies inside its parent, only container
prefix text (quote markers, list markers, footnote labels) and whitespace precede a block on its first line,
a block ends at the end of a line, list items start on their marker and setext headings end on their
underline, and every character outside the block ranges is whitespace or container prefix text. The only
permitted exception is the documented unclosed-fence case. The support helpers `SpanTilingAssertions`,
`SyntheticDocuments`, and `RepositoryMarkdownFiles` are test-support code.

### Test Environment

The tests locate the repository root by walking up from the test output directory to the solution file and
read every Markdown file beneath it, excluding `node_modules`, `bin`, `obj`, `.git`, `.venv`, `generated`,
`.agent-logs`, and `TestResults`. A repository root that cannot be found fails the test rather than
skipping it, so coverage cannot be lost silently. Otherwise the standard test environment applies.

### Acceptance Criteria

- All subsystem tests pass with zero failures on every supported runtime and platform.
- No span violation is reported for any repository file in any of the three line-ending variants.
- The normalized text and block structure of the LF, CRLF, and mixed variants of every file are identical, and
  `ToBytes` restores the original bytes for LF and CRLF.
- Parsing the repository corpus concurrently gives results identical to sequential parsing.
- Synthetic container chains produce no span violations and paragraph line content and laziness match the
  fixture.

### Test Scenarios

**DocumentSubsystem_SpanTiling_RepositoryMarkdownLf_HasNoViolations**: Every repository Markdown file is
parsed with LF line endings and the span-tiling assertions report no violations. This scenario is tested by
`DocumentSubsystem_SpanTiling_RepositoryMarkdownLf_HasNoViolations`.

**DocumentSubsystem_SpanTiling_RepositoryMarkdownCrLf_HasNoViolations**: The same files are converted to CRLF
and parsed; no violations are reported. This scenario is tested by
`DocumentSubsystem_SpanTiling_RepositoryMarkdownCrLf_HasNoViolations`.

**DocumentSubsystem_SpanTiling_RepositoryMarkdownMixed_HasNoViolations**: The same files are converted to mixed
line endings and parsed; no violations are reported. This scenario is tested by
`DocumentSubsystem_SpanTiling_RepositoryMarkdownMixed_HasNoViolations`.

**DocumentSubsystem_LineEndingVariants_RepositoryMarkdown_IdenticalBlocksAndRoundTrip**: The normalized text
and block structure of the three variants of each file are equal, and writing the normalized text back
with the original style restores the original bytes for LF and CRLF. This scenario is tested by
`DocumentSubsystem_LineEndingVariants_RepositoryMarkdown_IdenticalBlocksAndRoundTrip`.

**DocumentSubsystem_ConcurrentParse_RepositoryMarkdown_MatchesSequential**: The repository corpus is parsed
on many threads with the shared pipeline and the results equal those of a sequential run. This scenario is
tested by `DocumentSubsystem_ConcurrentParse_RepositoryMarkdown_MatchesSequential`.

**DocumentSubsystem_SyntheticNesting_ContainerChainsLf_HaveNoSpanViolations**: Generated container chains
with LF line endings are parsed and no span violation is reported. This scenario is tested by
`DocumentSubsystem_SyntheticNesting_ContainerChainsLf_HaveNoSpanViolations`.

**DocumentSubsystem_SyntheticNesting_ContainerChainsCrLf_HaveNoSpanViolations**: The same generated chains
with CRLF line endings are parsed and no span violation is reported. This scenario is tested by
`DocumentSubsystem_SyntheticNesting_ContainerChainsCrLf_HaveNoSpanViolations`.

**DocumentSubsystem_SyntheticNesting_ParagraphLines_ContentAndLazinessMatchFixture**: For generated
paragraphs inside containers, the line content ranges and lazy flags returned by `GetLines` match the
fixture. This scenario is tested by
`DocumentSubsystem_SyntheticNesting_ParagraphLines_ContentAndLazinessMatchFixture`.

**DocumentSubsystem_ParseToSuppressionAndReferences_Workflow_ProducesConsistentResults**: A document with
suppression comments and reference definitions is loaded and parsed; the suppression ranges, the
suppressed-rule queries, and the reference lookup give the expected results. This scenario is tested by
`DocumentSubsystem_ParseToSuppressionAndReferences_Workflow_ProducesConsistentResults`.
