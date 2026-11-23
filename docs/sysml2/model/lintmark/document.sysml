package LintMark_DocumentSubsystem {
    private import SoftwareItemMetadata::*;
    public import LintMark_DocumentParser::*;
    public import LintMark_SourceDocument::*;
    public import LintMark_SuppressionMap::*;
    public import LintMark_TextWidth::*;

    part def DocumentSubsystem {
        doc /* Reads a Markdown file into an immutable document model: the LF-normalized source text with the facts needed to write it back unchanged, the classified blocks with corrected source ranges, the link reference definitions, the suppression comments, and the display width of text. */

        metadata info : SoftwareInfo {
            testFiles = (
                "test/DemaConsulting.LintMark.Tests/Document/DocumentSubsystemTests.cs",
                "test/DemaConsulting.LintMark.Tests/Document/SpanTilingAssertions.cs");
            designFiles = ("docs/design/lintmark/document.md");
            verificationFiles = ("docs/verification/lintmark/document.md");
            requirementFiles = ("docs/reqstream/lintmark/document.yaml");
        }

        part sourceDocument : SourceDocument;
        part documentParser : DocumentParser;
        part suppressionMap : SuppressionMap;
        part textWidth : TextWidth;
    }
}
