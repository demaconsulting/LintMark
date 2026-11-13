### TextWidth

#### Verification Approach

`TextWidth` is verified with unit tests in `TextWidthTests.cs`. The class is static, pure, and depends only on the .NET
base class library, so no test doubles are used. Tests call `TextWidth.Of` with literal text and assert on the number
of columns. Many tests use data-driven inputs for a family of characters and often append or prepend a visible
letter, to prove that an invisible character does not hide its neighbors. Text that the source file could not hold
safely is written with escape sequences, and a lone surrogate is built from its code unit because a literal in test
data would be repaired. The test for culture independence sets `CultureInfo.CurrentCulture` to the invariant culture
and then to `tr-TR` and restores the original culture in a `finally` block. The test for linear time measures long
texts against a generous time limit and compares repeated results.

#### Test Environment

N/A - standard test environment. The culture test changes the culture of the current thread only and restores it
afterward.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Ideographs (also outside the basic plane), kana, Hangul syllables, full-width forms, and the ideographic space take
  two columns each.
- An empty text takes zero columns, ASCII and half-width forms and characters of ambiguous width take one column each,
  and mixed text is the sum of its parts.
- Emoji, emoji sequences, flags, numbered key sequences, and symbols with the emoji presentation selector take two
  columns, and a symbol with the text presentation selector takes one.
- Combining marks, zero-width characters, variation selectors, control characters, tabs, line and paragraph
  separators, and the soft hyphen take no column.
- A base character with combining marks keeps its own width, and a sequence of conjoining Hangul letters is one wide
  character.
- A lone surrogate takes one column, and a null text throws `ArgumentNullException`.
- The result does not depend on the culture, and very long texts are measured quickly with the same result each time.

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

**The edges of the Hangul letter block are measured by range**: The character just before the block (`U+10FF`) gives
1, the first and last leading consonants (`U+1100` and `U+115F`) give 2, and the first vowel (`U+1160`) gives 0.
Appending a letter adds one column in each case.

**A lone combining mark has no width**: A combining acute accent without a base gives 0.

**A lone variation selector has no width**: Each of the emoji and text presentation selectors alone gives 0, also
the emoji one, and a letter after it is still counted (1).

**A lone surrogate counts as one column**: An unpaired high surrogate and an unpaired low surrogate each give 1 with
no exception.

**Very long texts are measured quickly and the same way each time**: A text of 100000 narrow characters and one of
50000 wide characters are each measured twice. Each gives 100000 columns both times, and the four measurements together
finish within ten seconds.

##### Error handling

**A missing text is rejected**: `Of` is called with null. It throws `ArgumentNullException` for the parameter `text`.
