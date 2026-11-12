package LintMark {
    private import SoftwareItemMetadata::*;

    part def DocumentSubsystem {
        doc /* Loads Markdown files into a normalized, source-accurate document model with suppression directives and reference definitions. */

        metadata info : SoftwareInfo {
            testFiles = (
                "test/DemaConsulting.LintMark.Tests/Document/DocumentSubsystemTests.cs",
                "test/DemaConsulting.LintMark.Tests/Document/SpanTilingAssertions.cs",
                "test/DemaConsulting.LintMark.Tests/Document/SyntheticDocuments.cs",
                "test/DemaConsulting.LintMark.Tests/Document/SyntheticDocument.cs",
                "test/DemaConsulting.LintMark.Tests/Document/RandomDocuments.cs",
                "test/DemaConsulting.LintMark.Tests/Document/RepositoryMarkdownFiles.cs");
            designFiles = ("docs/design/lintmark/document.md");
            verificationFiles = ("docs/verification/lintmark/document.md");
            requirementFiles = ("docs/reqstream/lintmark/document.yaml");
        }

        part sourceDocument : SourceDocument;
        part documentParser : DocumentParser;
        part suppressionMap : SuppressionMap;
        part referenceIndex : ReferenceIndex;
    }
}
