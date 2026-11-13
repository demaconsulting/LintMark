### LintMarkConfig

![Configuration Structure](ConfigurationView.svg)

#### Purpose

`LintMarkConfig` is the complete, validated, deeply immutable set of settings that governs how one Markdown
file is linted and formatted. It owns the definition of every setting, its default, and the rule that an
invalid configuration cannot exist. Its supporting types are described in this document: `LinkRefactorOptions`
(the nested `link-refactor` options), and twelve setting
enumerations. They have no design documents of their own. The table of rule identifiers that the `rules` setting is
validated against is owned by `RuleCatalog` of the `Linting` subsystem (see *RuleCatalog Design*) and consumed here.

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
always complete: it holds exactly the 22 ids of `RuleCatalog.ConfigurableRules` (with an ordinal comparer), so a
consumer never needs a second lookup of the defaults. The defaults of the map are `RuleCatalog.Defaults`. The table of
the 22 rules with their aliases and default states, the nine dropped aliases, and the identifiers that are registered
but not configurable are described in *RuleCatalog Design*; this unit does not repeat them. Rules that the formatter
always applies (such as `line-ending`) are not configurable, and their aliases (for example `MD009`) are unknown, not
dropped.

**Ignore**: `Ignore` holds glob patterns of files to skip. The default is `.git/**`, `node_modules/**`,
`**/.venv/**`, `**/generated/**`, `**/thirdparty/**`, and `.agent-logs/**`. A configured `ignore` list replaces
the default list; it is never merged with it. Patterns use forward slashes only; a pattern that contains a
backslash is rejected by the property (the configuration file loader converts backslashes before storing).
The patterns are relative to the directory of the configuration file that applies to a file (the current directory
when none does). The engine applies them (see *LintMarkEngine Design* and *FileDiscovery Design*): it skips the files
that a directory, a glob, or the default pattern selected, and never a file named on the command line.

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

**Use of the invariants**: The configuration holds and refuses values as described above: the documented defaults,
every value within its range, refused values, and collections that holders and source collections cannot change. The
refusal is raised as an exception from the initializer, which is a design decision described above. Deriving a copy
that differs in chosen settings (a `with` expression) is how the engine fills in the line ending that the editor
settings give, without changing a shared instance; equality by content lets two configurations be compared by value.
The `Ignore` setting is consumed by the engine and the `EndOfLine` setting by `DocumentFormatter`.

#### Key Methods

**Default** (static property): The configuration with every documented default.

- *Returns*: `LintMarkConfig`; the same shared instance for every caller.
- *Postconditions*: `EndOfLine` is `null`; `Rules` equals `RuleCatalog.Defaults`; `Ignore` equals the
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
- `ArgumentNullException`: a null `LinkRefactor`, `Rules`, or `DefinitionsMarker`.
- `ArgumentException`: an uninitialized (default) `Ignore` array; an `Ignore` element that is null, empty,
  blank, spans several lines, or contains a backslash; a `Rules` map that is missing a configurable id or holds
  a key that is not one; and a `DefinitionsMarker` that is not a single-line HTML comment.

User-supplied configuration text never reaches these accessors unchecked: `ConfigLoader` validates the text
first and reports problems as values, so these exceptions signal programming errors, not bad configuration
files.

#### Dependencies

- **LineEnding** (`Document` subsystem) — the type of `EndOfLine`.
- **RuleCatalog** (`Linting` subsystem) — the default state of every rule and the set of identifiers that a `rules`
  map must hold exactly. This is the unit-level dependency behind the subsystem-level cycle described in
  *Linting Design*.
- **.NET BCL** — immutable and frozen collections and the standard exception types.

#### Callers

- **ConfigLoader** — builds configurations from files with `with` expressions on `LintMarkConfig.Default`, and
  uses `LinkRefactorOptions.IsValidMarker` while validating.
- **LintMarkEngine** — carries the configuration of each file in `FileResult`, derives the effective
  configuration with a `with` expression, and applies the `Ignore` patterns.
- **DocumentFormatter** — reads `EndOfLine` and passes the configuration to block formatters.
