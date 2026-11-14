### RuleCatalog

![Linting Structure](LintingView.svg)

#### Purpose

`RuleCatalog` is the single registry of every rule identifier the tool knows: the kebab-case identifiers, their
legacy rule-number aliases (such as `MD013`), the kind and default state of each rule, and the aliases of rules that
no longer exist. It is the one place where a rule identifier is defined, so a configuration can be validated against
it, a diagnostic can be traced to a registered rule, and the default state of every configurable rule has one home.
Its supporting type `RuleInfo` is described in this document.

#### Data Model

`RuleCatalog` is a static class; all of its data is immutable and initialized once.

**RuleInfo**: A sealed record `(string Id, DiagnosticKind Kind, string Description, ImmutableArray<string> Aliases,
bool DefaultEnabled, bool IsConfigurable)`. `Id` is the canonical kebab-case identifier; `Kind` is `Formatting` for a
rule that rewriting fixes and `LintOnly` otherwise; `Aliases` lists the legacy aliases (empty when there are none);
`IsConfigurable` says whether the `rules` setting of a configuration can switch the rule on or off.

**Configurable rules**: 22 rules, the ones the `rules` setting accepts. Their ids, aliases, and default states were
moved unchanged from the table that the configuration unit held before the registry existed.

| Rule id | Kind | Alias | Default |
| --- | --- | --- | --- |
| `line-length` | formatting | `MD013` | on |
| `paragraph-wrap` | formatting | none | on |
| `heading-increment` | lint only | `MD001` | on |
| `heading-duplicate` | lint only | `MD024` | on |
| `heading-trailing-punctuation` | lint only | `MD026` | on |
| `emphasis-as-heading` | lint only | `MD036` | on |
| `image-alt-text` | lint only | `MD045` | on |
| `link-empty` | lint only | `MD042` | on |
| `link-fragment` | lint only | `MD051` | on |
| `link-reference-undefined` | lint only | `MD052` | on |
| `link-definition-unused` | lint only | `MD053` | on |
| `link-reversed` | lint only | `MD011` | on |
| `descriptive-link-text` | lint only | `MD059` | off |
| `code-fence-language` | lint only | `MD040` | on |
| `table-too-wide` | lint only | none | on |
| `table-column-count` | lint only | `MD056` | on |
| `table-grid-unformatted` | lint only | none | off |
| `fence-unclosed` | lint only | none | on |
| `grid-table-invalid` | lint only | none | on |
| `footnote-unused` | lint only | none | on |
| `blockquote-blank-line` | lint only | `MD028` | on |
| `dollar-sign-in-shell-code` | lint only | `MD014` | off |

**Dropped aliases**: Nine aliases of rules that no longer exist: `MD002`, `MD006`, `MD008`, `MD025`, `MD033`,
`MD041`, `MD043`, `MD044`, and `MD057`. A dropped alias is recognized (so a legacy configuration file is still
accepted) but never resolves to a rule and is never stored.

**Always-applied formatting rules**: Rules of kind formatting that the formatter applies to every document: the 22
rules below and `line-ending` (listed with the other identifiers that the tool emits). They are enabled, not
configurable, and may be suppressed for a passage with a comment, except `line-ending`, which has no partial form. The 22
rules carry 24 legacy rule-number aliases (`heading-style` and `list-indent` have two each). The aliases resolve through
`TryResolve`, so a suppression comment may use them, but a configuration that names the identifier or the alias in
`rules` is still rejected as an unknown rule, because `TryResolveConfigurable` does not accept them. The description of
each rule is the message of the diagnostic that the formatter reports for it.

| Rule id | Alias | Description |
| --- | --- | --- |
| `trailing-whitespace` | `MD009` | A line ends with trailing whitespace, or a hard break is not exactly two spaces |
| `hard-tabs` | `MD010` | A tab character is used in text instead of spaces |
| `max-blank-lines` | `MD012` | More blank lines in a row than the configuration allows |
| `heading-style` | `MD003`, `MD021` | A heading is not written in the configured style (setext or closed headings) |
| `heading-space` | `MD019` | A heading has more or less than one space after its number signs |
| `heading-start-left` | `MD023` | A heading is indented |
| `heading-blank-lines` | `MD022` | A heading is not surrounded by blank lines |
| `fence-blank-lines` | `MD031` | A fenced code block is not surrounded by blank lines |
| `list-blank-lines` | `MD032` | A list is not surrounded by blank lines |
| `table-blank-lines` | `MD058` | A table is not surrounded by blank lines |
| `thematic-break` | `MD035` | A thematic break is not written in the configured style |
| `code-block-style` | `MD046` | A code block is not written in the configured style |
| `code-fence` | `MD048` | A code fence does not use the configured fence character |
| `final-newline` | `MD047` | The file does not end with the configured final line ending |
| `bullet` | `MD004` | A bullet list item does not use the configured marker character |
| `ordered-style` | `MD029` | An ordered list is not numbered in the configured style |
| `list-indent` | `MD005`, `MD007` | List items are misaligned, or a nested bullet list is not indented as configured |
| `list-marker-space` | `MD030` | A list marker is followed by more than one space |
| `blockquote-space` | `MD027` | A block quote marker is followed by more than one space |
| `emphasis` | `MD049` | Emphasis is not written with the configured delimiter |
| `strong` | `MD050` | Strong emphasis is not written with the configured delimiter |
| `bare-urls` | `MD034` | A bare URL is not wrapped in angle brackets |

The description of `list-indent` is shortened in this table to fit the page; the registry holds "The items of a list are
not aligned or a nested bullet list is not indented as configured".

The aliases `MD018` and `MD020` are not registered: the text they describe (`#Title`, `##Title##`) is a paragraph to
the parser, and no safe rewrite exists. The aliases `MD037`, `MD038`, and `MD039` (spaces inside emphasis markers, code
spans, and link text) are not registered either: the rewrite is not safe, because the text renders differently without
the space, so no fixer exists; they are planned as lint-only rules.

**Other non-configurable identifiers**: Registered so that the registry is complete, enabled, without aliases, and not
configurable. A configuration that names one of them is rejected as an unknown rule. Together with the 22 always-applied
rules above they make 33 non-configurable entries.

| Rule id | Kind | Reported when |
| --- | --- | --- |
| `line-ending` | formatting | A line ending differs from the line ending chosen for the file (always applied) |
| `safety-html-mismatch` | lint only | A rewrite would change how the document renders |
| `safety-structure-mismatch` | lint only | A rewrite would change Pandoc structure |
| `safety-not-idempotent` | lint only | Formatting the rewritten text would change it again |
| `file-read-error` | lint only | A file cannot be read |
| `file-encoding-error` | lint only | A file is not valid UTF-8 or has an unsupported byte order mark |
| `file-line-ending-error` | lint only | A file contains a bare carriage return |
| `file-parse-error` | lint only | The Markdown parser failed on a file |
| `file-format-error` | lint only | Formatting a file failed |
| `file-write-error` | lint only | A rewritten file could not be written |
| `config-error` | lint only | A configuration source has a problem |

The description of each of the three safety identifiers in the registry ends with "so the file is not rewritten".

The identifiers that other units emit are exposed as constants (`LineEndingRuleId`, the identifiers of the 22
always-applied rules such as `TrailingWhitespaceRuleId`, `FinalNewlineRuleId`, `BulletRuleId`, `ListIndentRuleId`, and
`BareUrlsRuleId`, `HtmlMismatchRuleId`,
`StructureMismatchRuleId`, `NotIdempotentRuleId`, `GridTableInvalidRuleId`, `FileReadErrorRuleId`,
`FileEncodingErrorRuleId`, `FileLineEndingErrorRuleId`, `FileParseErrorRuleId`, `FileFormatErrorRuleId`,
`FileWriteErrorRuleId`, and `ConfigErrorRuleId`).

**Lookups and views**:

- `Resolution` (private): a frozen dictionary, keyed ignoring case, from every identifier and every alias of every
  registered rule to the canonical identifier.
- `ById` (private): a frozen dictionary, keyed ignoring case, from every registered identifier to its `RuleInfo`.
- `Rules`: every registered `RuleInfo` in ordinal order of the identifier.
- `ConfigurableRules`: the canonical identifiers of the configurable rules in ordinal order.
- `Defaults`: an immutable sorted dictionary (ordinal comparer) of the default state of every configurable rule.
- `Aliases`: an immutable sorted dictionary (comparer ignoring case) from each legacy alias of a configurable rule to
  its canonical identifier. The aliases of the always-applied rules are not in it; they resolve through `TryResolve`.
- `DroppedAliases`: the nine dropped aliases in ordinal order.

**Invariants**: identifiers are unique and every table is built once from one list, so the views cannot disagree.
Names are compared with an ordinal comparison that ignores case, independent of the current culture, so the result
never depends on the machine's locale. Every collection is immutable, so a caller cannot change it even by casting.

#### Key Methods

**TryResolve**: Resolves a rule identifier or alias of any registered rule to the canonical identifier.

- *Parameters*: `string name` (any letter case), and an out parameter that receives the canonical identifier.
- *Returns*: `true` for a registered rule, configurable or not; `false` for an unknown name and for a dropped alias.
- *Preconditions*: `name` is not null.

**TryResolveConfigurable**: Resolves a name to the canonical identifier of a rule that the `rules` setting accepts.

- *Parameters*: as `TryResolve`.
- *Returns*: `true` only for a configurable rule. A name that resolves to a rule that is registered but not
  configurable (for example `line-ending`) returns `false`, so naming it in a configuration remains an error.
- *Preconditions*: `name` is not null.

This is the method that `ConfigParser` calls to validate the rule names of a configuration file. It keeps the
behavior that existed before the registry: the always-applied rules (with their aliases, for example `MD009`) and the
failure identifiers are unknown there.

**TryGet**: Looks up a registered rule by its canonical identifier (an alias is not accepted).

- *Parameters*: `string id` (any letter case), and an out parameter that receives the `RuleInfo`.
- *Returns*: `true` when the identifier is registered.
- *Preconditions*: `id` is not null.

**IsDroppedAlias**: Determines whether a name is one of the nine dropped aliases, ignoring case.

- *Parameters*: `string name`.
- *Returns*: `bool`.
- *Preconditions*: `name` is not null.

The private factory methods `Configurable`, `Applied`, and `Fixed` create the entries of the three groups
(configurable rules, always-applied formatting rules with aliases, and the other non-configurable identifiers), so the
group of an entry is stated once where it is declared.

#### Error Handling

`ArgumentNullException` for a null name or identifier in every lookup; there are no other error cases. An unknown name
is an ordinary result (`false`), not an error. The data is built when the type is first used from literal tables, so
there is no failure at run time other than a programming error that a test would catch (for example a duplicate
identifier).

#### Dependencies

- **DiagnosticKind** (this subsystem) — the kind recorded for each rule.
- **.NET BCL** — frozen and immutable collections and string comparers. No other tool units are used.

#### Callers

- **LintMarkConfig** — takes the default state of every rule (`Defaults`) and the set of configurable identifiers
  that a `rules` map must hold exactly.
- **ConfigParser** (the `ConfigLoader` unit) — validates the names in the `rules` setting with `TryResolveConfigurable`
  and `IsDroppedAlias`.
- **DocumentFormatter**, the block formatters of the `Blocks` subsystem, the **InlineNormalizer** of the `Inline`
  subsystem, and **LintMarkEngine** — use the identifier
  constants for the diagnostics and edits they create; the document formatter also reads the description of a rule to
  report an edit.
- **DocumentParser** — resolves the rule names of suppression comments with `TryResolve`, so legacy numbers suppress the
  rule they stand for.
