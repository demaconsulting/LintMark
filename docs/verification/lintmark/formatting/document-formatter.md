### DocumentFormatter

#### Verification Approach

`DocumentFormatter` is verified with unit tests in `DocumentFormatterTests.cs`. The real `DocumentParser` and real
`SourceDocument` produce the documents, the real `LintMarkConfig` supplies the settings, and the real `SourcePatcher`
applies the edits so that a test can assert on the patched text. Only the block formatters are test doubles, and they
come from `TestBlockFormatters.cs`:

- `ReplacingFormatter` is a simple, correct paragraph formatter registered under the `paragraph-wrap` rule. A private
  helper builds a `DocumentFormatter` with one that replaces a double space by a single space; other tests use
  two of them with different replacements to prove which one is asked.
- `OverlappingEditsFormatter` is a deliberately broken formatter that proposes two edits of the same characters.

Two parse helpers are used: one parses LF-normalized text, and one parses the bytes of a file so that its original
line endings are counted. Suppression is driven by real suppression comments in the Markdown text. The test for
parallel calls shares one formatter and one document among 64 calls that run with up to 16 threads, and compares
every result with the result of a single call.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Without block formatters, or for a block that no formatter accepts, no edit is proposed.
- The edits of an accepting formatter are collected and ordered, and only the first accepting formatter is used for a
  block.
- A block is left verbatim when its formatter's rule is disabled by a region, when all rules are disabled, when a
  prettier-ignore comment precedes it, when a disable-next-line comment precedes it, or when a suppression comment lies
  inside the block; other blocks are still formatted, and the suppression of another rule has no effect.
- A document that is unsafe to rewrite yields no edit and no problem, even when its line endings differ from the
  configuration, and its line ending is the majority ending of the file.
- The configured line ending wins; without one the majority ending of the file is the target, and LF is the target
  when the endings are equally frequent or the file has no terminator.
- Exactly one `line-ending` formatting problem at line 1 and column 1 is reported when any line terminator differs
  from the target, with a message that names the endings found and the one expected; nothing is reported otherwise.
- Conflicting edits of a formatter make formatting throw `InvalidOperationException`.
- An exception that a block formatter throws reaches the caller with its type and message unchanged.
- Null arguments are rejected, a null list means no formatters, and later changes of the caller's list are ignored.
- A format context without a document or configuration, and a format result with a default list, a default edit, a
  null finding, or an undefined line ending, are rejected; valid ones keep their values.
- Parallel calls give the same result as a single call.

#### Test Scenarios

##### Normal operation

**A formatter without any block formatters changes nothing**: A document with a double space is formatted by a
`DocumentFormatter` that has no block formatters. The result has no edit and no problem, and the line ending is LF.

**The edits of an accepting formatter are collected**: A paragraph `a  b  c` is formatted with the double-space
formatter. The result holds two edits at offsets 1 and 4, ordered, both under the rule `paragraph-wrap`, and the
patched text is `a b c`.

**Only the first accepting formatter is used**: Two `ReplacingFormatter` doubles that both accept the paragraph, one
replacing the double space by an underscore and one by a single space, are registered in that order. The result holds
two edits, and the patched text is `a_b_c`, so the second formatter was never asked.

**A block with a suppressed rule is left verbatim**: Three paragraphs are formatted, of which the middle one lies in a
region that disables `paragraph-wrap`. The middle paragraph keeps its double space, and the first and last
paragraphs are formatted.

**A region that disables all rules leaves its block verbatim**: A paragraph inside a region that disables every rule,
and one after the region, are formatted. Only the paragraph after the region is changed.

**A prettier-ignore comment leaves the next block verbatim**: A prettier-ignore comment precedes the first of two
paragraphs. The first keeps its double space and the second is formatted.

**A suppression comment inside a block leaves the whole block verbatim**: A paragraph with three double spaces holds
an inline comment that disables `paragraph-wrap` after its first part and an inline comment that enables it again
before its last part. A paragraph follows it. The first paragraph keeps all its double spaces, and the later
paragraph is formatted.

**A disable-next-line comment leaves the block on the next line verbatim**: A comment that disables `paragraph-wrap`
for the next line directly precedes the first of two paragraphs. The first keeps its double space and the second is
formatted.

**Suppressing one block does not stop other blocks**: A prettier-ignore comment lies between two paragraphs, and a
paragraph comes before and after it. The result holds exactly two edits, for the first and the last paragraph, and the
ignored paragraph is unchanged.

**Suppressing another rule does not stop the formatter**: A paragraph lies inside a region that disables
`line-length` only. The paragraph is formatted.

**A block that no formatter accepts is left verbatim**: A heading and a paragraph are formatted by a formatter that
accepts paragraphs only. The heading keeps its double space and the paragraph is formatted.

**The configured line ending wins over the majority**: A file with two CRLF terminators and one LF terminator is
formatted with a configuration that asks for LF. The line ending of the result is LF.

**Without a configured line ending the majority is the target**: The same file is formatted with a configuration that
has no line ending. The line ending of the result is CRLF, and one problem reads `Mixed line endings (2 CRLF, 1 LF);
expected CRLF`.

**A CRLF file with a LF configuration is reported once at the first position**: A CRLF file is formatted with a
configuration that asks for LF. One problem is returned with the rule `line-ending`, the formatting kind, the path
`a.md`, line 1, column 1, and the message `Line endings are CRLF; expected LF`. The line ending of the result is LF.

**A mixed file is reported with the counts of each ending**: A file with two CRLF terminators and one LF terminator is
formatted with a configuration that asks for LF. The one problem has the message `Mixed line endings (2 CRLF, 1 LF);
expected LF`.

**A LF file with a CRLF configuration is reported**: A LF file is formatted with a configuration that asks for CRLF.
The one problem has the message `Line endings are LF; expected CRLF`, and the line ending of the result is CRLF.

**A file that already has the configured line ending has no problem**: A CRLF file is formatted with a configuration
that asks for CRLF. No problem is reported and the line ending of the result is CRLF.

**Parallel calls give the same result as a single call**: One formatter and one CRLF document with three double
spaces are shared by 64 calls on up to 16 threads, with a configuration that asks for LF. The single-call result
holds three edits, and every parallel result has the same edits, the same problems, and the same line ending.

##### Boundary conditions

**A document without line terminators never violates the line ending rule**: A file `abc` without a terminator is
formatted once with a configuration that asks for LF and once with one that asks for CRLF. Neither result has a
problem.

**Equally frequent line endings target LF**: A file with one CRLF terminator and one LF terminator is formatted with a
configuration that has no line ending. The line ending of the result is LF, and one problem reads `Mixed line endings
(1 CRLF, 1 LF); expected LF`.

**A file without terminators and without a configured line ending targets LF**: The file `abc` is formatted with a
configuration that has no line ending. The line ending of the result is LF and no problem is reported.

**A document that is unsafe to rewrite is not touched**: A CRLF file with a line that looks like the border of an
invalid grid table is flagged as unsafe to rewrite. It is formatted with a configuration that asks for LF. The result
has no edit and no problem, and its line ending is the majority ending of the file, which is CRLF.

**A formatter list of null means no formatters**: A `DocumentFormatter` is created with a null list and formats a
document that has a double space. The call works and proposes no edit.

##### Error handling

**Conflicting edits of a formatter are refused**: A formatter with an `OverlappingEditsFormatter` formats a document.
The call throws `InvalidOperationException`, so the defect is not resolved silently.

**A null element in the formatter list is rejected**: A `DocumentFormatter` is created with a list that holds a null
element. The constructor throws `ArgumentNullException`.

**An exception of a block formatter reaches the caller unchanged**: A `DocumentFormatter` with a formatter that
throws `InvalidOperationException` (message `The formatter failed on purpose`) formats a one-paragraph document. The
call throws that same exception type with that same message, so nothing is caught or wrapped.

**Later changes of the caller's list are ignored**: A formatter is built from a list with one `ReplacingFormatter`
that writes a single space. The caller then clears the list and adds a formatter that writes an underscore. The
result of formatting still uses the original formatter, so `a  b` becomes `a b`.

**A missing document or configuration is rejected**: `Format` is called with a null document, and then with a null
configuration. Each call throws `ArgumentNullException`.

**A format context without a document or configuration is rejected**: A null document and a null configuration each
throw `ArgumentNullException`. A valid context keeps both values.

**A format result with invalid parts is rejected**: A default list of edits and a list that holds a default edit each
throw `ArgumentException`; a default list of findings throws `ArgumentException`, and a null finding throws
`ArgumentNullException`; a line ending that is not defined throws `ArgumentOutOfRangeException`. A valid result keeps
its edits, line ending, and findings.
