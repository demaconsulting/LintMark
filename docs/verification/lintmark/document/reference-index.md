### ReferenceIndex

#### Verification Approach

`ReferenceIndex` is verified with unit tests in `ReferenceIndexTests.cs`. Test inputs are built by parsing
small Markdown texts with `DocumentParser.Parse` (a test-time dependency: the index is built from the real
block list, so the handling of the parser's definition groups is exercised for real), and the tests then
inspect `Definitions` and call `TryGet` and `NormalizeLabel`. No mocking is used.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Every definition has the correct label, destination, title, range, and line.
- Lookup ignores case and whitespace differences; the first of duplicate definitions wins.
- Footnotes never appear as link reference definitions.

#### Test Scenarios

**ReferenceIndex_Build_NoDefinitions_IsEmpty**: A document without definitions has an empty index. Tested by
`ReferenceIndex_Build_NoDefinitions_IsEmpty`.

**ReferenceIndex_Build_Definitions_CarryLabelUrlTitleSpanAndLine**: Definitions carry label, destination,
title, range, and line. Tested by `ReferenceIndex_Build_Definitions_CarryLabelUrlTitleSpanAndLine`.

**ReferenceIndex_TryGet_CaseAndWhitespaceInsensitive_FindsDefinition**: Lookups differing in case or
whitespace find the definition (theory with several queries). Tested by
`ReferenceIndex_TryGet_CaseAndWhitespaceInsensitive_FindsDefinition`.

**ReferenceIndex_TryGet_DuplicateLabels_FirstWinsAndBothRetained**: With duplicate labels the first
definition is returned and both stay in `Definitions`. Tested by
`ReferenceIndex_TryGet_DuplicateLabels_FirstWinsAndBothRetained`.

**ReferenceIndex_Build_DefinitionInBlockQuote_IsIndexed**: A definition inside a block quote is indexed.
Tested by `ReferenceIndex_Build_DefinitionInBlockQuote_IsIndexed`.

**ReferenceIndex_Build_FootnotesAndUnreferencedFootnotes_AreExcluded**: Referenced and unreferenced
footnotes are not in the index. Tested by `ReferenceIndex_Build_FootnotesAndUnreferencedFootnotes_AreExcluded`.

**ReferenceIndex_Build_Headings_ProduceNoSyntheticDefinitions**: Headings add no synthetic definitions.
Tested by `ReferenceIndex_Build_Headings_ProduceNoSyntheticDefinitions`.

**ReferenceIndex_NormalizeLabel_CollapsesWhitespaceAndFoldsCase**: Normalization trims, collapses whitespace
runs, and folds case (theory). Tested by `ReferenceIndex_NormalizeLabel_CollapsesWhitespaceAndFoldsCase`.

**ReferenceIndex_Build_NullArguments_Throw**: Null arguments are rejected. Tested by
`ReferenceIndex_Build_NullArguments_Throw`.
