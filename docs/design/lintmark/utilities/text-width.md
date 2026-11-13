### TextWidth

![Utilities Structure](UtilitiesView.svg)

#### Purpose

`TextWidth` measures how many terminal columns a text occupies. Line wrapping and table layout must agree with what a
reader sees, and the number of UTF-16 code units is a poor measure: East Asian characters and emoji take two columns,
and combining marks, joiners, and variation selectors take none.

#### Data Model

`TextWidth` is a static class with one constant and one immutable table, and no mutable state.

**EmojiPresentationSelector**: the code point `U+FE0F`, the variation selector that requests emoji presentation.

**WideRanges**: an array of inclusive code point ranges `(First, Last)`, sorted by start, of the characters that
terminals draw two columns wide. It holds the Wide and full-width values of the EastAsianWidth property of Unicode
18.0.0, with adjacent ranges merged, and adds the regional indicator letters `U+1F1E6` to `U+1F1FF`, which the
property keeps neutral but terminals draw wide as flags. Unassigned code points that the property leaves out, between
and next to the ranges, are narrow. The ranges cover, in order:

- the leading consonants of Hangul conjoining letters (`U+1100` to `U+115F`) and a set of wide symbols, trigrams, and
  default-emoji characters between `U+231A` and `U+2B55`;
- CJK radicals, punctuation, Hiragana, Katakana, Hangul compatibility letters, and CJK ideographs, in the
  sub-ranges `U+2E80` to `U+2E99`, `U+2E9B` to `U+2EF3`, `U+2F00` to `U+2FD5`, `U+2FF0` to `U+303E` (which includes the
  ideographic space `U+3000`), `U+3041` to `U+3096`, `U+3099` to `U+30FF`, `U+3105` to `U+312F`, `U+3131` to `U+318E`,
  `U+3190` to `U+31E5`, `U+31EF` to `U+321E`, `U+3220` to `U+3247`, `U+3250` to `U+A48C`, and `U+A490` to `U+A4C6`;
- the extended Hangul conjoining leading consonants (`U+A960` to `U+A97C`), Hangul syllables (`U+AC00` to `U+D7A3`),
  and CJK compatibility ideographs (`U+F900` to `U+FAFF`);
- vertical forms and CJK compatibility forms (`U+FE10` to `U+FE19`, `U+FE30` to `U+FE52`, `U+FE54` to `U+FE66`, and
  `U+FE68` to `U+FE6B`), and full-width forms (`U+FF01` to `U+FF60` and `U+FFE0` to `U+FFE6`);
- the supplementary ranges for historic ideographic scripts, symbols, and kana (`U+16FE0` to `U+16FE4`, `U+16FF0` to
  `U+16FF6`, `U+17000` to `U+18CDA`, `U+18CFF` to `U+18D20`, `U+18D80` to `U+18DF2`, `U+18E00` to `U+19191`,
  `U+191A0` to `U+191D2`, `U+1AFF0` to `U+1AFF3`, `U+1AFF5` to `U+1AFFB`, `U+1AFFD` to `U+1AFFE`, `U+1B000` to
  `U+1B128`, `U+1B132`, `U+1B150` to `U+1B152`, `U+1B155`, `U+1B164` to `U+1B168`, and `U+1B170` to `U+1B2FB`), and the
  counting rod and tetragram symbols (`U+1D300` to `U+1D356` and `U+1D360` to `U+1D376`);
- the emoji and enclosed symbol blocks between `U+1F004` and `U+1FAFA`, in the sub-ranges that the property gives,
  that terminals draw wide, including the regional indicator letters of flags (`U+1F1E6` to `U+1F1FF`);
- the CJK ideograph extensions of planes 2 and 3 (`U+20000` to `U+2FFFD` and `U+30000` to `U+3FFFD`).

The half-width forms (`U+FF61` to `U+FFDC` and `U+FFE8` to `U+FFEE`), which include half-width Katakana and Hangul,
lie outside the table, so they are narrow. Characters whose width is ambiguous (such as Greek and Cyrillic letters)
are measured as narrow, the choice western terminals make. A range may contain a combining mark (such as `U+3099`);
the mark has width 0 because the category test comes before the table lookup.

**Cluster model**: The text is cut into grapheme clusters (what a reader perceives as one character) with
`StringInfo`, and each cluster is measured as a unit:

- a cluster whose characters are all control, format, combining, or separator characters has width 0;
- the width of a cluster is the width of its first visible character: 2 for a wide character, otherwise 1;
- a cluster that carries the emoji presentation selector has width 2, which makes emoji sequences joined with the
  zero-width joiner, emoji with a skin tone modifier, key-cap sequences, and flags one wide character;
- a lone surrogate counts as one narrow character.

A character has width 0 when its Unicode category is control, format (zero-width space, joiners, bidirectional marks,
soft hyphen), non-spacing mark, enclosing mark, line separator, or paragraph separator, and for the conjoining Hangul
vowels and final consonants (`U+1160` to `U+11FF` and `U+D7B0` to `U+D7FF`, the two blocks of conjoining Hangul
letters), which only extend a syllable that starts with a leading consonant.
A tab and a line break have width 0, so a caller measures one line at a time and accounts for tabs itself.

#### Key Methods

**Of**: Measures the display width of a text.

- *Parameters*: `string text`.
- *Returns*: `int` — the number of terminal columns; zero for an empty text.
- *Preconditions*: `text` is not null.
- *Postconditions*: the result is the sum of the widths of the grapheme clusters of the text; it does not depend on the
  current culture.

Algorithm: scan the text from the start; at each position ask `StringInfo.GetNextTextElementLength` for the length of
the next cluster, add the width of the cluster, and advance. The cost is linear in the text length.

**ClusterWidth** (private): Decodes the cluster rune by rune, remembers whether the emoji presentation selector
occurs, takes the width of the first rune whose width is not zero, and returns 0 for a cluster with no visible
character (whatever its selectors say), 2 when the selector occurs, and otherwise that width.

**RuneWidth** (private): Returns 0 for the invisible categories and the conjoining Hangul ranges, 2 for a wide code
point, and 1 otherwise.

**IsWide** (private): A binary search over `WideRanges`.

#### Error Handling

`ArgumentNullException` for a null text; there is no other failure. A malformed UTF-16 sequence (a lone surrogate) is
not an error and counts as one narrow character.

#### Dependencies

- **.NET BCL** — `StringInfo`, `Rune`, and `UnicodeCategory`. No other tool units and no OTS library are used.

#### Callers

N/A - no production caller yet; the line wrapping and table formatting of later phases will use it. It is verified
by unit and subsystem tests only.
