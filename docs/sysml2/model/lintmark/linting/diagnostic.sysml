package LintMark {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::*;

    enum def DiagnosticKind {
        doc /* Whether rewriting the file removes a finding. */

        enum lintOnly;
        enum formatting;
    }

    part def Diagnostic {
        doc /* One finding about a file, located at a 1-based line and column and identified by a kebab-case rule id; immutable with value equality (also documents the DiagnosticKind supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.LintMark/Linting/Diagnostic.cs",
                "src/DemaConsulting.LintMark/Linting/DiagnosticKind.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/Linting/DiagnosticTests.cs");
            designFiles = ("docs/design/lintmark/linting/diagnostic.md");
            verificationFiles = ("docs/verification/lintmark/linting/diagnostic.md");
            requirementFiles = ("docs/reqstream/lintmark/linting/diagnostic.yaml");
        }

        attribute ruleId : String;
        attribute kind : DiagnosticKind;
        attribute path : String;
        attribute line : Integer;
        attribute column : Integer;
        // The message property of the C# type is named messageText here because message is a SysML2 keyword.
        attribute messageText : String;
        // Derived: true exactly when kind is formatting, so the two can never disagree.
        attribute fixable : Boolean;

        // The kind of a finding is the diagnostic kind enumeration.
        dependency Diagnostic to DiagnosticKind;
    }
}
