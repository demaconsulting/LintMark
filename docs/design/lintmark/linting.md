## Linting

![Linting Structure](LintingView.svg)

### Overview

The `Linting` subsystem is the vocabulary and the checks of findings that rewriting cannot fix. It defines the one
model that every check of the tool reports through (`Diagnostic`), the one registry of every rule identifier the tool
knows (`RuleCatalog`), the command-line rule overrides (`RuleOverrides`), and the lint-only rules themselves. The rules
that rewrite a document live in the `Formatting` subsystem; this subsystem only reports. It does no file-system access,
reads no configuration source, and writes no output.

The subsystem contains two units:

- `RuleCatalog` holds the registry, `RuleInfo`, `RuleOverrides`, `Diagnostic` and `DiagnosticKind`. It is described in
  *RuleCatalog Design*.
- `LintRules` holds the rule engine (`LintRuleEngine`), the `ILintRule` contract, the shared helpers (`RuleSupport`)
  and the four rule families: `HeadingRules`, `LinkRules`, `StructureRules` and `TableRules`. It is described in
  *LintRules Design*.

### Interfaces

The subsystem exposes the following to the rest of the tool:

- `Diagnostic` and `DiagnosticKind`, created by the `Engine`, `Formatting` and `Configuration` units and consumed by
  the reporters.
- `RuleCatalog`, queried by `Configuration` (which rule names a configuration may switch), by `Document` (which
  rule a suppression comment names) and by `Formatting` (the description and kind of a rule it reports).
- `RuleOverrides`, created by `Context` from `--enable` and `--disable` and applied to the configuration by the
  `Engine`.
- `LintRuleEngine.CreateDefault` and `LintRuleEngine.Run`, called by the `Engine` once per document.

The subsystem consumes a `ParsedDocument` (blocks, syntax tree, source text, reference definitions and suppression
map) from the `Document` subsystem and a `LintMarkConfig` from the `Configuration` subsystem. The structure, link and
table rules also reuse a few read-only layout helpers of the `Formatting` subsystem (`InlineMap`, `LineLayout`,
`PipeTable` and `TableFormatter.PrefixWidth`) so that lint and formatting agree on what a table or a text run is.

### Design

`RuleCatalog` is the single place where an identifier is defined, so every other unit refers to a rule by one of its
constants. The kebab-case identifier is the primary name; a legacy rule number (such as `MD013`) is an alias that is
accepted wherever a rule name is accepted, and a finding is always reported under the kebab-case identifier.

`LintRuleEngine` runs after parsing and before reporting. It asks each `ILintRule` family for diagnostics, drops the
ones whose rule is switched off in the effective configuration (the command-line overrides are already applied to it),
drops the ones that a suppression comment covers at their position, and returns the rest in one deterministic order.
A rule can only return diagnostics, never edits, so a lint-only rule cannot change a file or make a rewrite unsafe.
The formatting rules do not run here: the `Formatting` subsystem builds its own diagnostics, using `RuleCatalog` for
their text and kind, and the `Engine` combines both lists. The grid-table finding is also produced by the `Engine`,
because it comes from a parser fallback and not from a rule family.
