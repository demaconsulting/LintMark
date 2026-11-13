package LintMark {
    private import SoftwareItemMetadata::*;

    part def StructuralTokenChecker {
        doc /* Decides whether a rewrite keeps the structural tokens that Pandoc reads but the HTML rendering cannot show (fences, display math delimiters, new-page commands, grid borders, attribute groups and citations) in the same order and count. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.LintMark/Safety/StructuralTokenChecker.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/Safety/StructuralTokenCheckerTests.cs");
            designFiles = ("docs/design/lintmark/safety/structural-token-checker.md");
            verificationFiles = ("docs/verification/lintmark/safety/structural-token-checker.md");
            requirementFiles = ("docs/reqstream/lintmark/safety/structural-token-checker.yaml");
        }

        // The verdict is returned as an equivalence result.
        dependency StructuralTokenChecker to EquivalenceResult;
    }
}
