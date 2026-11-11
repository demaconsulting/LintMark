### SourceDocument

#### Verification Approach

`SourceDocument` is verified with unit tests in `SourceDocumentTests.cs`. The class depends only on the .NET
base class library, so no mocking is needed. Tests build documents from byte arrays through `Create` and
from text through `FromText`, and assert on the normalized text, the terminator counts, the preserved byte
order mark, the typed failures (rule identifier, line, and column), the position arithmetic, and the bytes
produced by `ToBytes`.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- LF, CRLF, and mixed files normalize to LF text with correct terminator counts.
- Invalid UTF-8, a UTF-16 byte order mark, and a bare carriage return are rejected with the documented rule
  identifier and position.
- A UTF-8 byte order mark is preserved and restored; a file without one gets none.
- Unchanged text written back in the original terminator style reproduces the original bytes.

#### Test Scenarios

**SourceDocument_Create_LfOnly_KeepsTextAndCountsLf**: A file with LF terminators keeps its text and
counts only LF terminators. Tested by `SourceDocument_Create_LfOnly_KeepsTextAndCountsLf`.

**SourceDocument_Create_CrLf_NormalizesToLfAndCountsCrLf**: A CRLF file is normalized to LF and counts CRLF
terminators. Tested by `SourceDocument_Create_CrLf_NormalizesToLfAndCountsCrLf`.

**SourceDocument_Create_MixedEndings_IsMixedAndMajorityResolved**: A file with both terminators reports
`IsMixed` and resolves the majority style. Tested by
`SourceDocument_Create_MixedEndings_IsMixedAndMajorityResolved`.

**SourceDocument_MajorityLineEnding_Tie_IsLf**: Equal counts resolve to LF. Tested by
`SourceDocument_MajorityLineEnding_Tie_IsLf`.

**SourceDocument_MajorityLineEnding_NoTerminators_IsLf**: A file without terminators resolves to LF.
Tested by `SourceDocument_MajorityLineEnding_NoTerminators_IsLf`.

**SourceDocument_Create_EmptyFile_HasSingleEmptyLine**: An empty file has one empty line and no final
newline. Tested by `SourceDocument_Create_EmptyFile_HasSingleEmptyLine`.

**SourceDocument_Create_FinalNewline_IsDetected**: `HasFinalNewline` is true only when the text ends with a
terminator. Tested by `SourceDocument_Create_FinalNewline_IsDetected`.

**SourceDocument_Create_Utf8Bom_IsStrippedAndPreserved**: A UTF-8 byte order mark is removed from the text,
`HasBom` is true, and the encoding emits it again. Tested by
`SourceDocument_Create_Utf8Bom_IsStrippedAndPreserved`.

**SourceDocument_Create_NoBom_WritesNoBom**: A file without a byte order mark gets none when written.
Tested by `SourceDocument_Create_NoBom_WritesNoBom`.

**SourceDocument_Create_Utf16Bom_ThrowsEncodingError**: A UTF-16 byte order mark raises a
`file-encoding-error` at 1:1. Tested by `SourceDocument_Create_Utf16Bom_ThrowsEncodingError`.

**SourceDocument_Create_InvalidUtf8_ThrowsWithPosition**: An invalid byte raises a `file-encoding-error`
at the line and column of the byte. Tested by `SourceDocument_Create_InvalidUtf8_ThrowsWithPosition`.

**SourceDocument_Create_LoneCr_ThrowsLineEndingErrorWithPosition**: A bare carriage return raises a
`file-line-ending-error` at its position. Tested by
`SourceDocument_Create_LoneCr_ThrowsLineEndingErrorWithPosition`.

**SourceDocument_FromText_TrailingCr_ThrowsLineEndingError**: A carriage return at the very end of the text
is a bare carriage return. Tested by `SourceDocument_FromText_TrailingCr_ThrowsLineEndingError`.

**SourceDocument_FromText_Crlf_NormalizesLikeCreate**: `FromText` normalizes exactly like `Create`. Tested
by `SourceDocument_FromText_Crlf_NormalizesLikeCreate`.

**SourceDocument_Create_NullPath_ThrowsArgumentNullException**: A null path is rejected. Tested by
`SourceDocument_Create_NullPath_ThrowsArgumentNullException`.

**SourceDocument_LineStarts_RecordEveryLine**: The line table has the start of every line, including the
empty line after a final newline. Tested by `SourceDocument_LineStarts_RecordEveryLine`.

**SourceDocument_PositionOf_TabsAndSurrogates_CountsUtf16UnitsWithTabAsOne**: Columns count UTF-16 code
units (a surrogate pair is two) and a tab is one. Tested by
`SourceDocument_PositionOf_TabsAndSurrogates_CountsUtf16UnitsWithTabAsOne`.

**SourceDocument_OffsetOf_RoundTripsWithPositionOf**: `OffsetOf` is the inverse of `PositionOf` for every
offset. Tested by `SourceDocument_OffsetOf_RoundTripsWithPositionOf`.

**SourceDocument_PositionAndOffset_OutOfRange_Throw**: Offsets, lines, and columns outside the document raise
`ArgumentOutOfRangeException`. Tested by `SourceDocument_PositionAndOffset_OutOfRange_Throw`.

**SourceDocument_Slice_ReturnsRequestedCharacters**: `Slice` returns the requested characters. Tested by
`SourceDocument_Slice_ReturnsRequestedCharacters`.

**SourceDocument_ToBytes_Lf_RestoresOriginalBytes**: An LF file written back with LF reproduces its bytes.
Tested by `SourceDocument_ToBytes_Lf_RestoresOriginalBytes`.

**SourceDocument_ToBytes_CrLf_RestoresOriginalBytes**: A CRLF file written back with CRLF reproduces its
bytes. Tested by `SourceDocument_ToBytes_CrLf_RestoresOriginalBytes`.

**SourceDocument_ToBytes_BomAndCrLf_RestoresOriginalBytes**: A file with a byte order mark and CRLF
terminators is restored byte for byte. Tested by `SourceDocument_ToBytes_BomAndCrLf_RestoresOriginalBytes`.

**SourceDocument_ToDisk_Target_ConvertsLineEndings**: The static `ToDisk` converts normalized text to the
requested terminator style. Tested by `SourceDocument_ToDisk_Target_ConvertsLineEndings`.
