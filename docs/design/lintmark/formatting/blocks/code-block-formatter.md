#### CodeBlockFormatter

![Blocks Structure](BlocksView.svg)

##### Purpose

`CodeBlockFormatter` proposes the edits that write each code block in the canonical form: a fenced block with the fence
character of the configuration (backticks by default), never an indented block, with no trailing whitespace on the
fence lines. It enforces the rules `code-fence` and `code-block-style`, and the rule `trailing-whitespace` on the fence
lines. The content of the code is never changed.

##### Data Model

`CodeBlockFormatter` is an internal sealed class without fields; the constants `MinimumFenceLength` (three) and
`IndentColumns` (four, the columns that make a line indented code) are the only data.

- `RuleIds`: an immutable array of `code-fence`, `code-block-style`, and `trailing-whitespace`.
- *Accepted blocks*: a block of kind `FencedCode` whose node is a Markdig `FencedCodeBlock`, and a block of kind
  `IndentedCode`. A fence that is never closed has the kind `UnclosedFence` and is not accepted, because the parser's
  view of where it ends is not trusted.

Invariants: every edit lies on a fence line or replaces the indentation of an indented block; no line of the content of
a fenced block is touched; a new fence is longer than any run of its character at the start of a content line; every
edit carries the identifier of the rule that it enforces.

##### Key Methods

**CanFormat**: Accepts a code block that is closed.

- *Parameters*: `BlockInfo block`.
- *Returns*: `true` for a closed fenced code block and an indented code block, `false` for an unclosed fence and every
  other block.
- *Errors*: `ArgumentNullException` for a null block.

**Format**: Adds the edits of one code block.

- *Parameters*: `FormatContext context`, `BlockInfo block` (accepted by `CanFormat`), `EditSet edits`.
- *Preconditions*: no argument is null and the block is accepted by `CanFormat`.
- *Postconditions*: `edits` holds the edits that make the block canonical; a canonical block adds none.

Algorithm for a fenced block (`FormatFenced`): the opening and closing fences are located from the source lines and
their lengths must equal the lengths that the parser reported, and only blanks may follow the closing fence; otherwise
the block is left alone. The fence character is converted when it differs from the configured one and the conversion is
allowed (`CanUseFence`): a backtick fence cannot have a backtick in its info string, and an info string that starts with
a brace (a Pandoc attribute or raw block such as `{=html}`) keeps its fence character because only the backtick form is
recognized there. The new fence has the length of the old one, or more when a content line starts with a run of the
new character that would close it (`LongerThanContent`). Both fences are edited with the rule `code-fence`. Trailing
whitespace on the two fence lines is removed with the rule `trailing-whitespace`; the content lines are code and stay.
A fenced block inside a quote or list is converted in place, so its prefix and indentation stay.

Algorithm for an indented block (`FormatIndented`): only a top-level block is converted; one inside a quote or list
stays indented because its indentation also carries the container. Each content line loses exactly four columns of
leading whitespace (a tab counts up to the next tab stop, and a tab that crosses the fourth column leaves its remaining
columns as spaces), a blank line with fewer columns becomes empty, and the blank lines after the last line with content
are not part of the block. The replacement is the fence, the unindented lines, and the fence in one edit under the rule
`code-block-style`. The block is left as written when an unindented line would be `---` or `...` and the document could
open front matter on its first line (`FrontMatterGuard`), because the fenced form would otherwise close front matter
that the document never had.

##### Error Handling

`ArgumentNullException` for a null argument and `ArgumentException` for a block that `CanFormat` rejects. A block that
does not have the shape the parser promised produces no edit at all. An edit that conflicts with another edit would make
`EditSet.Add` throw `InvalidOperationException`; that is a defect of a formatter and `LintMarkEngine` turns it into a
`file-format-error` for the file. Nothing is caught here.

##### Dependencies

- **IBlockFormatter**, **FormatContext**, **EditSet**, and **TextEdit** (`Formatting` subsystem) - the contract, the
  inputs, and the edits.
- **ParsedDocument**, **BlockInfo**, **BlockKind**, and **SourceDocument** (`Document` subsystem) - the code block and
  its source text.
- **LintMarkConfig** and **FenceMarker** (`Configuration` subsystem) - the `code-fence` setting.
- **RuleCatalog** (`Linting` subsystem) - the rule identifiers of the edits.
- **Markdig** (OTS) - `FencedCodeBlock` and `CodeBlock`.
- **LineLayout**, **LineOwnership**, and **FrontMatterGuard** (supporting types of `DocumentFormatter`) - line
  arithmetic, the lines of a block, and the front matter guard.

##### Callers

- **DocumentFormatter** - calls `CanFormat` for every block and `Format` for the accepted blocks that are not
  suppressed.
- **LintMarkEngine** - creates the instance that the production `DocumentFormatter` is configured with.
