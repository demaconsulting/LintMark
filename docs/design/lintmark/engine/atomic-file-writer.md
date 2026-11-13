### AtomicFileWriter

![Engine Structure](EngineView.svg)

#### Purpose

`AtomicFileWriter` replaces the content of a file in one step, so that a reader, or a crash, never sees a partial
file, and refuses to replace a file that is no longer the one the rewrite was computed from. It is the only code of
the tool that writes a Markdown file.

#### Data Model

`AtomicFileWriter` is a static class with one constant and no state.

**TemporarySuffix**: `.lintmark-tmp`. The temporary file is named `.{file name}.{guid}.lintmark-tmp` in the same
directory as the target, so the final step stays on one volume and a leftover from a crash is recognizable. The name
carries a unique identifier, and the file is created with `FileMode.CreateNew`, so it never overwrites an existing
file.

Inputs: the path of the file, the resolved target that the caller captured when it read the file (`expectedTarget`),
the bytes that the new content was computed from (`expectedOriginal`), and the new content (which may be empty). There
is also an internal overload with a hook called just before the final check and the replacement; it is a test seam
that simulates a crash or a concurrent change at the critical moment.

**Guarantees**:

- The target is either unchanged or completely replaced; the temporary file never outlives the call.
- A file whose bytes differ from `expectedOriginal` is never replaced: the rewrite was computed from those bytes, and
  applying it to different content would destroy someone else's change.
- A path that no longer resolves to `expectedTarget` is never written: if a symbolic link was redirected after the
  read, the file now behind the path is not the file that was read, even when it holds the same bytes. The check is
  made when the write starts and again right before the replacement, and the temporary file is created next to, and
  replaces, the one resolved target that passed the check.
- A read-only file is refused.
- A symbolic link is followed: the file it points to is replaced and the link stays.
- On Unix the temporary file is created readable and writable by its owner only, so the new content is never exposed
  to other users while it is written, and the permission bits of the target are then copied to it; permissions and
  attributes on Windows are left to the replacement operation of the platform and are not a guarantee of this unit.

**Known limit**: the checks and the replacement are separate steps, so a change made by another program in the
instant between the last check (the second resolution of the path) and the `File.Replace` call is not detected. A
redirect in that instant makes the replacement go to the file that was checked, not to the file the link points to
afterwards; the link itself is never replaced. Closing the window completely would need file locking or
handle-based replacement from the operating system, which portable code cannot rely on. Two calls for the same file
are not coordinated; the engine writes sequentially.

#### Key Methods

**Write**: Replaces the content of a file.

- *Parameters*: `string path`; `string expectedTarget`; `ReadOnlySpan<byte> expectedOriginal`;
  `ReadOnlySpan<byte> content`; the internal overload adds `Action? beforeReplace`.
- *Returns*: `void`.
- *Preconditions*: `path` and `expectedTarget` are not null or blank and the file exists.
- *Postconditions*: on return the file holds exactly `content`, with the permission bits (Unix) of the old file; on any
  exception the file is as it was and no temporary file remains.

Algorithm: (1) validate the path and the expected target; (2) resolve the final target of a symbolic link, or the
full path when it is not a link, and refuse (`IOException`: the link now points elsewhere) when it is not
`expectedTarget`; (3) refuse a missing file (`FileNotFoundException`), a read-only file
(`UnauthorizedAccessException`), and a file whose current bytes are not `expectedOriginal` (`IOException`: the file
changed after it was read); (4) write the complete content to the temporary file, in the directory of the resolved
target, with exclusive access, created through `FileStreamOptions` with `UnixCreateMode` set to owner read and write
only (ignored on Windows), and flush it to disk; (5) on Unix copy the permission bits of the target to the temporary
file; (6) call the hook, if any; (7) resolve the path again and refuse (`IOException`) when the result is not the
target of step 2; (8) replace that target with the temporary file with `File.Replace` (no backup); (9) in a `finally`
block, delete the temporary file if it still exists.

**ResolveTarget** (internal): Returns the full path of the final target of a link, or the full path of the path
itself. The caller uses it when it reads the file to capture the identity that is later passed as `expectedTarget`.

**EnsureSameTarget** (internal): Throws an `IOException` when the expected and the current resolved target differ
(ordinal comparison, so that a doubt is a refusal). It is the single comparison of the identity check, used by steps
2 and 7, and is testable on every platform without symbolic links.

**DeleteQuietly** (private): Deletes a file and swallows an `IOException` or `UnauthorizedAccessException`. It runs in
a `finally` block, where a failure to clean up must not hide the failure that is propagating; a leftover file has a
recognizable name.

#### Error Handling

`ArgumentNullException` for a null path or expected target and `ArgumentException` for a blank one. Every other
failure is an exception that leaves the target as it was: `FileNotFoundException` or another `IOException` when the
file does not exist, the link was redirected, the file has changed since it was read, or the temporary file or the
replacement fails; `UnauthorizedAccessException` when the file is read-only or the directory cannot be written. The
exceptions are not handled here: `LintMarkEngine` converts each into a `file-write-error` diagnostic for that file,
keeps the file's other diagnostics, and continues with the other files.

#### Dependencies

- **.NET BCL** — `File`, `FileStream`, `FileInfo.ResolveLinkTarget`, and `Path`. No other tool units are used.

#### Callers

- **LintMarkEngine** — captures `ResolveTarget` for each file when it reads it, and writes each rewrite that passed
  every safety check, one file after the other in path order, after every worker has finished, with the captured
  target as `expectedTarget` and the bytes it read as `expectedOriginal`; a test seam of the engine can replace the
  writer.
