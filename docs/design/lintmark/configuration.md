## Configuration

![Configuration Structure](ConfigurationView.svg)

### Overview

The `Configuration` subsystem decides which settings apply to a Markdown file. It defines the settings
themselves, finds and validates the `.lintmark.yaml` file that sets them, and reads the line ending that
the editor settings (`.editorconfig` files) specify. It does not lint or format anything and it does not
decide which files are processed; the `Engine` subsystem asks it for the answer for each file. The subsystem
contains three units: `LintMarkConfig`, `ConfigLoader`, and `EditorConfigResolver`.

- `LintMarkConfig` is the complete, validated, deeply immutable set of settings for one file. Its supporting
  types (`LinkRefactorOptions`, `RuleIdentifiers`, and twelve setting enumerations) are described in
  *LintMarkConfig Design*.
- `ConfigLoader` finds the configuration file that applies to a Markdown file, reads it once, and either
  returns a valid `LintMarkConfig` or a complete list of positioned problems. Its supporting types
  (`ConfigParser`, `ConfigLoadResult`, `ConfigError`) are described in *ConfigLoader Design*.
- `EditorConfigResolver` determines the `end_of_line` value that the editor settings specify for a Markdown
  file. Its supporting type (`EditorConfigResult`) is described in *EditorConfigResolver Design*.

The subsystem owns the file-system access needed for configuration (probing for `.lintmark.yaml` and `.git`
entries and reading the configuration file; the editor settings files are read by the editorconfig library
on its behalf) and the wording of every configuration problem message. It never writes a file and never
writes output; problems are returned as values.

### Interfaces

**ConfigLoader factories**: `ForDefaults`, `ForDiscovery`, and `ForFile`.

- *Type*: In-process .NET static methods.
- *Role*: Provider.
- *Contract*: Each returns a loader in one mode. `ForDefaults` always answers with the built-in defaults and
  never touches the file system. `ForDiscovery` finds the nearest `.lintmark.yaml` for each Markdown file.
  `ForFile` reads exactly one named file, once, immediately, and answers with it for every Markdown file.
- *Constraints*: `ForFile` throws `ArgumentNullException` for null and `ArgumentException` for an empty or
  blank name. A problem with the named file is not an exception; it is available as `InitialErrors`.

**ConfigLoader.Load**: Gets the configuration that applies to a Markdown file.

- *Type*: In-process .NET instance method.
- *Role*: Provider.
- *Contract*: Takes the path of a Markdown file (it need not exist) and returns a `ConfigLoadResult` holding
  either a valid `LintMarkConfig` or the problems found. Results are cached, so every file in one directory
  receives the same result instance.
- *Constraints*: Never throws for environment or content problems. Throws `ArgumentNullException` for null and
  `ArgumentException` for an empty path. Safe for concurrent use.

**ConfigLoader.InitialErrors**: The problems of an explicitly named configuration file.

- *Type*: In-process .NET property.
- *Role*: Provider.
- *Contract*: For a loader created by `ForFile`, the errors of that file, so they can be reported even when no
  Markdown file is selected; empty in the other modes and for a valid file.
- *Constraints*: Immutable.

**ConfigLoader.Parse, LoadFile, and FindConfigFile**: The uncached building blocks.

- *Type*: In-process .NET static methods.
- *Role*: Provider.
- *Contract*: `Parse` decodes and validates the bytes of a configuration file; `LoadFile` reads a file and
  parses it; `FindConfigFile` runs the discovery walk for a directory. None of them caches or keeps state.
- *Constraints*: Same exception rules as `Load`; they are exposed so that each step can be exercised alone.

**EditorConfigResolver.Resolve**: Gets the line ending that the editor settings specify for a Markdown file.

- *Type*: In-process .NET instance method.
- *Role*: Provider.
- *Contract*: Returns an `EditorConfigResult` with `lf`, `crlf`, or "not specified", or an error message when
  the applicable editor settings could not be read.
- *Constraints*: Never throws for file-system or content problems. Throws `ArgumentNullException` for null
  and `ArgumentException` for an empty path. Safe for concurrent use.

**LintMarkConfig**: The settings of one file.

- *Type*: In-process .NET immutable record.
- *Role*: Provider.
- *Contract*: Exposes every setting as a property, the built-in defaults as `LintMarkConfig.Default`, and
  value equality that includes the collections.
- *Constraints*: Every initializer validates and throws the documented exception type, so an invalid
  configuration cannot be created, not even through a `with` expression.

The subsystem consumes the `Document` subsystem (the `LineEnding` type is the type of the line-ending
setting), the YamlDotNet library (YAML syntax), and the EditorConfig library (editor settings files). It is
consumed by the `Engine` subsystem. In terms of the architecture model:

- `ConfigLoader` depends on `LintMarkConfig`, `LinkRefactorOptions`, the setting enumerations, `LineEnding`
  from the `Document` subsystem, and YamlDotNet.
- `EditorConfigResolver` depends on EditorConfig and `LineEnding`.
- `LintMarkEngine` from the `Engine` subsystem depends on `ConfigLoader`, `EditorConfigResolver`,
  `LintMarkConfig`, `ConfigError`, `ConfigLoadResult`, and `EditorConfigResult`.

### Design

The three units are deliberately independent of one another. `ConfigLoader` and `EditorConfigResolver` do not
call each other and both produce plain values; `LintMarkConfig` is the shared vocabulary. The engine is the only
collaborator that combines them, which keeps the precedence rule in one place.

The flow for one Markdown file is:

1. The engine asks the `ConfigLoader` for the file's configuration. In discovery mode the loader starts in the
   file's directory and walks upward (see *ConfigLoader Design* for the exact algorithm). The nearest
   `.lintmark.yaml` wins; files are never merged. The file is read and validated once per run, and the result is
   shared by every Markdown file that resolves to it.
2. If the load returned problems, the file has no configuration. The problems are attached to the file's result
   and the editor settings are not consulted.
3. If the load returned a configuration whose `EndOfLine` is set, that value is final.
4. Otherwise, unless `--no-config` was given, the engine asks the `EditorConfigResolver` for the line ending of
   that file. A specified `lf` or `crlf` becomes the `EndOfLine` of the file's effective configuration (a
   `with` copy of the shared configuration, so nothing shared is changed). "Not specified" leaves `EndOfLine`
   unset, which means the file's own majority line ending decides later.

The resulting precedence is: the `end-of-line` setting of the configuration file, then `end_of_line` from the
editor settings, then unspecified. `--no-config` makes the engine use a defaults-only loader and no resolver, so
neither source is read; `--config` replaces discovery with one named file but does not disable the editor
settings.

Both units cache their file-system answers so that a large tree costs one probe per directory rather than one
per file. The loader caches per start directory and per configuration file; the resolver caches the resolved
chain of settings files per directory. Both caches are `ConcurrentDictionary` instances of `Lazy` values with
`ExecutionAndPublication` thread safety, so concurrent workers share one computation and always observe the
same immutable answer. The answers are pure functions of the file-system content, so the outcome does not
depend on scheduling. Because content is read once per run, edits made while a run is in progress are not seen.

Configuration problems are values, not exceptions. `ConfigLoadResult` carries either a configuration or a
list of `ConfigError` entries and enforces that exactly one of the two is present, so settings from a file
that was reported as invalid can never be used. The editor settings follow the same principle with
`EditorConfigResult`. Only argument misuse (null or empty paths) raises exceptions.

Dependencies on OTS items: YAML syntax is read by YamlDotNet at event level only, which exposes the positions,
scalar styles, anchors, and tags that the schema checks need (see *YamlDotNet Integration Design* in the OTS
design documents); `.editorconfig` discovery and section matching are delegated to the EditorConfig library
(see *EditorConfig Integration Design* in the OTS design documents). Both libraries have quirks that the units
compensate for; the compensation is described in the unit designs.

**Scope of the requirements in this phase**: The line ending that the editor settings or the configuration give is
stored on each file's effective configuration but nothing consumes it before the formatter exists, so it cannot be
observed from outside the tool. The requirement therefore states only what is observable now: editor settings that
cannot be used never produce an error. The report of settings that cannot be read (a message that the engine places
at the start of the Markdown file) is verified by tests that have no requirement, like the determination of the line
ending (nearest file, sections, root file, patterns, letter case, the `end-of-line` setting taking precedence). The
formatting phase adds the system requirement that the tool uses the line
ending given by the editor settings when the configuration gives none, and re-adds a requirement for the report of
unreadable settings together with an observable system-level behavior, with the subsystem and unit
requirements that decompose them, and links the existing tests to them.

**Guards against pathological files**: A configuration file that is implausibly large or implausibly deeply nested
is reported as a problem with a position, promptly. The limits and the reasons for them are described in
*ConfigLoader Design*.
