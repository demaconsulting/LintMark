package LintMark {
    private import SoftwareItemMetadata::*;

    part def IdempotencyChecker {
        doc /* Decides whether formatting is stable by formatting the already formatted text again through a supplied function and requiring the same text back, failing closed when the function throws or returns nothing. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.LintMark/Safety/IdempotencyChecker.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/Safety/IdempotencyCheckerTests.cs");
            designFiles = ("docs/design/lintmark/safety/idempotency-checker.md");
            verificationFiles = ("docs/verification/lintmark/safety/idempotency-checker.md");
            requirementFiles = ("docs/reqstream/lintmark/safety/idempotency-checker.yaml");
        }

        // The verdict is returned as an equivalence result.
        dependency IdempotencyChecker to EquivalenceResult;
    }
}
