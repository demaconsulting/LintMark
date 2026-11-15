### ConfigLoader

![Configuration Structure](ConfigurationView.svg)

#### Purpose

`ConfigLoader` finds, reads, and validates the `.lintmark.yaml` configuration that applies to a Markdown file. It
returns either a valid `LintMarkConfig` or the complete list of positioned problems, never both. Its supporting types
`ConfigParser`, `ConfigLoadResult`, and `ConfigError` are described in this document.

#### Data Model

- **ConfigLoadResult**: immutable record `(LintMarkConfig? Config, string? SourcePath, ImmutableArray<ConfigError>
  Errors)`. `Config` is set exactly when `Errors` is empty, so settings of a file reported as invalid can never be
  used. `SourcePath` is null for the built-in defaults. Errors are sorted by line, column, then message (ordinal).
- **ConfigError**: immutable record `(Path, Line, Column, Message)`; line and column are 1-based (UTF-16 code units, a
  tab counts as one); the message is one line. The constructor rejects an empty path, a position below 1, and a
  blank message. Every problem is reported under the rule id `config-error` (`ConfigLoader.ErrorRuleId`).
- **ConfigLoader** has one of three modes, chosen by a factory: `ForDefaults` (fixed result, no file access),
  `ForFile(path)` (fixed result from one file loaded immediately), `ForDiscovery` (no fixed result). Discovery mode
  holds two `ConcurrentDictionary` caches of `Lazy` results (`ExecutionAndPublication`): by start directory and by
  configuration file path. `InitialErrors` holds the errors of an explicit file so they are reportable when no Markdown
  file is processed. Constants: `FileName` (`.lintmark.yaml`), `MaxFileBytes` (1 MiB).
- **ConfigParser** is a stateless static class. Limit: `MaxNestingDepth` is 64 bracket levels.

#### Key Methods

**Load(markdownFile)**: in the fixed modes returns the fixed result. In discovery mode it takes the full path's
directory (an unresolvable path yields the defaults) and returns the cached result for that directory, computed once:
`FindConfigFile`, then `LoadFile` through the per-file cache, so directories sharing a configuration file parse it
once. No file found yields the shared defaults result.

**FindConfigFile(directory)** (config discovery): starting at the directory, repeat: if `<dir>/.lintmark.yaml` is a
file, return it (a directory of that name is not a configuration); else if `<dir>/.git` exists (directory or file, as
in linked working trees and submodules), stop with none; else move to the parent, stopping at the file-system root.
The nearest file wins, files are never merged or inherited, and the config file is checked before the `.git` marker, so
a configuration in the repository root is found and nothing above the root is searched.

**LoadFile(path)**: resolves the path (an invalid path is a problem at line 1, column 1), reads up to one byte over
`MaxFileBytes`, and passes the bytes to `Parse`. Read failures become one problem at 1:1: not found, access denied, the
path is a directory, or another I/O failure. The file is opened with shared read, write, and delete access.

**Parse(path, bytes)**: rejects content over 1 MiB, decodes it with `SourceDocument.Decode` (UTF-8 byte order mark
removed silently; UTF-16 or UTF-32 mark and invalid UTF-8 reported at the offending position), and calls
`ConfigParser.Parse`.

**ConfigParser.Parse(path, text)** (parse and validation algorithm), YAML parsed at event level so key positions,
duplicate keys, scalar styles, and nulls stay visible:

1. Nesting guard: count every `[`, `{` as an opener and `]`, `}` as a closer (ignoring quoting and comments); more than
   64 open levels is an error at that bracket and ends the parse, because the parser's time grows quadratically.
2. `ReadTree`: feed the YamlDotNet parser events to a `TreeBuilder` that uses an explicit stack (input nesting cannot
   exhaust the call stack) and builds scalar, mapping, and sequence nodes carrying line, column, and scalar style. A
   second document ends the parse ("Only one YAML document is allowed"). Any parser exception ends the parse with
   `Invalid YAML: ...`; the position is the parser's, or the end of the last event read for non-YAML exception types.
   An anchor, alias, or tag is reported ("Anchors, aliases and tags are not supported") and the node is marked
   reported, so later checks do not repeat it.
3. An empty file, comments only, a lone document marker, or a null document (`~`, `null`, empty) means no settings:
   the defaults. A root that is not a mapping is an error.
4. `Validator` walks the root mapping, starting from `LintMarkConfig.Default` and replacing one setting per entry with
   `with`. Duplicate keys are errors naming the first line; unknown settings and non-scalar keys are errors. All
   semantic problems are collected so one run reports them all. The catalog (kebab-case keys): enums accept the exact
   lower-case token; booleans only plain `true` or `false`; numbers plain decimal without plus sign or leading
   zeros, within a range (`line-length` 0 to 100000, `max-blank-lines` 1 to 100000); `list-indent` plain 2 or 4;
   `link-refactor` a mapping with `enabled`, `threshold`, `in-tables`, `break-link-text`, `label-max-length`,
   `definitions-marker` (valid per `LinkRefactorOptions.IsValidMarker`); `ignore` a list of non-blank single-line
   patterns, backslashes normalized to `/`, exact duplicates dropped, a null value rejected, an empty list valid.
5. `rules` is a mapping of rule name to boolean. Each name resolves through `RuleCatalog.TryResolveConfigurable`
   (kebab-case id or legacy rule-number alias, any letter case) to the canonical id; the alias of a dropped rule is
   accepted and ignored; any other name is `Unknown rule`. Two entries naming the same rule (id and alias, or
   different case) are a conflict reported at the later one.
6. Success only when no error was collected; errors are de-duplicated and sorted.

`end-of-line` accepts `lf` or `crlf` and stays null when absent, so the editor settings and then the file's
majority ending decide later (see *Configuration Design*).

#### Error Handling

Loading never throws for environment or content problems; each becomes a `ConfigError` in the result. Argument
errors throw: `ArgumentNullException` or `ArgumentException` for a null, empty, or (for `ForFile` and `LoadFile`)
blank path. Syntax and structure problems (invalid YAML, second document, root not a mapping, excessive nesting) end
the parse with that one problem set; semantic problems accumulate. Quoted values in messages are flattened to one line
and shortened to 40 characters. Results and failures are cached per run; later edits are not seen.

#### Dependencies

- YamlDotNet OTS item, event-level parser only (see *YamlDotNet Design*).
- `LintMarkConfig` and its enumerations; `RuleCatalog` (`Linting`); `SourceDocument` and `LineEnding` (`Document`).
- File system, read-only.

#### Callers

`LintMarkEngine` selects the mode from the `Context` options (`--no-config` selects `ForDefaults`; `--config <file>`
selects `ForFile`; otherwise `ForDiscovery`), reports `InitialErrors`, and calls `Load` for each file.
