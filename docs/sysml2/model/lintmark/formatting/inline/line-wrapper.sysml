package LintMark_LineWrapper {
    private import SoftwareItemMetadata::*;
    private import LintMark_DocumentParser::*;
    private import LintMark_LintMarkConfig::*;
    private import LintMark_TextWidth::*;
    private import OtsDependencies::*;
    private import ScalarValues::*;

    attribute def WrappedLines {
        doc /* The lines that a run of atoms was laid out into, and whether a line that could have been broken stays wider than the line length. */

        attribute lines : String [0..*];
        attribute hasBreakableOverflow : Boolean;
    }

    part def LineWrapper {
        doc /* Fills lines with atoms: puts as many atoms on a line as fit in the width, or lays them out one sentence or one clause per line in the sentence and semantic modes, never starts a line with block syntax, and never splits an atom; it also decides before a paragraph is wrapped whether its text must stay exactly as written, and recognizes the words that may not start a line (also documents the WrappedLines, SentenceSplitter, WrapGuard, and BlockSyntaxGuard supporting types). */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.LintMark/Formatting/Inline/LineWrapper.cs",
                "src/DemaConsulting.LintMark/Formatting/Inline/WrapGuard.cs");
            testFiles = (
                "test/DemaConsulting.LintMark.Tests/Formatting/Inline/LineWrapperTests.cs",
                "test/DemaConsulting.LintMark.Tests/Formatting/Inline/BlockSyntaxGuardTests.cs");
            designFiles = ("docs/design/lintmark/formatting/inline/line-wrapper.md");
            verificationFiles = ("docs/verification/lintmark/formatting/inline/line-wrapper.md");
            requirementFiles = ("docs/reqstream/lintmark/formatting/inline/line-wrapper.yaml");
        }

        // The wrapper reads the paragraph, its blocks, and the lines of the paragraph from the parsed
        // document and the document parser, and the inline syntax tree from the Markdown parser library; the
        // wrap mode comes from the configuration.
        dependency LineWrapper to DocumentParser;
        dependency LineWrapper to Markdig;
        dependency LineWrapper to LintMarkConfig;

        // Line widths are measured in display columns.
        dependency LineWrapper to TextWidth;
    }
}
