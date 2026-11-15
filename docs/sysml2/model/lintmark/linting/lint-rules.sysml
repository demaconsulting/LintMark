package LintMark {
    private import SoftwareItemMetadata::*;

    part def LintRules {
        doc /* Runs the lint-only rules over a parsed document and keeps the diagnostics that are switched on and not suppressed: the rule engine and the plug-in contract and shared helpers of the rules, and the rule families for headings, links and reference definitions, document structure and stray marker characters, and tables (also documents the ILintRule contract and the RuleSupport helpers). */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.LintMark/Linting/LintRuleEngine.cs",
                "src/DemaConsulting.LintMark/Linting/HeadingRules.cs",
                "src/DemaConsulting.LintMark/Linting/LinkRules.cs",
                "src/DemaConsulting.LintMark/Linting/StructureRules.cs",
                "src/DemaConsulting.LintMark/Linting/TableRules.cs");
            testFiles = (
                "test/DemaConsulting.LintMark.Tests/Linting/LintRuleEngineTests.cs",
                "test/DemaConsulting.LintMark.Tests/Linting/HeadingRulesTests.cs",
                "test/DemaConsulting.LintMark.Tests/Linting/LinkRulesTests.cs",
                "test/DemaConsulting.LintMark.Tests/Linting/StructureRulesTests.cs",
                "test/DemaConsulting.LintMark.Tests/Linting/TableRulesTests.cs");
            designFiles = ("docs/design/lintmark/linting/lint-rules.md");
            verificationFiles = ("docs/verification/lintmark/linting/lint-rules.md");
            requirementFiles = ("docs/reqstream/lintmark/linting/lint-rules.yaml");
        }

        // The rules read the parsed document (its blocks, source text, suppressions, and link reference
        // definitions) and the syntax tree of the Markdown parser library, and are told which rules are
        // switched on by the configuration.
        dependency LintRules to DocumentParser;
        dependency LintRules to SourceDocument;
        dependency LintRules to LintMarkConfig;
        dependency LintRules to Markdig;

        // Every finding is a diagnostic with a rule identifier registered in the rule catalog.
        dependency LintRules to RuleCatalog;

        // The table rules measure cells in display columns and share the pipe table reading of the table
        // formatter; the emphasis and link rules share the inline map and the line layout of the formatting
        // subsystem.
        dependency LintRules to TextWidth;
        dependency LintRules to TableFormatter;
        dependency LintRules to WhitespacePasses;
        dependency LintRules to DocumentFormatter;
    }
}
