## Configuration

![Configuration Structure](ConfigurationView.svg)

### Overview

The `Configuration` subsystem decides which settings apply to a Markdown file. It defines the settings, finds and
validates the `.lintmark.yaml` file that sets them, and reads the line ending that the editor settings
(`.editorconfig` files) specify. It does not lint or format anything and does not decide which files are processed;
the `Engine` subsystem asks it for the answer for each file. It only reads files and never writes output: problems
are returned as values. The subsystem contains three units:

- `LintMarkConfig` is the complete, immutable set of settings for one file, with `LinkRefactorOptions` and twelve
  setting enumerations (see *LintMarkConfig Design*). Rule identifiers, aliases, and defaults come from `RuleCatalog`
  of the `Linting` subsystem.
- `ConfigLoader` finds the configuration file that applies to a file, reads and validates it, and returns either a
  valid `LintMarkConfig` or positioned problems. Its supporting types are `ConfigParser`, `ConfigLoadResult`, and
  `ConfigError` (see *ConfigLoader Design*). It uses the YamlDotNet OTS item (see *YamlDotNet Design*).
- `EditorConfigResolver` reads the `end_of_line` value that the editor settings specify for a file, using the
  EditorConfig OTS item (see *EditorConfig Design*). Its supporting type is `EditorConfigResult`.

### Interfaces

**Provided to Engine** (in-process .NET, internal):

- `ConfigLoader.ForDefaults`, `ForDiscovery`, `ForFile`: create a loader in one of three modes (built-in defaults
  only, nearest-file discovery, or one explicit file). `ForFile` reads the file immediately; its problems are
  available as `InitialErrors`.
- `ConfigLoader.Load(markdownFile)`: returns a `ConfigLoadResult` (valid `LintMarkConfig` or problems); never throws
  for environment or content problems; thread-safe and cached.
- `EditorConfigResolver.Resolve(markdownFile)`: returns an `EditorConfigResult` (line ending, not specified, or a
  read problem); never throws for file-system or content problems; thread-safe.
- `LintMarkConfig` and its enumerations: read-only settings consumed by `Engine`, `Formatting`, and `Linting`.

**Consumed**:

- File system: probes for `.lintmark.yaml` and `.git` entries and reads the configuration file.
- `SourceDocument.Decode` (`Document`): strict decoding of the configuration bytes.
- `RuleCatalog` (`Linting`): rule id and alias resolution and the default rule states.
- `LineEnding` (`Document`): the line-ending values.
- YamlDotNet and EditorConfig (OTS).

### Design

Configuration is resolved per Markdown file in three steps by `LintMarkEngine`, and every step is a pure function
of the file-system content:

1. `ConfigLoader.Load` yields the `LintMarkConfig` of the nearest `.lintmark.yaml`, found by walking up from the file's
   directory (nearest file wins, no merging), the explicit `--config` file, or the built-in defaults.
2. If that configuration has no `end-of-line` and `--no-config` is not set, `EditorConfigResolver.Resolve` supplies
   the line ending from `.editorconfig`.
3. If neither names one, the formatter falls back to the file's majority line ending.

With `--no-config` the engine creates the defaults loader and no resolver, so both `.lintmark.yaml` and
`.editorconfig` are ignored. A configuration that fails validation yields no `LintMarkConfig` at all, so the engine
cannot use half-valid settings; its problems are reported under the rule id `config-error`.

Each unit caches its file-system work per directory (and per configuration file) behind `Lazy` values with
`ExecutionAndPublication` thread-safety, so parallel file processing shares one resolution per directory and sees
identical results. Edits made to configuration files during a run are not seen.
