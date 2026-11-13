package LintMark {
    private import SoftwareItemMetadata::*;

    part def ThematicBreakFormatter {
        doc /* Proposes the edits that write each thematic break as three characters of the configured style with no trailing whitespace, and leaves a break alone where the conversion could turn text into a heading, start front matter, or create a break of a list marker. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.LintMark/Formatting/Blocks/ThematicBreakFormatter.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/Formatting/Blocks/ThematicBreakFormatterTests.cs");
            designFiles = ("docs/design/lintmark/formatting/blocks/thematic-break-formatter.md");
            verificationFiles = ("docs/verification/lintmark/formatting/blocks/thematic-break-formatter.md");
            requirementFiles = ("docs/reqstream/lintmark/formatting/blocks/thematic-break-formatter.yaml");
        }

        // The formatter implements the block formatter contract, receives its context, and adds edits to the
        // edit set of the document.
        dependency ThematicBreakFormatter to IBlockFormatter;
        dependency ThematicBreakFormatter to FormatContext;
        dependency ThematicBreakFormatter to EditSet;
        dependency ThematicBreakFormatter to TextEdit;

        // It reads the break from the parsed document and its blocks (including the sibling before it), and the
        // style it writes from the configuration.
        dependency ThematicBreakFormatter to ParsedDocument;
        dependency ThematicBreakFormatter to BlockInfo;
        dependency ThematicBreakFormatter to BlockKind;
        dependency ThematicBreakFormatter to SourceDocument;
        dependency ThematicBreakFormatter to LintMarkConfig;
        dependency ThematicBreakFormatter to ThematicBreakStyle;

        // Each edit is tagged with a rule identifier from the rule catalog, and the break node comes from the
        // Markdown parser library.
        dependency ThematicBreakFormatter to RuleCatalog;
        dependency ThematicBreakFormatter to Markdig;

        // The line arithmetic shared with the document formatter, and the guard against creating front matter.
        dependency ThematicBreakFormatter to LineLayout;
        dependency ThematicBreakFormatter to FrontMatterGuard;
    }
}
