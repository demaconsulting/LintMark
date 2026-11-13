### LintMarkConfig

![Configuration Structure](ConfigurationView.svg)

#### Purpose

`LintMarkConfig` is the complete, validated, deeply immutable set of settings that governs how one Markdown
file is linted and formatted. It owns the definition of every setting, its default, and the rule that an
invalid configuration cannot exist. Its supporting types are described in this document: `LinkRefactorOptions`
(the nested `link-refactor` options), `RuleIdentifiers` (the table of configurable rules), and twelve setting
enumerations. They have no design documents of their own.

#### Data Model

Every setting is an enumeration of deterministic canonical forms or a bounded number, so a configuration
that would leave a formatting violation unfixable cannot be expressed. Settings omitted from a configuration
file keep the defaults below.

**Settings and defaults**:

| Setting (YAML key) | Property | Type | Default |
| --- | --- | --- | --- |
| `line-length` | `LineLength` | `int`, 0 to 100000 (0 = unlimited) | 120 |
| `wrap` | `Wrap` | `WrapMode` | `fill` |
| `end-of-line` | `EndOfLine` | `LineEnding?` (`lf`, `crlf`) | not specified (`null`) |
| `final-newline` | `FinalNewline` | `bool` | `true` |
| `heading-style` | `Headings` | `HeadingStyle` | `atx` |
| `bullet` | `Bullet` | `BulletMarker` | `dash` |
| `list-indent` | `ListIndent` | `int`, 2 or 4 | 2 |
| `ordered-style` | `OrderedStyle` | `OrderedStyle` | `incrementing` |
| `emphasis` | `Emphasis` | `EmphasisMarker` | `asterisk` |
| `strong` | `Strong` | `EmphasisMarker` | `asterisk` |
| `code-fence` | `CodeFence` | `FenceMarker` | `backtick` |
| `code-block-style` | `CodeBlockStyle` | `CodeBlockStyle` | `fenced` |
| `hard-break` | `HardBreak` | `HardBreakStyle` | `spaces` |
| `thematic-break` | `ThematicBreak` | `ThematicBreakStyle` | `dashes` |
| `tables` | `Tables` | `TableStyle` | `compact` |
| `bare-urls` | `BareUrls` | `BareUrlStyle` | `autolink` |
| `max-blank-lines` | `MaxBlankLines` | `int`, 1 to 100000 | 1 |
| `trailing-whitespace` | `TrailingWhitespace` | `TrailingWhitespaceStyle` | `strip` |
| `link-refactor` | `LinkRefactor` | `LinkRefactorOptions` | `LinkRefactorOptions.Disabled` |
| `rules` | `Rules` | `ImmutableSortedDictionary<string, bool>` | the default of every rule (see below) |
| `ignore` | `Ignore` | `ImmutableArray<string>` | the six default patterns (see below) |

`EndOfLine` is the only setting without a built-in value: `null` means the configuration does not specify a
line ending. The editor settings and then the file's own majority line ending decide later (see the
*Configuration* subsystem design). It reuses the `LineEnding` type of the `Document` subsystem, which models
only LF and CRLF.

**LinkRefactorOptions**: A sealed record for the opt-in conversion of inline links to reference links.

| Option (YAML sub-key) | Property | Type | Default (`Disabled`) |
| --- | --- | --- | --- |
| `enabled` | `Enabled` | `bool` | `false` |
| `threshold` | `Threshold` | `int`, 1 to 100000 | 60 |
| `in-tables` | `InTables` | `bool` | `true` |
| `break-link-text` | `BreakLinkText` | `bool` | `false` |
| `label-max-length` | `LabelMaxLength` | `int`, 1 to 100000 | 40 |
| `definitions-marker` | `DefinitionsMarker` | `string`, a single-line HTML comment | `<!-- Link References -->` |

A marker is valid when it is exactly one HTML comment on one line: it starts with `<!--`, the text right after that
does not start with `>` or `->` (an HTML parser ends the comment there), the first `-->` after the opening is the
final three characters (nothing follows the comment), it is at least seven characters long (an empty comment), and
it contains no line break. A line break is CR, LF, NEL (U+0085), line separator (U+2028), or paragraph separator
(U+2029); the same set is used for the single-line check of ignore patterns and is defined once, as
`LintMarkConfig.LineBreakCharacters`.
`LinkRefactorOptions.IsValidMarker` is shared with `ConfigLoader` so that a file is rejected with a message
exactly when the property would have thrown.

**Setting enumerations**: Twelve `internal enum` types, one per file. The YAML token of a member is its name in
lower case; tokens are case-sensitive in a configuration file (see *ConfigLoader Design*). `end-of-line` uses
the `LineEnding` enumeration of the `Document` subsystem (tokens `lf` and `crlf`) and is not listed here.

| Enumeration | Tokens in declaration order | Used by |
| --- | --- | --- |
| `WrapMode` | `fill`, `sentence`, `semantic` | `wrap` |
| `HeadingStyle` | `atx` | `heading-style` |
| `BulletMarker` | `dash`, `asterisk`, `plus` | `bullet` |
| `OrderedStyle` | `incrementing`, `one` | `ordered-style` |
| `EmphasisMarker` | `asterisk`, `underscore` | `emphasis`, `strong` |
| `FenceMarker` | `backtick`, `tilde` | `code-fence` |
| `CodeBlockStyle` | `fenced` | `code-block-style` |
| `HardBreakStyle` | `spaces`, `backslash` | `hard-break` |
| `ThematicBreakStyle` | `dashes`, `asterisks`, `underscores` | `thematic-break` |
| `TableStyle` | `compact`, `aligned` | `tables` |
| `BareUrlStyle` | `autolink`, `leave` | `bare-urls` |
| `TrailingWhitespaceStyle` | `strip` | `trailing-whitespace` |

Single-member enumerations (`HeadingStyle`, `CodeBlockStyle`, `TrailingWhitespaceStyle`) exist so that the
setting can be written explicitly today and gain further forms without changing the schema.

**Rules**: `Rules` maps the canonical kebab-case id of every configurable rule to its enabled state. The map is
always complete: it holds exactly the 22 ids below (with an ordinal comparer), so a consumer never needs a
second lookup of the defaults. `RuleIdentifiers` is the single home of this table.

| Rule id | Alias | Default |
| --- | --- | --- |
| `line-length` | `MD013` | on |
| `paragraph-wrap` | none | on |
| `heading-increment` | `MD001` | on |
| `heading-duplicate` | `MD024` | on |
| `heading-trailing-punctuation` | `MD026` | on |
| `emphasis-as-heading` | `MD036` | on |
| `image-alt-text` | `MD045` | on |
| `link-empty` | `MD042` | on |
| `link-fragment` | `MD051` | on |
| `link-reference-undefined` | `MD052` | on |
| `link-definition-unused` | `MD053` | on |
| `link-reversed` | `MD011` | on |
| `descriptive-link-text` | `MD059` | off |
| `code-fence-language` | `MD040` | on |
| `table-too-wide` | none | on |
| `table-column-count` | `MD056` | on |
| `table-grid-unformatted` | none | off |
| `fence-unclosed` | none | on |
| `grid-table-invalid` | none | on |
| `footnote-unused` | none | on |
| `blockquote-blank-line` | `MD028` | on |
| `dollar-sign-in-shell-code` | `MD014` | off |

Rule ids and aliases are resolved ignoring case, with an ordinal comparison that does not depend on the current
culture. The aliases of nine rules that no longer exist are dropped: `MD002`, `MD006`, `MD008`, `MD025`, `MD033`,
`MD041`, `MD043`, `MD044`, and `MD057`. A dropped alias is recognized (so a legacy configuration file is still
accepted) but never resolves to a rule and is never stored. Rules that the formatter always applies are not
configurable; their aliases (for example `MD009`) are unknown, not dropped.

`RuleIdentifiers` exposes `ConfigurableRules` (the ids in ordinal order), `Defaults`, `Aliases`,
`DroppedAliases`, `TryResolve`, and `IsDroppedAlias`. All of its data is immutable and initialized once, so
every member is safe for concurrent use.

Hand-over note: `RuleIdentifiers` is a supporting type of this unit only because the `rules` setting must be
validated before the linting subsystem exists. When the rule catalog is introduced it must absorb this table
(move it and add the rule metadata) rather than duplicate it, and the suppression map's rule-id canonical-form
function can then be bound to the same resolution.

**Ignore**: `Ignore` holds glob patterns of files to skip. The default is `.git/**`, `node_modules/**`,
`**/.venv/**`, `**/generated/**`, `**/thirdparty/**`, and `.agent-logs/**`. A configured `ignore` list replaces
the default list; it is never merged with it. Patterns use forward slashes only; a pattern that contains a
backslash is rejected by the property (the configuration file loader converts backslashes before storing).
The setting is validated and stored but is not yet applied: the engine does not consult it, so it has no effect
on which files are processed.

**Invariants**:

- Every `init` accessor validates its value and throws the documented exception, also when used by a `with`
  expression, so an invalid instance cannot be created.
- The record is deeply immutable: `Rules` is an immutable sorted dictionary, `Ignore` is an immutable array,
  and `LinkRefactor` is itself an immutable record. A `with` expression can therefore never share a mutable
  collection between two configurations.
- `Rules` is always stored with an ordinal key comparer (a value given with another comparer is re-keyed) and
  holds exactly the 22 configurable ids.
- Numeric settings are capped at `LintMarkConfig.MaxNumber` (100000), a guard against arithmetic overflow in
  later width calculations.
- An enumeration value must be a defined member; a value produced by an integer cast that no member names is
  rejected.
- Value equality covers every setting, and the collections are compared by content (`Rules` pair by pair in
  key order, `Ignore` element by element with an ordinal comparison, so order matters). `GetHashCode` combines
  the same content and is therefore consistent with `Equals`.

**Scope of the requirements in this phase**: The requirements state what the configuration holds and refuses:
the documented defaults, every value within its range, rule identification, refused values, and collections that
holders and source collections cannot change. They state the refusal as an observable outcome (the value cannot be
set); that it is raised as an exception from the initializer is a design decision described above. The rule
identification requirements name `RuleIdentifiers`, the supporting type that implements them. Deriving a copy
that differs in chosen settings, and equality by content, are needed only to fill in the line ending that the
editor settings give, which takes effect with the formatter. Their tests are kept as tests without a requirement,
and the formatting phase re-creates the requirement together with the system requirement that observes the line
ending. Defensive checks without a separate observable need, such as the refusal of an uninitialized ignore array,
are likewise verified by tests without a requirement.

#### Key Methods

**Default** (static property): The configuration with every documented default.

- *Returns*: `LintMarkConfig`; the same shared instance for every caller.
- *Postconditions*: `EndOfLine` is `null`; `Rules` equals `RuleIdentifiers.Defaults`; `Ignore` equals the
  default patterns.

**Property initializers** (`LineLength`, `Wrap`, `EndOfLine`, `Headings`, `Bullet`, `ListIndent`, `OrderedStyle`,
`Emphasis`, `Strong`, `CodeFence`, `CodeBlockStyle`, `HardBreak`, `ThematicBreak`, `Tables`, `BareUrls`,
`MaxBlankLines`, `TrailingWhitespace`, `LinkRefactor`, `Rules`, `Ignore`): Validate and store one setting.

- *Parameters*: the new value.
- *Preconditions*: the value satisfies the rules in the settings table; for `ListIndent` it is 2 or 4; for
  `Rules` it contains exactly the configurable ids; for `Ignore` every element is non-blank, single-line, and
  free of backslashes.
- *Postconditions*: the value is stored; nothing is stored when the initializer throws.

**Equals and GetHashCode**: Value equality as described under Invariants.

**RuleIdentifiers.TryResolve**: Resolves a rule id or legacy alias (such as `MD013`) to the canonical id.

- *Parameters*: `string name` (any letter case), and an out parameter that receives the canonical id.
- *Returns*: `true` for a configurable rule; `false` for an unknown name and for a dropped alias.
- *Preconditions*: `name` is not null.

**RuleIdentifiers.IsDroppedAlias**: Determines whether a name is one of the nine dropped aliases, ignoring case.

- *Parameters*: `string name`.
- *Returns*: `bool`.
- *Preconditions*: `name` is not null.

**LinkRefactorOptions.IsValidMarker**: Determines whether text is acceptable as a definitions marker.

- *Parameters*: `string value`.
- *Returns*: `bool`; `true` for exactly one single-line HTML comment (see the marker rule above).
- *Preconditions*: `value` is not null.

**LinkRefactorOptions constructor and initializers**: The constructor assigns every option through the validating
accessors, so a constructor call and a `with` expression share one set of rules.

#### Error Handling

All errors are argument errors raised at the point where an invalid value would enter the record; nothing is
logged and nothing is retried.

- `ArgumentOutOfRangeException`: a number outside its range (`LineLength`, `MaxBlankLines`, the numeric
  `LinkRefactorOptions` options), a `ListIndent` that is neither 2 nor 4, and an enumeration value that is not a
  defined member.
- `ArgumentNullException`: a null `LinkRefactor`, `Rules`, or `DefinitionsMarker`, and a null name given to
  `TryResolve` or `IsDroppedAlias`.
- `ArgumentException`: an uninitialized (default) `Ignore` array; an `Ignore` element that is null, empty,
  blank, spans several lines, or contains a backslash; a `Rules` map that is missing a configurable id or holds
  a key that is not one; and a `DefinitionsMarker` that is not a single-line HTML comment.

User-supplied configuration text never reaches these accessors unchecked: `ConfigLoader` validates the text
first and reports problems as values, so these exceptions signal programming errors, not bad configuration
files.

#### Dependencies

- **LineEnding** (`Document` subsystem) — the type of `EndOfLine`.
- **.NET BCL** — immutable and frozen collections and the standard exception types.

#### Callers

- **ConfigLoader** — builds configurations from files with `with` expressions on `LintMarkConfig.Default`, and
  uses `RuleIdentifiers` and `LinkRefactorOptions.IsValidMarker` while validating.
- **LintMarkEngine** — carries the configuration of each file in `FileResult` and derives the effective
  configuration with a `with` expression.
