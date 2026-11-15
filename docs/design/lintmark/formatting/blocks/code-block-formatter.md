#### CodeBlockFormatter

![Blocks Structure](BlocksView.svg)

##### Purpose

`CodeBlockFormatter` proposes the edits that write each code block in canonical form: a fenced block with the fence
character of the configuration (backticks by default), never an indented block, with no trailing whitespace on the
fence lines. It enforces `code-fence` and `code-block-style`, and `trailing-whitespace` on the fence lines. The content
of the code is never changed.

##### Data Model

`CodeBlockFormatter` is an internal sealed class without fields. The constants `MinimumFenceLength` (three) and
`IndentColumns` (four, the columns that make a line indented code) are its only data.

- `RuleIds`: `code-fence`, `code-block-style`, and `trailing-whitespace`.
- *Accepted blocks*: a block of kind `FencedCode` whose node is a `FencedCodeBlock`, and a block of kind
  `IndentedCode`. A fence that is never closed has kind `UnclosedFence` and is not accepted, because the parser's view
  of where it ends is not trusted.

Invariants: every edit lies on a fence line or replaces the indentation of an indented block; no content line of a
fenced block is touched; a new fence is longer than any run of its character at the start of a content line; every
edit carries the identifier of the rule it enforces.

##### Key Methods

**CanFormat**: Returns `true` for a closed fenced code block and for an indented code block, `false` otherwise.

**Format**: Adds the edits of one code block (`FormatContext context`, `BlockInfo block`, `EditSet edits`). The block
must be accepted by `CanFormat` (not verified). A canonical block adds no edit. The target fence character comes from
`LintMarkConfig.CodeFence`.

*Fenced block* (`FormatFenced`). Both fences are located from the source lines; their lengths must equal the lengths
the parser reported, and only blanks may follow the closing fence, otherwise the block is left alone. The fence
character is converted when it differs from the configured one and `CanUseFence` allows it: a backtick fence cannot
have a backtick in its info string, and an info string that starts with a brace (a Pandoc raw block such as
`{=html}`) keeps its fence character. The new fence has the old length, or more when a content line starts with a run
of the new character that could close it (`LongerThanContent`). Both fences are edited under `code-fence`. Trailing
whitespace on the two fence lines is removed under `trailing-whitespace`. A fenced block inside a quote or list is
converted in place and keeps its prefix.

*Indented block* (`FormatIndented`). Only a top-level block is converted; inside a quote or list the indentation also
carries the container, so the block stays. Each content line loses exactly four columns of leading whitespace
(`TryDedent`; a tab counts to the next tab stop, and tab stops are four columns wide, so a tab never crosses the fourth
column). A blank line with fewer columns becomes empty. The last line is the last line with content, or a
whitespace-only line wider than the indentation, whose rest is code (`LineOwnership.LinesOf`). The replacement is the
fence, the dedented lines, and the fence, in one edit under `code-block-style`. The block is left as written when a
line with content has fewer than four columns of indentation, or when a dedented line would be `---` or `...` and
`FrontMatterGuard.CouldOpenFrontMatter` says the document could open front matter (the new fence would close it).

##### Error Handling

A null argument throws `ArgumentNullException`. A block that does not have the shape the parser promised produces no
edit. An edit that conflicts with another throws `InvalidOperationException` from `EditSet.Add`; that is a defect, and
`LintMarkEngine` reports it as a `file-format-error` for the file. Nothing is caught here.

##### Dependencies

- **IBlockFormatter**, **FormatContext**, **LineLayout**, **LineOwnership**, and **FrontMatterGuard**
  (`DocumentFormatter` unit) - the contract, the settings, line arithmetic, the lines of a block, and the front matter
  guard.
- **EditSet** and **TextEdit** (`EditSet` unit) - the edits.
- **BlockInfo**, **BlockKind**, and **ParsedDocument** (`DocumentParser` unit) - the code block and its source.
- **LintMarkConfig** and **FenceMarker** (`LintMarkConfig` unit) - the `code-fence` setting.
- **RuleCatalog** (`Linting` subsystem) - the rule identifiers.
- **Markdig** (OTS) - `FencedCodeBlock` and `CodeBlock`.

##### Callers

- **DocumentFormatter** - calls `CanFormat` for every block and `Format` for the accepted blocks that are not
  suppressed.
