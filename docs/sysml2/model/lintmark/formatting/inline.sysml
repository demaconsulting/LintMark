package LintMark {
    private import SoftwareItemMetadata::*;

    part def InlineSubsystem {
        doc /* Holds the inline normalizer of the tool, which proposes the changes that write the emphasis, strong emphasis, bare URLs, and code spans inside each paragraph that no block formatter claims in canonical form, and only where the paragraph renders the same afterwards, and the atom tokenizer, line wrapper, and block syntax guard that the paragraph formatter of the Blocks subsystem uses to fill a paragraph up to the line length without splitting an inline construct or starting a line with block syntax. */

        metadata info : SoftwareInfo {
            testFiles = (
                "test/DemaConsulting.LintMark.Tests/Formatting/Inline/InlineSubsystemTests.cs",
                "test/DemaConsulting.LintMark.Tests/Formatting/Inline/InlineSubsystemWrapTests.cs");
            designFiles = ("docs/design/lintmark/formatting/inline.md");
            verificationFiles = ("docs/verification/lintmark/formatting/inline.md");
            requirementFiles = ("docs/reqstream/lintmark/formatting/inline.yaml");
        }

        part inlineNormalizer : InlineNormalizer;
        part atomTokenizer : AtomTokenizer;
        part lineWrapper : LineWrapper;
        part blockSyntaxGuard : BlockSyntaxGuard;
    }
}
