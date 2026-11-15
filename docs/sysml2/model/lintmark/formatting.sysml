package LintMark {
    private import SoftwareItemMetadata::*;

    part def FormattingSubsystem {
        doc /* Turns a parsed document into a conflict-free set of text edits plus the line ending to write: it applies the rules that concern every document (whitespace, blank lines, spacing between blocks, and the end of the file), the block formatters of its Blocks subsystem, and the inline normalizer, line wrapper, and link refactorer of its Inline subsystem, reports each edit as a problem, and applies edits to the source text, without ever writing a file. */

        metadata info : SoftwareInfo {
            testFiles = (
                "test/DemaConsulting.LintMark.Tests/Formatting/FormattingSubsystemTests.cs",
                "test/DemaConsulting.LintMark.Tests/Formatting/TestBlockFormatters.cs");
            designFiles = ("docs/design/lintmark/formatting.md");
            verificationFiles = ("docs/verification/lintmark/formatting.md");
            requirementFiles = ("docs/reqstream/lintmark/formatting.yaml");
        }

        part documentFormatter : DocumentFormatter;
        part whitespacePasses : WhitespacePasses;
        part editSet : EditSet;
        part blocks : BlocksSubsystem;
        part inline : InlineSubsystem;
    }
}
