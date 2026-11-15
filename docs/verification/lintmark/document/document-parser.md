### DocumentParser

#### Verification Approach

`DocumentParser` and its result types (`ParsedDocument` and `ReferenceIndex`) are verified with unit tests in
`DocumentParserTests.cs` and `ReferenceIndexTests.cs`. The parser runs the real Markdown library with the fixed
pipeline of the tool on real `SourceDocument` text and corrects the library's known range deviations, so the tests
use no mocks and assert on the produced blocks: kind, exact source range, depth, and parent. Most scenarios use small
inline documents; the shared `SpanTilingAssertions` helper checks that sibling ranges never overlap. Line and
continuation-prefix helpers are asserted on the exact content ranges of each line. Library deviations that the
parser compensates for are pinned as OTS evidence; here the corrected behavior is verified.

#### Test Environment

N/A - standard test environment. The tests use inline text only and no files.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Blocks are returned in source order with exact ranges, depth, and parents, and sibling ranges never overlap.
- Unclosed fences and math blocks extend to the end of the document or of their container; lists and indented code
  end on the last visible character.
- Link reference definitions and footnotes get their own ranges; unreferenced and duplicate footnotes are reported.
- Content lines exclude container prefixes, and lazy continuation lines are flagged.
- A grid table border that is not part of a valid grid table marks the document format-unsafe at that line, and
  blocks are still produced.
- Suppression comments, by id or alias, are collected with the parse.
- Inline elements nested deeper than 128 levels are refused with `InvalidDataException`; moderate nesting is parsed.

#### Test Scenarios

##### Normal operation

**Simple document yields blocks in source order**: Headings, paragraphs, and lists are classified with exact spans.

**Nested containers report depth and parent**: Quotes and lists in each other give the right nesting.

**Pipe tables, grid tables, and front matter are recognized**: Each is its own block kind.

**Inline constructs are recognized**: Emphasis, code spans, links, and entities appear in the inline view.

**Definitions and footnotes appear with their own ranges**: Definitions in quotes, list items, and nested containers
are indexed in source order; labels match without regard to case or white space; duplicate labels keep the first.

**Paragraph lines have exact content ranges**: Lazy lines, tab-indented continuations, setext underlines, and
footnote paragraphs give the right content and prefix.

**Suppression comments are collected**: Aliases resolve to the canonical rule and unknown names suppress nothing.

##### Boundary conditions

**Unclosed fence extends to the end**: In a quote or list item it ends with the container.

**Footnote and definition ranges never overlap their neighbors**: Paragraphs and headings after a definition, and a
dropped footnote before a definition on the same line, keep disjoint ranges.

**Tabs keep exact spans**: Lists, code, and text with tabs report correct columns.

**Moderate inline nesting is parsed**: Emphasis nested far below the limit gives a normal result
(`DocumentParser_Parse_ModeratelyNestedEmphasis_IsParsed`).

##### Error handling

**Invalid grid border marks the document format-unsafe**: The location is the offending line; a border in a fence or
other literal block is not marked, and a plain document is never marked.

**Over-deep inline nesting is refused**: Thousands of nested `*` or `_` throw `InvalidDataException` instead of
exhausting the stack (`DocumentParser_Parse_DeeplyNestedEmphasis_ThrowsInvalidData`); the engine and system tests
(`LintMarkEngine_Process_DeeplyNestedEmphasis_ReportsParseErrorAndKeepsGoing`,
`LintMark_FileProblems_DeeplyNestedEmphasis_IsReportedWithoutStoppingTheRun`) show it reported as `file-parse-error`.

**A grid-table fallback still produces blocks**: Parsing never fails because of the fallback; an unknown location
falls back to line 1, column 1.
