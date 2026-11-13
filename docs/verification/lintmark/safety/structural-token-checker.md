### StructuralTokenChecker

#### Verification Approach

`StructuralTokenChecker` is verified with unit tests in `StructuralTokenCheckerTests.cs`. The unit is a static class
with no state and no dependency other than the shared `EquivalenceResult` type, so the tests call `Check` directly
with small fixed texts that hold fenced div lines, raw page break commands, display math lines, grid table borders,
code fences with braces, attribute groups, and citations, and assert on the verdict and on the exact description.
Pairs that damage the structure (joined lines, moved lines, changed borders, reordered fences, a lost, added, or
duplicated attribute group or citation) must be refused. Pairs that only change indentation, block quote prefixes, or
trailing blanks, or that contain no structure at all, must pass. The concurrency test calls `Check` from up to sixteen
threads and compares every result with the single-threaded result. The failure test calls the overload of `Check`
that takes the function that collects the line tokens, and passes one that throws, to show that a comparison that
cannot be completed is reported as a difference; this overload is a seam for tests. No test double is used otherwise,
because the behavior under test is a pure function of the two texts.

#### Test Environment

N/A - standard test environment. The tests use only in-memory text.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Joined fence lines, a fence line moved into a paragraph, a joined page break command, a joined display math
  delimiter, a changed grid table border, reordered fences, and a joined code fence with braces are different.
- A lost, added, or duplicated attribute group or citation is different, and the description names the text with
  the counts before and after.
- Changes of indentation, of block quote prefixes, and of trailing blanks on a structural line are equivalent, as
  are a list that is re-indented around a fence, text without any structure, unchanged structure inside code, and
  identical text.
- The description names the position and both versions of the first structural line that differs, with `(none)` for
  a line that is missing on one side.
- Concurrent calls give the same results as sequential calls.
- A carriage return in either text throws `ArgumentException`, and a null text or a null collection function throws
  `ArgumentNullException`.
- A collection function that throws gives a difference that names the exception type.

#### Test Scenarios

##### Normal operation

**Indentation alone is not a difference**: A fenced div compared with the same div whose fence lines are indented
is equivalent.

**Re-indenting a list item that holds a fenced div is not a difference**: A list item with a div indented by two
columns is compared with one indented by four columns; the verdict is equivalent.

**Text without structure is equivalent after any reflow**: Two lines of plain prose joined into one line are
equivalent, because there is nothing structural to compare.

**Structure inside code that stays as it is keeps the texts equivalent**: A code block that holds a fence line,
followed by a paragraph that loses its trailing blanks, is equivalent.

**Block quote prefixes do not change a token**: A div inside a block quote compared with the same div without the
quote markers is equivalent.

**Trailing blanks on a structural line are not a difference**: A fence line with trailing blanks compared with the
same line without them is equivalent.

**Identical text is equivalent**: The same text twice, with a div, an attribute group, and a citation, is equivalent
with no description.

**Concurrent calls give the same result**: One equivalent and one different pair are checked sequentially, then 64
times from up to sixteen threads; every concurrent result equals the sequential one.

##### Boundary conditions

**Joined fence lines are a difference**: A fenced div of three lines compared with the same words joined into one
line is different, and a description is given.

**A fence line moved into a paragraph is a difference**: A closing fence on its own line compared with the same fence
appended to the paragraph is different.

**A joined page break command is a difference**: A raw page break command on its own line compared with the command
inside a line of prose is different.

**A joined display math delimiter is a difference**: Display math with delimiter lines compared with the same math in
one line is different.

**A changed grid table border is a difference**: A grid table compared with the same table with shorter borders is
different, and the description contains the original border line.

**Reordered fences are a difference**: Two fence lines swapped are different, because the sequence is compared.

**A joined code fence with braces is a difference**: A code fence with a braced attribute compared with the same text
in one line is different, and the description contains the fence line.

**A lost attribute group is a difference**: A heading with an identifier compared with the same heading without it
gives `Attribute group "{#sec}" occurs 1 time(s) before and 0 after`.

**An added attribute group is a difference**: The reverse pair gives the description
`Attribute group "{#sec}" occurs 0 time(s) before and 1 after`.

**A duplicated citation is a difference**: One citation compared with the same citation twice gives the description
`Citation "[@key]" occurs 1 time(s) before and 2 after`.

**A lost citation is a difference**: A sentence with a citation compared with the sentence without it gives
`Citation "[@key]" occurs 1 time(s) before and 0 after`.

**The description names the first differing structural line**: Two texts whose second fence line differs give
`Pandoc structural line 2 differs: before ":::b" after ":::c"`.

**A structural line on one side only is described as none**: A fence line that disappeared gives a description with
`after (none)`, and one that appeared gives a description with `before (none)`.

##### Error handling

**A carriage return is rejected in either text**: A carriage return and line feed pair in the first text, and in the
second text, each throw `ArgumentException`.

**Null texts are rejected**: A null first text and a null second text each throw `ArgumentNullException`.

**A token collection that throws is reported as a difference**: Two different texts are compared with a collection
function that throws `InvalidOperationException`; the verdict is not equivalent, and the description says that the
structure could not be compared and names the exception type.

**A missing token collection is rejected**: A null collection function throws `ArgumentNullException`.
