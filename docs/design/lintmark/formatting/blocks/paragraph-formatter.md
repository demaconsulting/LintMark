#### ParagraphFormatter

![Blocks Structure](BlocksView.svg)

##### Purpose

`ParagraphFormatter` decides what to do about the layout of one paragraph. In the default `fill` mode it is an
overflow-only reflow: a paragraph is rewritten only when some line is wider than the line length and has a place where
it can be broken, so hand-wrapped text that fits is never touched. In the `sentence` and `semantic` modes every
eligible paragraph has one canonical layout, and a paragraph laid out differently is rewritten. It enforces
`line-length` (fill) and `paragraph-wrap` (sentence and semantic). A line too wide that cannot be shortened is reported
as a lint-only `line-length` finding. The supporting type `ParagraphOutcome` is described here.

##### Data Model

`ParagraphFormatter` is an internal sealed class with one property. It is **not** an `IBlockFormatter` and claims no
lines: it works on the text the other rules produce, and a claim would silence their rules for every paragraph.

- `RuleIds`: `line-length`, `trailing-whitespace`, `hard-tabs`, `emphasis`, `strong`, `bare-urls`, `code-span-space`,
  `list-indent`, `list-marker-space`, `blockquote-space`, and `paragraph-wrap`. A suppression of any of them inside the
  paragraph keeps it as written, because a wrap rewrites whitespace, line starts, and the container prefix of every
  line. `line-length` and `paragraph-wrap` are checked separately, as the rules that ask for the wrap.
- Constants: `Unlimited` (the width for a line length of 0, large enough that no line reaches it) and `Blanks`.

**ParagraphOutcome** (sealed record) is the result of one call: `Edit` (a `TextEdit` rewriting the whole paragraph, or
`null`), `OverflowOffset` and `OverflowWidth` (the first character beyond the line length on the first line that is
too wide and breakable, and that line's width; -1 and 0 for none), `HasResidualOverflow` (a breakable line is still
too wide, after the edit if there is one), and `WrapOffset` (the first line that is not in the sentence or semantic
layout, or -1). `None` is the outcome for a paragraph that needs nothing, and `HasFinding` is true when
`OverflowOffset` or `WrapOffset` is set. Offsets and the edit refer to the text the formatter was given, not to the
original file; `DocumentFormatter` translates them.

A line is *too wide* when its display width (`TextWidth`), container prefix included and trailing blanks excluded,
exceeds the line length. It is *breakable* when the tokenizer counts a break opportunity in it.

##### Key Methods

**IsEnabled** (static): `true` when the line length is not 0 and `line-length` is on, or the wrap mode is not `fill`
and `paragraph-wrap` is on. **IsLayoutEnforced** (static): `true` when the mode is `sentence` or `semantic` and
`paragraph-wrap` is on; then every paragraph is checked whatever the width of its lines. A null configuration throws
`ArgumentNullException`.

**Format**: Takes a `FormatContext` and a paragraph `BlockInfo` and returns a `ParagraphOutcome`. It is pure; nothing
is written. A null argument throws `ArgumentNullException`; a block that is not a paragraph with inline content gives
`None`.

1. *Exempt*: `None` when the settings do not ask for a wrap, the paragraph is in a footnote or its first line is
   protected (`LineOwnership`), or a rule of `RuleIds` other than the two wrap rules is suppressed over its range.
2. *Trigger*: measure every line; `tooWide` needs `line-length` on and unsuppressed, and `layoutActive` needs
   `IsLayoutEnforced` and `paragraph-wrap` unsuppressed. `None` when neither holds.
3. *Keep as written, silently*: a tab in the text or prefixes, or a line that is Pandoc structure
   (`BlockSyntaxGuard.IsPandocStructuralLine`).
4. Tokenize (`AtomTokenizer.Tokenize`) and find the first too-wide line with a break opportunity. When there is none
   and the layout is not enforced (every too-wide line is a single link, URL, code span, or image), the result is
   `None`.
5. *Keep as written, reporting the overflow*: the tokenizer refused the paragraph, a continuation line is lazy and a
   list item contains the paragraph (rewriting would let the list rule move the item next run), a line after a hard
   break would start with block syntax, or `WrapGuard.KeepsAsWritten` applies. The outcome has `HasResidualOverflow`
   and no edit; the layout finding is not reported because it is not known.
6. *Wrap*: for each segment (the text between hard breaks) call `LineWrapper.Wrap` with the first-line or
   continuation width and `config.Wrap`, which dispatches to the fill, sentence, or semantic layout. A segment that
   ends in a double-space hard break gets exactly two spaces on its last line; a backslash break is part of its last
   atom. The lines are joined with a line feed and the continuation prefix.
7. When the replacement equals the original text, return without an edit (residual overflow is that of the wrap).
8. In the layout modes `FindFirstDifference` gives the `WrapOffset`. When the first wrapped line would start a block by
   itself (`WrapGuard.OpensBlockByItself`), return no edit and the findings. Otherwise return one `TextEdit` from the
   start of the paragraph to its end, under `paragraph-wrap` when there is a `WrapOffset` and `line-length` otherwise.

**Why it is a fixpoint.** The layout is a pure function of the atoms and the two widths, never of how the words were
laid out. Blanks and line breaks are the only atom separators, so the wrapped text tokenizes to the same atoms and the
wrap of its own output is identical: step 7 proposes nothing. In `fill` mode a paragraph that was wrapped has no
breakable line wider than its width, so it does not trigger again. When the guard leaves a breakable line over width,
the same layout is computed again, equals the text, and the overflow is reported again as a finding that rewriting
cannot fix. Text that fits is never a trigger in `fill` mode.

**Container prefixes.** The edit starts at the first character of the paragraph, so the text before it on the first
line (list marker, quote markers) stays. Every other line is written with the continuation prefix
(`DocumentParser.GetContinuationPrefix`): the text before the paragraph on its first line with each character other
than `>`, a space, or a tab replaced by a space. List items therefore keep the text indented under it, and a lazy
quote line gets its quote markers. The first-line width is the line length minus `TextWidth` of the text before the
paragraph and the continuation width is the line length minus that of the continuation prefix, each at least 1; a line
length of 0 gives `Unlimited`. The whole paragraph is refilled, not only the line that overflows.

##### Error Handling

A null argument throws `ArgumentNullException`. Every case that makes a paragraph unsafe to rewrite (refused
tokenization, lazy line in a list item, block syntax after a hard break, a wrap guard rule, a first line that opens a
block) is a result: the paragraph stays as written. Nothing is proved at run time; the safeguards are the guards
above. The tokenizer and the wrap guard turn a regular expression timeout into a refusal themselves. Nothing is
caught here.

##### Dependencies

- **FormatContext**, **LineLayout**, and **LineOwnership** (`DocumentFormatter` unit) - the document, settings, line
  arithmetic, and the footnote and protected lines.
- **TextEdit** (`EditSet` unit) - the edit.
- **AtomTokenizer**, **TokenizedParagraph**, **ParagraphSegment**, **HardBreakKind**, **LineWrapper**,
  **WrappedLines**, **WrapGuard**, and **BlockSyntaxGuard** (`LineWrapper` and `AtomTokenizer` units, `Inline`
  subsystem) - the atoms, the layouts, and the guards.
- **ParsedDocument**, **BlockInfo**, **LeafLine**, **DocumentParser**, and **SuppressionMap** (`Document` subsystem) -
  the paragraph, its lines, the continuation prefix, and `IsSuppressedRange`.
- **TextWidth** (`Document` subsystem) - display widths.
- **LintMarkConfig**, **WrapMode**, and the `BreakLinkText` link option (`LintMarkConfig` unit) - the settings.
- **RuleCatalog** (`Linting` subsystem) - the rule identifiers.
- **Markdig** (OTS) - `ParagraphBlock`, `ListItemBlock`, and the inline tree.

##### Callers

- **DocumentFormatter** - in its last stage (the wrap stage) reads `IsEnabled` and `IsLayoutEnforced` to decide
  whether the stage runs, then calls `Format` for every paragraph of the text the other rules produce and turns each
  `ParagraphOutcome` into edits and `line-length` or `paragraph-wrap` findings.
- **LintMarkEngine** - creates the instance with which the production `DocumentFormatter` is configured.
