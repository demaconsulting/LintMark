### FileDiscovery

![Engine Structure](EngineView.svg)

#### Purpose

`FileDiscovery` expands the positional arguments of the command line (files, directories, globs, and
exclusions) into the list of Markdown files to process, remembers which files the command line named, and judges
whether the `ignore` patterns of a configuration match a file. It is the only unit that enumerates directories.

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

**Separators**: both `/` and `\` are accepted in every argument on every platform. `DiscoverFiles` converts every
backslash to a forward slash (`NormalizeSeparators`) before any file-system check, so `docs\guide.md` names the
same file on Unix as on Windows, for literal paths and directories and for exclusions as well as for globs. `IsIgnored`
converts the `ignore` patterns the same way. On
Unix a backslash is therefore never part of a file name; Windows accepts both styles, so the conversion loses
nothing there.

**DiscoveredFile**: A readonly record struct with the constructor `(string path, bool isExplicit)`, which rejects a
null or empty path: the full path of a selected file, and
whether the command line named it. A file is **explicit** when an include argument names it as an existing file or as a
path without wildcards (also when it does not exist); a file found only through a directory, a glob, or the default
pattern is not. The distinction exists because a file the user names is a deliberate request: neither the built-in
exclusion of `node_modules` and `.git` nor the `ignore` patterns of a configuration apply to it (only `!` excludes on
the command line do). A file that is both named and found, also under another spelling that differs only in case, is
explicit.

Argument interpretation:

- **Null element**: `ArgumentNullException` (parameter `patterns`), raised before any element is interpreted.
- **Blank, whitespace, or only `!`**: `ArgumentException`.
- **`!pattern`**: an exclusion.
- **Existing file**: taken as given and explicit; exempt from the built-in excludes and the `.md` extension, but not
  from user exclusions. This is the **only** exemption: a file named explicitly is processed even inside
  `node_modules` or `.git`.
- **Existing directory**: every `*.md` file beneath it (`**/*.md`), minus the built-in excludes.
- **Anything else with a wildcard**: a glob (`*`, `**`; either separator; relative, parent-relative, or
  absolute).
- **Anything else without a wildcard**: passed through as a full path (and explicit) and reported at read time.

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

**Discover**: Expands the arguments into paths.

- *Parameters*: `IReadOnlyList<string> patterns`, `string baseDirectory`.
- *Returns*: `IReadOnlyList<string>` — distinct full paths in ordinal order.
- *Preconditions*: arguments are not null.
- *Postconditions*: the paths are those of `DiscoverFiles`, in the same order.

**DiscoverFiles**: Expands the arguments into files and remembers which ones the command line named.

- *Parameters*: `IReadOnlyList<string> patterns`, `string baseDirectory`.
- *Returns*: `IReadOnlyList<DiscoveredFile>` — distinct files in ordinal order of the full path.
- *Preconditions*: arguments are not null.
- *Postconditions*: the result does not depend on the order of `patterns` or on enumeration order.

Algorithm: (1) check that no argument is null, then split the arguments into includes and exclusions, converting
separators; (2) when there is no include, use the
default pattern, so only exclusions mean "everything except these"; (3) expand every include into a
candidate set (exact strings) and a set of named paths: an existing file, or a path without wildcards, is a candidate
and is named; for a glob, the leading wildcard-free segments name
the search root and the rest is the relative pattern; a root that has a third-party folder segment (above)
yields nothing, otherwise the globbing library is run against that root with the built-in excludes added;
(4) remove candidates matched by the user exclusions; (5) group the remainder by `PathComparison`, keep
from each group its smallest-in-ordinal-order spelling, and mark the file explicit when any spelling in the group was
named; (6) sort the result with the ordinal comparer.

Step 5 matters only where names are compared without regard to case (Windows and macOS): one file can then be
found under spellings that differ only in case (an argument typed `docs/GUIDE.md` and a glob that finds
`docs/guide.md` on disk). Keeping whichever spelling arrived first would make the reported path depend on the
order of the arguments; the smallest-in-ordinal-order spelling does not. The chosen spelling is not necessarily
the one on disk, but it names the same file.

A user exclusion is resolved like an include: its wildcard-free leading segments give a root directory
(relative to the base directory unless absolute) and the rest is a glob relative to that root. A
candidate is excluded when it lies below the root and matches the glob, so exclusions work for absolute
patterns and for files outside the base directory. An exclusion without a wildcard excludes a file, or a
directory and everything beneath it.

**IsIgnored**: Judges the `ignore` patterns of a configuration against one file.

- *Parameters*: `string fullPath`; `string ignoreRoot`; `IReadOnlyList<string> patterns`.
- *Returns*: `bool`.
- *Preconditions*: no argument and no pattern is null.
- *Postconditions*: `true` exactly when the file lies below the root and its path relative to the root matches a
  pattern; no file-system access happens.

Algorithm: (1) with no patterns, return `false`; (2) make the root and the file path absolute and compute the path
of the file relative to the root; (3) a relative path that is `..`, starts with `..` and a separator, or is rooted
(another drive) is outside the root, and a file outside the root is never ignored; (4) create a `Matcher` with
`PathComparison`, add every pattern (backslashes converted to `/`) as an include, and match the relative path with `/`
separators. A pattern is therefore a glob relative to the directory of the configuration file that applies to the file
(the current directory when none does), with `*` and `**` as in the command line. The method judges the path alone;
whether the patterns are consulted at all is decided by the caller from the explicit-file rule above. The default
patterns (`.git/**`, `node_modules/**`, `**/.venv/**`, `**/generated/**`, `**/thirdparty/**`, `.agent-logs/**`) are
ordinary patterns of the configuration, not a separate mechanism.

#### Error Handling

`ArgumentNullException` for null arguments, including a null element of `patterns` or of the ignore patterns;
`ArgumentException` for blank include or exclude patterns. A glob whose search
root does not exist matches nothing and is not an error. File-system errors during enumeration propagate to
the caller; existence of explicitly named files is deliberately not checked here so that read problems are
reported uniformly by `LintMarkEngine`. `IsIgnored` has no error case beyond its arguments: a file outside the root
is simply not ignored.

#### Dependencies

- **FileSystemGlobbing** — pattern matching and directory enumeration for discovery, and the in-memory matching of
  `IsIgnored` (see the FileSystemGlobbing OTS design).
- **.NET BCL** — `Path`, `File`, and `Directory`.

#### Callers

- **LintMarkEngine** — discovers the files for every run with `DiscoverFiles`, and calls `IsIgnored` for every file
  that was not named on the command line, with the `ignore` patterns of the file's configuration.
