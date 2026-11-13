package LintMark {
    private import SoftwareItemMetadata::*;

    part def SourcePatcher {
        doc /* Applies an ordered, non-overlapping array of edits to the text of a source document and returns the patched LF-normalized text. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.LintMark/Formatting/SourcePatcher.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/Formatting/SourcePatcherTests.cs");
            designFiles = ("docs/design/lintmark/formatting/source-patcher.md");
            verificationFiles = ("docs/verification/lintmark/formatting/source-patcher.md");
            requirementFiles = ("docs/reqstream/lintmark/formatting/source-patcher.yaml");
        }

        // The patcher reads the text to patch from the source document and applies the edits it is given.
        dependency SourcePatcher to SourceDocument;
        dependency SourcePatcher to TextEdit;
    }
}
