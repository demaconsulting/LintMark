### FileDiscovery

![Engine Structure](EngineView.svg)

#### Purpose

`FileDiscovery` expands the positional arguments of the command line (files, directories, globs, and
exclusions) into the list of Markdown files to process. It is the only unit that enumerates directories.

#### Data Model

`FileDiscovery` is a static class with no instance state.

**DefaultPattern**: `string` constant `**/*.md` — used when no include argument is given and for
directory arguments.

**DefaultExcludes**: `**/node_modules/**` and `**/.git/**` — added to every matcher so that excluded folders
found *beneath* a search root are pruned. Because the matcher judges patterns relative to its search root, they
cannot exclude a root that is, or lies inside, such a folder; the next constant covers that.

**ExcludedDirectoryNames**: `node_modules` and `.git` — the folder names (compared with `PathComparison`) that
make a search root excluded; see *Third-party folders* below.

**PathComparison**: `StringComparison` — `OrdinalIgnoreCase` on Windows and macOS, `Ordinal` elsewhere; it
controls glob matching and the removal of duplicates.

Argument interpretation:

- **Blank, whitespace, or only `!`**: `ArgumentException`.
- **`!pattern`**: an exclusion.
- **Existing file**: taken as given; exempt from the built-in excludes and the `.md` extension, but not from
  user exclusions. This is the **only** exemption: a file named explicitly is processed even inside
  `node_modules` or `.git`.
- **Existing directory**: every `*.md` file beneath it (`**/*.md`), minus the built-in excludes.
- **Anything else with a wildcard**: a glob (`*`, `**`; either separator; relative, parent-relative, or
  absolute).
- **Anything else without a wildcard**: passed through as a full path and reported at read time.

*Third-party folders*: files from `node_modules` and `.git` folders are never selected by directory, glob, or
default expansion, whether the folder is named by the argument or found beneath the folder searched. Before a
search root is enumerated, its path **relative to the base directory** is split into segments; when any
segment, including the last, is `node_modules` or `.git`, the root yields nothing and is not enumerated (a root
outside the base directory is split on its full path, see below). So
`node_modules`, `docs/node_modules`, `.git`, an absolute path to such a folder, and globs whose wildcard-free
prefix is, or lies inside, such a folder (`node_modules/**/*.md`, `.git/**`) select nothing, while the matcher's
`DefaultExcludes` prune such folders found beneath an ordinary root. Two consequences are deliberate:

- Segments of the base directory itself and of its ancestors are ignored, because they are not part of the
  relative path. A tool started from inside `node_modules/pkg` therefore still finds the files of its own
  directory, while a `node_modules` folder beneath it is still skipped.
- Segments that are `.` are ignored. A root outside the base directory (reached with `..`, or on another
  drive) is judged on its **full path** instead of the relative one, so climbing out of the base directory
  into or beside a third-party folder selects nothing: from inside `node_modules/pkg`, the arguments `..`,
  `../other`, and `../**/*.md` select nothing, while `../..` (a project folder above `node_modules`) still
  selects that folder's own files and skips the `node_modules` beneath it.

#### Key Methods

**Discover**: Expands the arguments.

- *Parameters*: `IReadOnlyList<string> patterns`, `string baseDirectory`.
- *Returns*: `IReadOnlyList<string>` — distinct full paths in ordinal order.
- *Preconditions*: arguments are not null.
- *Postconditions*: the result does not depend on the order of `patterns` or on enumeration order.

Algorithm: (1) split the arguments into includes and exclusions; (2) when there is no include, use the
default pattern, so only exclusions mean "everything except these"; (3) expand every include into a
candidate set (case-insensitive on Windows and macOS): for a glob, the leading wildcard-free segments name
the search root and the rest is the relative pattern; a root that has a third-party folder segment (above)
yields nothing, otherwise the globbing library is run against that root with the built-in excludes added;
(4) remove candidates matched by the user exclusions; (5) sort the result with the ordinal comparer.

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
