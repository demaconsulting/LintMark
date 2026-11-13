### TextWidth

![Utilities Structure](UtilitiesView.svg)

#### Purpose

`TextWidth` measures how many terminal columns a text occupies. Line wrapping and table layout must agree with what a
reader sees, and the number of UTF-16 code units is a poor measure: East Asian characters and emoji take two columns,
and combining marks, joiners, and variation selectors take none.

#### Data Model

`TextWidth` is a static class with two constants and one immutable table, and no mutable state.

**EmojiPresentationSelector**: the code point `U+FE0F`, the variation selector that requests emoji presentation.

**WideRanges**: an array of inclusive code point ranges `(First, Last)`, sorted by start, of the characters that
terminals draw two columns wide. It follows the East Asian Width property of Unicode (wide and full-width) and adds
the emoji that are drawn wide. The ranges cover, in order:

- the leading consonants of Hangul conjoining letters (`U+1100` to `U+115F`) and a set of wide symbols and
  default-emoji characters between `U+231A` and `U+2B55`;
- CJK radicals, punctuation, Hiragana, Katakana, and CJK ideographs (`U+2E80` to `U+A4CF`, which includes the
  ideographic space `U+3000`), the extended Hangul conjoining letters (`U+A960` to `U+A97F`), Hangul syllables
  (`U+AC00` to `U+D7A3`),
  CJK compatibility ideographs (`U+F900` to `U+FAFF`), vertical forms and CJK compatibility forms (`U+FE10` to
  `U+FE19` and `U+FE30` to `U+FE6F`), and full-width forms (`U+FF00` to `U+FF60` and `U+FFE0` to `U+FFE6`);
- the supplementary ranges for historic ideographic scripts and symbols, the Kana supplements, and the CJK ideograph
  extensions (`U+16FE0` to `U+18AFF`, `U+1B000` to `U+1B2FF`, `U+20000` to `U+2FFFD`, and `U+30000` to `U+3FFFD`);
- the emoji blocks of `U+1F004` to `U+1FAFF` that terminals draw wide, including the regional indicator letters of
  flags.

Half-width Katakana and half-width Hangul (`U+FF61` and above) are not in the table, so they are narrow. Characters
whose width is ambiguous (such as Greek and Cyrillic letters) are measured as narrow, the choice western terminals
make.

**Cluster model**: The text is cut into grapheme clusters (what a reader perceives as one character) with
`StringInfo`, and each cluster is measured as a unit:

- a cluster whose characters are all control, format, combining, or separator characters has width 0;
- the width of a cluster is the width of its first visible character: 2 for a wide character, otherwise 1;
- a cluster that carries the emoji presentation selector has width 2, which makes emoji sequences joined with the
  zero-width joiner, emoji with a skin tone modifier, key-cap sequences, and flags one wide character;
- a lone surrogate counts as one narrow character.

A character has width 0 when its Unicode category is control, format (zero-width space, joiners, bidirectional marks,
soft hyphen), non-spacing mark, enclosing mark, line separator, or paragraph separator, and for the conjoining Hangul
vowels and final consonants (`U+1160` to `U+11FF`), which only extend a syllable that starts with a leading consonant.
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

**RuneWidth** (private): Returns 0 for the invisible categories and the conjoining Hangul range, 2 for a wide code
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
