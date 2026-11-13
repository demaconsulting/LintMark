#### CodeBlockFormatter

##### Verification Approach

`CodeBlockFormatter` is verified with unit tests in `CodeBlockFormatterTests.cs`. The real `DocumentParser`,
`SourceDocument`, `LintMarkConfig`, `EditSet`, and `SourcePatcher` are used; nothing is replaced by a double. A helper of
the test project (`BlockFormatterAssertions`) applies the formatter alone, without the document passes, patches the
text, and asserts that the result is the expected text, that it renders to the same HTML as the input, that it keeps its
Pandoc structure, and that the formatter has nothing more to do with the result. Most scenarios are theories with
several inputs, and the fence character is varied through the configuration. Suppression is driven by real suppression
comments. The test for concurrent use parses one document once and shares it, with one formatter instance, among eight
threads that start together at a barrier; each thread formats it many times into its own edit set, and every result is
compared with the result of a single call. Two tests build a damaged block by hand (the reported end of a fenced block
moved to a content line, and an indented block that reaches an unindented line) to reach the guards for lines that are not
what the parser reported.

##### Test Environment

N/A - standard test environment.

##### Acceptance Criteria

- All unit tests pass with zero failures.
- A closed fenced code block and an indented code block are accepted and nothing else, in particular not a fence that is
  never closed; null arguments and blocks that are not accepted are rejected; the list of rules is immutable.
- The fence character follows the configuration, the new fence is long enough that no content line can close it, and a
  fence is not converted when that would change the block (a backtick in the info string for a backtick fence, a braced
  info string).
- Fences in a quote or list, and a top-level fence indented by one to three columns, are converted in place.
- A top-level indented block becomes a fenced block with the same content, including what is left of a whitespace-only
  line wider than the indentation at its end, and one in a list or quote, or one whose fenced form would create front
  matter, stays as written.
- Trailing whitespace is removed from the fence lines only, and the content of a fence is never changed.
- Each edit is attributed to the rule that it enforces, and a canonical block produces no edit.
- A block in which a rule is suppressed is left as written.
- CRLF line endings, mixed line endings, and a byte order mark do not change the edits, and one shared parsed document
  gives the same result on many threads.

##### Test Scenarios

###### Normal operation

**A tilde fence becomes a backtick fence**: Tilde fences with no info string, with an info string, with four tildes, and
with a closing fence that is longer than the opening one are formatted. Each becomes a backtick fence of the same
lengths, with the info string kept.

**A fence of the configured character is left alone**: Backtick fences, with and without an info string and with four
backticks, are formatted with the default configuration. No edit results.

**The tilde configuration writes tilde fences**: Backtick fences, with an info string, and one that contains a tilde
fence are formatted with `code-fence: tilde`. They become tilde fences, longer where the content needs it.

**A new fence is longer than its content needs**: A tilde fence whose content holds backtick fences of three and four
backticks, or two backticks, is formatted. The new fence has one more backtick than the longest run, and at least three.

**Fence lines lose trailing whitespace, content lines keep it**: A fence with trailing spaces on both fence lines and on
a content line is formatted. Only the fence lines change.

**A fence in a container is converted in place**: Tilde fences in a list item, a quote, an ordered list item with an info
string, and a list in a quote are formatted, and so are a top-level fence indented by one to three columns, fences in a
quote or list with trailing whitespace, and a fence whose closing fence is longer than the opening one around content
that looks like a fence. Each is converted, its prefix and indentation stay, and the fence lines lose trailing
whitespace.

**An indented block becomes a fenced block**: Indented blocks of one and several lines, with a tab, with a blank line
inside, with a line of extra indentation, with trailing spaces, and after a heading are formatted. Each becomes a fenced
block that has the content of the block with four columns of indentation removed. A line of fewer than four columns
between two lines of code (one or two spaces, a space, a tab) becomes an empty line of the code.

**Blank lines after an indented block stay outside the fence**: An indented block followed by blank lines and a
paragraph is formatted. The fence ends after the last line of code and the blank lines follow it.

**A wide whitespace-only line that ends an indented block stays in the fence**: An indented block that ends with a line
of six or five spaces, or of a tab and two spaces, with or without blank lines and text after it, and one with such lines
before an exactly four columns wide line, is formatted. What is left of the wide line after four columns is the last
line of the fenced block (an exactly four columns wide line is dropped), and the result renders the same, with the
production formatters and with the formatter alone.

**An indented block that holds fence lookalikes gets a longer fence**: An indented block whose lines start with three and
four backticks is formatted. The new fence is longer than any of them.

**An indented block follows the configured fence character**: An indented block is formatted with `code-fence: tilde`.
It becomes a tilde fence.

**Each edit is attributed to its rule**: A tilde fence with trailing whitespace is formatted and the edits are credited
to `code-fence` and `trailing-whitespace`; an indented block is formatted and its edit is credited to `code-block-style`.

###### Boundary conditions

**A fence whose info string has a backtick stays a tilde fence**: A tilde fence with a backtick in its info string is
formatted with the default configuration. It is unchanged.

**A braced info string keeps its fence character**: Tilde fences with a braced attribute list or a raw format, and a
backtick fence with a braced info string under the tilde configuration, are formatted. They are unchanged.

**An indented block in a list item stays indented**: An indented code block in a list item is formatted. It is unchanged.

**A dashes line in an indented block can keep it indented**: An indented block with a dashes line after a first line of
dashes is formatted. It stays indented, because the fenced form would close front matter that the document never had. An
indented block with such a line after real front matter is converted.

**The content of a fence is never changed**: Fences whose content looks like headings, lists, tables, tabs, HTML, many
blank lines, or a dashes line are formatted. Their content is unchanged.

**A fence that is never closed is not touched**: An unclosed fence is formatted. No edit results.

**A block with an unexpected shape is not touched (defensive, no requirement)**: A fenced block whose reported end is
moved to a content line, one whose reported end is its opening line, and an indented block that reaches an unindented
line are formatted. No edit results.

**A suppressed rule leaves the block alone**: A comment that disables `code-fence`, `MD048`, `MD046`, `MD009`, or all rules
precedes a block that breaks that rule. The block is unchanged.

**CRLF line endings and a byte order mark do not change the edits**: A document parsed from bytes with CRLF line endings,
one with mixed line endings, and one with a byte order mark give the same edits as the LF text (separate tests for the
line endings and for the mark), and the formatted bytes keep the endings and the mark.

###### Error handling

**A missing block, context, or edit set is rejected**: `CanFormat` with a null block, and `Format` with each null
argument, throw `ArgumentNullException`.

**A block that is not accepted is rejected**: `Format` for a fence that is never closed and for a paragraph throws
`ArgumentException`.

**The rule list is immutable**: The `RuleIds` property lists the three code block rules, and a cast to a mutable type
cannot change it.

**One shared instance gives one result on many threads**: One formatter and one parsed document are used by eight threads
that start together; each formats it many times into its own edit set. Every result equals the result of a single call.
