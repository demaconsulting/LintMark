package TemplateDotNetTool {
    private import SoftwareItemMetadata::*;

    part def Validation {
        doc /* Orchestrates the self-validation test suite and reports pass/fail results. */

        metadata info : SoftwareInfo {
            description = "Orchestrates the self-validation test suite and reports pass/fail results.";
            sourceFiles = ("src/DemaConsulting.TemplateDotNetTool/SelfTest/Validation.cs");
            testFiles = ("test/DemaConsulting.TemplateDotNetTool.Tests/SelfTest/ValidationTests.cs");
            designFiles = ("docs/design/template-dot-net-tool/self-test/validation.md");
            verificationFiles = ("docs/verification/template-dot-net-tool/self-test/validation.md");
            requirementFiles = ("docs/reqstream/template-dot-net-tool/self-test/validation.yaml");
        }
    }
}
