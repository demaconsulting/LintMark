### Blocks

![Blocks Structure](BlocksView.svg)

#### Overview

The `Blocks` subsystem is part of the `Formatting` subsystem. It holds the block formatters of the tool: one for
headings, one for code blocks, and one for thematic breaks. A block formatter proposes the edits that bring one kind of
block into canonical form and nothing else; it never applies an edit, never writes a file, and never touches the
content of code. The subsystem contains three units: `HeadingFormatter`, `CodeBlockFormatter`, and
`ThematicBreakFormatter`.

- `HeadingFormatter` writes headings as ATX headings with one space after the number signs, no closing number signs, no
  indentation, no trailing whitespace, and no tabs in the text. It enforces `heading-style`, `heading-space`, and
  `heading-start-left`, and also the `trailing-whitespace` and `hard-tabs` rules on its own lines.
- `CodeBlockFormatter` writes code blocks as fenced blocks with the configured fence character, and removes trailing
  whitespace from fence lines. It enforces `code-fence` and `code-block-style`, and `trailing-whitespace` on the fence
  lines.
- `ThematicBreakFormatter` writes thematic breaks as three characters of the configured style. It enforces
  `thematic-break`, and `trailing-whitespace` on the line of the break.

The units share no state and do not call each other. They use the line arithmetic and the whitespace helpers that
`DocumentFormatter` owns (`LineLayout`, `LineOwnership`, `InlineMap`, `LeafWhitespace`, and `FrontMatterGuard`; see
*DocumentFormatter Design*), so that the passes of the document formatter and the block formatters treat whitespace and
tab stops in the same way.

#### Interfaces

**IBlockFormatter**: Each unit implements the extension point of the `Formatting` subsystem.

- *Type*: In-process .NET interface, implemented by three internal sealed classes.
- *Role*: Provider (to `DocumentFormatter`).
- *Contract*: `RuleIds` lists the rules the formatter enforces (the document formatter leaves a block alone when any of
  them is suppressed inside it), `CanFormat` accepts the blocks of the unit, and `Format` adds edits to the `EditSet` of
  the file. Edits lie on the lines of the accepted block and each is attributed to the rule it enforces.
- *Constraints*: Throws `ArgumentNullException` for a null argument and `ArgumentException` for a block that `CanFormat`
  rejects. Every formatter is stateless, so one instance serves every worker thread.

The subsystem consumes the `Document` subsystem (`ParsedDocument`, `BlockInfo`, `BlockKind`, `SourceDocument`), the
`Configuration` subsystem (`LintMarkConfig` and the settings `code-fence` and `thematic-break`), and the `Linting`
subsystem (`RuleCatalog` identifiers). It is consumed by the `Engine` subsystem, which creates the production
`DocumentFormatter` with the three formatters.

#### Design

The formatters are written for one safety rule: an edit is proposed only when the source line has exactly the shape the
parser promised and the new form means the same thing. Every unit therefore computes its edits from the source lines,
never from the span of a block (the spans of headings, lists, and code blocks are inconsistent about trailing
whitespace), and each unit declines a conversion whose result could be read differently:

- a setext heading that is nested, spans several lines, or whose text could change meaning as an ATX heading stays a
  setext heading;
- a fence whose info string would become invalid, or that is Pandoc syntax (a braced info string), keeps its fence
  character, and a new fence is made longer than any fence-like line in the code;
- an indented code block inside a container stays indented, and one whose fenced form could create front matter stays as
  written;
- a thematic break that would become a heading underline, could open or close front matter, would join a list marker
  of the same character into a break of its own, or would look like a grid table border after a plus sign list marker
  stays as written;
- an ATX heading with a tab in its text whose inline spans do not fit its line (the parser reports them too small when a
  tab follows the number signs) is not changed at all, and an indented code block or fence whose lines are not what the
  parser reported is not changed either;
- a whitespace-only line at the end of a top-level indented code block that is wider than the indentation is part of the
  code (what remains of it after the indentation stays in the fence).

Edits of one formatter never overlap, because they cover disjoint parts of a line (the indentation, the number signs,
the space after them, a closing sequence, and the trailing whitespace of a heading, for example). A formatter never adds
a zero-length edit at the start of a line, because the block spacing pass of `DocumentFormatter` inserts blank lines
there.

The three formatters claim their blocks. `DocumentFormatter` gives each block to the first formatter whose `CanFormat`
accepts it and then keeps its own passes away from the lines of that block, also when a rule of the formatter is
suppressed there. A block that no formatter accepts (an unclosed fence, a paragraph, a table) is handled by the passes.

Verification of the subsystem relies on three checkers of the `Safety` subsystem at test time: the HTML equivalence
check, the structural token check, and the idempotency check run over every document that the tests format, and a
corpus of the Markdown files of the repository and of generated hostile documents (see the verification design). They
are collaborators of the tests only; the production formatters do not call them, because the `Engine` subsystem applies
the same checks to every rewrite.

Thread safety: the units are stateless and immutable. The shared helper types are immutable per document, so one parsed
document may be formatted by several threads at once, each with its own `EditSet`.
