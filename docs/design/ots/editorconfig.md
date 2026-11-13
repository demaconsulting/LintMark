## EditorConfig

This document describes the integration and usage design for the `EditorConfig` OTS software item (the
`editorconfig` package, namespace `EditorConfig.Core`).

### Purpose

EditorConfig is the library that finds and evaluates `.editorconfig` files. `EditorConfigResolver` uses it to learn
which line ending the editor settings specify for a Markdown file, so that a repository that already states its
line endings for editors does not have to repeat them in `.lintmark.yaml`. It is chosen because it implements the
EditorConfig specification (file discovery, `root = true`, section globs, and precedence), which avoids a
hand-written glob and precedence engine.

### Features Used

- `EditorConfigParser.GetResolvedChainForDirectory`, which finds the `.editorconfig` files that apply to a
  directory, up to a file that sets `root = true`, and returns the resolved chain of their sections
- `EditorConfigParser.Parse` with a file path and a resolved chain, which evaluates the sections that match that file
- The raw `Properties` dictionary of the resulting file configuration, from which only `end_of_line` is read
- Library-provided behavior that the resolver relies on and does not reimplement: nearest file wins for each
  property; later sections win over earlier ones; section globs (`*`, `**`, `?`, `[seq]`, `{a,b}`, numeric ranges,
  and path-qualified patterns); case-insensitive property names and values; and silently ignored malformed lines

Not used: the typed properties of the file configuration (notably the typed line-ending accessor), any property
other than `end_of_line`, and the library's file-writing or command-line facilities.

Behaviors of the library that the design depends on and compensates for (each is pinned by the integration tests in
`test/OtsSoftwareTests`):

- The typed line-ending accessor is unreliable: for a value of `unset` or an invalid word it still returns a value
  inherited from a farther file. `EditorConfigResolver` therefore interprets the raw property.
- Section patterns are matched case-sensitively on every platform, including Windows. The resolver documents this and
  passes the path exactly as discovered, because changing the case of a path can stop a match.
- Parsing is lenient: lines that cannot be parsed are ignored and later valid sections still apply, so malformed
  content is never an error.
- The raw value `unset` is reported as the word `unset`, and `cr` is a valid typed value that LintMark does not
  support; both are treated as "not specified".
- A file or directory that does not exist can still be resolved.

### Integration Pattern

EditorConfig is consumed as a NuGet package reference of the main project and of the OTS integration test project.
The package brings a transitive dependency on the file-system abstraction package `System.IO.Abstractions`, through
which the library reads files; LintMark does not use that package directly. `EditorConfigResolver` creates one
`EditorConfigParser` per resolver instance and shares it between all threads; the parser is safe for concurrent use.
The resolver caches the resolved chain per directory and calls `Parse` for each file, so the file system is searched
once per directory. No initialization, configuration file, or disposal is required. I/O failures from the library
are converted into an error message of the resolver result; the engine reports it as a `config-error` against the
Markdown file and does not format that file.

EditorConfig has no self-validation mode. The evidence for the behaviors LintMark depends on is the integration
test project `test/OtsSoftwareTests`, which exercises the library directly: glob patterns, case sensitivity of
section patterns, case-insensitivity of property names and values, later-section and nearer-file precedence,
`root = true`, evaluation with a resolved chain compared with a direct parse, the raw `unset` value, the unreliable
typed accessor, `cr`, nonexistent files, malformed lines, and parallel parsing with a shared parser, with one
resolved chain shared by many threads, and with a parser per thread.

The line ending that `EditorConfigResolver` reads is used by the tool: the engine copies it into the effective
configuration of the file and `DocumentFormatter` writes the file with it. The raw property text (including `unset`),
the leniency toward malformed lines, and concurrent use decide whether editor settings that cannot be used are
tolerated. The remaining behaviors above (section patterns and their letter case, layering, the root file,
evaluation with a resolved chain, the typed accessor, and nonexistent files) decide which line ending a file gets.
