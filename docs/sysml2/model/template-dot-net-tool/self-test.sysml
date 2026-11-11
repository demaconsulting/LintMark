package TemplateDotNetTool {
    private import SoftwareItemMetadata::*;

    part def SelfTestSubsystem {
        doc /* Self-validation framework exercising the tool's own capabilities on demand. */

        metadata info : SoftwareInfo {
            description = "Self-validation framework exercising the tool's own capabilities on demand.";
            testFiles = ("test/DemaConsulting.TemplateDotNetTool.Tests/SelfTest/SelfTestSubsystemTests.cs");
            designFiles = ("docs/design/template-dot-net-tool/self-test.md");
            verificationFiles = ("docs/verification/template-dot-net-tool/self-test.md");
            requirementFiles = ("docs/reqstream/template-dot-net-tool/self-test.yaml");
        }

        part validation : Validation;
    }
}
