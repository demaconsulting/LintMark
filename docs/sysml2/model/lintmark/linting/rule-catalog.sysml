package LintMark {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::*;

    attribute def RuleInfo {
        doc /* One entry of the rule catalog: the rule's identifier, kind, description, accepted aliases and configuration behavior. */

        attribute id : String;
        attribute kind : DiagnosticKind;
        attribute descriptionText : String;
        attribute aliases : String [0..*];
        attribute defaultEnabled : Boolean;
        attribute isConfigurable : Boolean;
    }

    part def RuleCatalog {
        doc /* The single registry of every rule the tool knows: identifiers, kinds, descriptions, aliases, dropped aliases and defaults, from which configuration validation and the identifiers of emitted diagnostics are derived (also documents the RuleInfo supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.LintMark/Linting/RuleCatalog.cs",
                "src/DemaConsulting.LintMark/Linting/RuleInfo.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/Linting/RuleCatalogTests.cs");
            designFiles = ("docs/design/lintmark/linting/rule-catalog.md");
            verificationFiles = ("docs/verification/lintmark/linting/rule-catalog.md");
            requirementFiles = ("docs/reqstream/lintmark/linting/rule-catalog.yaml");
        }

        // Each rule is described by a rule info entry whose kind is the diagnostic kind enumeration.
        dependency RuleCatalog to RuleInfo;
        dependency RuleCatalog to DiagnosticKind;
    }
}
