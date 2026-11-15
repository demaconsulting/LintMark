### FileDiscovery

![Engine Structure](EngineView.svg)

#### Purpose

`FileDiscovery` expands the positional path arguments of the command line into the Markdown files to process, and
decides whether the `ignore` patterns of a configuration skip a file. It is the only unit that enumerates directories.
Its supporting type `DiscoveredFile` is described in this document.

#### Data Model

`FileDiscovery` is a static, stateless class. `DefaultPattern` is `**/*.md`. `PathComparison` is ordinal ignoring
case on Windows and macOS, ordinal elsewhere. Built-in excluded directory names are `node_modules` and `.git`.

**DiscoveredFile**: readonly record struct `(string Path, bool IsExplicit)`. `Path` is a full path. `IsExplicit` is
true when an include argument named the file (an existing file, or a path without wildcards, even if missing). Files
found through a directory, glob, or the default pattern are not explicit.

#### Key Methods

**DiscoverFiles(patterns, baseDirectory)**: takes `IReadOnlyList<string>` and a string; returns
`IReadOnlyList<DiscoveredFile>`.

1. Validate: null arguments throw `ArgumentNullException`; a blank pattern (after removing a leading `!`) throws
   `ArgumentException`. Backslashes are converted to `/` in every argument on every platform.
2. Split into excludes (`!` prefix) and includes. With no include, the default pattern is used.
3. Expand each include: an existing file is taken as given (and marked explicit); an existing directory is searched
   with `**/*.md`; a path without wildcards that does not exist is passed through (explicit) so that the read failure
   is reported uniformly; otherwise it is a glob whose leading wildcard-free segments give the search root.
4. A search (`Matcher` from FileSystemGlobbing) yields nothing when the root itself lies inside `node_modules` or
   `.git`, judged relative to the base directory (full path when outside it), and prunes those folders below the root.
   Explicitly named files are exempt from this built-in exclusion.
5. Apply user excludes to all candidates, explicit files included. An exclude is split like an include into a root and
   a glob; without wildcards it names a file, or a directory and everything below it. A candidate outside the root
   never matches.
6. Group by path under `PathComparison`, keep the smallest ordinal spelling, mark the group explicit if any spelling
   was named, and sort by ordinal path order.

The search itself (`CycleSafeDirectory`, private) hands the matcher a `DirectoryInfoWrapper` subclass that keeps the
chain of real paths from the file system root down to the current directory (the folders above the search root
included). A directory link or junction whose final target is in that chain is a cycle and is skipped, so a walk ends
after one lap at the latest; a link to any other folder is followed, and files are always returned. A link that cannot
be resolved is skipped, and a directory that cannot be read (`IOException`, `UnauthorizedAccessException` during
enumeration) contributes nothing, instead of the failure escaping from the middle of the enumeration.

**IsIgnored(fullPath, ignoreRoot, patterns)**: returns true when the file lies
below `ignoreRoot` and its relative path matches an `ignore` glob. Empty patterns, or a file outside the root (`..` or
another drive), give false. Pure; no file system access. The caller (`LintMarkEngine`) consults it only for files that
are not explicit.

**Discover**: same as `DiscoverFiles` but returns only the paths; used by tests.

#### Error Handling

Argument errors throw as described above and reach `Program` as usage errors. A pattern that matches nothing is not
an error. An unreadable directory below the search root, or a link that cannot be resolved, is skipped rather than
reported. A missing named file is not detected here; it
becomes a read-error diagnostic in `LintMarkEngine`.

#### Dependencies

- **FileSystemGlobbing** OTS (`Matcher`, `DirectoryInfoWrapper`); see the OTS design *FileSystemGlobbing*.
- The .NET `Path`, `File`, and `Directory` classes.

#### Callers

- `LintMarkEngine.Run` calls `DiscoverFiles`; `LintMarkEngine.ProcessFile` calls `IsIgnored`.
