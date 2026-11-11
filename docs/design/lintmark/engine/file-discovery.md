### FileDiscovery

![Engine Structure](EngineView.svg)

#### Purpose

`FileDiscovery` expands the positional arguments of the command line (files, directories, globs, and
exclusions) into the list of Markdown files to process. It is the only unit that enumerates directories.

#### Data Model

`FileDiscovery` is a static class with no instance state.

**DefaultPattern**: `string` constant `**/*.md` — used when no include argument is given and for
directory arguments.

**DefaultExcludes**: `**/node_modules/**` and `**/.git/**` — never searched by directory, glob, or default
expansion.

**PathComparison**: `StringComparison` — `OrdinalIgnoreCase` on Windows and macOS, `Ordinal` elsewhere; it
controls glob matching and the removal of duplicates.

Argument interpretation:

- **Blank, whitespace, or only `!`**: `ArgumentException`.
- **`!pattern`**: an exclusion.
- **Existing file**: taken as given; exempt from the built-in excludes and the `.md` extension, but not from
  user exclusions.
- **Existing directory**: every `*.md` file beneath it (`**/*.md`), minus the built-in excludes.
- **Anything else with a wildcard**: a glob (`*`, `**`; either separator; relative, parent-relative, or
  absolute).
- **Anything else without a wildcard**: passed through as a full path and reported at read time.

#### Key Methods

**Discover**: Expands the arguments.

- *Parameters*: `IReadOnlyList<string> patterns`, `string baseDirectory`.
- *Returns*: `IReadOnlyList<string>` — distinct full paths in ordinal order.
- *Preconditions*: arguments are not null.
- *Postconditions*: the result does not depend on the order of `patterns` or on enumeration order.

Algorithm: (1) split the arguments into includes and exclusions; (2) when there is no include, use the
default pattern, so only exclusions mean "everything except these"; (3) expand every include into a
candidate set (case-insensitive on Windows and macOS): for a glob, the leading wildcard-free segments name
the search root and the rest is the relative pattern, and the globbing library is run against that root
with the built-in excludes added; (4) remove candidates matched by the user exclusions; (5) sort the
result with the ordinal comparer.

A user exclusion is resolved like an include: its wildcard-free leading segments give a root directory
(relative to the base directory unless absolute) and the rest is a glob relative to that root. A
candidate is excluded when it lies below the root and matches the glob, so exclusions work for absolute
patterns and for files outside the base directory. An exclusion without a wildcard excludes a file, or a
directory and everything beneath it.

#### Error Handling

`ArgumentNullException` for null arguments; `ArgumentException` for blank patterns. A glob whose search
root does not exist matches nothing and is not an error. File-system errors during enumeration propagate to
the caller; existence of explicitly named files is deliberately not checked here so that read problems are
reported uniformly by `LintMarkEngine`.

#### Dependencies

- **FileSystemGlobbing** — pattern matching and directory enumeration (see the FileSystemGlobbing OTS
  design).
- **.NET BCL** — `Path`, `File`, and `Directory`.

#### Callers

- **LintMarkEngine** — discovers the files for every run.
