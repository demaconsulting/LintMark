package LintMark {
    private import SoftwareItemMetadata::*;

    part def BlockSyntaxGuard {
        doc /* Decides whether a word of a paragraph would be read as the start of another block if it began a line (a list or number marker, a heading marker, a quote, a fence, a table row or delimiter, a link reference or footnote definition, a block-level HTML start, a thematic break or setext underline, or a Pandoc construct) and whether a whole line is a Pandoc structural line that a paragraph must keep as written, using no state and giving the same answer for the same text. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.LintMark/Formatting/Inline/BlockSyntaxGuard.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/Formatting/Inline/BlockSyntaxGuardTests.cs");
            designFiles = ("docs/design/lintmark/formatting/inline/block-syntax-guard.md");
            verificationFiles = ("docs/verification/lintmark/formatting/inline/block-syntax-guard.md");
            requirementFiles = ("docs/reqstream/lintmark/formatting/inline/block-syntax-guard.yaml");
        }

        // The guard is pure text matching with regular expressions of the .NET base library; it uses no other
        // unit, so it declares no dependency.
    }
}
