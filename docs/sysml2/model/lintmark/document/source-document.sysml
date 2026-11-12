package LintMark {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::*;

    enum def LineEnding {
        doc /* Line terminator style of a text file. */

        enum lf;
        enum crLf;
    }

    part def SourceDocument {
        doc /* Immutable, LF-normalized view of one Markdown file with the facts needed to write it back unchanged (also documents the LineEnding and LineEndingInfo supporting types). */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.LintMark/Document/SourceDocument.cs",
                "src/DemaConsulting.LintMark/Document/SourceDocumentException.cs",
                "src/DemaConsulting.LintMark/Document/LineEnding.cs",
                "src/DemaConsulting.LintMark/Document/LineEndingInfo.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/Document/SourceDocumentTests.cs");
            designFiles = ("docs/design/lintmark/document/source-document.md");
            verificationFiles = ("docs/verification/lintmark/document/source-document.md");
            requirementFiles = ("docs/reqstream/lintmark/document/source-document.yaml");
        }

        // Source of the key facts: path, normalized text, terminator counts, and BOM presence.
        attribute path : String;
        attribute text : String;
        attribute lfCount : Integer;
        attribute crLfCount : Integer;
        attribute hasBom : Boolean;
        attribute hasFinalNewline : Boolean;
        attribute majorityLineEnding : LineEnding;
    }
}
