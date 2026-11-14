#### ParagraphFormatter

##### Verification Approach

`ParagraphFormatter` is verified with unit tests in `ParagraphFormatterTests.cs`. The real `DocumentParser`,
`SourceDocument`, `LintMarkConfig`, `EditSet`, `SourcePatcher`, and `RuleCatalog` are used, and so are the real
`AtomTokenizer`, `LineWrapper`, and `BlockSyntaxGuard` that the formatter calls; nothing is replaced by a double. The
formatter is not a block formatter, so the tests call it directly: a helper parses an LF-normalized text with the
`BlockFormatterAssertions` helper of the test project, picks the paragraph by its index among the paragraphs of the
document, formats it with a configuration whose line length is 20 columns unless the scenario says otherwise, and
applies the edit that the formatter proposes with the real `SourcePatcher`. The scenarios assert the exact text that
results, which is the input where the paragraph must be kept as written. A wrapped result is also formatted again, and
the second run must propose no edit, so that a wrap is a fixed point. A paragraph that is kept must give the shared
outcome that reports nothing, and a paragraph that is kept but reported must give no edit and must report a residual
overflow.

The formatter proves every wrap with a stand-alone check: it renders the paragraph as written and the wrapped paragraph,
each on its own, with the pipeline of the tool, and compares the pages. The shipped formatter uses the real proof, which
is the one that all scenarios except the two proof scenarios use. The test project cannot make the real proof fail on
demand, so the formatter has an internal constructor that takes the proof function; it is a test double of the proof and
is used only by the two scenarios about proof failures. One of them passes a proof that always refuses, and the other
passes a proof that records its arguments and accepts. The refusal inside a quote is not simulated: it is a real parse
of a text that the parser reads differently in its container. The Safety checkers that serve as test-time oracles for
the block formatters (`HtmlEquivalenceChecker`, `StructuralTokenChecker`, and `IdempotencyChecker`) are applied to the
wrapped documents by the corpus tests of the `Blocks` subsystem; they are verified in the `Safety` subsystem.

Suppression is driven by real suppression comments. The settings (`line-length`, the rule switch, and `wrap`) are varied
through the configuration. The scenario about stored forms builds the bytes of the same text with CRLF line endings, a
byte order mark, both, and mixed line endings, and parses each with the real parser. The scenario about concurrent use
parses one document once and shares it, with one formatter instance, among several threads, each of which formats it
many times; every result is compared with the result of a single call.

##### Test Environment

N/A - standard test environment.

##### Acceptance Criteria

- All unit tests pass with zero failures.
- Null arguments and a block that is not a paragraph are rejected; the list of rules (`line-length`,
  `trailing-whitespace`, `hard-tabs`, `emphasis`, `strong`, `bare-urls`, `code-span-space`, `list-indent`,
  `list-marker-space`, and `blockquote-space`) is immutable and holds only registered rules.
- A paragraph that has a line wider than the line length is filled up to the width, and the edit is attributed to the
  `line-length` rule; wrapping the result proposes nothing.
- A paragraph whose lines all fit, also short lines, is kept exactly as written and nothing is reported.
- The width is measured in display columns: wide characters and emoji take two columns and combining marks take none.
- A line that is wide only because of one link or other single unit is never reported or changed; a word before that
  unit is moved apart and the unit stays whole.
- Code spans, links, and bare URLs are never split.
- A hard break stays where it is, in its spaces form and in its backslash form, and each segment is wrapped on its own.
- The continuation lines of a list item and of a quote carry the indentation or the markers of the first line, at any
  depth; a lazy continuation line of a quote gets the quote marker.
- A paragraph with a lazy continuation line in a list item, or with a line after a hard break that would start with
  block syntax, is kept as written and its overflow is reported as one that cannot be fixed automatically.
- No wrapped line starts with a bullet marker, a number marker, a heading marker, or a quote marker.
- A paragraph with Pandoc structure, in a footnote, or with a tab is kept as written and nothing is reported.
- A paragraph in which any rule of the list of rules is suppressed is kept as written and nothing is reported; a
  suppressed rule that is not on the list does not keep the paragraph.
- A line that stays too wide because no good break exists is reported next to the edit and again after the wrap.
- A wrap that the stand-alone proof refuses, or that the parser reads differently inside the quote that holds the
  paragraph, leaves the paragraph as written and reports the overflow.
- No paragraph is wrapped when the line length is 0, when the `line-length` rule is switched off, or when the wrap mode
  is `sentence` or `semantic`.
- LF line endings, CRLF line endings, mixed line endings, and a byte order mark do not change the outcome, and one
  shared parsed document gives the same outcome on many threads.
- An outcome that is inconsistent in its arguments cannot be created.

##### Test Scenarios

###### Normal operation

**An overflowing paragraph is filled up to the width**: The paragraph `aaa bbb ccc ddd eee fff ggg` is formatted with a
line length of 20. The result is `aaa bbb ccc ddd eee` (19 columns) and `fff ggg`. The overflow offset is 20, which is
the first column beyond the width, the overflow width is 27, no overflow stays, and the edit is attributed to the
`line-length` rule.

**A paragraph that fits is kept as written**: `aaa bbb ccc` over `ddd eee`, three one-letter lines, and the single
line `aaa bbb ccc ddd eee` (19 columns) are formatted. Each text is unchanged, the formatter proposes no edit, and the
outcome is the shared one that reports nothing.

**The width is measured in display columns**: The paragraph of two words of five Japanese characters each (21 columns,
11 characters) is wrapped into one word per line. A paragraph of 8 and 7 letters `e` with a combining accent
(31 characters, 16 columns) is kept as written. Emoji count with two columns: two words of five emoji (21 columns) are
wrapped into one word per line.

**A line that is wide because of one link is kept**: A paragraph that is only a link with a long destination is
formatted. It is unchanged and nothing is reported, because the line has no place to break. The same link after the word
`see` is formatted: the word moves apart, the link stays whole on the second line, and nothing overflows by a break that
the formatter could make.

**Code spans and URLs are never split**: A paragraph of four words, a code span that holds the words `x y z w v`, and
the word `fff` is wrapped so that the code span moves whole to the second line, with `fff` after it. The paragraph
`aaa bbb ccc ddd http://example.org/a/b fff` is wrapped into `aaa bbb ccc ddd`, the URL on a line of its own (wider than
the limit, because it cannot be split), and `fff`. No overflow stays.

**Hard breaks stay where they are**: A paragraph with a hard break written as two spaces, a paragraph with a hard break
written as a backslash, and a paragraph whose first line fills the width and ends in a hard break are formatted. Each
segment before and after the break is wrapped on its own, the break stays at the end of the same words with its spaces
or its backslash, and a segment that fits is unchanged.

**A list item keeps the indentation of its content**: `- aaa bbb ccc ddd eee fff ggg`,
`10. aaa bbb ccc ddd eee fff ggg`, a nested item with the words `bbb` to `hhh` under the item `- a`, and the task item
`- [ ] aaa bbb ccc ddd eee fff` are formatted. Each continuation line starts under the content of the first line (two
columns, four columns, four columns, and two columns), and the task box stays with the first words.

**A quote keeps its markers**: `> aaa bbb ccc ddd eee fff ggg`, the nested quote `> > aaa bbb ccc ddd eee fff`, and a
list item in a quote `> - aaa bbb ccc ddd eee fff` are formatted. Each continuation line has the markers of the first
line, and the list item in the quote has the quote marker and the indentation of the item.

**A lazy quote line gets the quote marker**: `> aaa bbb ccc ddd` followed by the unmarked line `eee fff ggg hhh iii jjj`
is formatted. The result is three lines, each starting with the quote marker: the words are filled up to the width and
the lazy line is no longer lazy.

**No wrapped line starts with block syntax**: Paragraphs in which a lone `-`, `+`, `*`, `2026.`, `1)`, `#`, or `>` falls
at the wrap column are formatted: `aaa bbb ccc ddd eee - fff ggg` and the same with each of the other marks. The break
moves to before `eee`, so the mark is in the middle of the second line (`eee - fff ggg`) and never starts a line;
nothing overflows.

**An unrelated rule that is suppressed does not keep the paragraph**: A paragraph after a comment that disables
`heading-style` is formatted. The paragraph is wrapped, and the comment is unchanged.

**An edit is attributed to the line length rule**: Every wrapped paragraph in the scenarios above gives an edit whose
rule is `line-length`, and the outcome of the first scenario is checked in detail: the edit, the offset, and the width.

###### Boundary conditions

**A lazy list line keeps the paragraph and is reported**: `- aaa bbb ccc ddd` followed by the lazy line
`eee fff ggg hhh iii jjj` is formatted. The text is unchanged, no edit is proposed, and the overflow is reported as one
that cannot be fixed, with a non-negative offset. Rewriting would let the list rule move the item on the next run.

**A forced line start that is block syntax keeps the paragraph and is reported**: `aaa bbb ccc ddd eee fff ggg` with a
hard break, followed by the line `2026. hhh`, is formatted. The line after the break would start with a number marker,
and the break cannot be moved, so the text is unchanged, no edit is proposed, and the overflow is reported.

**A Pandoc paragraph is kept as written**: A paragraph with a LaTeX command line after the long line, a paragraph that
follows a fenced div opener `::: note`, and a paragraph with a raw attribute line `{=html}` after the long line are
formatted. Each text is unchanged and nothing is reported.

**A footnote paragraph is kept as written**: The paragraph `aaa bbb ccc ddd eee fff ggg hhh` in the definition of a
referenced footnote is formatted (the second paragraph of the document). The text is unchanged and nothing is reported.

**A paragraph with a tab is kept as written**: A paragraph with a tab between two words, and a paragraph with a long
line followed by a line that starts with a tab, are formatted. Each text is unchanged and nothing is reported.

**A suppressed rule keeps the paragraph**: The paragraph `aaa bbb ccc ddd eee fff ggg` follows a comment that disables
one of `line-length`, `trailing-whitespace`, `hard-tabs`, `emphasis`, `strong`, `bare-urls`, `code-span-space`,
`list-indent`, `list-marker-space`, or `blockquote-space`. For each, the text is unchanged and nothing is reported.

**A line that cannot be shortened is reported next to the edit**: `aaa - bbb` is formatted with a line length of 4. The
result is `aaa -` and `bbb`: the lone dash cannot start a line, so it stays on the first line, which is five columns
wide. An edit is proposed and a residual overflow is reported. The wrapped text is formatted again: no edit is proposed
and the residual overflow is still reported.

**A wrap that the proof refuses keeps the paragraph and is reported**: The paragraph `aaa bbb ccc ddd eee fff ggg` is
formatted by a formatter whose proof function always says that the pages differ. The text is unchanged, no edit is
proposed, and the overflow is reported with a non-negative offset.

**The proof receives the stand-alone paragraph before and after the wrap**: The proof function records its arguments and
accepts. The paragraph `> aaa bbb ccc ddd eee fff ggg` in a quote is formatted. An edit is proposed, the text that the
proof saw as written is `aaa bbb ccc ddd eee fff ggg`, and the wrapped text is `aaa bbb ccc ddd` over `eee fff ggg`;
both have no quote markers and no trailing line ending.

**A quote that the parser reads differently after the wrap keeps the paragraph and is reported**: A quote without a
space after its marker, whose first line starts with a URL, followed on the line by 24 letters and a word, is formatted
with the real proof. The stand-alone paragraph renders the same either way, but inside the quote the parser links the
URL only when it ends the line, so the container check refuses the wrap. The text is unchanged, no edit is proposed, and
the overflow is reported.

**Settings that disable wrapping keep the paragraph**: The paragraph `aaa bbb ccc ddd eee fff ggg` is formatted with a
line length of 0, with the `line-length` rule switched off, with `wrap: sentence`, and with `wrap: semantic`. Each time
the text is unchanged and nothing is reported. `IsEnabled` is false for a line length of 0 and true for a line length
of 20.

###### Robustness

**Stored forms give the same edit**: The text `# T`, then the list item `- aaa bbb ccc ddd eee fff ggg` with the
continuation line `hhh iii` indented by two columns, is parsed from LF text, from CRLF bytes, from LF bytes with a byte
order mark, from CRLF bytes with a byte order mark, and from bytes with mixed line endings. The outcome of the formatter
is equal for every form, and it includes an edit.

**One shared instance gives one result on many threads**: One formatter and one parsed document, a quote of two lines
with a code span and a link that holds a space in its text, are used by eight threads that start together; each formats
the paragraph 100 times. Every outcome equals the outcome of a single call, which includes an edit.

###### Error handling

**Missing arguments are rejected**: `Format` with a null context and with a null block, `IsEnabled` with a null
configuration, and the constructor with a null proof function throw `ArgumentNullException`, each naming its parameter
(`context`, `block`, `config`, and `rendersTheSame`).

**A block that is not a paragraph is rejected**: `Format` for a heading block throws `ArgumentException` that names
`block`.

**The rule list is immutable**: The `RuleIds` property lists the ten rules in the order of the acceptance criteria, each
of which is registered in the `RuleCatalog`, and a cast to a mutable type cannot change it.

**An inconsistent outcome is rejected**: Creating an outcome with an overflow offset below -1 or with a negative width
throws `ArgumentOutOfRangeException`. Creating one with an edit, with a residual overflow, or with a width but without
an overflow offset throws `ArgumentException`, and so does an edit that was not created with its constructor. The shared
outcome that reports nothing has the offset -1.
