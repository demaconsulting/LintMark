package LintMark {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::*;

    enum def BlockKind {
        doc /* Classification of a block in the flattened, source-ordered block list. */

        enum heading;
        enum paragraph;
        enum fencedCode;
        enum unclosedFence;
        enum indentedCode;
        enum htmlBlock;
        enum thematicBreak;
        enum blockQuote;
        enum list;
        enum listItem;
        enum table;
        enum gridTable;
        enum frontMatter;
        enum linkReferenceDefinition;
        enum footnote;
        enum unreferencedFootnote;
        enum mathBlock;
        enum other;
    }

    // The parser's own block nodes (node and parent) are runtime references into the per-file syntax tree
    // and are deliberately not modeled; only the offsets, depth and classification are.
    attribute def BlockInfo {
        doc /* One block of the flattened, filtered and source-ordered block list. */

        attribute kind : BlockKind;
        attribute start : Integer;
        attribute endInclusive : Integer;
        attribute depth : Integer;
    }

    attribute def LeafLine {
        doc /* One source line of a leaf block (paragraph or heading) as located by the line-rebuilding helper. */

        attribute line : Integer;
        attribute lineStart : Integer;
        attribute contentStart : Integer;
        attribute contentEnd : Integer;
        attribute isLazy : Boolean;
    }

    // The syntax tree (ast) is a runtime reference to the parser's own type and is not modeled.
    item def ParsedDocument {
        doc /* The result of parsing one source document: the source, its source-ordered blocks, suppressions and reference definitions. */

        part sourceDocument : SourceDocument;
        attribute blocks : BlockInfo [0..*];
        part suppressionMap : SuppressionMap;
        part referenceIndex : ReferenceIndex;
    }

    part def DocumentParser {
        doc /* Parses a source document with the fixed Markdown pipeline into a source-ordered block list with corrected ranges (also documents the ParsedDocument, BlockInfo, BlockKind and LeafLine supporting types). */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.LintMark/Document/DocumentParser.cs",
                "src/DemaConsulting.LintMark/Document/ParsedDocument.cs",
                "src/DemaConsulting.LintMark/Document/BlockInfo.cs",
                "src/DemaConsulting.LintMark/Document/BlockKind.cs",
                "src/DemaConsulting.LintMark/Document/LeafLine.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/Document/DocumentParserTests.cs");
            designFiles = ("docs/design/lintmark/document/document-parser.md");
            verificationFiles = ("docs/verification/lintmark/document/document-parser.md");
            requirementFiles = ("docs/reqstream/lintmark/document/document-parser.yaml");
        }

        // Parsing needs the normalized text, builds the suppression map and reference index of each
        // document, uses the Markdown parser library for the syntax tree, and produces the parsed
        // document with its classified blocks and located leaf lines.
        dependency DocumentParser to SourceDocument;
        dependency DocumentParser to SuppressionMap;
        dependency DocumentParser to ReferenceIndex;
        dependency DocumentParser to Markdig;
        dependency DocumentParser to ParsedDocument;
        dependency DocumentParser to BlockInfo;
        dependency DocumentParser to BlockKind;
        dependency DocumentParser to LeafLine;
    }
}
