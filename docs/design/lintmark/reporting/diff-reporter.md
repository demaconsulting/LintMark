### DiffReporter

![Reporting Structure](ReportingView.svg)

#### Purpose

`DiffReporter` writes what the fix option would change as a unified diff, so a person (or a patch tool) can see the
proposed rewrite of every file before anything is written. Its supporting type `FileDiff` is described in this
document.

#### Data Model

**FileDiff**: A readonly record struct with the constructor `(string path, string beforeLf, string afterLf,
LineEndingInfo originalEndings, LineEnding targetEnding)`, which rejects a null or empty path, a null text, and a line
ending that is not defined: the full path of the file, the LF-normalized text as it is on disk, the LF-normalized text
after rewriting, the counts of the line terminators in the file as it is on disk, and the line ending the file would
be written with. Both texts are LF-normalized, so a change of line endings alone leaves them equal; the original
counts and the target let the reporter say so. Immutable; a default instance is rejected.

`DiffReporter` is a static class with no state and three constants:

- `ContextLines` (3): the number of unchanged lines shown around each change.
- `MaxEditDistance` (2000): the largest edit distance the Myers search tries before it gives up.
- a private enumeration of the kinds of line in a script: equal, delete, and insert.

**Output format**: For each changed file, in display-path order:

```text
--- a/path
+++ b/path
\ Line endings change from CRLF to LF
@@ -3 +3 @@
-old line
+new line
```

- The two header lines use the display path (relative to the base directory with `/` separators).
- The note line `\ Line endings change from X to Y` follows the headers when writing the file would change a line
  ending; `X` is `CRLF`, `LF`, or `mixed (n CRLF, m LF)` and `Y` is the target. It uses the comment-line convention of
  the no-newline marker, so a person sees why a file with no visible text change is rewritten. It is shown when the
  target is LF and the file has a CRLF terminator, or the target is CRLF and the file has an LF terminator.
- Hunks start with `@@ -start,count +start,count @@` and show three lines of context on each side. A count of one is
  written without the comma, and an empty range is written with a count of zero and the number of the line before it,
  as GNU diff does. Changes that are six or fewer unchanged lines apart share one hunk.
- Lines are prefixed with a space (unchanged), `-` (removed), or `+` (added). A last line without a line ending is
  followed by `\ No newline at end of file`, so adding or removing the final newline is a visible change.
- A file with neither a text change nor a line-ending change writes nothing.

#### Key Methods

**Report**: Writes the diffs of the changed files to standard output.

- *Parameters*: `IEnumerable<FileDiff> diffs`; `string baseDirectory`; `Context context`.
- *Returns*: `void`.
- *Preconditions*: no argument is null and every element is initialized.
- *Postconditions*: the lines of every changed file have been written with `Context.WriteLine`, files in ordinal
  order of the display path; the exit code is unchanged.

**Format** (internal): Formats the diff of one file.

- *Parameters*: `FileDiff diff`; `string displayPath`.
- *Returns*: the lines of the diff; empty when nothing would change.

Steps: (1) decide whether a line ending changes and whether the text changes; (2) with neither, return no lines;
(3) write the two header lines and, when a line ending changes, the note line; (4) when the text changes, compare the
two texts line by line and group the result into hunks.

**SplitLines** (private): Splits LF-normalized text into lines that keep their terminator, so a missing final line
ending is a difference; empty text has no lines.

**Compare** (private): Computes the script of equal, deleted, and inserted lines. The common head and the common tail
are kept out of the search. The middle is searched with the Myers algorithm; when the search does not finish within
the edit distance limit, the middle is shown as all of its original lines removed and all of its new lines added.
That fallback is still a correct diff that applies cleanly, though not a minimal one, and it keeps the cost bounded
for two files that are completely different.

**Myers** (private): The greedy shortest-edit-script algorithm on whole lines. For each number of edits `d`, from 0 up
to the limit, it extends the furthest-reaching point on every diagonal `k` from `-d` to `d` in steps of two: it starts
from the better of the two neighboring diagonals and follows the run of equal lines (the "snake"). It remembers the
furthest-reaching values of each step. When a path reaches the end of both texts, **WalkBack** reconstructs the script
from the remembered values, from the end to the start: for each step it finds the diagonal the last edit came from,
emits the equal lines that followed it, and emits the edit (a move down is an insertion, a move right a deletion).
It returns `null` when the edit distance exceeds the limit. Trivial cases (one side empty) are handled without a
search.

**AddHunks** and **AddHunk** (private): Number the lines of the script, find the positions of the changes, split them
into hunks wherever more than twice the context lies between two changes, and write each hunk with its header. The
**Range** helper formats the range of a header.

#### Error Handling

`ArgumentNullException` for a null argument and `ArgumentException` for a default `FileDiff` element, raised before
anything is written. A `FileDiff` that is created through its constructor cannot lack a path, a text, or a defined line
ending: the constructor throws `ArgumentNullException`, `ArgumentException`, or `ArgumentOutOfRangeException`. The
diff is for display only, so there is no failure that changes whether a file is written. The
algorithm has no failure case of its own: the limit on the edit distance turns an expensive search into the fallback
rather than an error.

#### Dependencies

- **FileDiff** (this unit) — the input.
- **TextReporter** (`Reporting` subsystem) — the display path function.
- **Context** (`Cli` subsystem) — the receiver; `WriteLine` writes to standard output unless `--silent` is given and
  always to the log file.
- **LineEnding** and **LineEndingInfo** (`Document` subsystem) — the line-ending note.
- **.NET BCL** — strings, arrays, and lists. No OTS library is used for the diff.

#### Callers

- **LintMarkEngine** — reports the diffs of the proposed rewrites when the `--diff` option is given, after the
  findings have been reported.
