package LintMark {
    private import SoftwareItemMetadata::*;

    part def InlineSubsystem {
        doc /* Holds the inline normalizer of the tool, which proposes the changes that write the emphasis, strong emphasis, and bare URLs inside each paragraph that no block formatter claims in canonical form, and only where the paragraph renders the same afterwards. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.LintMark.Tests/Formatting/Inline/InlineSubsystemTests.cs");
            designFiles = ("docs/design/lintmark/formatting/inline.md");
            verificationFiles = ("docs/verification/lintmark/formatting/inline.md");
            requirementFiles = ("docs/reqstream/lintmark/formatting/inline.yaml");
        }

        part inlineNormalizer : InlineNormalizer;
    }
}
