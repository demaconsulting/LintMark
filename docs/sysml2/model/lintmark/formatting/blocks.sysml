package LintMark {
    private import SoftwareItemMetadata::*;

    part def BlocksSubsystem {
        doc /* Proposes the edits that write each kind of block in the canonical form: headings, code blocks, thematic breaks, block quotes, lists, pipe tables, and the wrapping of paragraphs, each as one formatter that leaves a block alone where a change could alter its meaning or how it renders. */

        metadata info : SoftwareInfo {
            testFiles = (
                "test/DemaConsulting.LintMark.Tests/Formatting/Blocks/BlocksSubsystemTests.cs",
                "test/DemaConsulting.LintMark.Tests/Formatting/Blocks/BlockFormatterAssertions.cs");
            designFiles = ("docs/design/lintmark/formatting/blocks.md");
            verificationFiles = ("docs/verification/lintmark/formatting/blocks.md");
            requirementFiles = ("docs/reqstream/lintmark/formatting/blocks.yaml");
        }

        part headingFormatter : HeadingFormatter;
        part codeBlockFormatter : CodeBlockFormatter;
        part thematicBreakFormatter : ThematicBreakFormatter;
        part blockQuoteFormatter : BlockQuoteFormatter;
        part listFormatter : ListFormatter;
        part tableFormatter : TableFormatter;
        part paragraphFormatter : ParagraphFormatter;
    }
}
