## Linting

![Linting Structure](LintingView.svg)

### Overview

The `Linting` subsystem is the vocabulary of findings. It defines the one model that every check of the tool
reports through (`Diagnostic`) and the one registry of every rule identifier the tool knows (`RuleCatalog`), with
the kind, default state, and legacy aliases of each rule. It contains no rule logic yet: the rules that find lint
problems arrive in a later phase and will be added to this subsystem. It does no file-system access, reads no
configuration, and writes no output. The subsystem contains two units: `Diagnostic` and `RuleCatalog`.

- `Diagnostic` is the immutable record of one finding about a file. Its supporting type `DiagnosticKind` says
  whether rewriting the file removes the finding. It is described in *Diagnostic Design*.
- `RuleCatalog` is the static registry of rule identifiers. Its supporting type `RuleInfo` holds the facts about one
  rule. It is described in *RuleCatalog Design*.

### Interfaces

**Diagnostic**: The finding that every unit reports.

- *Type*: In-process .NET sealed record.
- *Role*: Provider.
- *Contract*: Carries the rule identifier, the kind, the full path of the file, a 1-based line and column, and a
  single-line message. Two diagnostics with the same members are equal, which the reporter uses to report a finding
  once. `Fixable` is derived from the kind.
- *Constraints*: The constructor validates every argument, so an invalid diagnostic cannot be created: the rule
  identifier must be kebab-case, the path and message must not be blank, the message must be a single line, and line
  and column must be at least one.

**RuleCatalog.TryResolve and RuleCatalog.TryResolveConfigurable**: Resolve a name to the canonical rule identifier.

- *Type*: In-process .NET static methods.
- *Role*: Provider.
- *Contract*: Both accept a kebab-case rule identifier or a legacy rule-number alias in any letter case.
  `TryResolve` answers for every registered identifier; `TryResolveConfigurable` answers only for the rules that the
  `rules` setting of a configuration can switch on or off.
- *Constraints*: Throw `ArgumentNullException` for a null name; an unknown name and the alias of a dropped rule
  return `false`.

**RuleCatalog.TryGet, IsDroppedAlias, and the identifier constants**: Look up the registry.

- *Type*: In-process .NET static members.
- *Role*: Provider.
- *Contract*: `TryGet` returns the `RuleInfo` of a canonical identifier. `IsDroppedAlias` tells whether a name is
  the alias of a rule that no longer exists. The constants name the identifiers that other units emit.
- *Constraints*: Throw `ArgumentNullException` for a null argument.

**RuleCatalog collections**: `Rules`, `ConfigurableRules`, `Defaults`, `Aliases`, and `DroppedAliases`.

- *Type*: In-process .NET static properties.
- *Role*: Provider.
- *Contract*: Immutable views of the registry in ordinal order; `Defaults` is the default state of every
  configurable rule.
- *Constraints*: Backed by immutable collections, so a caller cannot change them even by casting.

The subsystem consumes only the .NET base class library. It is consumed by the `Configuration` subsystem
(`RuleCatalog`, for validating the `rules` setting and for the defaults), the `Formatting` subsystem (`Diagnostic`
and the line-ending identifier), the `Reporting` subsystem (`Diagnostic`), and the `Engine` subsystem (`Diagnostic`
and every identifier it emits).

### Design

The two units are independent of each other: `Diagnostic` checks only the shape of a rule identifier (kebab-case)
and does not consult `RuleCatalog`, so a unit that reports can be tested without the registry. Instead, the
registry is made the single source of truth by a test that proves every identifier the tool emits is registered.

`RuleCatalog` registers three groups of identifiers. The 22 configurable rules are the ones the `rules` setting
accepts. The always-applied formatting rule `line-ending` and the identifiers under which the tool reports its own
failures (safety refusals, file problems, and configuration problems) are registered as not configurable, so a
configuration that names one of them is still rejected as an unknown rule, exactly as before the registry existed.
Nine legacy aliases of rules that no longer exist are recognized so that an older configuration file is still
accepted, but they never resolve to a rule.

`DiagnosticKind` makes the contract of the fix option checkable. A `Formatting` diagnostic is one that rewriting the
file removes (the engine drops those of a file it has written); a `LintOnly` diagnostic stays until a person changes
the file. The kind of every identifier is recorded in the registry.

**Subsystem-level cycle (accepted)**: The `Configuration` subsystem consumes `RuleCatalog` of this subsystem now, in
`LintMarkConfig` and `ConfigParser`. The lint rules of a later phase will consume `LintMarkConfig`, so the two
subsystems will depend on each other at the subsystem level. The dependency is accepted because the unit level stays
acyclic: `LintMarkConfig` depends on `RuleCatalog`, a later lint rule engine will depend on `LintMarkConfig`, and
neither `RuleCatalog` nor `Diagnostic` depends on any configuration unit. The cycle is recorded here so that a later
change does not introduce a dependency from `RuleCatalog` or `Diagnostic` to the `Configuration` subsystem.

Thread safety: every member of the subsystem is immutable or static over immutable data, so all of it is safe for
concurrent use by the worker threads of the engine.
