### DocumentFormatter

#### Verification Approach

`DocumentFormatter` is verified with unit tests in `DocumentFormatterTests.cs`, `DocumentFormatterSupportTests.cs`,
`RawTextElementsTests.cs`, `DocumentFormatterWrapTests.cs`, and `DocumentFormatterSentenceWrapTests.cs`. The real
`DocumentParser`, `SourceDocument`, `LintMarkConfig`, and `SourcePatcher` are used. The orchestration tests register
small test formatters from `TestBlockFormatters.cs`: they are deliberately simple or deliberately broken, so that the
tests can prove how the formatter composes edits, which formatter wins, and how a failing formatter is reported. The
wrap tests use the real `ParagraphFormatter`.

The support tests cover the helpers that belong to the unit: `LineLayout` (line extents, blank lines, border-shaped
lines), `LineOwnership` (which lines are claimed by a formatter, protected, or free for the passes), `FrontMatterGuard`,
and `RawTextElements` (paragraphs next to raw Pandoc elements).

#### Test Environment

N/A - the tests need only the standard test runner and in-memory text.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- A block is formatted by the first formatter that accepts it and is left verbatim when none accepts it or when a rule
  of that formatter is suppressed inside the block.
- A claimed block is never edited by the passes, also when it is suppressed.
- The line ending target is the configured one, else the majority one, with a tie going to LF, and a differing file
  gets one `line-ending` violation at line 1, column 1.
- A format-unsafe document gets no edit and no violation.
- A paragraph is wrapped only when it overflows the width, and the wrap is reported at the first column beyond the
  width; a wrap that other rules would change again is withdrawn and reported.
- Lines are classified correctly: code, HTML, math, front matter, and grid tables are never free for the passes.

#### Test Scenarios

**First matching formatter wins**: Two formatters that accept the same block are registered. Only the first one's edits
are collected.

**Unaccepted block stays verbatim**: A block that no formatter accepts is left unchanged.

**Suppressed block stays verbatim**: A block with a suppressed formatter rule, by `disable`, `disable-next-line`,
`prettier-ignore`, or a comment inside the block, keeps its text. A different suppressed rule does not stop formatting.

**Claimed or suppressed paragraph is not normalized**: A paragraph that a formatter claims, or that is suppressed, does
not reach the inline normalizer.

**Line ending decision**: A CRLF file with an LF setting, an LF file with a CRLF setting, a mixed file, a tie, and a
file without terminators produce the expected target and the `line-ending` violation.

**Format-unsafe document is left alone**: A format-unsafe CRLF document produces no edit, no violation, and no wrap.

**Overflowing paragraph is wrapped and reported**: A paragraph beyond the width is wrapped, and the violation is placed
at the first column beyond the width. Two overflowing paragraphs are reported separately.

**Paragraph that fits is left as written**: Paragraphs within the width, or with wrapping disabled by the settings, are
not changed and not reported.

**Wide characters are measured in display columns**: A paragraph of wide characters wraps by columns, not characters.

**Other rules change the lines first**: A list that moves and overflows reaches its final form in one run, and a wrap
that other rules would change again is withdrawn and reported.

**No good break is reported**: A paragraph with a single long atom stays and is reported as a finding that cannot be
fixed. A lazy list line is reported and not changed.

**Sentence and semantic modes lay out in context**: Paragraphs are laid out one sentence per line, or broken at clauses,
inside lists and quotes. The modes are idempotent, and suppressed rules are respected independently.

**Raw Pandoc element next to a paragraph**: A closed raw element protects the next paragraph from wrapping, and an
open one leaves the paragraph as written.

**Lines are classified**: Mixed documents, an unclosed fence in a list, and indented code with whitespace-only lines are
classified into claimed, protected, and free lines. Front matter is recognized only at the first line.
