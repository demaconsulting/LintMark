## FileSystemGlobbing

This document describes the integration and usage design for the `FileSystemGlobbing` OTS software item
(the `Microsoft.Extensions.FileSystemGlobbing` package).

### Purpose

FileSystemGlobbing provides glob pattern matching with include and exclude patterns. `FileDiscovery` uses
it to expand the glob arguments of the command line into files, to apply the built-in exclusions, and to match the
`ignore` patterns of a configuration against a file. It is
chosen because it is a maintained library from the .NET platform team with the `*` and `**` semantics users
expect, and it avoids a hand-written pattern matcher.

### Features Used

- The `Matcher` type with include and exclude patterns, constructed with a case-sensitivity comparison
- `**` (any number of directory levels) and `*` (any characters within one path segment) wildcards
- Matching against a directory on the file system (through the directory wrapper) and against an in-memory
  list of relative paths
- An exclude pattern ending in `/**` to remove a directory at any depth

Patterns are judged relative to the directory the matcher is run against, so the library cannot exclude that
directory itself or a folder above it. `FileDiscovery` therefore checks the search root before running the
matcher (see the *FileDiscovery* design) and uses the library's excludes only for folders found beneath the root.

### Integration Pattern

FileSystemGlobbing is consumed as a NuGet package reference of the main project. `FileDiscovery` creates a
`Matcher` per search root, with the comparison that matches the platform (case-insensitive on Windows and
macOS, case-sensitive elsewhere), adds the include pattern relative to that root and the built-in excludes,
and executes it against the directory. User exclusions are matched against in-memory relative paths, and so are the
`ignore` patterns (`IsIgnored` creates a matcher with the same platform comparison, adds the patterns as includes,
and matches the path of the file relative to the directory of the configuration file). The
library has no initialization or disposal requirements.

FileSystemGlobbing has no self-validation mode. The evidence for the behaviors LintMark depends on is the
integration test project `test/OtsSoftwareTests`, which exercises recursive includes, excludes, directory
exclusion at any depth, matches that are empty, case-sensitivity control, in-memory matching, and the
single-segment wildcard.
