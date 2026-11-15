#### LinkRefactorer

![Inline Structure](InlineView.svg)

##### Purpose

`LinkRefactorer` implements the opt-in `link-refactor` rule: it converts long inline links and images
(`[text](destination "title")`) to reference links (`[text][label]`) and writes every new definition in one block at
the end of the document. It decides which links are converted, chooses the labels, finds the place for the block, and
returns the edits and one finding per converted link. It writes nothing itself.

##### Data Model

`LinkRefactorer` is an internal sealed class without state. It reads `LinkRefactorOptions` of `LintMarkConfig`:
`Enabled`, `Threshold` (the width in display columns of an inline link that triggers a conversion, default 60),
`InTables` (default on), `BreakLinkText` (used by the tokenizer, not here), `LabelMaxLength` (default 40), and
`DefinitionsMarker` (key `definitions-marker`, a single-line HTML comment, default `<!-- Link References -->`).

**LinkRefactorPlan**: an immutable record with `Edits` (ordered, conflict-free, against the text the plan was made for),
`Conversions` (one `LinkConversion` per converted link: its `Offset` and `Message`), and `ChangesTables` (a link in a
table cell was converted, so the table must be laid out again). `LinkRefactorPlan.Empty` changes nothing.

**InlineLinkTail**: an immutable record for the destination part of an inline link: `Start` (the opening
parenthesis), `End` (just after the closing one), `Destination` and `Title` as written, and `Url` and `TitleText` as
the parser reports them. `InlineLinkTail.TryRead` reads it from the text between the parentheses only, so the text of
the link, with its brackets, emphasis and code, cannot confuse it. It returns `null` (not certain) when the link is not
an inline link, the part spans lines, a backslash or an entity (`&name;`) occurs in the destination or title, a title
holds a nested parenthesis, or the parser's `Url` or `Title` differs from what was read.

**LinkLabelAllocator**: a per-document builder. Its taken set holds the normalized label of every existing definition
and every bracketed text `[x]` of the document, except the brackets of a link or image that the parser read (a new
definition would turn such text into a link). It also remembers, by destination and title, the label of each existing
definition that the parser uses and that has a simple label (letters, digits, blanks, and any of `-_.:/#+`).

Private types: `Candidate` (a convertible link: tail, base label, offset, saving in columns) and `Unit` (the links that
are converted together, or not at all, with the message of the finding).

##### Key Methods

**IsEnabled** (static): true when `link-refactor.enabled` is on and the `link-refactor` rule is switched on.

**Plan**: `LinkRefactorPlan Plan(ParsedDocument document, LintMarkConfig config)`; pure.

1. Return `Empty` when the feature is off, the document is format-unsafe, or nothing qualifies.
2. Collect the units (`CollectUnits`). Only paragraph and table blocks are looked at. A block in a footnote, in a
   protected block, or with `link-refactor` suppressed is skipped, and so are headings, code, and HTML.
3. A paragraph link is a unit when `TryRead` reads its tail, it is not inside an image description, its source (on one
   line, container markers dropped) is wider than `Threshold`, and the conversion saves columns: the tail is wider than
   the brackets plus the label that the link text alone gives (`LinkLabelAllocator.DeriveBase`). The decision never
   depends on which labels happen to be free, so a second run converts nothing more.
4. A table row is a unit when `InTables` is on, the line length is not 0, `table-column-style` is not suppressed, the
   row in the compact layout (`PipeTable.ComposeCompact`, with its prefix) is wider than the line length, and the
   conversion of every convertible link of the row makes it fit; the links of the row are converted together.
5. Allocate a label for each link in document order (`Allocate`): reuse the label of an existing definition or an
   earlier link with the same destination and title; otherwise take the base label, or the base with `-2`, `-3`
   and so on until it is free, within `LabelMaxLength`. Each tail is replaced by `[label]`.
6. Place the new definitions (`TryPlaceDefinitions`), one line each (destination and title as written), sorted by label.
   When a top-level HTML block equals the marker, they follow the last definition directly after it, and every
   existing line stays. Otherwise a block (blank line, marker, definitions) is written after the last line that has
   content, provided that line may be followed by a block (it is not in an unclosed fence, an unclosed HTML block, a
   block the tool does not model, or a footnote) and the end of the text is the configured final line ending. When no
   safe place exists the whole plan is `Empty`.

**LinkLabelAllocator.DeriveBase** (static): the link text in lower case, every run of characters that are not letters or
digits replaced by one hyphen, cut to the maximum length at a hyphen; when nothing is left, the host (without `www.`)
and first path segment of an `http` or `https` destination (or the destination without query and fragment) the same
way; otherwise `link`.

##### Error Handling

A link or document that is not read with certainty is skipped, never reported as an error. `ArgumentNullException`
is thrown for a null argument, `ArgumentException` for an empty base label, and `ArgumentOutOfRangeException` for a
maximum length below 1. Edits are added to an `EditSet`, whose `InvalidOperationException` for a conflict signals a
defect. Nothing is caught.

##### Dependencies

- **TextWidth**, **ParsedDocument**, **BlockInfo**, **ReferenceIndex**, **LeafLine** (`Document` subsystem).
- **LineLayout**, **LineOwnership**, **EditSet**, **TextEdit** (`Formatting` subsystem) - lines, protected and footnote
  lines, and edits.
- **PipeTable** and **TableFormatter** (`Blocks` subsystem) - the compact row width.
- **LintMarkConfig**, **LinkRefactorOptions** (`Configuration` subsystem), **RuleCatalog** and **RuleSupport**
  (`Linting` subsystem) - settings, rule identifiers, and the plain text of a link.
- **Markdig** (OTS) - links and tables.

##### Callers

- **DocumentFormatter** - calls `IsEnabled` and `Plan` on the text that the other rules produce, after the lists and
  before any wrap; composes the edits with theirs, lays out a changed table again, and reports each conversion.
- **AtomTokenizer** - uses `InlineLinkTail.TryRead` when link text may be broken.
