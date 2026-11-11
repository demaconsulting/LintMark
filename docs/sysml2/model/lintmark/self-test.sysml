package LintMark {
    private import SoftwareItemMetadata::*;

    part def SelfTestSubsystem {
        doc /* Self-validation framework exercising the tool's own capabilities on demand. */

        metadata info : SoftwareInfo {
            description = "Self-validation framework exercising the tool's own capabilities on demand.";
            testFiles = ("test/DemaConsulting.LintMark.Tests/SelfTest/SelfTestSubsystemTests.cs");
            designFiles = ("docs/design/lintmark/self-test.md");
            verificationFiles = ("docs/verification/lintmark/self-test.md");
            requirementFiles = ("docs/reqstream/lintmark/self-test.yaml");
        }

        part validation : Validation;
    }
}
