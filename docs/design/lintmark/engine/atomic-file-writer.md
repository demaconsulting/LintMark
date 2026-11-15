### AtomicFileWriter

![Engine Structure](EngineView.svg)

#### Purpose

`AtomicFileWriter` replaces the content of a file in one step, so that a reader or a crash never sees a partial file,
and refuses to replace a file that is no longer the one the rewrite was computed from. It is the only code of the tool
that writes a Markdown file.

#### Data Model

`AtomicFileWriter` is a static class with no state. The constant `TemporarySuffix` is `.lintmark-tmp`; the temporary
file is named `.{file name}.{guid}.lintmark-tmp` in the same directory as the target, so the final step stays on one
volume and a leftover from a crash is recognizable. Inputs of a write: the path, the resolved target captured when the
file was read (`expectedTarget`), the bytes the rewrite was computed from (`expectedOriginal`), and the new content.

#### Key Methods

**Write(path, expectedTarget, expectedOriginal, content)**: replaces the file. An internal overload adds a
`beforeReplace` hook that tests use to simulate a crash or a concurrent change. Algorithm:

1. Validate: null throws `ArgumentNullException`; a blank `expectedTarget` throws `ArgumentException`.
2. Resolve the path with `ResolveTarget` and require it to equal `expectedTarget`. The file the link points to is the
   one replaced; the link itself stays.
3. Refuse when the target does not exist, is read-only, or its bytes differ from `expectedOriginal`.
4. Create the temporary file with `FileMode.CreateNew` and `FileShare.None` (never overwrites). On Unix it is created
   owner-read-write only. Write the content and flush to disk.
5. On Unix copy the permission bits of the target to the temporary file. On Windows attributes are left to the
   replace operation of the platform.
6. Run the hook, resolve the path again and require the same target, then call `File.Replace(temporary, target)`.
7. In a `finally` block delete the temporary file, ignoring I/O failures so the original failure is reported.

**ResolveTarget(path)**: full path of the final symbolic link target, or the full path of the path itself.
**EnsureSameTarget(expected, current)**: throws `IOException` when they differ (ordinal comparison).

Postcondition: the target is either unchanged or completely replaced. The check and the replace are separate steps, so
a change made in the instant between them is not detected; closing that window would need locking or handle-based
replacement that portable code cannot rely on. Line endings and byte order mark are not handled here: the caller
passes the final bytes.

#### Error Handling

`IOException` for a changed file, a redirected link, a missing file, a cycle of links, or a failed write or replace.
`UnauthorizedAccessException` for a read-only file or an unwritable directory. Nothing is handled locally except
cleanup of the temporary file. `LintMarkEngine` converts any exception into a `file-write-error` diagnostic and
leaves the file as it was.

#### Dependencies

N/A - the unit uses only .NET file system types (`File`, `FileStream`, `FileInfo`) and no other unit or OTS item.

#### Callers

- `LintMarkEngine` (`WriteChanges`, under `--fix`), once per changed file, sequentially after all workers finished;
  `LintMarkEngine.ReadAndRewrite` calls `ResolveTarget` when it reads the file.
