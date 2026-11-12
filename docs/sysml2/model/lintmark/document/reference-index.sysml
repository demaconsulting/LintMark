package LintMark {
    private import SoftwareItemMetadata::*;

    part def ReferenceIndex {
        doc /* Indexes the link reference definitions of a document and resolves references by label (also documents the ReferenceDefinition supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.LintMark/Document/ReferenceIndex.cs",
                "src/DemaConsulting.LintMark/Document/ReferenceDefinition.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/Document/ReferenceIndexTests.cs");
            designFiles = ("docs/design/lintmark/document/reference-index.md");
            verificationFiles = ("docs/verification/lintmark/document/reference-index.md");
            requirementFiles = ("docs/reqstream/lintmark/document/reference-index.yaml");
        }

        // Line numbers come from the source document, and definitions are read from the parser's block nodes.
        dependency ReferenceIndex to SourceDocument;
        dependency ReferenceIndex to Markdig;

        // The block list types are owned by the DocumentParser unit; this is a type-level use only, with
        // no behavioral call back into the parser.
        dependency ReferenceIndex to BlockInfo;
        dependency ReferenceIndex to BlockKind;
    }
}
