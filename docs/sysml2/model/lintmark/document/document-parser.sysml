package LintMark {
    private import SoftwareItemMetadata::*;

    part def DocumentParser {
        doc /* Parses a source document with the fixed Markdown pipeline into a source-ordered block list with corrected ranges. */

        metadata info : SoftwareInfo {
            description = "Parses a source document with the fixed Markdown pipeline into a source-ordered block list with corrected ranges (also documents the ParsedDocument, BlockInfo, BlockKind and LeafLine supporting types).";
            sourceFiles = (
                "src/DemaConsulting.LintMark/Document/DocumentParser.cs",
                "src/DemaConsulting.LintMark/Document/ParsedDocument.cs",
                "src/DemaConsulting.LintMark/Document/BlockInfo.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/Document/DocumentParserTests.cs");
            designFiles = ("docs/design/lintmark/document/document-parser.md");
            verificationFiles = ("docs/verification/lintmark/document/document-parser.md");
            requirementFiles = ("docs/reqstream/lintmark/document/document-parser.yaml");
        }

        // Parsing needs the normalized text, builds the suppression map and reference index of each
        // document, and uses the Markdown parser library for the syntax tree.
        dependency DocumentParser to SourceDocument;
        dependency DocumentParser to SuppressionMap;
        dependency DocumentParser to ReferenceIndex;
        dependency DocumentParser to Markdig;
    }
}
