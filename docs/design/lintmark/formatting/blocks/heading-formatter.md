#### HeadingFormatter

![Blocks Structure](BlocksView.svg)

##### Purpose

`HeadingFormatter` proposes the edits that write each heading in the canonical form: an ATX heading (`# Title`) with
exactly one space after the number signs, no closing number signs, no indentation, no trailing whitespace, and no tabs in
the text. It enforces the rules `heading-style`, `heading-space`, and `heading-start-left`, and the rules
`trailing-whitespace` and `hard-tabs` on the lines of the headings that it handles.

##### Data Model

`HeadingFormatter` is an internal sealed class without fields; the constants `CanonicalSpace` (one space) and
`MaxIndentation` (three, the most spaces that leave a heading a heading) are the only data.

- `RuleIds`: an immutable array of the five rule identifiers above, in the order `heading-style`, `heading-space`,
  `heading-start-left`, `trailing-whitespace`, `hard-tabs`. A heading is left verbatim by `DocumentFormatter` when any
  of them is suppressed anywhere inside the heading.
- *Accepted blocks*: a block of kind `Heading` whose node is a Markdig `HeadingBlock`, ATX or setext.

Invariants: every edit starts and ends on the source line of the heading; the edits of one heading are disjoint; no edit
is a zero-length insertion at the start of a line; every edit carries the identifier of the rule that it enforces.

##### Key Methods

**CanFormat**: Accepts a heading.

- *Parameters*: `BlockInfo block`.
- *Returns*: `true` for an ATX or a setext heading, `false` for every other block.
- *Errors*: `ArgumentNullException` for a null block.

**Format**: Adds the edits of one heading.

- *Parameters*: `FormatContext context`, `BlockInfo block` (accepted by `CanFormat`), `EditSet edits`.
- *Preconditions*: no argument is null and the block is a heading.
- *Postconditions*: `edits` holds the edits that make the heading canonical; a canonical heading adds none.

Algorithm for an ATX heading (`FormatAtx`): the line is taken from the source, and the parts are located from the
number signs: the indentation, the opening number signs, the blanks after them, the content, an optional closing
sequence, and the trailing whitespace. The line is left alone when the opening number signs are not followed by a blank
or the end of the line, or when the indentation of a top-level heading is not spaces only. Otherwise: (1) the
indentation of a top-level heading is removed (`heading-start-left`); a heading in a quote or list keeps its prefix;
(2) a closing sequence is a run of number signs at the end that is preceded by a blank or fills the whole content, and
it is removed together with the blanks before it (`heading-style`); a run that follows another character (`C#`) or an
escape (`\#`) is text; (3) the blanks after the opening number signs become exactly one space, and an empty heading keeps
its number signs only (`heading-space`); (4) trailing whitespace is removed (`trailing-whitespace`); (5) tabs in
plain-text parts of the content are replaced by spaces up to the next tab stop of four columns (`hard-tabs`) through
`LeafWhitespace` and `InlineMap`, so a tab inside a code span or other literal text stays.

Algorithm for a setext heading (`FormatSetext`): the underline is verified from the source (a run of `=` or `-` that
ends the line, preceded by blanks and quote markers only). A setext heading becomes an ATX heading of the same level in
one edit that replaces the text line and the underline when it is top-level, has exactly one text line, and the
conversion is safe (`TryConvertToAtx`): the text is not empty, is indented by at most three spaces without a tab, does
not end with a number sign or a backslash, and does not start like a Pandoc structure line (a plus sign, a colon, a
backslash, a dollar sign, or a braced code fence). Tabs in the text are expanded in the same edit, counted from the
column that the text has in the new line, so that formatting the result again changes nothing. Any other setext heading
keeps its form and only has its trailing whitespace and tabs cleaned through `LeafWhitespace`. All edits of a
conversion carry the rule `heading-style`.

Text that merely looks like a heading (`#Title`, no space) is a paragraph to the parser, is not accepted by
`CanFormat`, and is never changed: adding the space would change how the document renders.

##### Error Handling

`ArgumentNullException` for a null argument and `ArgumentException` for a block that is not a heading. A line that does
not have the shape the parser promised produces no edit at all; the formatter never guesses. An edit that conflicts with
another edit would make `EditSet.Add` throw `InvalidOperationException`; that is a defect of a formatter and
`LintMarkEngine` turns it into a `file-format-error` for the file. Nothing is caught here.

##### Dependencies

- **IBlockFormatter**, **FormatContext**, **EditSet**, and **TextEdit** (`Formatting` subsystem) - the contract, the
  inputs, and the edits.
- **ParsedDocument**, **BlockInfo**, **BlockKind**, and **SourceDocument** (`Document` subsystem) - the heading and its
  source text.
- **RuleCatalog** (`Linting` subsystem) - the rule identifiers of the edits.
- **Markdig** (OTS) - `HeadingBlock` and the inline tree.
- **LineLayout**, **InlineMap**, and **LeafWhitespace** (supporting types of `DocumentFormatter`) - line arithmetic and
  the whitespace and tab edits.

##### Callers

- **DocumentFormatter** - calls `CanFormat` for every block and `Format` for the accepted blocks that are not
  suppressed.
- **LintMarkEngine** - creates the instance that the production `DocumentFormatter` is configured with.
