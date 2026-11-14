#### LineWrapper

##### Verification Approach

`LineWrapper` is verified with unit tests in `LineWrapperTests.cs`. Nothing is mocked or replaced by a double. The
wrapper is pure: it takes an array of atoms and two widths and returns the lines, with no file, parser, or document
involved, so every test calls it directly and asserts the exact lines. The real `TextWidth` is used by the wrapper to
measure the atoms, and the tests that check the width of a line call `TextWidth` themselves. The real
`BlockSyntaxGuard` decides which atoms may not start a line; the tests that need a guarded atom use real guarded atoms
(`-`, `+`, `*`, `#`, `>`, and `2026.`), and the block-syntax scenario also asks the real guard about the first word of
every line it gets back. The result type `WrappedLines` is verified together with the wrapper: its constructor is called
directly with invalid arguments. Most tests build their atoms from one string that is split at the blanks, and the
scenarios with a fixed point use a theory with three widths. The test for concurrent use shares one array of atoms among
several threads; each wraps it many times, and every result is compared with the result of a single call.

##### Test Environment

N/A - standard test environment.

##### Acceptance Criteria

- All unit tests pass with zero failures.
- A default array, an empty array, an array with a null element, and an array with an empty atom are rejected and the
  parameter `atoms` is named; a first width or a continuation width below one is rejected and named.
- Atoms are put on a line as long as they fit, and the next atom starts the next line.
- A line that is exactly as wide as the width stays on one line, and one column less splits it.
- An atom that is wider than the width stands alone on its own line, is never cut, and is not reported as an overflow.
- The width is counted in display columns: East Asian and emoji characters count two columns each and combining marks
  count none.
- A break before an atom that the block-syntax guard refuses moves to the nearest earlier atom that may start a line
  when the line that starts there still fits; no returned line starts with a guarded atom in that case.
- When no such break exists, the guarded atom stays on the line it was on, over width, and the result reports a
  breakable overflow.
- The first line uses its own width, and the other lines use the continuation width.
- No line has a leading or trailing blank, and no line of ordinary words is wider than the width.
- Wrapping the lines that a wrap produced, split into atoms again, gives the same lines and the same overflow flag.
- The result is immutable, and a result with a default array, an empty array, a null line, or an empty line cannot be
  created.
- Every thread that wraps the same atoms gets the same lines and the same overflow flag.

##### Test Scenarios

###### Normal operation

**Atoms that do not fit go to the next line**: The atoms `aaa bbb ccc ddd` are wrapped with a first width and a
continuation width of seven columns. The lines are `aaa bbb` and `ccc ddd`, and no breakable overflow is reported.

**An atom that fits exactly stays on the line**: The atoms `aaa bbb`, which need seven columns, are wrapped to seven
columns and to six columns. Seven columns hold both on one line (`aaa bbb`); six columns split them into `aaa` and
`bbb`.

**An atom wider than the width stands alone**: The atoms `a`, `[a long link](http://example.org/a/long/path)`, and `b`
are wrapped to ten columns. Each atom has its own line, the link is not cut, and no breakable overflow is reported
because the wide line holds one atom.

**Only wide atoms get one line each**: Three atoms of ten letters (`aaaaaaaaaa`, `bbbbbbbbbb`, and `cccccccccc`) are
wrapped to five columns. Each atom has its own line and no breakable overflow is reported.

**The width is counted in display columns**: The atoms `日本 日本 日本`, where each word needs four columns, are
wrapped to nine columns and to eight columns. Nine columns give `日本 日本` and `日本`; eight columns give three lines
of one word. The atoms `😀😀 😀😀 x`, where each emoji needs two columns, are wrapped to nine columns and give
`😀😀 😀😀` and `x`. Three atoms are also wrapped to seven columns: one of three letters `e` that each carry a combining
accent (which takes no column of its own), the letter `x`, and one more accented `e`. They give the single line of
those three atoms, whose width measured with `TextWidth` is seven.

**A break before block syntax moves to the atom before it**: The atoms `aaa bbb - ccc` are wrapped to eight columns. The
natural break is before the lone dash, which would start a list item, so the break moves earlier. The lines are `aaa`,
`bbb -`, and `ccc`, and no breakable overflow is reported.

**Block syntax at the break is kept off the line start**: Five inputs are wrapped, each with a guarded atom at the
natural break. The atoms `in the year 2026. was good` are wrapped to fifteen columns and give `in the`,
`year 2026. was`, and `good`, so the number with a period is not at a line start. The atoms `one two three + four`,
`one two three * four`, `one two three # four`, and `one two three > four` are each wrapped to thirteen columns and
give `one two` and `three` followed by the guarded atom and `four` on one line. In every case no returned line starts
with a word that the guard refuses.

**The first line has its own width**: The atoms `aaa bbb ccc ddd` are wrapped with a first width of five columns and a
continuation width of ten columns. The lines are `aaa`, `bbb ccc`, and `ddd`, so the first line holds one atom and the
others hold as many as fit.

**No line has whitespace at its ends**: Sixty words `w0` to `w59` are wrapped to twenty-five columns. Every line equals
its trimmed text and none is wider than twenty-five columns.

**Wrapped lines are a fixed point**: Eighty words are wrapped to ten, twenty-five, and sixty columns. Every seventh word
(from the fourth) is a lone dash and every eleventh word (from the sixth) is `2026.`, so the text holds words that the
guard refuses at a line start; the others are `word` and the index. The lines are joined with blanks, split into atoms,
and wrapped again with the same width. The second result has the same lines and the same overflow flag as the first.

###### Boundary conditions

**A guarded atom that cannot move stays over width and is reported**: The atoms `aaa - bbb` are wrapped to four columns.
The lone dash does not fit after `aaa` and no earlier atom can start the line, so the dash stays on the first line. The
lines are `aaa -` and `bbb`, and a breakable overflow is reported because the first line holds two atoms and is five
columns wide.

###### Error handling

**Invalid arguments are rejected and named**: A default array, an empty array, an array with a null element, and an
array with an empty atom are wrapped; each throws and names `atoms` (an argument exception, an argument exception, an
argument null exception, and an argument exception). A first width of zero throws an argument out of range exception
that names `firstWidth`; a continuation width of zero throws the same exception that names `continuationWidth`.

**The result cannot be changed**: The lines of a result of `a b c` wrapped to three columns report that they are read
only when cast to a mutable list interface. Creating a result with a default array, an empty array, an array with a
null element, or an array with an empty line throws an argument exception (an argument null exception for the null
element).

**Concurrent calls give the same lines**: One hundred atoms are built, every ninth (from the fifth) a lone plus sign and
the others `日本` followed by the index. They are wrapped once with a first width of thirty and a continuation width of
twenty-eight columns. Eight threads then wrap the same atoms one hundred times each; every result has the same lines
and the same overflow flag as the single call.
