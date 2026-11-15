### EditorConfigResolver

![Configuration Structure](ConfigurationView.svg)

#### Purpose

`EditorConfigResolver` determines the line ending that the editor settings (`.editorconfig` files) specify for a
Markdown file. It reads one property only, `end_of_line`, and turns it into a `LineEnding` or "not specified". Its
supporting type `EditorConfigResult` is described in this document.

#### Data Model

**EditorConfigResult** is a sealed immutable record `(LineEnding? EndOfLine, string? ErrorMessage)` with three
outcomes:

| EndOfLine | ErrorMessage | Meaning |
| --- | --- | --- |
| `Lf` or `CrLf` | null | The editor settings specify that line ending |
| null | null | Not specified; ordinary outcome, available as `EditorConfigResult.NotSpecified` |
| null | set | The applicable editor settings could not be read |

Line breaks in a message are replaced by spaces, so one problem is one line of output.

**Resolver state**: one shared library parser (safe for concurrent use) and a `ConcurrentDictionary` from directory
(ordinal) to `Lazy` values (`ExecutionAndPublication`) that hold the resolved chain of settings files for the
directory, or the reason it could not be read. The chain is cached per directory; the per-file result is not,
because sections can depend on the file name.

**Interpretation of `end_of_line`**: the resolver interprets the raw property value itself instead of the library's
typed value, because the typed value does not honor `unset` and still reports a value inherited from a farther file.

| Raw value (any letter case) | Result |
| --- | --- |
| `lf` | `LineEnding.Lf` |
| `crlf` | `LineEnding.CrLf` |
| `cr`, `unset`, any other word, or no property | not specified |

#### Key Methods

**Resolve(markdownFile)**: returns the `EditorConfigResult` for one file. The file need not exist; relative paths
are resolved against the current directory.

1. Replace backslashes with forward slashes and take the full path and its directory. A path that cannot be resolved
   (invalid, too long, not supported, or security failure) or has no directory yields "not specified".
2. Get or create the lazy chain for the directory. If reading the chain failed, return a result that carries the
   problem message `Cannot read .editorconfig settings: ...`.
3. Evaluate the file against the chain with the library parser and look up the raw `end_of_line` property. If it is
   present, interpret it (table above); otherwise return "not specified".

Finding the settings files, nearest-file precedence, `root = true`, later sections overriding earlier ones, and
section pattern matching are delegated to the library (see *EditorConfig Design*). Its section patterns are
case-sensitive on every platform, and the path is passed exactly as discovered. Malformed `.editorconfig` content
is ignored by the library and is never an error.

#### Error Handling

An unreadable settings file (`IOException`, `UnauthorizedAccessException`, `NotSupportedException`, or
`SecurityException`, during chain creation or evaluation) is handled locally and returned as the `ErrorMessage` of the
result; nothing is thrown for file-system or content problems. Other exceptions are not caught. "Not specified" is
not an error because editor settings are shared with other file types and often say nothing about line endings.
The caller (`LintMarkEngine`) turns a message into a configuration problem at line 1, column 1 of the Markdown file
and does not format that file, because its line ending is unknown.

#### Dependencies

- EditorConfig OTS library (`EditorConfigParser` and its resolved chain; see *EditorConfig Design*).
- `LineEnding` (`Document`).

#### Callers

`LintMarkEngine` creates one resolver per run (none with `--no-config`) and calls `Resolve` for each file whose
configuration has no `end-of-line` value. Safe for concurrent calls: concurrent callers share one resolution per
directory and always see the same result.
