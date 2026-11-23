package LintMark_ListFormatter {
    private import SoftwareItemMetadata::*;
    private import LintMark_DocumentParser::*;
    private import LintMark_EditSet::*;
    private import LintMark_LineWrapper::*;
    private import LintMark_LintMarkConfig::*;
    private import LintMark_RuleCatalog::*;
    private import OtsDependencies::*;

    part def ListFormatter {
        doc /* Proposes the edits that write each outermost list with the configured bullet marker, the configured ordered numbering style, aligned item columns, nested bullet lists indented as configured, and exactly one space after each marker, and leaves a list alone where a change could alter its meaning or how it renders (also documents the ListModel and ListPlan supporting types, the immutable model of one list with its items, columns, markers, and line ranges, and the pure plan that decides the changes and the shift of each line). */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.LintMark/Formatting/Blocks/ListFormatter.cs",
                "src/DemaConsulting.LintMark/Formatting/Blocks/ListModel.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/Formatting/Blocks/ListFormatterTests.cs");
            designFiles = ("docs/design/lintmark/formatting/blocks/list-formatter.md");
            verificationFiles = ("docs/verification/lintmark/formatting/blocks/list-formatter.md");
            requirementFiles = ("docs/reqstream/lintmark/formatting/blocks/list-formatter.yaml");
        }

        // The formatter implements the block formatter contract, receives its context, uses the line
        // arithmetic and the line ownership of the document formatter, and adds edits to the edit set.
        dependency ListFormatter to LintMark_DocumentFormatter::DocumentFormatter;
        dependency ListFormatter to EditSet;

        // It reads the list, its items, and the blocks around them from the parsed document and the Markdown
        // parser library, the bullet marker, the numbering style, and the nested list indent it writes from
        // the configuration, and tags each edit with a rule identifier from the rule catalog.
        dependency ListFormatter to DocumentParser;
        dependency ListFormatter to Markdig;
        dependency ListFormatter to LintMarkConfig;
        dependency ListFormatter to RuleCatalog;

        // A list item that starts a line that could be read as block syntax is recognized with the wrap
        // guard of the line wrapper.
        dependency ListFormatter to LineWrapper;
    }
}
