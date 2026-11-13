package LintMark {
    private import SoftwareItemMetadata::*;

    part def BlocksSubsystem {
        doc /* Holds the block formatters of the tool, which propose the changes that bring each heading, code block, and thematic break of a document into canonical form without ever changing the content of code. */

        metadata info : SoftwareInfo {
            testFiles = (
                "test/DemaConsulting.LintMark.Tests/Formatting/Blocks/BlocksSubsystemTests.cs",
                "test/DemaConsulting.LintMark.Tests/Formatting/Blocks/BlocksCorpusTests.cs",
                "test/DemaConsulting.LintMark.Tests/Formatting/Blocks/BlockFormatterAssertions.cs");
            designFiles = ("docs/design/lintmark/formatting/blocks.md");
            verificationFiles = ("docs/verification/lintmark/formatting/blocks.md");
            requirementFiles = ("docs/reqstream/lintmark/formatting/blocks.yaml");
        }

        part headingFormatter : HeadingFormatter;
        part codeBlockFormatter : CodeBlockFormatter;
        part thematicBreakFormatter : ThematicBreakFormatter;
    }
}
