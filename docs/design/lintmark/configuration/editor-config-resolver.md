### EditorConfigResolver

![Configuration Structure](ConfigurationView.svg)

#### Purpose

`EditorConfigResolver` determines the line ending that the editor settings (`.editorconfig` files) specify for a
Markdown file. It reads one property only, `end_of_line`, and turns it into a `LineEnding` or "not specified".
Its supporting type, `EditorConfigResult`, is described in this document.

#### Data Model

**EditorConfigResult**: A sealed record `(LineEnding? EndOfLine, string? ErrorMessage)` with three outcomes:

| EndOfLine | ErrorMessage | Meaning |
| --- | --- | --- |
| `Lf` or `CrLf` | `null` | The editor settings specify that line ending |
| `null` | `null` | Not specified (an ordinary outcome, also available as `EditorConfigResult.NotSpecified`) |
| `null` | set | The applicable editor settings could not be read |

The constructor enforces that a value and a message are mutually exclusive, that a message is not blank, and that
a line ending is a defined member (`ArgumentException` otherwise). Line breaks in a message are replaced by spaces.
Instances are immutable.

**Interpretation of `end_of_line`**: The resolver interprets the **raw** property value itself instead of using the
library's typed accessor. The typed accessor ignores a value of `unset` or an invalid word and still returns a value
inherited from a farther `.editorconfig` file, which contradicts the EditorConfig rule that `unset` in a nearer file
removes the property. The mapping of the raw value is:

| Raw value (any letter case) | Result |
| --- | --- |
| `lf` | `LineEnding.Lf` |
| `crlf` | `LineEnding.CrLf` |
| `cr`, `unset`, any other word, or no `end_of_line` property | not specified |

"Not specified" is never an error, because editor settings are shared with other file types and often say nothing
about line endings. `cr` is understood by EditorConfig but not supported by LintMark, so it is treated like an
unknown value.

**Delegated behavior**: The EditorConfig library, not the resolver, finds the settings files and decides which
sections apply. The library provides: the nearest file wins for each property; a file with `root = true` ends the
upward search; later sections win over earlier ones within a file; and section globs follow the EditorConfig
specification. Property names and values are case-insensitive, and lines that cannot be parsed are ignored. Section
patterns are matched **case-sensitively on every platform**, including Windows, so `[*.MD]` does not match `a.md`.
The path is passed to the library exactly as discovered (made absolute and with slashes), because changing its
letter case can stop a match.

**Cache**: The resolved chain of settings files is cached per directory in a
`ConcurrentDictionary<string, Lazy<...>>` with an ordinal key comparer and `ExecutionAndPublication` thread
safety, so a directory with many Markdown files costs one search of the file system. The cached value is either the
chain or the reason it could not be read. The result for a file is deliberately **not** cached, because which
sections apply depends on the file name; each call evaluates the file against the cached chain, which does not touch
the file system again. As a consequence, edits to `.editorconfig` files made while a run is in progress are not seen
for a directory whose chain was already resolved.

**Thread safety**: One library parser is created per resolver and shared; it is safe for concurrent use. The cache
gives every concurrent caller the same chain, so results are identical to sequential use.

**Use of the result**: The engine copies a specified line ending into the effective configuration of the file, and
`DocumentFormatter` writes the file with it, so the layering of files and sections, the root file, the section
patterns, letter case, and the per-directory cache decide which line ending a file gets. Editor settings that cannot
be used (a `cr` or `unset` value, an unknown word, a missing setting, no applicable section, malformed lines) never
produce an error: the line ending is then "not specified" and the majority line ending of the file decides. A
settings file that cannot be read is reported as a message, which the engine reports as a `config-error` and which
also prevents the file from being formatted, because the line ending it should get is not known.

**Test seam**: The internal constructor takes two delegates, one that resolves the chain for a directory and one
that evaluates a file against a chain. The public constructor binds them to one shared library parser. The seam lets
a test simulate unreadable editor settings without making a real file unreadable, and count how often the chain of
a directory is looked up.

#### Key Methods

**Resolve**: Determines the line ending the editor settings specify for a Markdown file.

- *Parameters*: `string markdownFile` — relative paths are resolved against the current directory; the file need
  not exist.
- *Returns*: `EditorConfigResult`.
- *Preconditions*: `markdownFile` is not null or empty.
- *Postconditions*: Never throws for file-system or content problems.

Steps: (1) replace backslashes with slashes and make the path absolute once; a path that cannot be resolved, or has
no directory, is "not specified"; (2) get or add the lazy chain of the file's directory; if the chain could not be
read, return a result with that message; (3) evaluate the file against the chain; (4) take the raw `end_of_line`
property, if present, and map it as in the table above; (5) a failure while evaluating returns the message.

**ReadChain** (private): Resolves the chain for a directory and converts an expected failure into the message.

#### Error Handling

The only error outcome is an unreadable settings file: an I/O failure, access denied, an unsupported operation, or a
security failure while resolving the chain or evaluating the file. It is returned as
`EditorConfigResult.ErrorMessage` with the text `Cannot read .editorconfig settings: {detail}` and the line ending
stays unspecified. The engine reports it against the Markdown file at line 1, column 1 and does not format that
file. Malformed `.editorconfig` content is never an error; the library ignores bad lines and later valid sections
still apply. A failed chain is
cached for the directory like a successful one, so the failure is reported consistently for every file there.

Only argument misuse raises exceptions: `ArgumentNullException` for a null path, `ArgumentException` for an empty
path, and `ArgumentNullException` for a null delegate given to the test constructor.

#### Dependencies

- **EditorConfig** (OTS) — finds the settings files and evaluates sections; see *EditorConfig Integration Design*
  in the OTS design documents.
- **LineEnding** (`Document` subsystem) — the result type for `lf` and `crlf`.
- **.NET BCL** — path handling and concurrent collections.

#### Callers

- **LintMarkEngine** — creates one resolver per run (unless `--no-config` is given) and calls `Resolve` for each
  file whose configuration does not specify a line ending.
