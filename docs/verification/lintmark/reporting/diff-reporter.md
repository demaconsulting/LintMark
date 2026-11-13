### DiffReporter

#### Verification Approach

`DiffReporter` is verified with unit tests in `DiffReporterTests.cs`. Most tests call the internal `Format` function
with a `FileDiff` built from small LF texts and compare the returned lines with the exact expected diff, so the
format, the hunk headers, the context, the line-ending note, and the missing-newline marker are checked without any
output channel. The tests of the output channel call `Report` with a real `Context` that has a log file (the
`TemporaryDirectory` test helper provides the directory) and replace the console output with a string writer. The log
file and the captured console show which channel received the diff.

Whether a diff is correct is established by applying it, not only by comparing it with a fixed copy: the `PatchApplier`
helper applies the unified diff lines to the original text and throws when a context line or removed line does not
match, when the counts of a hunk header disagree with its body, or when a hunk starts at the wrong place. Several tests
use it. The first generates 200 pairs of texts with a small seeded random number generator, so the same
texts are produced on every platform; the lines come from an alphabet that holds repeated lines, empty lines, and
lines that look like diff syntax (including the `\ No newline at end of file` marker and a hunk header), and the
final newline is dropped in one case in four. For every pair, the patched text must equal the rewritten text and the
diff must be empty exactly when the texts are equal. The second test compares 3000 lines with 3000 completely
different lines, which is more edits than the search limit of the algorithm allows, and shows that the fallback is
fast (within 10 seconds, which is far above the expected time so that slow build agents do not fail it) and still
applies cleanly. Two more tests apply a diff that has the line-ending note, with and without a text change, to show
that the note does not disturb the application. No test double is needed, because the reporter only depends on the
`Context` and on text. The tests that replace the console are run sequentially with the other tests that touch
process-wide state.

#### Test Environment

Temporary directories are created and removed by the tests that use a `Context`. The tests restore the console
output before they finish. The generator of the random texts is deterministic, so no test depends on the time or on
the platform.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- A change is written with the headers `--- a/path` and `+++ b/path` and hunk headers of the form
  `@@ -start,count +start,count @@`, without a count of one, and with a count of zero that starts at the line before
  the range for an empty range.
- Three unchanged lines are shown on each side of a change. Changes with six or fewer unchanged lines between them
  share one hunk, and changes with seven are written as two hunks.
- Identical texts, and an LF file that stays LF, write no note; a file without any line terminator never has a note.
- A change of line endings writes the note `\ Line endings change from X to Y` before the hunks, naming CRLF, LF, or
  the counts of a mixed file, also when the text does not change.
- A missing final newline is marked on the side of the diff that lacks it, and file content that looks like a marker
  is not mistaken for one.
- The diffs of several files are written in order of path whatever the order of the input.
- The patch helper turns the diff of every generated pair, and of two very different large texts, into the
  rewritten text exactly, and the large case finishes within the time bound. A diff with a line-ending note, with or
  without a text change, applies like the same diff without the note.
- With `--silent`, nothing reaches the console and the log receives the full diff; without it, the console and the log
  both receive the full diff.
- A null list, base directory, or context throws `ArgumentNullException`, and a default `FileDiff` throws
  `ArgumentException`.
- A `FileDiff` without a path, without a text, or with an undefined line ending is rejected, and a valid one keeps its
  values.

#### Test Scenarios

##### Normal operation

**A one-line change is written as headers and one hunk**: Three lines of which the middle one changes give the two
headers, the header `@@ -1,3 +1,3 @@`, the unchanged lines as context, and the removed and added lines.

**Three lines of context are shown on each side**: Line 10 of 20 changes; the hunk header is `@@ -7,7 +7,7 @@` and
lines 7 to 9 and 11 to 13 are shown around the change.

**Distant changes are written as separate hunks**: Lines 3 and 15 of 20 change with eleven unchanged lines between
them; two hunks (`@@ -1,6 +1,6 @@` and `@@ -12,7 +12,7 @@`) are written, each with its own context.

**Nearby changes share one hunk**: Lines 3 and 10 of 20 change with exactly six unchanged lines between them; one
hunk `@@ -1,13 +1,13 @@` spans from the context of the first change to the context of the second.

**A change at the start is described from the start of the file**: A line added in front of four lines gives the
header `@@ -1,3 +1,4 @@`, the added line first, and three lines of trailing context.

**A change at the end is described with leading context only**: The last of five lines is removed; the header is
`@@ -2,4 +2,3 @@` with three lines of leading context and the removal.

**A line-ending change alone writes a note and no hunk**: Equal texts with two CRLF terminators and an LF target give
the headers and the line `\ Line endings change from CRLF to LF`; two LF terminators and a CRLF target give the line
`\ Line endings change from LF to CRLF`.

**A mixed file names both counts in the note**: Two CRLF and one LF terminator with an LF target give the note
`\ Line endings change from mixed (2 CRLF, 1 LF) to LF`.

**A line-ending change and a text change write the note before the hunk**: A CRLF file whose text also changes gives
the headers, the note, and then the hunk.

**A missing final newline is marked**: The final line that loses its terminator is followed by the marker
`\ No newline at end of file` after the added line.

**An added final newline is marked on the old line**: The final line that gains a terminator has the marker directly
after the removed line.

**Several files are written in order of path**: Three changed files given out of order, one in a folder, are written
with the old-file headers in the order `a.md`, `m/b.md`, `z.md`.

**A silent context keeps the diff in the log**: One changed file is reported to a silent context; nothing reaches the
console, and the log holds the complete diff, `--- a/a.md`, `+++ b/a.md`, `@@ -1 +1 @@`, `-a`, and `+b`.

**A context that is not silent writes the diff to the console and the log**: The same changed file is reported to a
context that is not silent; the console and the log both hold the complete diff.

**A diff with a line-ending note applies cleanly**: A CRLF file whose text also changes is formatted and applied with
the patch helper; the note is ignored and the result is the new text. A diff of a line-ending change alone applies as
no change to the text.

**Random edits give a diff that applies cleanly**: 200 generated pairs, with empty texts, repeated lines, a dropped
final newline, and lines that look like diff syntax, are formatted and applied with the patch helper; every patched
text equals the rewritten text, and every diff is empty exactly when the texts are equal.

##### Boundary conditions

**A range of one line is written without a count**: A one-line replacement gives `@@ -1 +1 @@`, an added second line
gives `@@ -1 +1,2 @@`, and a removed second line gives `@@ -1,2 +1 @@`.

**An insertion into an empty file uses a zero count**: An empty file that gains two lines gives `@@ -0,0 +1,2 @@`, so
the empty old range starts at the line before it.

**Changes seven lines apart stay two hunks**: Lines 3 and 11 of 20 change with seven unchanged lines between them; the
hunk headers are `@@ -1,6 +1,6 @@` and `@@ -8,7 +8,7 @@`.

**Identical text writes nothing**: The same text before and after, with matching line endings, returns no lines, not
even the headers.

**An LF file that stays LF has no note**: A text change in an LF file with an LF target has a hunk and no
line-ending note.

**A file without terminators has no note**: A one-line file without a line terminator, with either an LF or a CRLF
target, shows the text change and no note.

**Very different large texts still apply cleanly**: 3000 lines compared with 3000 completely different lines give a
single hunk `@@ -1,3000 +1,3000 @@`, are formatted within 10 seconds, and the patch helper reproduces the new text.

**Content that looks like a marker is not a marker**: A line equal to the missing-newline marker inside the context
of a change is written as a context line behind a space, and the patch reproduces the new text.

##### Error handling

**A default file diff is refused**: A list that holds a default `FileDiff` throws `ArgumentException`.

**Null arguments are refused**: A null list, a null base directory, and a null context each throw
`ArgumentNullException`.

**A file diff without a path or text is rejected**: A null path throws `ArgumentNullException` and an empty path
throws `ArgumentException`; a null before text and a null after text each throw `ArgumentNullException`. A valid file
diff keeps its path, both texts, and its target line ending.

**A file diff with an undefined line ending is rejected**: A target line ending that is not a defined value throws
`ArgumentOutOfRangeException`.
