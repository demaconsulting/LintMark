### TextWidth

![Document Structure](DocumentView.svg)

#### Purpose

`TextWidth` measures how many terminal columns a text occupies. Wrapping and table layout must agree with what a
reader sees, and the number of UTF-16 code units is a poor measure: East Asian characters and emoji take two columns,
and combining marks, joiners and variation selectors take none.

#### Data Model

A static, stateless class with one constant and one immutable table:

- **EmojiPresentationSelector**: code point `U+FE0F`, the variation selector that requests emoji presentation.
- **WideRanges**: sorted inclusive code point ranges `(First, Last)` of the characters drawn two columns wide: the Wide
  and full-width values of the East Asian Width property of the Unicode standard (adjacent ranges merged, covering
  Hangul conjoining leading consonants, CJK radicals, punctuation, kana, ideographs, Hangul syllables, compatibility
  and full-width forms, and default-emoji symbols and pictographs), plus the regional indicator letters `U+1F1E6` to
  `U+1F1FF` that terminals draw wide as flags. Everything else, including unassigned code points between ranges and
  characters of ambiguous width (Greek, Cyrillic), is narrow.

#### Key Methods

- `Of(text)`: returns the display width, 0 for an empty text. The text is cut into grapheme clusters with
  `StringInfo.GetNextTextElementLength` and the cluster widths are summed. The text is measured as one line, so a caller
  passes one line at a time and accounts for tabs itself.
- `ClusterWidth` (private): decodes the runes of a cluster. The width is that of the first rune whose own width is not
  zero, so combining marks and joiners never add width. If the cluster has no such rune its width is 0, whatever its
  selectors say. A cluster that contains `U+FE0F` has width 2, which makes emoji presentation sequences, flags and
  zero-width-joiner sequences one wide character.
- `RuneWidth` (private): 0 for the categories control (this includes tab, CR and LF), format (zero-width space and
  joiners, bidirectional marks), non-spacing mark, spacing combining mark, enclosing mark, line separator and paragraph
  separator; 0 for the conjoining Hangul vowel and final consonant blocks (`U+1160` to `U+11FF`, `U+D7B0` to
  `U+D7FF`), which only extend a syllable; 2 when `IsWide`; otherwise 1. Marks are tested first, so a combining
  mark inside a wide range, such as `U+3099`, has width 0.
- `IsWide` (private): binary search of `WideRanges`.

A lone surrogate is not a valid rune; it decodes as the replacement character and counts as one narrow character.

#### Error Handling

`Of` throws `ArgumentNullException` for null text. No other failure exists: every character has a width, malformed
surrogates count as one column, and the unit has no state to corrupt. It is safe for concurrent use.

#### Dependencies

The .NET base class library only (`System.Globalization.StringInfo`, `System.Text.Rune`, `UnicodeCategory`). It is
a unit of the `Document` subsystem but depends on no other unit.

#### Callers

`ParagraphFormatter` (prefix, line and wrap widths), `LineWrapper` (atom widths), `TableFormatter` and the table lint
rules (`TableRules`, cell widths), `LinkRefactorer` (link and reference label widths) and `DocumentFormatter` (the
`line-length` overflow prefilter).
