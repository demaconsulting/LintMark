### LintRules

![Linting Structure](LintingView.svg)

#### Purpose

`LintRules` finds the problems of a document that rewriting cannot fix. It holds the rule engine `LintRuleEngine`,
the `ILintRule` contract, the shared helpers in `RuleSupport`, and four stateless rule families: `HeadingRules`,
`LinkRules`, `StructureRules` and `TableRules`. Every rule only reports; none proposes an edit, because each fix would
change what the document says.

#### Data Model

**ILintRule**: `RuleIds` (the ids whose findings the family reports) and `Check(document, config)`, which returns
lint-only `Diagnostic` values in any order. A family ignores switches and suppressions; the engine applies both.

**LintRuleEngine**: an immutable list of `ILintRule`, copied at construction. `CreateDefault` registers the four
families in the order heading, link, structure, table. All families are stateless, so one engine serves every worker.

**RuleSupport**: `Finding` (a lint-only finding at an offset, converted to line and column and clamped to the text),
`PlainText` (text, code, entity and link text of an inline tree; optionally without images) and `CollapseWhitespace`.

#### Key Methods

**LintRuleEngine.Run(document, config)**: for each family, skip it when none of its `RuleIds` is on in
`config.Rules` (an id absent from the map, such as a failure id, counts as on). Otherwise run `Check`, and keep each
finding whose own id is on and not suppressed: the engine converts the finding's line and column to an offset
and asks the document's `SuppressionMap` `IsSuppressed(offset, ruleId)`. The map was built from the suppression
comments and resolved names through `RuleCatalog`, so a comment that names `MD013` also covers `line-length`. The
result is sorted by line, column, rule id (ordinal) and message (ordinal), so it never depends on family order.

**HeadingRules** (document order; setext, quoted and listed headings included):

- `heading-increment`: a heading more than one level deeper than the previous heading; the first is never reported.
- `heading-duplicate`: a heading whose text (whitespace collapsed, case kept) matches an earlier one; reported at the
  later one. Empty headings are skipped.
- `heading-trailing-punctuation`: heading text ends with `.,;:!` or a full-width form; `?` is allowed.
- `emphasis-as-heading`: a top-level, single-line paragraph that is only plain emphasis and has no final punctuation.

**LinkRules** (syntax tree plus plain text of paragraphs, headings and table cells):

- `image-alt-text`: an image with blank alternative text, and an HTML `img` without `alt`.
- `link-empty`: destination empty or `#`. `link-fragment`: a `#fragment` matched by no heading slug (GitHub style, with
  `-1` suffixes for repeats), `{#id}`, or HTML `id` and `name`; `top` and `L10` line anchors pass, case is ignored.
- `link-reference-undefined`: `[text][label]` or `[label][]` with no definition; shortcut and footnote forms are not.
- `link-definition-unused` (lint only, never deleted by `--fix`): a definition no link uses, or a label defined twice;
  the `[//]: #` comment convention is ignored.
- `link-reversed`: `(text)[url]`. `link-space`: link text starting or ending with a space or tab (not images,
  shortcuts or wrapped breaks); the message shows the corrected text.
- `descriptive-link-text` (off by default): text that is only `click here`, `here`, `link` or `more`.
- `footnote-unused`: a footnote definition no reference uses.

**StructureRules**:

- `code-fence-language`: a fenced code block without an info string.
- `fence-unclosed`: a code or math fence never closed.
- `blockquote-blank-line`: a blank gap between two quotes of one container, found recursively.
- `dollar-sign-in-shell-code` (off by default): every non-blank line of a code block starts with `$` and a blank.
- `heading-missing-space` and `heading-closed-missing-space`: a paragraph starting at column 1 with one to six `#`
  directly followed by a letter; the closed form also ends with `#` after a non-blank character.
- `emphasis-space`: `*text *` or `* text*` (also `_`, `**`, `__`), single line, on delimiters that stayed plain text.
  Pairs of equal runs are tested; the flanking and letter conditions keep arithmetic and wildcards out.

**TableRules** (pipe tables parsed with the formatter's `PipeTable`):

- `table-column-count`: a body row whose cell count differs from the header's.
- `table-too-wide`: the first row whose compact layout, with its container prefix, is wider in
  terminal columns than the line length; skipped when the length is 0. Aligned layout is never the measure.
- `table-grid-unformatted` (off by default): reports every grid table.

Rules reported by other units: `line-length` and `paragraph-wrap` come from the paragraph formatter. A wrap edit is a
formatting finding; a line that can be broken but stays too wide, or a paragraph that cannot be laid out per the
wrap mode, is a lint-only finding, and an unbreakable line is not reported. `code-span-space` and the other
always-applied rules are formatting findings created from the edits of their formatters. `grid-table-invalid` is
created by the `Engine` when the parser had to read the file without its grid tables.

#### Error Handling

Rule families do not catch exceptions. Null arguments throw `ArgumentNullException`. Damaged block spans are skipped
by the structure and link rules. Regular expressions have a 1-second match timeout. Any exception from `Run` is
caught by the `Engine` and reported as a `file-format-error` for that file only.

#### Dependencies

`ParsedDocument` with its `SourceDocument` (offsets, positions), blocks, syntax tree, `ReferenceIndex` and
`SuppressionMap`; `LintMarkConfig` (`Rules`, `LineLength`); `TextWidth`; the `Formatting` helpers `InlineMap`,
`LineLayout`, `PipeTable` and `TableFormatter.PrefixWidth`; `RuleCatalog` and `Diagnostic`; and the Markdig syntax
tree (see the OTS design "Markdig").

#### Callers

The `Engine` calls `LintRuleEngine.CreateDefault` once and `Run` for each parsed document: on the original text,
on the rewritten text under `--fix` (to report what remains), and on the reading without grid tables when the parser
fell back.
