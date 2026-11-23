package LintMark_TextWidth {
    private import SoftwareItemMetadata::*;

    part def TextWidth {
        doc /* Measures the display width of a string in terminal columns, counting each grapheme cluster once as zero, one or two columns (combining marks and control characters as zero, East Asian wide characters and emoji as two). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.LintMark/Document/TextWidth.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/Document/TextWidthTests.cs");
            designFiles = ("docs/design/lintmark/document/text-width.md");
            verificationFiles = ("docs/verification/lintmark/document/text-width.md");
            requirementFiles = ("docs/reqstream/lintmark/document/text-width.yaml");
        }
    }
}
