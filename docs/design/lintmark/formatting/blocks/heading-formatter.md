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

Invariants: every edit starts and ends on the source line of the heading; the edits of one heading are disjoint (the tab
edits lie between the first character of the content and the closing sequence, never in the blanks after the number
signs); no edit is a zero-length insertion at the start of a line; every edit carries the identifier of the rule that it
enforces.

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
its number signs only (`heading-space`); (4) trailing whitespace is removed (`trailing-whitespace`), also behind a
final backslash (the backslash then ends the content of an ATX heading, so it can never turn into a hard break);
(5) tabs in the text between the first character of the content and the closing sequence (or the trailing whitespace)
are replaced by spaces up to the next tab stop of four columns (`hard-tabs`), counted from the column that the text has
in the canonical line (the container prefix, the number signs, and one space), so a tab after
the number signs never moves the tab stops of the text. The plain-text parts are found through `LeafWhitespace` and
`InlineMap`, so a tab inside a code span or other literal text stays. The tabs are collected before any edit is added,
because the parser's inline spans of an ATX heading are only usable after they are matched to the line: when a tab
follows the opening number signs, Markdig reports the spans of the inlines too small by a constant (the line offset of the
heading), and a heading that is not on the first line would then have its spans point into the blanks after the marker.
`TryBuildMap` takes the shift from the first inline, moves every span by it, and accepts the result only when the end of
the last inline lands on the end of the content; otherwise the whole heading, not only its tabs, is left as it is (an
edit for the blanks alone would leave the tabs for the next run and the result would not be stable). A heading without a
tab in its text needs no spans and is never affected.

Algorithm for a setext heading (`FormatSetext`): the underline is verified from the source (a run of `=` or `-` that
ends the line, preceded by blanks and quote markers only). A setext heading becomes an ATX heading of the same level in
one edit that replaces the text line and the underline when it is top-level, has exactly one text line, and the
conversion is safe (`TryConvertToAtx`): the text is not empty, is indented by at most three spaces without a tab, does
not end with a number sign or a backslash, and does not start like a Pandoc structure line (a plus sign, a colon, a
backslash, or a dollar sign; a braced code fence cannot start the text of a setext heading, because such a line opens a
code block). Tabs in the text are expanded in the same edit, counted from the
column that the text has in the new line, so that formatting the result again changes nothing; the same span check
as for an ATX heading applies, and a heading that fails it stays setext. Any other setext heading
keeps its form and only has its trailing whitespace and tabs cleaned through `LeafWhitespace`. That cleanup keeps
trailing whitespace behind a final backslash of a text line (removing it would turn the backslash into a hard break; the
ATX heading has no such line break, so the whitespace goes there) and never edits a line that looks like the border of a
grid table. All edits of a conversion carry the rule `heading-style`.

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
