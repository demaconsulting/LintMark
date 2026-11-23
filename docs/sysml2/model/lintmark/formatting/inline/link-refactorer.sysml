package LintMark_LinkRefactorer {
    private import SoftwareItemMetadata::*;
    private import LintMark_DocumentParser::*;
    private import LintMark_EditSet::*;
    private import LintMark_LintMarkConfig::*;
    private import LintMark_LintRules::*;
    private import LintMark_RuleCatalog::*;
    private import LintMark_TableFormatter::*;
    private import LintMark_TextWidth::*;
    private import OtsDependencies::*;

    part def LinkRefactorer {
        doc /* Converts inline links and images to reference links (the opt-in link-refactor setting) with labels derived from the link text and reused for definitions with the same destination and title, keeping every definition in one block at the end of the document and leaving exempt blocks as written, and proposing the edits as one plan with the conversions it makes (also documents the LinkRefactorPlan, LinkConversion, LinkLabelAllocator, and InlineLinkTail supporting types). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.LintMark/Formatting/Inline/LinkRefactorer.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/Formatting/Inline/LinkRefactorerTests.cs");
            designFiles = ("docs/design/lintmark/formatting/inline/link-refactorer.md");
            verificationFiles = ("docs/verification/lintmark/formatting/inline/link-refactorer.md");
            requirementFiles = ("docs/reqstream/lintmark/formatting/inline/link-refactorer.yaml");
        }

        // The refactorer reads the links, the blocks, and the existing reference definitions from the parsed
        // document and the Markdown parser library, and the conversion options from the configuration.
        dependency LinkRefactorer to DocumentParser;
        dependency LinkRefactorer to Markdig;
        dependency LinkRefactorer to LintMarkConfig;

        // Exempt blocks and the end of the file are found with the line arithmetic and the line ownership of
        // the document formatter, edits are added to the edit set, and each edit is tagged with a rule
        // identifier from the rule catalog.
        dependency LinkRefactorer to LintMark_DocumentFormatter::DocumentFormatter;
        dependency LinkRefactorer to EditSet;
        dependency LinkRefactorer to RuleCatalog;

        // The plain text of a link is taken with the helpers of the lint rules, and a link in a table cell
        // is measured, in display columns, with the pipe table reading of the table formatter, so that a
        // conversion never makes a table wider than the line length allows.
        dependency LinkRefactorer to LintRules;
        dependency LinkRefactorer to TableFormatter;
        dependency LinkRefactorer to TextWidth;
    }
}
