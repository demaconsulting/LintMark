package LintMark {
    private import SoftwareItemMetadata::*;

    part def PathHelpers {
        doc /* Safe path-combination utility guarding against path-traversal. */

        metadata info : SoftwareInfo {
            description = "Safe path-combination utility guarding against path-traversal.";
            sourceFiles = ("src/DemaConsulting.LintMark/Utilities/PathHelpers.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/Utilities/PathHelpersTests.cs");
            designFiles = ("docs/design/lintmark/utilities/path-helpers.md");
            verificationFiles = ("docs/verification/lintmark/utilities/path-helpers.md");
            requirementFiles = ("docs/reqstream/lintmark/utilities/path-helpers.yaml");
        }
    }
}
