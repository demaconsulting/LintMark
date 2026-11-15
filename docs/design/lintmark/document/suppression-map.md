### SuppressionMap

![Document Structure](DocumentView.svg)

#### Purpose

`SuppressionMap` turns suppression directives written as HTML comments into offset ranges and answers "is this rule
suppressed here?" queries. It is built per file by `DocumentParser` and is the single source of truth for suppression
for both lint (finding-level) and formatting (block-level).

#### Data Model

- **Ranges** (`IReadOnlyList<SuppressionRange>`, immutable array): sorted by start, then end. Ranges may overlap when
  their rule sets differ.
- **SuppressionRange** (record): `Start` inclusive, `End` exclusive offsets into the LF-normalized text (`int.MaxValue`
  when the range runs to the end of the file), and `Rules` (`RuleSet`).
- **RuleSet**: `Kind` is `All`, `Only` (listed rules) or `AllExcept` (everything except the listed rules, which arises
  when a region disabled everything and then re-enabled some). Rule ids compare ordinal, ignoring case, in a frozen set.
- A private rule name resolver (`Func<string, string>`) applied to every id before comparison.

#### Key Methods

- `Build(source, blocks, ast, canonicalize)`: collects comments, then applies them in source order.
  - Comment sources: every comment of an HTML block of type comment (several per block are possible), and every inline
    HTML node that is a complete comment. The text is never scanned, so comments in code spans, code blocks and front
    matter are not directives.
  - Directive forms (verbs are case-sensitive): `lintmark-` or `markdownlint-` followed by `disable`, `enable`,
    `disable-line`, `disable-next-line`, `disable-file`, `capture` or `restore`, with an optional space-separated rule
    list (an empty list means all rules); and, for block-level comments only, `prettier-ignore`,
    `prettier-ignore-start` and `prettier-ignore-end` (always all rules).
  - Rule names go through the resolver. `DocumentParser` supplies `RuleCatalog.TryResolve`, so a canonical id or an
    alias such as `MD009` (any letter case) yields the canonical id; an unregistered name is kept unchanged and so
    suppresses nothing that is registered.
- Range semantics:
  - `disable` starts a region at the start of its comment; the matching `enable` ends it at the start of the `enable`
    comment, and an unterminated region runs to the end of the file. A state tracker holds the all-disabled flag and
    the rule exceptions, so `disable` then `enable X` yields `AllExcept {X}`. Each state change emits the range of
    the state that ends.
  - `capture` pushes a snapshot of the state and `restore` pops it (ignored without a snapshot).
  - `disable-file` covers `[0, end of file)`.
  - `disable-line` covers the whole lines the comment occupies; `disable-next-line` covers the line after the comment's
    last line (nothing when the comment is on the last line).
  - `prettier-ignore` covers the next block, meaning the first flattened block that starts after the comment, not the
    next line. `prettier-ignore-start` and `-end` bracket a region from the start comment through the end
    comment; a second start is redundant, an end without a start is ignored, and an unterminated start runs to
    the end of the file.
- `IsSuppressedRange(start, end, ruleId)`: true when any range that intersects the query
  (`range.Start < end` and `range.End > start`; an empty or inverted query counts as one character) selects the
  canonicalized rule. A linear scan over the sorted ranges stops at the first range that starts at or after the end.
- `IsSuppressed(offset, ruleId)`: the range query for the single character at `offset`.

Effect of granularity: lint (`LintRuleEngine`) asks about the offset of each finding, so only findings inside the range
disappear. The formatter asks about whole blocks (and spacing gaps or lines), so a block that intersects a suppressed
range for any of its formatter's rules is left verbatim, even if the range covers only one of its lines.

#### Error Handling

A comment that matches no directive is ignored, and unknown rule names suppress nothing, so no input is rejected. Spans
outside the text are skipped. The regular expressions carry a one-second timeout; a timeout would propagate as an
exception to the engine. `ArgumentNullException` is thrown for a null rule id. The map is immutable and safe for
concurrent reads.

#### Dependencies

`SourceDocument` (line table for the line directives), the `BlockInfo` list and Markdig syntax tree (comment discovery,
next block for `prettier-ignore`), and the rule name resolver supplied by `DocumentParser` (backed by the `Linting`
subsystem's `RuleCatalog`).

#### Callers

`DocumentParser` (builds it), `LintRuleEngine` (`IsSuppressed` per finding), `LintMarkEngine` (the `grid-table-invalid`
finding), `DocumentFormatter`, `SpacingPasses`, `ParagraphFormatter`, `InlineNormalizer` and `LinkRefactorer`
(`IsSuppressedRange`, and `Ranges` in `SpacingPasses`).
