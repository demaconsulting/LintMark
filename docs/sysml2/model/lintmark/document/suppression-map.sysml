package LintMark {
    private import SoftwareItemMetadata::*;

    part def SuppressionMap {
        doc /* Resolves suppression comments into offset ranges and answers whether a rule is suppressed at a position. */

        metadata info : SoftwareInfo {
            description = "Resolves suppression comments into offset ranges and answers whether a rule is suppressed at a position (also documents the SuppressionRange and RuleSet supporting types).";
            sourceFiles = (
                "src/DemaConsulting.LintMark/Document/SuppressionMap.cs",
                "src/DemaConsulting.LintMark/Document/SuppressionRange.cs",
                "src/DemaConsulting.LintMark/Document/RuleSet.cs",
                "src/DemaConsulting.LintMark/Document/RuleSetKind.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/Document/SuppressionMapTests.cs");
            designFiles = ("docs/design/lintmark/document/suppression-map.md");
            verificationFiles = ("docs/verification/lintmark/document/suppression-map.md");
            requirementFiles = ("docs/reqstream/lintmark/document/suppression-map.yaml");
        }

        // Positions come from the source document, and directives are found in the parser's syntax tree.
        dependency SuppressionMap to SourceDocument;
        dependency SuppressionMap to Markdig;

        // The block list types are owned by the DocumentParser unit; this is a type-level use only, with
        // no behavioral call back into the parser.
        dependency SuppressionMap to BlockInfo;
        dependency SuppressionMap to BlockKind;
    }
}
