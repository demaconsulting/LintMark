## Markdig

This document describes the integration and usage design for the `Markdig` OTS software item.

### Purpose

Markdig is the Markdown parsing library used by the `Document` subsystem. It turns Markdown text into a
syntax tree of block and inline elements with source ranges, which LintMark needs in order to report
exact positions and to reason about which characters belong to which construct. It is chosen because it
is a widely used, actively maintained .NET parser with a CommonMark-compliant core and the extensions
(tables, footnotes, front matter, and others) that documentation repositories use.

### Features Used

- Parsing text to a syntax tree (`Markdown.Parse`) with precise source locations for blocks and inlines
- The pipeline builder with these extensions: YAML front matter, pipe tables (with GFM rules), grid tables,
  footnotes, task lists, automatic links, strikethrough, alert blocks, and mathematics
- Block types used for classification: headings, paragraphs, fenced and indented code, HTML blocks (including
  the comment type), thematic breaks, quotes, lists and list items, tables, footnotes, link reference
  definitions, math blocks, and YAML front matter
- Inline types used to locate content: inline HTML (comments), line breaks, and the inline tree of
  paragraphs and headings
- Link reference definitions with label, destination, title, and range
- Footnote definitions with their own range and label span, the group nodes that wrap definitions, and the fact
  that a math block is a kind of fenced code block (so an unclosed math block is recognized like an unclosed
  fence)

Not used: automatic heading identifiers, generic attributes, the advanced extension bundle, trivia tracking,
and rendering to HTML.

Grid table behavior: Markdig's grid table parser mishandles a line that looks like a grid table row but is not
a valid grid table (it rebuilds a paragraph without a proper start, parses the following line twice, and can
attach the paragraph to the wrong parent). `DocumentParser` therefore validates the blocks of every parse and,
when they do not nest, parses the text once more with a second private pipeline that has no grid tables. A
document with both a valid and an invalid grid table is thus read without grid tables; this is deliberate and
silent in Phase 1.

Definition and footnote span behavior: a link reference definition whose title on the next line is rejected (text
follows it) keeps the title in its span, with a null title, and a definition inside a footnote that the parser
drops (no reference) stays a link reference definition. `DocumentParser` ends such a definition at its
destination and ends the dropped footnote before it.

### Integration Pattern

Markdig is consumed as a NuGet package reference of the main project. `DocumentParser` builds one immutable
pipeline when the type is first used and shares it read-only between all threads; each file is parsed with
`Markdown.Parse` on LF-normalized text. No initialization, configuration file, or disposal is required.
Markdig exceptions propagate to the engine, which reports them as parse failures of the affected file.

Markdig has no self-validation mode. The evidence for the behaviors LintMark depends on is the integration
test project `test/OtsSoftwareTests`, which exercises the library directly (source ranges, the required
extensions, the known unclosed-fence and trailing-newline range quirks, link reference definitions, HTML
comments, and parallel parsing with one shared pipeline).
