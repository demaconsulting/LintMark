package LintMark_ThematicBreakFormatter {
    private import SoftwareItemMetadata::*;
    private import LintMark_DocumentFormatter::*;
    private import LintMark_DocumentParser::*;
    private import LintMark_EditSet::*;
    private import LintMark_LintMarkConfig::*;
    private import LintMark_RuleCatalog::*;
    private import OtsDependencies::*;

    part def ThematicBreakFormatter {
        doc /* Proposes the edits that write thematic breaks in the canonical form: three characters of the configured kind with no spaces between them and no trailing whitespace, leaving a break as written where the conversion could create a setext underline, front matter, or a list marker or grid table border lookalike. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.LintMark/Formatting/Blocks/ThematicBreakFormatter.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/Formatting/Blocks/ThematicBreakFormatterTests.cs");
            designFiles = ("docs/design/lintmark/formatting/blocks/thematic-break-formatter.md");
            verificationFiles = ("docs/verification/lintmark/formatting/blocks/thematic-break-formatter.md");
            requirementFiles = ("docs/reqstream/lintmark/formatting/blocks/thematic-break-formatter.yaml");
        }

        // The formatter implements the block formatter contract, receives its context, uses the line
        // arithmetic and the front matter guard of the document formatter, and adds edits to the edit set.
        dependency ThematicBreakFormatter to DocumentFormatter;
        dependency ThematicBreakFormatter to EditSet;

        // It reads the break and its neighbors from the parsed document and the Markdown parser library,
        // the break style from the configuration, and tags each edit with a rule identifier from the rule
        // catalog.
        dependency ThematicBreakFormatter to DocumentParser;
        dependency ThematicBreakFormatter to Markdig;
        dependency ThematicBreakFormatter to LintMarkConfig;
        dependency ThematicBreakFormatter to RuleCatalog;
    }
}
