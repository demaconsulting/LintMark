### TextWidth

#### Verification Approach

`TextWidth` is verified with unit tests in `TextWidthTests.cs`. It is a static function from text to a number of
terminal columns with no dependencies, so the tests pass strings and assert on the returned width. No mocks, files,
or process-wide state are used. Boundary cases come from the character ranges that the unit treats specially: wide
ideographs, Hangul letters and syllables, half-width forms, emoji, combining marks, and zero-width and control
characters.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- ASCII counts one column per character, wide characters (CJK, Hangul syllables, emoji) two, and ambiguous-width
  characters one.
- Combining marks, variation selectors, zero-width characters, and control characters add no width.
- A grapheme cluster (emoji ZWJ sequence, flag, conjoining Hangul, base with combining mark) is measured as one
  unit.
- Mixed text is the sum of the cluster widths, and the empty string has width zero.

#### Test Scenarios

##### Normal operation

**ASCII text is one column per character**: The width equals the length.

**CJK ideographs and Hangul syllables are two columns**: Each character counts two.

**Narrow katakana and half-width forms are one column**: Including the edges of the half-width range.

**Emoji, ZWJ families, flags, and presentation sequences are two columns**: Each cluster counts once; a text symbol
alone is one.

**Base with combining mark is one column**: The mark adds nothing.

**Mixed text adds each cluster**: ASCII, wide, and combining text measure as the sum.

##### Boundary conditions

**Empty string is zero**: No columns.

**Hangul letter range edges are measured**: The edge of each conjoining and extended range gives the documented width.

**Combining mark inside a wide range is zero alone**: A lone mark has no width.

**Spacing combining mark at the start uses the first visible character**: The cluster width follows the base.

##### Error handling

**Control, zero-width, and variation-selector characters are zero**: They never add width and never throw.
