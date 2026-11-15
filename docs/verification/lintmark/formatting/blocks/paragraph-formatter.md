#### ParagraphFormatter

##### Verification Approach

`ParagraphFormatter` is verified with unit tests in `ParagraphFormatterTests.cs` and with the document-level wrap tests
of `DocumentFormatter` in `DocumentFormatterWrapTests.cs` and `DocumentFormatterSentenceWrapTests.cs`. The real parser,
`AtomTokenizer`, `LineWrapper`, `LintMarkConfig`, and `EditSet` are used; nothing is replaced by a double. Tests assert
the exact wrapped text and the reported findings. A regression test of the wrap guard uses the HTML equivalence check
of the verification tool as an oracle to prove that a wrap which would change the rendered text is never produced.

##### Test Environment

The tests need the standard test runner and in-memory text. The shared helper `BlockFormatterAssertions` calls the
HTML-equivalence and structure checks of the development tool `tools/LintMark.Verify` as a test oracle, and
`ParagraphFormatterTests.cs` calls the HTML-equivalence check directly, so the test project references that tool. The
tool is not shipped and is not a software item; it is built with the tests and needs no network access and no Pandoc
installation.

##### Acceptance Criteria

- All unit tests pass with zero failures.
- A paragraph that fits the width is kept as written. A paragraph with a line beyond the width and a break opportunity
  is filled up to the width, and the result is stable.
- `sentence` puts each sentence on its own line, and `semantic` breaks only as needed, at clauses; both leave a
  paragraph as written when the layout is already right.
- Code spans, links, and URLs are never split, hard breaks stay, and quote and list prefixes are repeated on every line.
- A line never starts with block syntax, and a lazy line gets the quote marker.
- Shapes whose meaning depends on their breaks (definitions, footnote paragraphs, Pandoc structure, tabs, forced block
  starts, lazy list lines, quote markers without a blank) are kept as written and reported.
- Residual overflow that cannot be fixed is reported, never hidden.
- Wide and combining characters are measured in display columns.
- Disabled wrapping and suppressed rules keep the paragraph.
- Wrapping is enabled by a non-zero line length with `line-length` on, or by the `sentence` or `semantic` mode with
  `paragraph-wrap` on; the layout is enforced for every paragraph only in the second case.
- A `sentence` or `semantic` rewrite is attributed to `paragraph-wrap` and reports where the first line that is laid
  out differently starts; a `fill` rewrite is attributed to `line-length`.
- A null configuration, context, or block is refused with an `ArgumentNullException`.

##### Test Scenarios

**Overflowing paragraph is filled**: A paragraph beyond the width is filled up to it, and a paragraph within the width
is kept.

**Single long atom is kept**: One long word or URL stays, a word before it is separated, and the residual overflow is
reported.

**No good break is reported**: A paragraph with no break opportunity is kept and the overflow is reported.

**Atoms are never split**: Code spans and URLs are kept whole, and hard breaks stay where they are.

**Prefixes are repeated**: Quotes keep their markers, list items keep the indentation of the content, and a lazy quote
line gets the quote marker.

**Block syntax never starts a line**: A break before text that looks like a marker, a heading, a fence, or a fancy list
marker moves earlier, and text that looks like a marker in the middle of a line is still wrapped.

**Kept as written and reported**: Definition-like lines, footnote paragraphs, Pandoc structural paragraphs, tabs, a
forced line start that is block syntax, a lazy list line, a quote marker without a blank, and a shape that depends on
its breaks are not changed.

**Sentence and semantic modes lay out**: Paragraphs are laid out one sentence per line or by clauses, and a second run
changes nothing.

**Wide characters are measured in columns**: Wide and combining characters count by display width.

**Settings and suppression keep the paragraph**: Settings that disable wrapping, or a suppressed wrap rule, keep the
paragraph as written. A suppressed rule that is unrelated to wrapping does not stop the wrap.

**Wrap guard rejects a changed rendering**: A wrap that ignored the guard rules would change the rendered text, and the
formatter never produces it.

**Layout enforcement follows the wrap mode and the paragraph-wrap rule**: The layout is enforced only when the mode is
`sentence` or `semantic` and `paragraph-wrap` is on. It is not enforced in the `fill` mode, and not when
`paragraph-wrap` is switched off, whatever the mode. Wrapping is enabled when either wrap rule applies: a non-zero line
length with `line-length` on, or a layout mode with `paragraph-wrap` on.

**Sentence and semantic outcomes name the rule and the first difference**: A paragraph that is not in the `sentence`
or `semantic` layout is rewritten under `paragraph-wrap` and the outcome gives the offset of the first line laid out
differently, also when no line is too wide and when the line length is 0 (unlimited). A paragraph already in the layout
gives no edit and no finding.

**Null arguments are refused**: A null configuration for the enabled and enforced checks, and a null context or block
for the paragraph decision, throw an `ArgumentNullException`.
