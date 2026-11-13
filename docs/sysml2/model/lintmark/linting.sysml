package LintMark {
    private import SoftwareItemMetadata::*;

    part def LintingSubsystem {
        doc /* The vocabulary of findings: the diagnostic every check reports and the catalog of all rules with their identifiers, aliases and defaults. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.LintMark.Tests/Linting/LintingSubsystemTests.cs");
            designFiles = ("docs/design/lintmark/linting.md");
            verificationFiles = ("docs/verification/lintmark/linting.md");
            requirementFiles = ("docs/reqstream/lintmark/linting.yaml");
        }

        part diagnostic : Diagnostic;
        part ruleCatalog : RuleCatalog;
    }
}
