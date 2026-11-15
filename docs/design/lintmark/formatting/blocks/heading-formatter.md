#### HeadingFormatter

![Blocks Structure](BlocksView.svg)

##### Purpose

`HeadingFormatter` proposes the edits that write each heading in canonical form: an ATX heading (`# Title`) with
exactly one space after the number signs, no closing number signs, no indentation, no trailing whitespace, and no tabs
in the text. It enforces `heading-style`, `heading-space`, and `heading-start-left`, and enforces
`trailing-whitespace` and `hard-tabs` on the lines of the headings it handles.

##### Data Model

`HeadingFormatter` is an internal sealed class without fields. The constants `CanonicalSpace` (one space) and
`MaxIndentation` (three, the most spaces that leave a heading a heading) are its only data.

- `RuleIds`: `heading-style`, `heading-space`, `heading-start-left`, `trailing-whitespace`, and `hard-tabs`.
- *Accepted blocks*: a block of kind `Heading` whose node is a Markdig `HeadingBlock`, ATX or setext.

Invariants: every edit lies on the source line of the heading; the edits of one heading are disjoint; no edit is a
zero-length insertion at the start of a line; every edit carries the identifier of the rule it enforces.

##### Key Methods

**CanFormat**: Returns `true` for an ATX or setext heading and `false` for every other block.

**Format**: Adds the edits of one heading (`FormatContext context`, `BlockInfo block`, `EditSet edits`). The block must
be a heading accepted by `CanFormat` (not verified). A canonical heading adds no edit.

*ATX heading* (`FormatAtx`). The line is split into indentation, opening number signs, blanks, content, an optional
closing sequence, and trailing whitespace. The heading is left alone when the number signs are not followed by a blank
or the end of the line, or when the indentation of a top-level heading is not spaces only. Otherwise:

1. A top-level heading loses its indentation (`heading-start-left`); a heading in a container keeps its prefix.
2. A closing sequence (number signs at the end, preceded by a blank or filling the whole content) is removed with the
   blanks before it (`heading-style`); `C#` and `\#` are text.
3. The blanks after the number signs become one space; an empty heading keeps its number signs only (`heading-space`).
4. Trailing whitespace is removed (`trailing-whitespace`).
5. Tabs in the text become spaces up to the next four-column tab stop, counted from the column the text has in the
   canonical line (`hard-tabs`). `LeafWhitespace` and `InlineMap` find the plain-text parts, so a tab in a code span
   stays.

The tabs are collected before any edit is added. When a tab follows the opening number signs, Markdig reports the
inline spans too small by a constant. `TryBuildMap` takes the shift from the first inline, accepts it only when the
end of the last inline lands on the end of the content, and otherwise the whole heading is left as written, so that no
rule does half of its work in one run and the rest in the next.

*Setext heading* (`FormatSetext`). The underline is verified from the source (a run of `=` or `-` at the end of the
line, after blanks and quote markers only). A top-level heading with one text line becomes an ATX heading of the same
level in one edit under `heading-style` (`TryConvertToAtx`), unless the text is empty, is indented by more than three
spaces or by a tab, ends with a number sign or a backslash, starts with a plus sign, colon, backslash, or dollar sign
(Pandoc structure), has tabs whose spans do not fit (the same span check as above), or directly follows a link reference
definition and starts with a list marker (the block spacing rule does not separate it from the definition, and the
heading stays as it is so that a second run finds nothing to change). Tabs are expanded in the same
edit. Any other setext heading keeps its form and only has trailing whitespace and tabs cleaned by
`LeafWhitespace.AddBlockEdits`.

Text that only looks like a heading (`#Title`) is a paragraph to the parser and is never changed.

##### Error Handling

A null argument throws `ArgumentNullException`. A line that does not have the shape the parser promised produces no
edit; the formatter never guesses. An edit that conflicts with another throws `InvalidOperationException` from
`EditSet.Add`; that is a defect, and `LintMarkEngine` reports it as a `file-format-error` for the file. Nothing is
caught here.

##### Dependencies

- **IBlockFormatter**, **FormatContext**, **LineLayout** (`DocumentFormatter` unit) - the contract, the document and
  settings, and the line arithmetic.
- **EditSet** and **TextEdit** (`EditSet` unit) - the edits.
- **LeafWhitespace** and **InlineMap** (`WhitespacePasses` unit) - tab collection and the cleanup of other setext
  headings.
- **BlockInfo**, **BlockKind**, and **ParsedDocument** (`DocumentParser` unit) - the heading and its source.
- **RuleCatalog** (`Linting` subsystem) - the rule identifiers.
- **Markdig** (OTS) - `HeadingBlock` and the inline tree.

##### Callers

- **DocumentFormatter** - calls `CanFormat` for every block and `Format` for the accepted blocks that are not
  suppressed.
