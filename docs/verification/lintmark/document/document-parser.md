### DocumentParser

#### Verification Approach

`DocumentParser` is verified with unit tests in `DocumentParserTests.cs`. The real Markdown library and the
real `SourceDocument` are used, because the unit exists to compensate for the library's behavior; mocking
the parser would remove what is being tested. Most tests build a `SourceDocument` from a small text with
`FromText` and assert on the block list: kind, effective range, depth, and parent. There is one fixture per
quirk (Q-1 to Q-5 and Q-8) and per rule (R-4 and R-5), named after it. The structure over a large corpus is
verified separately at subsystem level.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Blocks are in source order with enclosing blocks first and no synthetic nodes.
- Each quirk fixture shows the corrected range or classification.
- Parsing LF and CRLF input gives the same structure.
- Concurrent calls give identical results.

#### Test Scenarios

**DocumentParser_Parse_SimpleDocument_ReturnsBlocksInSourceOrder**: A simple document returns its blocks in
source order. Tested by `DocumentParser_Parse_SimpleDocument_ReturnsBlocksInSourceOrder`.

**DocumentParser_Parse_EmptyDocument_ReturnsNoBlocks**: An empty document has no blocks. Tested by
`DocumentParser_Parse_EmptyDocument_ReturnsNoBlocks`.

**DocumentParser_Parse_NullSource_ThrowsArgumentNullException**: A null source is rejected. Tested by
`DocumentParser_Parse_NullSource_ThrowsArgumentNullException`.

**DocumentParser_Parse_NestedContainers_ReportsDepthAndParents**: Nested quotes and lists report depth and
parent. Tested by `DocumentParser_Parse_NestedContainers_ReportsDepthAndParents`.

**DocumentParser_Parse_PipeTable_IsTableWithGfmCells**: A pipe table is a `Table`, and a pipe inside a code
span stays in its cell. Tested by `DocumentParser_Parse_PipeTable_IsTableWithGfmCells`.

**DocumentParser_Parse_GridTable_IsGridTable**: A grid table is classified `GridTable`. Tested by
`DocumentParser_Parse_GridTable_IsGridTable`.

**DocumentParser_Parse_FrontMatter_IsFrontMatterBlock**: YAML front matter is a `FrontMatter` block. Tested
by `DocumentParser_Parse_FrontMatter_IsFrontMatterBlock`.

**DocumentParser_Parse_VariousBlocks_AreClassified**: An alert (classified as a block quote), a math block,
a closed fence, an HTML block, and a task list get the expected `BlockKind`, and no unclosed fence is
reported. Tested by `DocumentParser_Parse_VariousBlocks_AreClassified`.

**DocumentParser_Parse_InlineExtensions_AreRecognized**: Strikethrough and a bare-URL autolink are
recognized. Tested by `DocumentParser_Parse_InlineExtensions_AreRecognized`.

**DocumentParser_Parse_Headings_HaveNoAutoIdentifiers**: Headings carry no generated attributes and add no
synthetic definitions. Tested by `DocumentParser_Parse_Headings_HaveNoAutoIdentifiers`.

**DocumentParser_Parse_UnclosedFence_Q1_EffectiveSpanReachesEnd**: An unclosed fence is `UnclosedFence` and
extends to the end of the file. Tested by `DocumentParser_Parse_UnclosedFence_Q1_EffectiveSpanReachesEnd`.

**DocumentParser_Parse_UnclosedFenceInQuote_Q1_EndsWithParent**: An unclosed fence inside a quote ends with
the quote. Tested by `DocumentParser_Parse_UnclosedFenceInQuote_Q1_EndsWithParent`.

**DocumentParser_Parse_ListTrailingNewline_Q2_EffectiveEndTrimmed**: A list or item never ends on a line
feed. Tested by `DocumentParser_Parse_ListTrailingNewline_Q2_EffectiveEndTrimmed`.

**DocumentParser_Parse_IndentedCodeTrailingNewline_Q2_EffectiveEndTrimmed**: Indented code never ends on a
line feed. Tested by `DocumentParser_Parse_IndentedCodeTrailingNewline_Q2_EffectiveEndTrimmed`.

**DocumentParser_Parse_ReferenceAndFootnoteGroups_Q3_SourceOrderedNoShells**: Definitions and footnotes
appear in source order without group shells. Tested by
`DocumentParser_Parse_ReferenceAndFootnoteGroups_Q3_SourceOrderedNoShells`.

**DocumentParser_Parse_UnreferencedFootnote_Q3_DetectedByScan**: A footnote definition without a reference is
reported as `UnreferencedFootnote` with no node. Tested by
`DocumentParser_Parse_UnreferencedFootnote_Q3_DetectedByScan`.

**DocumentParser_Parse_FootnoteLikeLineInCode_Q3_IsNotReported**: A footnote-like line inside code is not
reported. Tested by `DocumentParser_Parse_FootnoteLikeLineInCode_Q3_IsNotReported`.

**DocumentParser_Parse_CrLfCodeSpan_Q4_ParsesLfNormalizedText**: A multi-line code span gives the same blocks
for CRLF and LF input. Tested by `DocumentParser_Parse_CrLfCodeSpan_Q4_ParsesLfNormalizedText`.

**DocumentParser_GetLines_LazyContinuation_Q5_FlagsLazyLines**: Lazy continuation lines in quotes and lists
are flagged. Tested by `DocumentParser_GetLines_LazyContinuation_Q5_FlagsLazyLines`.

**DocumentParser_GetLines_LazySetextLookalike_Q5_IsParagraphTextAndLazy**: A line that looks like a setext
underline but is lazy is paragraph text. Tested by
`DocumentParser_GetLines_LazySetextLookalike_Q5_IsParagraphTextAndLazy`.

**DocumentParser_GetLines_ParagraphLines_Q8_RebuiltFromSpan**: Paragraph lines are rebuilt from the source.
Tested by `DocumentParser_GetLines_ParagraphLines_Q8_RebuiltFromSpan`.

**DocumentParser_GetLines_NestedContainers_R4_ContentStartIsFirstInline**: The content start of
continuation lines in nested containers is the first inline. Tested by
`DocumentParser_GetLines_NestedContainers_R4_ContentStartIsFirstInline`.

**DocumentParser_GetLines_MultiLineCodeSpan_R4_FallsBackToPrefixStripping**: A line beginning inside a code
span falls back to prefix stripping. Tested by
`DocumentParser_GetLines_MultiLineCodeSpan_R4_FallsBackToPrefixStripping`.

**DocumentParser_GetLines_TabIndentedContinuation_ContentStartAfterTab**: The content of a tab-indented
continuation line starts after the tab. Tested by
`DocumentParser_GetLines_TabIndentedContinuation_ContentStartAfterTab`.

**DocumentParser_GetContinuationPrefix_R5_BlanksMarkers**: The continuation prefix blanks list markers and
numbers and keeps quote markers (for `-`, `1.`, `10.`, and quote forms). Tested by
`DocumentParser_GetContinuationPrefix_R5_BlanksMarkers`.

**DocumentParser_GetLines_SetextHeading_ReturnsTextAndUnderlineLines**: A setext heading yields its text and
underline lines. Tested by `DocumentParser_GetLines_SetextHeading_ReturnsTextAndUnderlineLines`.

**DocumentParser_GetLines_NullArguments_Throw**: Null arguments are rejected. Tested by
`DocumentParser_GetLines_NullArguments_Throw`.

**DocumentParser_Parse_Bom_OffsetsExcludeByteOrderMark**: With a byte order mark, offsets do not include it.
Tested by `DocumentParser_Parse_Bom_OffsetsExcludeByteOrderMark`.

**DocumentParser_Parse_Tabs_ListCodeAndTextSpansAreExact**: Tab-indented lists, code, and text have exact
ranges. Tested by `DocumentParser_Parse_Tabs_ListCodeAndTextSpansAreExact`.

**DocumentParser_Parse_ConcurrentCalls_ProduceIdenticalBlocks**: Concurrent parses of the same document
produce identical block lists. Tested by `DocumentParser_Parse_ConcurrentCalls_ProduceIdenticalBlocks`.

**DocumentParser_Parse_CommentsAndDefinitions_PopulatesSuppressionsAndReferences**: Parsing populates the
suppression map and the reference index. Tested by
`DocumentParser_Parse_CommentsAndDefinitions_PopulatesSuppressionsAndReferences`.
