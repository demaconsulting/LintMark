### SuppressionMap

![Document Structure](DocumentView.svg)

#### Purpose

`SuppressionMap` resolves the suppression directives written as HTML comments in a document into offset
ranges and answers the question "is this rule suppressed here?". Its single responsibility is the
interpretation of directive comments; deciding which rules exist belongs to later phases.

#### Data Model

**Ranges**: `IReadOnlyList<SuppressionRange>` — The resolved ranges sorted by start offset. Ranges may
overlap when they carry different rule sets.

**SuppressionRange**: A record `(int Start, int End, RuleSet Rules)`. Offsets refer to the LF-normalized text
and the range is half-open, `[Start, End)`. An unterminated range ends at `int.MaxValue`.

**RuleSet**: Describes which rules a range suppresses, with `RuleSetKind` `All` (every rule), `Only` (the
listed rules), or `AllExcept` (every rule but the listed ones). `Contains(ruleId)` compares rule
identifiers ordinal-ignore-case.

**Alias resolver**: An optional `Func<string, string>` applied to every rule identifier, both in directives
and in queries, before comparison. It is the identity in this phase; a later phase supplies rule alias
resolution through it so that this unit does not change.

Directive families (their different scoping semantics are kept distinct on purpose):

- **`lintmark-disable` / `markdownlint-disable` `[rules]`**: region start, from the start of the comment.
- **`lintmark-enable` / `markdownlint-enable` `[rules]`**: region end for the listed rules (or all).
- **`...-disable-line [rules]`**: the line or lines the comment occupies.
- **`...-disable-next-line [rules]`**: the line after the comment's last line (no range when the comment is on
  the last line of the file).
- **`...-disable-file [rules]`**: the whole file.
- **`...-capture` / `...-restore`**: save the current region state and return to it (a stack; `restore`
  without `capture` does nothing).
- **`prettier-ignore`**: the next block (the first block starting after the comment), all rules.
- **`prettier-ignore-start` / `-end`**: everything between, all rules; unterminated runs to the end of the
  file.

A directive without rules applies to all rules. After a bare `disable`, `enable` with a rule list
re-enables only those rules, so the region continues as `AllExcept` those rules. Only block-level
comments carry prettier directives, and inline comments carry only the lintmark and markdownlint families.

#### Key Methods

**Build**: Builds the map of a parsed document.

- *Parameters*: `SourceDocument source`, `IReadOnlyList<BlockInfo> blocks`, `MarkdownDocument ast`,
  optional `Func<string, string>? canonicalizeRuleId`.
- *Returns*: `SuppressionMap`.
- *Preconditions*: the required arguments are not null.
- *Postconditions*: `Ranges` is sorted by start; nothing is left open.

`Build` takes the `SourceDocument` for position arithmetic. Directives are found by walking the parsed tree,
never by scanning the text: block-level comments are HTML blocks of the comment type (a block may hold
several comments) and inline comments are inline HTML whose text is a complete comment. Comments inside
code spans, code blocks, and front matter are therefore not directives. The comments are processed in
source order by a region tracker that holds the current state (all disabled, or a set of rules), emits a
range each time the state changes, and closes every open region at the end of the file.

**IsSuppressed**: Whether a rule is suppressed at an offset.

- *Parameters*: `int offset`, `string ruleId`.
- *Returns*: `bool`.

**IsSuppressedRange**: Whether a rule is suppressed anywhere inside a range (used by block-granularity
consumers to route a block that intersects a suppressed region to verbatim handling).

- *Parameters*: `int start`, `int end`, `string ruleId` — an `end` not greater than `start` means one character.
- *Returns*: `bool`.

Both resolve the rule through the alias function and scan the sorted ranges until one starts at or after the
end of the query.

#### Error Handling

`ArgumentNullException` for null arguments. Unknown directives and malformed comments are ignored rather
than reported. All regular expressions have a match timeout. The map is immutable after `Build`.

#### Dependencies

- **SourceDocument** — line table for the line-scoped directives.
- **Markdig** — syntax-tree types (HTML blocks and inline HTML) used to locate comments.
- `BlockInfo` and `BlockKind` — plain value types that the `DocumentParser` unit declares and produces.
  They are inputs only, so there is no call back into the parser and no dependency cycle.

#### Callers

- **DocumentParser** — builds a map for every parsed document.
