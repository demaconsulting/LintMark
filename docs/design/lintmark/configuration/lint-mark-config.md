### LintMarkConfig

![Configuration Structure](ConfigurationView.svg)

#### Purpose

`LintMarkConfig` is the complete, immutable set of settings that governs how one Markdown file is linted and
formatted. Every option is an enumeration of deterministic canonical forms or a bounded number, so a configuration
that would leave a formatting violation unfixable cannot be expressed. Its supporting types `LinkRefactorOptions` and
the twelve setting enumerations are described in this document.

#### Data Model

`LintMarkConfig` is a sealed record; every property is `init`-only with a default, so a setting absent from a
configuration file keeps its default. Instances are immutable and thread-safe. The values are validated where they
come from (`ConfigParser` and the command line), not by the record.

| Property (YAML key) | Type | Default |
| --- | --- | --- |
| `LineLength` (`line-length`) | int, 0 = unlimited, 0 to 100000 | 120 |
| `Wrap` (`wrap`) | `WrapMode` | `Fill` |
| `EndOfLine` (`end-of-line`) | `LineEnding?` (`lf`, `crlf`) | null (unspecified) |
| `FinalNewline` (`final-newline`) | bool | true |
| `Headings` (`heading-style`) | `HeadingStyle` | `Atx` |
| `Bullet` (`bullet`) | `BulletMarker` | `Dash` |
| `ListIndent` (`list-indent`) | int, 2 or 4 | 2 |
| `OrderedStyle` (`ordered-style`) | `OrderedStyle` | `Incrementing` |
| `Emphasis`, `Strong` (`emphasis`, `strong`) | `EmphasisMarker` | `Asterisk` |
| `CodeFence` (`code-fence`) | `FenceMarker` | `Backtick` |
| `CodeBlockStyle` (`code-block-style`) | `CodeBlockStyle` | `Fenced` |
| `HardBreak` (`hard-break`) | `HardBreakStyle` | `Spaces` |
| `ThematicBreak` (`thematic-break`) | `ThematicBreakStyle` | `Dashes` |
| `Tables` (`tables`) | `TableStyle` | `Compact` |
| `BareUrls` (`bare-urls`) | `BareUrlStyle` | `Autolink` |
| `MaxBlankLines` (`max-blank-lines`) | int, 1 to 100000 | 1 |
| `TrailingWhitespace` (`trailing-whitespace`) | `TrailingWhitespaceStyle` | `Strip` |
| `LinkRefactor` (`link-refactor`) | `LinkRefactorOptions` | `LinkRefactorOptions.Disabled` |
| `Rules` (`rules`) | sorted map of canonical rule id to bool | `RuleCatalog.Defaults` |
| `Ignore` (`ignore`) | list of glob patterns | `DefaultIgnore` |

Invariants: `Rules` is always complete (it holds exactly the configurable rule ids, keyed by canonical kebab-case id,
ordinal), so consumers never need a second lookup of defaults. `Ignore` patterns use forward slashes and are relative
to the directory of the configuration file that applies (the base directory when none does). `DefaultIgnore` is
`.git/**`, `node_modules/**`, `**/.venv/**`, `**/generated/**`, `**/thirdparty/**`, `.agent-logs/**`. Constants:
`MaxNumber` (100000, guards width arithmetic against overflow) and `LineBreakCharacters` (CR, LF, NEL, U+2028, U+2029).

**Enumerations** (the YAML token of each member is its lower-case name, matched case-sensitively):
`WrapMode` (`fill`, `sentence`, `semantic`: `fill` re-wraps a paragraph only when a line exceeds the line length,
`sentence` starts every sentence on a new line, `semantic` also breaks a too-wide sentence at the latest clause break
that fits); `BulletMarker` (`dash`, `asterisk`, `plus`); `OrderedStyle` (`incrementing`, `one`); `EmphasisMarker`
(`asterisk`, `underscore`); `FenceMarker` (`backtick`, `tilde`); `HardBreakStyle` (`spaces`, `backslash`);
`ThematicBreakStyle` (`dashes`, `asterisks`, `underscores`); `TableStyle` (`compact`, `aligned`: aligned pads columns
only when the aligned table fits the line length, otherwise compact); `BareUrlStyle` (`autolink`, `leave`);
`HeadingStyle` (`atx`), `CodeBlockStyle` (`fenced`), and `TrailingWhitespaceStyle` (`strip`), which have one value
today so the canonical form is explicit and can grow without a format change.

**LinkRefactorOptions** is a sealed record `(Enabled, Threshold, InTables, BreakLinkText, LabelMaxLength,
DefinitionsMarker)`. Defaults (`Disabled`): off, 60, true, false, 40, `<!-- Link References -->`. `Threshold` and
`LabelMaxLength` are 1 to `MaxNumber`.

#### Key Methods

**LinkRefactorOptions.IsValidMarker(value)**: decides whether a definitions marker is acceptable. It returns true only
for exactly one single-line HTML comment: at least 7 characters, starts with `<!--`, contains no line-break character,
the text after the opening does not start with `>` or `->` (an HTML parser would treat those as an empty comment),
and the first `-->` after the opening is the final three characters, so nothing follows the comment. Pure.

The record has no other behavior; `Default` and `Disabled` are shared static instances.

#### Error Handling

N/A - the record performs no validation and throws nothing. Values from a file are validated by `ConfigParser` (see
*ConfigLoader Design*); command-line rule overrides are validated by `RuleOverrides` in the `Linting` subsystem.
`IsValidMarker` reports invalid input by returning false.

#### Dependencies

- `RuleCatalog` (`Linting`): supplies `Defaults` for the initial `Rules` map.
- `LineEnding` (`Document`): the type of `EndOfLine`.
- No OTS items.

#### Callers

- `ConfigParser` (inside `ConfigLoader`) builds instances from a file and uses `IsValidMarker`.
- `LintMarkEngine` reads `Ignore`, applies rule overrides, and sets `EndOfLine` from the editor settings.
- `DocumentFormatter` and the block and inline formatters read the style, width, and wrap settings.
- `Linting` rules read `Rules` through the rule overrides and the engine.
