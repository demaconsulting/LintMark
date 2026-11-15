package LintMark {
    private import SoftwareItemMetadata::*;

    part def SuppressionMap {
        doc /* Resolves suppression comments (disable, enable, next-line, and file-wide directives) into offset ranges and answers whether a rule is suppressed at a position (also documents the SuppressionRange, RuleSet, and RuleSetKind supporting types). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.LintMark/Document/SuppressionMap.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/Document/SuppressionMapTests.cs");
            designFiles = ("docs/design/lintmark/document/suppression-map.md");
            verificationFiles = ("docs/verification/lintmark/document/suppression-map.md");
            requirementFiles = ("docs/reqstream/lintmark/document/suppression-map.yaml");
        }

        // Positions come from the source document, and directives are found in the syntax tree of the
        // Markdown parser library.
        dependency SuppressionMap to SourceDocument;
        dependency SuppressionMap to Markdig;

        // The block list types are owned by the document parser; this is a type-level use only, with no
        // behavioral call back into the parser.
        dependency SuppressionMap to DocumentParser;
    }
}
