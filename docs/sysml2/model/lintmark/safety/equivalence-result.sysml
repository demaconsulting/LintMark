package LintMark {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::*;

    part def EquivalenceResult {
        doc /* The verdict of one safety check: equivalent without a description, or different with a description of the first difference in one line of bounded length, plus the helpers that locate and excerpt the first difference of two texts. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.LintMark/Safety/EquivalenceResult.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/Safety/EquivalenceResultTests.cs");
            designFiles = ("docs/design/lintmark/safety/equivalence-result.md");
            verificationFiles = ("docs/verification/lintmark/safety/equivalence-result.md");
            requirementFiles = ("docs/reqstream/lintmark/safety/equivalence-result.yaml");
        }

        // Whether the texts are equivalent.
        attribute isEquivalent : Boolean;
        // Present only when the texts are not equivalent.
        attribute descriptionText : String [0..1];
    }
}
