package TemplateDotNetTool {
    private import SoftwareItemMetadata::*;

    part def PathHelpers {
        doc /* Safe path-combination utility guarding against path-traversal. */

        metadata info : SoftwareInfo {
            description = "Safe path-combination utility guarding against path-traversal.";
            sourceFiles = ("src/DemaConsulting.TemplateDotNetTool/Utilities/PathHelpers.cs");
            testFiles = ("test/DemaConsulting.TemplateDotNetTool.Tests/Utilities/PathHelpersTests.cs");
            designFiles = ("docs/design/template-dot-net-tool/utilities/path-helpers.md");
            verificationFiles = ("docs/verification/template-dot-net-tool/utilities/path-helpers.md");
            requirementFiles = ("docs/reqstream/template-dot-net-tool/utilities/path-helpers.yaml");
        }
    }
}
