package LintMark {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::*;

    enum def SafetyStatus {
        doc /* The outcome of the safety checks of a proposed rewrite. */

        enum ok;
        enum htmlMismatch;
        enum structureMismatch;
        enum notIdempotent;
    }

    part def SafetySubsystem {
        doc /* The safety net that decides whether a rewrite may be written: it must render the same HTML, keep the Pandoc structural tokens, and be stable when formatted again, and each check returns its verdict as an equivalence result (also documents the SafetyStatus supporting type, which the engine uses). */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.LintMark.Tests/Safety/SafetySubsystemTests.cs");
            designFiles = ("docs/design/lintmark/safety.md");
            verificationFiles = ("docs/verification/lintmark/safety.md");
            requirementFiles = ("docs/reqstream/lintmark/safety.yaml");
        }

        part htmlEquivalenceChecker : HtmlEquivalenceChecker;
        part structuralTokenChecker : StructuralTokenChecker;
        part idempotencyChecker : IdempotencyChecker;
        part equivalenceResult : EquivalenceResult;
    }
}
