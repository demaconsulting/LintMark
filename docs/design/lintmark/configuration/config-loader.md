### ConfigLoader

![Configuration Structure](ConfigurationView.svg)

#### Purpose

`ConfigLoader` finds, reads, and validates the `.lintmark.yaml` configuration that applies to a Markdown file.
It returns either a valid `LintMarkConfig` or a complete list of positioned problems, and it never throws for
problems in the environment or in the content of the file. Its supporting types are described in this document:
`ConfigParser` (the YAML reader and schema validator), `ConfigLoadResult` (the outcome), and `ConfigError` (one
problem).

#### Data Model

**Modes**: A loader works in exactly one mode, chosen by its factory.

| Factory | Behavior |
| --- | --- |
| `ForDefaults` | Answers every file with the built-in defaults; never touches the file system |
| `ForDiscovery` | Finds the nearest `.lintmark.yaml` for each Markdown file and caches the answers |
| `ForFile` | Reads one named file once, at creation, and answers every Markdown file with it |

**Constants**: `ErrorRuleId` is `config-error`, the rule identifier under which every configuration problem is
reported. `FileName` is `.lintmark.yaml`. `MaxFileBytes` is 1048576 (1 MiB), a guard against reading an unrelated
huge file.

**_fixedResult**: `ConfigLoadResult?` — the answer for every file in the defaults and explicit-file modes;
`null` in discovery mode.

**InitialErrors**: `ImmutableArray<ConfigError>` — for an explicit file whose load failed, that file's errors,
so the engine can report them even when no Markdown file is selected; empty in every other case.

**_directories** and **_files**: `ConcurrentDictionary<string, Lazy<ConfigLoadResult>>` with an ordinal key
comparer — the discovery caches, keyed by start directory and by configuration file path. Every `Lazy` uses
`ExecutionAndPublication` thread safety.

**ConfigLoadResult**: A sealed record `(LintMarkConfig? Config, string? SourcePath, ImmutableArray<ConfigError> Errors)`.
`SourcePath` is the full path of the file the configuration came from, or `null` when the built-in defaults
apply. Invariant (checked by the constructor, `ArgumentException` on violation): `Config` is set exactly when
`Errors` is empty, the array is initialized, and a source path is not empty. The constructor stores the errors
sorted by line, column, and message (ordinal), so the report never depends on discovery order. A load can
therefore never yield a half-valid configuration. This invariant, the argument checks of the loader and the
problem type, and the per-directory sharing of results are construction details that no user can observe
directly; they are verified by tests that are not linked to a requirement.

**ConfigError**: A sealed record `(string Path, int Line, int Column, string Message)`. The path is non-empty,
line and column are at least one, and the message is non-blank; line breaks in the message are replaced by
spaces so one problem is always one line of output.

**Discovery algorithm** (discovery mode):

1. Take the path of the Markdown file. Replace every backslash with a slash and make the path absolute, before any
   check, so both separator styles behave the same on every platform. The file need not exist. The start
   directory is the directory part of that path. A path that cannot be resolved has no configuration.
2. In the current directory, in this order:
   1. If a **file** named `.lintmark.yaml` exists, use it and stop. A directory with that name is skipped; it is
      not a configuration.
   2. Otherwise, if a `.git` entry exists, stop with no configuration. The entry may be a directory or a file
      (linked working trees and submodules use a file).
   3. Otherwise move to the parent directory. Reaching the file-system root ends the walk with no configuration.
3. Because the configuration file is checked before the repository marker, the repository root itself is
   searched. Nothing above it is ever searched, and with no `.git` entry anywhere the walk continues to the
   file-system root.

The nearest file wins and files are never merged: a nearer partial file does not inherit settings from a farther
one, and a farther file is not even read, so errors in it are never reported. `FindConfigFile` implements the
walk without a cache and is the testable form of this algorithm.

**Cache design**: `Load` caches the discovery result per start directory, so every Markdown file in one directory
shares one result instance. Behind it, a second cache keyed by the full path of the configuration file ensures
that a file shared by several directories is read and parsed once. Content is therefore read once per
configuration file per run, and edits made while a run is in progress are not seen. Keys are compared with an
ordinal comparison;
two spellings of one directory (for example in different letter case on a case-insensitive file system) merely cost
one extra identical parse. Thread safety: both caches are `ConcurrentDictionary` instances of `Lazy` values with
`ExecutionAndPublication`, so concurrent callers share one computation and every caller observes the same
immutable result. Results are pure functions of the file-system content, independent of scheduling.

**Encoding rules**: The loader decodes bytes itself rather than letting the YAML library read the file.

- Content larger than 1 MiB is rejected (the reader stops a little beyond the limit, so a huge file is not loaded).
- A UTF-8 byte order mark is stripped silently. A UTF-16 or UTF-32 byte order mark is rejected.
- Decoding is strict UTF-8: invalid bytes are reported at their position, never replaced.
- LF and CRLF line endings are both accepted.
- Positions are 1-based, count UTF-16 code units, and count a tab as one, the same convention as the source
  document of the `Document` subsystem. They refer to the decoded text, so a stripped byte order mark does not
  shift columns.

**Schema**: The top level of the file must be a mapping. Keys are kebab-case and case-sensitive; omitted keys
keep the defaults of `LintMarkConfig` (see *LintMarkConfig Design* for the tokens of every enumeration).

| Key | Accepted value | Default |
| --- | --- | --- |
| `line-length` | whole number, 0 to 100000 (0 = unlimited) | 120 |
| `wrap` | `fill`, `sentence`, `semantic` | `fill` |
| `end-of-line` | `lf`, `crlf` | not specified |
| `final-newline` | `true`, `false` | `true` |
| `heading-style` | `atx` | `atx` |
| `bullet` | `dash`, `asterisk`, `plus` | `dash` |
| `list-indent` | `2`, `4` | 2 |
| `ordered-style` | `incrementing`, `one` | `incrementing` |
| `emphasis` | `asterisk`, `underscore` | `asterisk` |
| `strong` | `asterisk`, `underscore` | `asterisk` |
| `code-fence` | `backtick`, `tilde` | `backtick` |
| `code-block-style` | `fenced` | `fenced` |
| `hard-break` | `spaces`, `backslash` | `spaces` |
| `thematic-break` | `dashes`, `asterisks`, `underscores` | `dashes` |
| `tables` | `compact`, `aligned` | `compact` |
| `bare-urls` | `autolink`, `leave` | `autolink` |
| `max-blank-lines` | whole number, 1 to 100000 | 1 |
| `trailing-whitespace` | `strip` | `strip` |
| `link-refactor` | mapping of the options below | all options at their defaults |
| `rules` | mapping of rule id or alias to `true` or `false` | the default of every rule |
| `ignore` | list of glob patterns (possibly empty) | the six default patterns |

The `link-refactor` mapping accepts `enabled` (boolean), `threshold` (whole number, 1 to 100000), `in-tables`
(boolean), `break-link-text` (boolean), `label-max-length` (whole number, 1 to 100000), and
`definitions-marker` (a single-line HTML comment). Omitted options keep their defaults.

**Validation rules**:

- Enumeration tokens are lower case and case-sensitive (`Fill` and `LF` are errors). A token may be quoted.
  The allowed tokens are listed, in declaration order, in the message.
- Whole numbers must be plain (unquoted) decimal digits without a plus sign or leading zeros, fit in a 32-bit
  integer, and lie in the range of the setting. A quoted number, a decimal fraction, a hexadecimal number, and
  an underscore-separated number are errors. `list-indent` accepts only 2 or 4.
- Booleans must be plain `true` or `false`; `yes`, `on`, `True`, and quoted forms are errors.
- A null value is an error, so that a half-edited file never silently reverts to the defaults: `key:` with no
  value, `~`, `null`, and an empty `rules`, `link-refactor`, or `ignore` all report that the setting must not
  be empty. An explicit empty collection (`ignore: []`, `rules: {}`) is valid.
- An empty file, a file with only whitespace and comments, a lone document marker, and a lone null document all
  mean "no settings" and yield the defaults without an error.
- A key that appears twice is reported at the later occurrence and the message names the line of the first. The
  same applies to the options inside `link-refactor`.
- Rule names in `rules` are rule ids or legacy aliases such as `MD013`, compared ignoring case. The canonical id is
  stored, whatever spelling the file used, and the stored map is built from canonical ids in ordinal order, so
  reordering
  the entries never changes the result. Two entries that resolve to the same rule (by id and alias, or by letter
  case) are a conflict, reported at every occurrence after the first by source position, so the outcome depends
  only on the content of the file. The alias of a dropped rule is accepted and ignored (not stored) but still
  takes part in conflict detection. An unknown name is an error; so is the alias of a rule the formatter always
  applies. The value must be a boolean.
- `ignore` must be a list of non-blank, single-line text elements. A backslash in a pattern is converted to a
  slash, exact duplicates are removed keeping the first, and the order is preserved. A configured list replaces
  the default list. The setting is validated and stored but not applied by the engine yet.
- `definitions-marker` must be a single-line HTML comment (see *LintMarkConfig Design*).
- Anchors, aliases, and tags (including `!!str`) are rejected, because the meaning of a setting must be readable
  where it is written. Complex (non-scalar) keys are rejected. Literal and folded scalars are accepted by the
  syntax and then fail value validation where a setting cannot use them.
- Only one YAML document is allowed.
- Flow collections (`[...]` and `{...}`) may be nested at most 64 levels deep. The YAML library's running time
  grows quadratically with flow nesting, so the loader rejects deeper input before parsing rather than letting a
  small file consume unbounded time. The scan counts every opening and closing bracket, even inside quoted text
  or comments, which only matters for input that is invalid anyway.

**Guards against pathological files**: The requirements state the need as observable behavior: a file that is
implausibly large or implausibly deeply nested is reported as a problem promptly, with a position, instead of
stalling or failing the run. The numbers live here and in the user guide, not in the requirements. The size limit
is 1 MiB (no valid configuration is anywhere near it, and the reader stops a little beyond the limit, so a huge
unrelated file is never loaded). The nesting limit is 64 levels, applied by a scan before the YAML library sees the
text: measurement showed that unguarded input nested 100,000 levels deep occupied the parser for about a minute,
while the scan rejects it in milliseconds. The documented range of the numeric settings (up to 100000) is not a
guard; it is the range that the `LintMarkConfig` checks enforce so that later width calculations cannot overflow.
The system and subsystem tests for the two guards use a generous time bound (10 seconds) so that they do not depend
on the speed of the machine.

**Error catalog**: Every message is reported with the rule id `config-error` against the path of the
configuration file. Positions are 1-based. Errors about a null or empty value are reported at the **key**; every
other error about a value is reported at the **value**; errors about a name (unknown, duplicate, conflict) are
reported at the key.

| Failure class | Message | Position |
| --- | --- | --- |
| File missing | `Configuration file not found` | 1:1 |
| File not accessible | `Access to the configuration file is denied` | 1:1 |
| Other read failure | `Cannot read configuration file: {detail}` | 1:1 |
| Too large | `The configuration file is larger than 1 MiB` | 1:1 |
| Wrong byte order mark | `Unsupported byte order mark (only UTF-8 is supported)` | 1:1 |
| Invalid bytes | `Invalid UTF-8 byte sequence` | the first bad byte |
| Too deeply nested | `Nesting is too deep (more than 64 levels)` | the offending bracket |
| Invalid YAML | `Invalid YAML: {detail}` | the library's reported position |
| Invalid YAML, no detail | `Invalid YAML: the content is malformed` | the end of the last event read |
| Second document | `Only one YAML document is allowed` | the start of the second document |
| Root not a mapping | `The configuration must be a mapping of settings` | the root node |
| Anchor, alias, or tag | `Anchors, aliases and tags are not supported` | the decorated node |
| Complex key | `Complex keys are not supported` | the key |
| Unknown setting | `Unknown setting '{name}'` | the key |
| Duplicate setting | `Duplicate setting '{name}' (first defined on line {n})` | the later key |
| Null or empty value | `Setting '{name}' must not be empty` | the key |
| Collection for a scalar | `Setting '{name}' must be a single value` | the value |
| Scalar for `link-refactor` | `Setting 'link-refactor' must be a mapping of options` | the value |
| Scalar for `rules` | `Setting 'rules' must be a mapping of rule names to true or false` | the value |
| Scalar for `ignore` | `Setting 'ignore' must be a list of patterns` | the value |
| Invalid value | `Invalid value '{v}' for setting '{name}'; expected {expected}` | the value |
| Unknown rule | `Unknown rule '{name}'` | the key |
| Rule conflict | `Rule '{name}' is already configured as '{first}' on line {n}` | the later key |
| Ignore element not text | `Ignore patterns must be text` | the element |
| Ignore element blank | `Ignore patterns must not be empty or blank` | the element |
| Ignore element multi-line | `Ignore patterns must be on a single line` | the element |

The detail of `Cannot read configuration file` is `the path is a directory`, `the path is not valid`, or the
message of the underlying I/O failure. The name of a nested option in the unknown-setting, duplicate-setting,
and invalid-value messages is prefixed with its parent, for example `link-refactor.threshold`, and the name in an
invalid-value message for a rule is `rules.{name}`, with the rule name as written in the file. The `{expected}`
text of an invalid value is:

- for an enumeration: `one of: {tokens}`, with the tokens in declaration order and in lower case;
- for a boolean: `true or false without quotes`;
- for a number: `a whole number without quotes from {min} to {max}`;
- for the definitions marker: `a single-line HTML comment`.

A quoted value in a message is shortened to 40 characters followed by `...`, and its line breaks become spaces.
`list-indent` has one irregularity: the value 3 reports `expected one of: 2, 4`, and any other value outside 2
to 4 reports the number-range form.

**Reporting**: The engine, not the loader, writes errors. They appear as `display-path:line:col: config-error
message` with the display path of the configuration file (relative to the current directory with `/`
separators, possibly starting with `../`). Errors are values in `ConfigLoadResult`, never exceptions.

**YAML library caveats compensated for**: The YAML library has behaviors that a configuration reader cannot
accept, so the loader handles them (see *YamlDotNet Integration Design* in the OTS design documents):

- A leading byte order mark is not stripped and would become part of the first key; the loader decodes and strips
  it before parsing.
- Duplicate keys are not detected; the loader detects them while validating.
- Some malformed input (an unclosed flow sequence is one) raises an exception that is not a YAML exception and
  carries no position; the loader treats any exception from advancing the parser as invalid YAML and reports it
  at the end of the last event read.
- Running time is quadratic in flow nesting; the loader enforces the nesting limit above.

#### Key Methods

**ForDefaults**, **ForDiscovery**: Create a loader in the named mode.

- *Parameters*: none.
- *Returns*: `ConfigLoader`.

**ForFile**: Creates a loader that returns one configuration file for every Markdown file.

- *Parameters*: `string configFile` — relative to the current directory or absolute; both separator styles accepted.
- *Returns*: `ConfigLoader`; the file has been read once, immediately, and any problems are in `InitialErrors`.
- *Preconditions*: `configFile` is not null, empty, or blank.

**Load**: Gets the configuration that applies to a Markdown file.

- *Parameters*: `string markdownFile` — relative paths are resolved against the current directory; the file need
  not exist.
- *Returns*: `ConfigLoadResult`.
- *Preconditions*: `markdownFile` is not null or empty.
- *Postconditions*: Never throws for environment or content problems. In the defaults and explicit-file modes the
  shared fixed result is returned. In discovery mode the result is the cached result of the start directory.

Steps in discovery mode: resolve the start directory (a path that cannot be resolved yields the defaults); get or
add the lazy result for that directory; on first use, run `FindConfigFile`; with no file found the result is the
shared defaults result, otherwise it is the cached result of `LoadFile` for that file.

**FindConfigFile**: Runs the discovery walk for a directory without caching (see Discovery algorithm).

- *Parameters*: `string directory` — relative or absolute.
- *Returns*: `string?` — the full path of the nearest configuration file, or `null`.
- *Preconditions*: `directory` is not null or empty.

**LoadFile**: Reads and validates one configuration file without caching.

- *Parameters*: `string configFile` — relative or absolute.
- *Returns*: `ConfigLoadResult`; a missing, unreadable, or oversized file is a problem at 1:1.
- *Preconditions*: `configFile` is not null, empty, or blank.

Steps: normalize separators and make the path absolute (a path that cannot be resolved is reported against the
text as given); a directory in place of a file is reported; open the file for reading with sharing that tolerates
other readers, writers, and deletion; read up to the size limit plus a little; hand the bytes to `Parse`.

**Parse**: Decodes and validates the content of a configuration file; pure.

- *Parameters*: `string path` — reported in every error; `ReadOnlySpan<byte> content`.
- *Returns*: `ConfigLoadResult`.
- *Preconditions*: `path` is not null or empty.

Steps: reject content over the size limit; reject a UTF-16 or UTF-32 byte order mark; strip a UTF-8 byte order
mark; decode strictly (a failure is located by counting line feeds before the bad byte and measuring the partial
line in UTF-16 code units); delegate the text to `ConfigParser.Parse`.

**ConfigParser.Parse**: Turns decoded text into a valid configuration or a complete list of problems; pure.

- *Parameters*: `string path`, `string text`.
- *Returns*: `ConfigLoadResult`.
- *Preconditions*: `path` is not null or empty and `text` is not null.

Algorithm: (1) scan for excessive flow nesting and stop on a violation; (2) read the YAML events at event level
(no object mapping, which would hide key positions, duplicate keys, scalar styles, and null values) into a small
tree of scalar, mapping, sequence, and alias nodes, using an explicit stack so the nesting of the input cannot
exhaust the call stack; syntax errors and a second document end the parse here; (3) a missing document or a lone
null document yields the defaults; (4) a root that is not a mapping is an error; (5) validate every setting
against the schema by applying it to `LintMarkConfig.Default` with `with` expressions, collecting every semantic
error instead of stopping at the first, so one run reports all of them; (6) return the configuration when no
error was collected, otherwise the distinct errors. Syntax and structural problems stop the parse; semantic
problems never do.

#### Error Handling

Problems with the environment or the content are never exceptions; they become `ConfigError` entries in the
`ConfigLoadResult` (see the error catalog above). File access catches only the expected exception types:
file or directory not found, access denied, other I/O failures, unsupported operations, and security failures.
Path resolution catches only the exceptions that signal an invalid or too long path. The one generic catch is
around the YAML parser loop, where the library is known to raise unexpected exception types; its justification is
documented at the catch. All errors of one file are collected and returned sorted by line, column, and message
(ordinal), with exact duplicates removed.

Only argument misuse raises exceptions: `ArgumentNullException` for a null path and `ArgumentException` for an
empty or (for file names) blank path. The `ConfigLoadResult` and `ConfigError` constructors throw
`ArgumentException`, `ArgumentNullException`, or `ArgumentOutOfRangeException` for an inconsistent state, which
signals a programming error because the loader always builds consistent results.

A configuration problem is propagated to the caller as data; deciding how to report it (the `config-error` line,
the exit code) belongs to the engine.

#### Dependencies

- **LintMarkConfig**, **LinkRefactorOptions**, **RuleIdentifiers**, and the twelve setting enumerations — the
  target of validation and the vocabulary of the schema.
- **LineEnding** (`Document` subsystem) — the type behind the `end-of-line` setting.
- **YamlDotNet** (OTS) — the event-level YAML parser; see *YamlDotNet Integration Design*.
- **.NET BCL** — file access, strict UTF-8 decoding, and concurrent collections.

#### Callers

- **LintMarkEngine** — selects a loader mode from the command line, calls `Load` for each file, reads
  `InitialErrors`, and reports the errors.
