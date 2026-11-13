package LintMark {
    private import SoftwareItemMetadata::*;

    part def HeadingFormatter {
        doc /* Proposes the edits that write each heading in the canonical form, ATX with exactly one space after the number signs, no closing number signs, no indentation, no trailing whitespace, and no tabs in the text, and leaves a heading alone where the conversion could change its meaning. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.LintMark/Formatting/Blocks/HeadingFormatter.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/Formatting/Blocks/HeadingFormatterTests.cs");
            designFiles = ("docs/design/lintmark/formatting/blocks/heading-formatter.md");
            verificationFiles = ("docs/verification/lintmark/formatting/blocks/heading-formatter.md");
            requirementFiles = ("docs/reqstream/lintmark/formatting/blocks/heading-formatter.yaml");
        }

        // The formatter implements the block formatter contract, receives its context, and adds edits to the
        // edit set of the document.
        dependency HeadingFormatter to IBlockFormatter;
        dependency HeadingFormatter to FormatContext;
        dependency HeadingFormatter to EditSet;
        dependency HeadingFormatter to TextEdit;

        // It reads the heading from the parsed document and its blocks, and the source text for the line
        // arithmetic.
        dependency HeadingFormatter to ParsedDocument;
        dependency HeadingFormatter to BlockInfo;
        dependency HeadingFormatter to BlockKind;
        dependency HeadingFormatter to SourceDocument;

        // Each edit is tagged with a rule identifier from the rule catalog, and the heading nodes and their
        // inlines come from the Markdown parser library.
        dependency HeadingFormatter to RuleCatalog;
        dependency HeadingFormatter to Markdig;

        // The whitespace and tab handling shared with the document formatter, and its line arithmetic.
        dependency HeadingFormatter to LeafWhitespace;
        dependency HeadingFormatter to InlineMap;
        dependency HeadingFormatter to LineLayout;
    }
}
