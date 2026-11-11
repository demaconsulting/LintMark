package TemplateDotNetTool {
    private import SoftwareItemMetadata::*;

    part def UtilitiesSubsystem {
        doc /* Shared, independently testable helper functions used by other subsystems. */

        metadata info : SoftwareInfo {
            description = "Shared, independently testable helper functions used by other subsystems.";
            testFiles = ("test/DemaConsulting.TemplateDotNetTool.Tests/Utilities/UtilitiesSubsystemTests.cs");
            designFiles = ("docs/design/template-dot-net-tool/utilities.md");
            verificationFiles = ("docs/verification/template-dot-net-tool/utilities.md");
            requirementFiles = ("docs/reqstream/template-dot-net-tool/utilities.yaml");
        }

        part pathHelpers : PathHelpers;
    }
}
