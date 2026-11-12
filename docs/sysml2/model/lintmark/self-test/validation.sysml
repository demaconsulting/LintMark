package LintMark {
    private import SoftwareItemMetadata::*;

    part def Validation {
        doc /* Orchestrates the self-validation test suite and reports pass/fail results. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.LintMark/SelfTest/Validation.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/SelfTest/ValidationTests.cs");
            designFiles = ("docs/design/lintmark/self-test/validation.md");
            verificationFiles = ("docs/verification/lintmark/self-test/validation.md");
            requirementFiles = ("docs/reqstream/lintmark/self-test/validation.yaml");
        }
    }
}
