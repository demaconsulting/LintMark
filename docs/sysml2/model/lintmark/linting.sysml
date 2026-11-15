package LintMark {
    private import SoftwareItemMetadata::*;

    part def LintingSubsystem {
        doc /* Knows every rule the tool has (identifiers, kinds, descriptions, aliases, and defaults), carries each finding as a diagnostic, and runs the lint-only rules for headings, links, document structure, and tables over a parsed document, keeping the findings that are switched on and not suppressed. */

        metadata info : SoftwareInfo {
            testFiles = (
                "test/DemaConsulting.LintMark.Tests/Linting/LintingSubsystemTests.cs",
                "test/DemaConsulting.LintMark.Tests/Linting/LintRuleTestSupport.cs");
            designFiles = ("docs/design/lintmark/linting.md");
            verificationFiles = ("docs/verification/lintmark/linting.md");
            requirementFiles = ("docs/reqstream/lintmark/linting.yaml");
        }

        part ruleCatalog : RuleCatalog;
        part lintRules : LintRules;
    }
}
