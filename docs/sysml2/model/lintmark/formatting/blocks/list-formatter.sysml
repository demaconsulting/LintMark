package LintMark {
    private import SoftwareItemMetadata::*;

    part def ListFormatter {
        doc /* Proposes the edits that write each outermost list with the configured bullet marker, the configured ordered numbering style, aligned item columns, nested bullet lists indented as configured, and exactly one space after each marker, and leaves a list alone where a change could alter its meaning or how it renders (also documents the ListModel and ListPlan supporting types, the immutable model of one list with its items, columns, markers, and line ranges, and the pure plan that decides the changes and the shift of each line). */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.LintMark/Formatting/Blocks/ListFormatter.cs",
                "src/DemaConsulting.LintMark/Formatting/Blocks/ListModel.cs",
                "src/DemaConsulting.LintMark/Formatting/Blocks/ListPlan.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/Formatting/Blocks/ListFormatterTests.cs");
            designFiles = ("docs/design/lintmark/formatting/blocks/list-formatter.md");
            verificationFiles = ("docs/verification/lintmark/formatting/blocks/list-formatter.md");
            requirementFiles = ("docs/reqstream/lintmark/formatting/blocks/list-formatter.yaml");
        }

        // The formatter implements the block formatter contract, receives its context, and adds edits to the
        // edit set of the document.
        dependency ListFormatter to IBlockFormatter;
        dependency ListFormatter to FormatContext;
        dependency ListFormatter to EditSet;
        dependency ListFormatter to TextEdit;

        // It reads the list, its items, and the blocks around them from the parsed document, and the bullet
        // marker, the numbering style, and the nested list indent it writes from the configuration.
        dependency ListFormatter to ParsedDocument;
        dependency ListFormatter to BlockInfo;
        dependency ListFormatter to BlockKind;
        dependency ListFormatter to LintMarkConfig;
        dependency ListFormatter to BulletMarker;
        dependency ListFormatter to OrderedStyle;

        // Each edit is tagged with a rule identifier from the rule catalog, and the list nodes come from the
        // Markdown parser library, which also renders the list before and after a change with the fixed
        // pipeline of the document parser, so that a change that alters the rendering is dropped.
        dependency ListFormatter to RuleCatalog;
        dependency ListFormatter to Markdig;
        dependency ListFormatter to DocumentParser;

        // The line arithmetic and the ownership of lines (which lines are protected or hold other content) that
        // the document formatter shares with the formatter through the context.
        dependency ListFormatter to LineLayout;
        dependency ListFormatter to LineOwnership;
    }
}
