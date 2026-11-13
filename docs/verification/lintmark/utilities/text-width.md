### TextWidth

#### Verification Approach

`TextWidth` is verified with unit tests in `TextWidthTests.cs`. The class is static, pure, and depends only on the .NET
base class library, so no test doubles are used. Tests call `TextWidth.Of` with literal text and assert on the number
of columns. Many tests use data-driven inputs for a family of characters and often append or prepend a visible
letter, to prove that an invisible character does not hide its neighbors. Text that the source file could not hold
safely is written with escape sequences, and a lone surrogate is built from its code unit because a literal in test
data would be repaired. The test for the edges of the wide ranges is data-driven over the East Asian Wide and
full-width
ranges of Unicode 18.0.0 (merged where adjacent) and the regional indicators: for each range it measures the first and
last code point, which must be wide, and the code points just before and after, which must not be wide, so that every
edge of the binary search over the range table is exercised. A range edge that is a combining mark (non-spacing,
spacing, or enclosing) is moved inward.
The test for culture independence sets `CultureInfo.CurrentCulture` to the invariant culture
and then to `tr-TR` and restores the original culture in a `finally` block. The test for linear time measures texts of
one million and half a million characters after a warm-up, against a time limit that a linear algorithm meets by a wide
margin on a slow machine and that a quadratic algorithm exceeds by orders of magnitude, and compares repeated results.

#### Test Environment

N/A - standard test environment. The culture test changes the culture of the current thread only and restores it
afterward.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Ideographs (also outside the basic plane), kana, Hangul syllables, full-width forms, and the ideographic space take
  two columns each, and the first and last code point of every wide range take two columns while the code points just
  outside it do not.
- An empty text takes zero columns, ASCII and half-width forms and characters of ambiguous width take one column each,
  and mixed text is the sum of its parts.
- Emoji, emoji sequences, flags, numbered key sequences, and symbols with the emoji presentation selector take two
  columns, and a symbol with the text presentation selector takes one.
- Combining marks (non-spacing, spacing, and enclosing), zero-width characters, variation selectors, control
  characters, tabs, line and paragraph
  separators, and the soft hyphen take no column.
- A base character with combining marks keeps its own width, also when the mark is a spacing combining mark, a text
  that starts with a combining mark is measured by its first visible character, and a sequence of conjoining Hangul
  letters, also those
  of the extended block, is one wide character.
- A lone surrogate takes one column, and a null text throws `ArgumentNullException`.
- The result does not depend on the culture, and very long texts are measured within a time limit that only a linear
  algorithm meets, with the same result each time.

#### Test Scenarios

##### Normal operation

**Ideographs take two columns each**: Two common ideographs give 4 columns, and one ideograph from the extension plane
outside the basic plane gives 2.

**Hiragana and Katakana take two columns each**: Four Hiragana and four Katakana characters give 8 columns each.

**Hangul syllables take two columns each**: Two Hangul syllables, each written as one character, give 4 columns.

**Full-width forms take two columns each**: A full-width Latin letter gives 2, two of them give 4, and a full-width
currency symbol gives 2.

**The ideographic space takes two columns**: The character `U+3000` gives 2.

**ASCII text takes one column per character**: The texts `a`, `abc`, `Hello, world!`, a single space, and `a b` give
1, 3, 13, 1, and 3 columns, each equal to the text length.

**Half-width Katakana takes one column**: A half-width Katakana letter gives 1.

**The half-width forms take one column**: The first and last half-width Katakana and Hangul characters (`U+FF61` and
`U+FF9F`) and the half-width forms after the full-width signs (`U+FFDC`, `U+FFE8`, and `U+FFEE`) each give 1, and a
letter after them is still counted.

**Characters of ambiguous width take one column**: A Greek letter, a Cyrillic letter, a box drawing character, and an
accented Latin letter each give 1, also when a zero-width space follows.

**Mixed text is the sum of its parts**: The texts `a漢b`, `abc漢字def`, an accented letter with a combining mark
followed by an ideograph and an emoji, and a letter followed by zero-width characters give 4, 10, 5, and 1 columns.
Appending the letter `z` adds one column in each case.

**An emoji takes two columns**: An emoji outside the basic plane gives 2, as one wide character and not as two code
units. A default-emoji symbol in the basic plane (the heavy check mark in a box) also gives 2.

**An emoji sequence is one wide character**: A family written with three emoji joined by the zero-width joiner gives 2,
and an emoji followed by a skin tone modifier gives 2.

**A flag is one wide character**: Two regional indicator letters that form a flag give 2.

**A numbered key sequence takes two columns**: The digit one, the emoji presentation selector, and the combining
enclosing mark give 2.

**The emoji presentation selector widens a narrow symbol**: The heavy black heart followed by the emoji presentation
selector gives 2, while the heart alone gives 1.

**The text presentation selector keeps a symbol narrow**: The heavy black heart followed by the text presentation
selector gives 1.

**A combining mark does not change the width of its base**: A letter followed by a combining acute accent gives 1, as
does the same letter written as one character, and an ideograph followed by a combining accent gives 2.

**Conjoining Hangul letters are one wide character**: A leading consonant, a vowel, and a final consonant of the
conjoining Hangul letters give 2 columns as one cluster.

**Zero-width characters take no column**: The zero-width space, the zero-width non-joiner, the zero-width joiner,
the word joiner, and the byte order mark each give 0, and a zero-width space between two letters gives 2.

**Control characters and separators take no column**: A tab, line feed, carriage return, carriage return with line
feed, the null character, the escape character, the delete character, and the next-line character each give 0, and
the letters around them are still counted (2). A tab between two letters gives 2. The line separator and the paragraph
separator give 0, and a letter before them is still counted.

**The soft hyphen takes no column**: The character `U+00AD` gives 0.

**Measuring does not depend on the culture**: The text `IİıiI漢` holds the letter i with and without a dot, which a
Turkish culture treats specially. It gives 7 columns under the invariant culture and the same under `tr-TR`.

##### Boundary conditions

**An empty text has no width**: The empty string gives 0 columns.

**The edges of every wide range are measured by range**: For each range of the table of wide characters (126 ranges
that follow Unicode 18.0.0 and the regional indicators), the first and last code point give 2, and the code point just
before and the one just after the range do not give 2. This covers the first and last entries of the table, entries
with a single code point, and the ranges that were added to follow the property (for example `U+16FF2` to `U+16FF6`,
`U+18CFF` to `U+18D20`, and `U+1AFF0` to `U+1AFF3`). A combining mark at the edge of a range (`U+3099`, `U+16FE4`,
`U+16FF0`, and `U+16FF1`) is
not measured as an edge; the first edge of that Katakana range is `U+309B`, the last edge of the range that starts
at `U+16FE0` is
`U+16FE3`, and the first edge of the range that ends at `U+16FF6` is `U+16FF2`.

**A combining mark inside a wide range has no width**: The combining voiced sound mark `U+3099`, which lies inside a
wide range, gives 0 alone, and after a Hiragana letter the pair gives 2.

**The edges of the Hangul letter block are measured by range**: The character just before the block (`U+10FF`) gives
1, the first and last leading consonants (`U+1100` and `U+115F`) give 2, and the first vowel (`U+1160`) gives 0.
Appending a letter adds one column in each case.

**The edges of the extended Hangul letter block are measured by range**: The last Hangul syllable (`U+D7A3`) gives 2,
the unassigned characters after it (`U+D7A4` and `U+D7AF`) give 1, and the first and last extended vowels and final
consonants (`U+D7B0`, `U+D7C6`, `U+D7FB`, and `U+D7FF`) give 0. Appending a letter adds one column in each case. A
leading consonant followed by an extended vowel and an extended final consonant gives 2 as one cluster.

**A lone combining mark has no width**: A combining acute accent without a base gives 0.

**A lone spacing combining mark has no width**: The spacing combining mark `U+0903` without a base gives 0.

**A spacing combining mark does not change the width of its base**: The letter `U+0915` followed by the spacing
combining vowel sign `U+093E` gives 1, as does the letter alone.

**A text that starts with a spacing combining mark is measured by its first visible character**: The mark `U+0903`
followed by a combining acute accent gives 0, the mark followed by `A` gives 1, the vowel sign `U+093E` followed by the
letter `U+0915` gives 1, and the mark followed by an ideograph gives 2.

**A lone variation selector has no width**: Each of the emoji and text presentation selectors alone gives 0, also
the emoji one, and a letter after it is still counted (1).

**A lone surrogate counts as one column**: An unpaired high surrogate and an unpaired low surrogate each give 1 with
no exception.

**Very long texts are measured quickly and the same way each time**: A text of 1000000 narrow characters and one of
500000 wide characters are each measured twice, after a warm-up. Each gives 1000000 columns both times, and the four
measurements together finish within thirty seconds, which a linear algorithm meets in milliseconds and a quadratic one
does not.

##### Error handling

**A missing text is rejected**: `Of` is called with null. It throws `ArgumentNullException` for the parameter `text`.
