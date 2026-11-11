### SuppressionMap

#### Verification Approach

`SuppressionMap` is verified with unit tests in `SuppressionMapTests.cs`. Test inputs are built by parsing
small Markdown texts with `DocumentParser.Parse` (a test-time dependency: the map is built from a real
syntax tree and block list, so that comment recognition is exercised against the real parser), and the
tests then ask the map `IsSuppressed` and `IsSuppressedRange` questions or inspect `Ranges`. The alias
resolver is exercised with a test function. No mocking is used.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Each directive family and form suppresses exactly the documented text.
- Unterminated regions run to the end of the file.
- Comment-like text inside code and front matter never suppresses anything.
- Rule identifiers are matched ignoring case after alias resolution.

#### Test Scenarios

**SuppressionMap_Build_NoDirectives_SuppressesNothing**: A document without directives has no ranges.
Tested by `SuppressionMap_Build_NoDirectives_SuppressesNothing`.

**SuppressionMap_IsSuppressed_DisableEnableRegion_CoversTextBetween**: Text between a disable and an enable
comment is suppressed and text outside is not. Tested by
`SuppressionMap_IsSuppressed_DisableEnableRegion_CoversTextBetween`.

**SuppressionMap_IsSuppressed_UnterminatedDisable_RunsToEndOfFile**: A disable without an enable suppresses
to the end of the file. Tested by `SuppressionMap_IsSuppressed_UnterminatedDisable_RunsToEndOfFile`.

**SuppressionMap_IsSuppressed_DisableWithRules_SuppressesOnlyListedRules**: A disable with rule names
suppresses only those rules. Tested by
`SuppressionMap_IsSuppressed_DisableWithRules_SuppressesOnlyListedRules`.

**SuppressionMap_IsSuppressed_EnableWithRules_ReEnablesOnlyListedRules**: An enable with rule names after a
bare disable re-enables only those rules. Tested by
`SuppressionMap_IsSuppressed_EnableWithRules_ReEnablesOnlyListedRules`.

**SuppressionMap_IsSuppressed_OverlappingRuleRegions_TrackedIndependently**: Overlapping regions of
different rules are tracked independently. Tested by
`SuppressionMap_IsSuppressed_OverlappingRuleRegions_TrackedIndependently`.

**SuppressionMap_IsSuppressed_DisableLine_CoversOwnLineOnly**: A disable-line comment covers its own line
only. Tested by `SuppressionMap_IsSuppressed_DisableLine_CoversOwnLineOnly`.

**SuppressionMap_IsSuppressed_DisableNextLine_CoversFollowingLineOnly**: A disable-next-line comment covers
the following line only. Tested by `SuppressionMap_IsSuppressed_DisableNextLine_CoversFollowingLineOnly`.

**SuppressionMap_Build_DisableNextLineOnLastLine_ProducesNoRange**: A disable-next-line comment on the last
line produces no range. Tested by `SuppressionMap_Build_DisableNextLineOnLastLine_ProducesNoRange`.

**SuppressionMap_IsSuppressed_DisableFile_CoversWholeFile**: A disable-file comment covers the whole file.
Tested by `SuppressionMap_IsSuppressed_DisableFile_CoversWholeFile`.

**SuppressionMap_IsSuppressed_CaptureRestore_ReturnsToCapturedState**: A restore returns to the state saved
by a capture. Tested by `SuppressionMap_IsSuppressed_CaptureRestore_ReturnsToCapturedState`.

**SuppressionMap_IsSuppressed_RestoreWithoutCapture_IsIgnored**: A restore without a capture has no effect.
Tested by `SuppressionMap_IsSuppressed_RestoreWithoutCapture_IsIgnored`.

**SuppressionMap_IsSuppressed_PrettierIgnore_CoversNextBlock**: A prettier-ignore comment covers the next
block. Tested by `SuppressionMap_IsSuppressed_PrettierIgnore_CoversNextBlock`.

**SuppressionMap_IsSuppressed_PrettierIgnoreBeforeList_CoversWholeList**: A prettier-ignore comment before a
list covers the whole list. Tested by `SuppressionMap_IsSuppressed_PrettierIgnoreBeforeList_CoversWholeList`.

**SuppressionMap_IsSuppressed_PrettierIgnoreStartEnd_CoversRegion**: Start and end comments cover the text
between them. Tested by `SuppressionMap_IsSuppressed_PrettierIgnoreStartEnd_CoversRegion`.

**SuppressionMap_IsSuppressed_UnterminatedPrettierStart_RunsToEndOfFile**: A start comment without an end
covers to the end of the file. Tested by `SuppressionMap_IsSuppressed_UnterminatedPrettierStart_RunsToEndOfFile`.

**SuppressionMap_Build_UnknownDirectives_AreIgnored**: Unrelated comments and unknown directive names
suppress nothing. Tested by `SuppressionMap_Build_UnknownDirectives_AreIgnored`.

**SuppressionMap_Build_CommentsInCodeAndFrontMatter_AreIgnored**: Comment-like text in code and front matter
is not a directive. Tested by `SuppressionMap_Build_CommentsInCodeAndFrontMatter_AreIgnored`.

**SuppressionMap_IsSuppressed_InlineComment_IsDirective**: An inline comment inside a paragraph is a
directive. Tested by `SuppressionMap_IsSuppressed_InlineComment_IsDirective`.

**SuppressionMap_Build_InlinePrettierComment_IsIgnored**: An inline prettier comment is ignored. Tested by
`SuppressionMap_Build_InlinePrettierComment_IsIgnored`.

**SuppressionMap_Build_AliasResolver_ResolvesAliases**: Rule identifiers in directives and queries pass
through the alias function. Tested by `SuppressionMap_Build_AliasResolver_ResolvesAliases`.

**SuppressionMap_IsSuppressedRange_BlockIntersectingRegion_IsSuppressed**: A range that intersects a
suppressed region is suppressed. Tested by
`SuppressionMap_IsSuppressedRange_BlockIntersectingRegion_IsSuppressed`.

**SuppressionMap_Ranges_AreSortedByStart**: `Ranges` is sorted by start offset. Tested by
`SuppressionMap_Ranges_AreSortedByStart`.

**SuppressionMap_Build_NullArguments_Throw**: Null arguments are rejected. Tested by
`SuppressionMap_Build_NullArguments_Throw`.
