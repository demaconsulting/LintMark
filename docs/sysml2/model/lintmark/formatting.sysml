package LintMark {
    private import SoftwareItemMetadata::*;

    part def FormattingSubsystem {
        doc /* Turns a parsed document into a conflict-free set of text edits plus the line ending to write, and applies edits to the source text, without ever writing a file. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.LintMark.Tests/Formatting/FormattingSubsystemTests.cs");
            designFiles = ("docs/design/lintmark/formatting.md");
            verificationFiles = ("docs/verification/lintmark/formatting.md");
            requirementFiles = ("docs/reqstream/lintmark/formatting.yaml");
        }

        part editSet : EditSet;
        part sourcePatcher : SourcePatcher;
        part documentFormatter : DocumentFormatter;
    }
}
