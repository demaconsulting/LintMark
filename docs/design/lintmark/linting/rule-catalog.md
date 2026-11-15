### RuleCatalog

![Linting Structure](LintingView.svg)

#### Purpose

`RuleCatalog` is the single registry of every rule identifier the tool knows, together with the command-line rule
overrides and the finding model that carries an identifier to the report. It holds `RuleCatalog` and `RuleInfo` (the
registry), `RuleOverrides` (`--enable` and `--disable`), and `Diagnostic` with `DiagnosticKind` (one finding). It is
the one place where a rule identifier is defined, so a configuration can be validated against it, a finding can be
traced to a registered rule, and the default state of every configurable rule has one home.

#### Data Model

**RuleInfo**: a sealed record `(Id, Kind, Description, Aliases, DefaultEnabled, IsConfigurable)`. `Id` is the kebab-case
identifier, `Kind` says whether rewriting fixes the rule, `Aliases` holds legacy rule numbers such as `MD013`, and
`IsConfigurable` says whether the `rules` setting may switch the rule.

**RuleCatalog**: a static class with 60 registered rules in three groups:

- 27 configurable rules: 3 formatting (`line-length`, `paragraph-wrap`, `link-refactor`) and 24 lint-only. All are on
  by default except `descriptive-link-text`, `table-grid-unformatted` and `dollar-sign-in-shell-code`.
- 26 always-applied formatting rules (`line-ending` has no alias; each of the other 25 carries one or two). They
  cannot be switched.
- 7 failure identifiers: `file-read-error`, `file-encoding-error`, `file-line-ending-error`, `file-parse-error`,
  `file-format-error`, `file-write-error` and `config-error`. They are lint-only and cannot be switched.

Frozen tables map every id and alias to the canonical id (case-insensitive, ordinal) and every id to its `RuleInfo`.
`Rules` lists all entries in ordinal order, `Defaults` gives the default state of each configurable rule, and `Aliases`
gives the aliases of configurable rules. Nine aliases (`MD002`, `MD006`, `MD008`, `MD025`, `MD033`, `MD041`, `MD043`,
`MD044`, `MD057`) belong to dropped rules: they are known but never resolve.

**RuleOverrides**: an immutable class holding two sorted sets of canonical ids, `Enabled` and `Disabled`, with the
static `None` for no overrides.

**Diagnostic**: an immutable record `(RuleId, Kind, Path, Line, Column, Message)` with value equality over all
members (the reporter uses it to print a finding once). `RuleId` is always the kebab-case id, line and column are
1-based, and the message is one line. **DiagnosticKind** is `LintOnly` (reported only) or `Formatting` (rewriting
removes it).

#### Key Methods

**TryResolve(name)**: looks the name up, ignoring case, in the id-and-alias table and returns the canonical id for any
registered rule, configurable or not. It returns false for unknown names and for dropped aliases. Suppression comments
use it, so `MD013` and `line-length` name the same rule there.

**TryResolveConfigurable(name)**: like `TryResolve`, but true only when the rule is configurable. The `rules:` block of
a configuration and the override options use it, so naming an always-applied rule or a failure id is an error.

**TryGet(id)** finds a `RuleInfo` by canonical id only (an alias is not accepted); `IsDroppedAlias(name)` tests the
dropped set. `Formatting` uses `TryGet` to turn an edit of an always-applied rule into a finding that carries the rule
description as its message.

**RuleOverrides.Create(enable, disable)**: for every option value, splits at commas, trims, resolves each name with
`TryResolveConfigurable`, skips dropped aliases, then rejects any id named by both options. It returns `None` when
nothing remains. **Apply(config)** sets each enabled id to true and each disabled id to false in the configuration's
`Rules` map (the two sets never overlap, so the order is immaterial); enabling `link-refactor` also turns on the
`LinkRefactor` setting, which is that feature's own switch. Overrides therefore win over the configuration file.

**Diagnostic.LintOnly(path, ruleId, line, column, message)**: the one builder for a lint-only finding. It replaces line
breaks in the message by spaces. Findings of kind `Formatting` are created with the constructor. Neither the record nor
the catalog orders findings: `LintRuleEngine` and `TextReporter` order by line, column, rule id and message.

#### Error Handling

`RuleCatalog` lookups never throw except `ArgumentNullException` for a null name; an unknown name is a false result and
the caller decides. `RuleOverrides.Create` throws `ArgumentException` for a blank list, an empty entry, an unknown
rule, an always-applied rule ("it is always applied") or a rule named by both options. `Context.Create` lets that
exception reach `Program.Main`, which prints it and exits with code 1. A configuration that names an unknown or
always-applied rule, or the same rule twice (also under an alias), gets a `config-error` finding from the
`Configuration` subsystem instead.

#### Dependencies

`LintMarkConfig` (the `Rules` map and `LinkRefactor` setting, from the `Configuration` subsystem) for
`RuleOverrides.Apply`; `RuleCatalog` and `RuleInfo` are otherwise self-contained.

#### Callers

`Context` (creates `RuleOverrides`); `Engine` (applies the overrides, creates failure and grid findings, and reads
`Defaults` through `LintMarkConfig`); `ConfigParser` (resolves `rules:` names, ignores dropped aliases);
`DocumentParser` (resolves suppression names); `DocumentFormatter` and the block and inline formatters (rule
constants, `TryGet`, `Diagnostic`); the four rule families and `LintRuleEngine`; and the reporters (`Diagnostic`).
