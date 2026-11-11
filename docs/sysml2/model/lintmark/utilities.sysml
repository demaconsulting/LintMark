package LintMark {
    private import SoftwareItemMetadata::*;

    part def UtilitiesSubsystem {
        doc /* Shared, independently testable helper functions used by other subsystems. */

        metadata info : SoftwareInfo {
            description = "Shared, independently testable helper functions used by other subsystems.";
            testFiles = ("test/DemaConsulting.LintMark.Tests/Utilities/UtilitiesSubsystemTests.cs");
            designFiles = ("docs/design/lintmark/utilities.md");
            verificationFiles = ("docs/verification/lintmark/utilities.md");
            requirementFiles = ("docs/reqstream/lintmark/utilities.yaml");
        }

        part pathHelpers : PathHelpers;
    }
}
