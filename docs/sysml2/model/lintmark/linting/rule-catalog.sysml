package LintMark {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::*;

    enum def DiagnosticKind {
        doc /* Whether rewriting the file removes a finding. */

        enum lintOnly;
        enum formatting;
    }

    item def Diagnostic {
        doc /* One finding about a file, located at a 1-based line and column and identified by a kebab-case rule id; immutable with value equality. */

        attribute ruleId : String;
        attribute kind : DiagnosticKind;
        attribute path : String;
        attribute line : Integer;
        attribute column : Integer;
        // The message property of the C# type is named messageText here because message is a SysML2 keyword.
        attribute messageText : String;
        // Derived: true exactly when kind is formatting, so the two can never disagree.
        attribute fixable : Boolean;
    }

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
        doc /* The single registry of every rule the tool knows: identifiers, kinds, descriptions, aliases, dropped aliases and defaults, including the formatting rules that the tool always applies and their legacy rule numbers, from which configuration validation, the resolution of rule names in suppression comments, the rules forced on or off by the command line, and the identifiers of emitted diagnostics are derived (also documents the Diagnostic, DiagnosticKind, RuleInfo, and RuleOverrides supporting types). */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.LintMark/Linting/RuleCatalog.cs",
                "src/DemaConsulting.LintMark/Linting/Diagnostic.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/Linting/RuleCatalogTests.cs");
            designFiles = ("docs/design/lintmark/linting/rule-catalog.md");
            verificationFiles = ("docs/verification/lintmark/linting/rule-catalog.md");
            requirementFiles = ("docs/reqstream/lintmark/linting/rule-catalog.yaml");
        }

        // The rules forced on or off by the command line are applied to a configuration, which is the
        // settings object of the configuration subsystem.
        dependency RuleCatalog to LintMarkConfig;
    }
}
