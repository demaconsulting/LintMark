package LintMark {
    private import SoftwareItemMetadata::*;

    part def BlocksSubsystem {
        doc /* Holds the block formatters of the tool, which propose the changes that bring each heading, code block, thematic break, list, and block quote of a document into canonical form without ever changing the content of code. */

        metadata info : SoftwareInfo {
            testFiles = (
                "test/DemaConsulting.LintMark.Tests/Formatting/Blocks/BlocksSubsystemTests.cs",
                "test/DemaConsulting.LintMark.Tests/Formatting/Blocks/BlocksCorpusTests.cs",
                "test/DemaConsulting.LintMark.Tests/Formatting/Blocks/ListQuoteInlineCorpusTests.cs",
                "test/DemaConsulting.LintMark.Tests/Formatting/Blocks/ListQuoteInlineDocuments.cs",
                "test/DemaConsulting.LintMark.Tests/Formatting/Blocks/BlockFormatterAssertions.cs");
            designFiles = ("docs/design/lintmark/formatting/blocks.md");
            verificationFiles = ("docs/verification/lintmark/formatting/blocks.md");
            requirementFiles = ("docs/reqstream/lintmark/formatting/blocks.yaml");
        }

        part headingFormatter : HeadingFormatter;
        part codeBlockFormatter : CodeBlockFormatter;
        part thematicBreakFormatter : ThematicBreakFormatter;
        part listFormatter : ListFormatter;
        part blockQuoteFormatter : BlockQuoteFormatter;
    }
}
