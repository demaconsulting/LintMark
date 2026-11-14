package LintMark {
    private import SoftwareItemMetadata::*;

    part def BlockQuoteFormatter {
        doc /* Proposes the edits that collapse a run of two to four spaces after a block quote marker into one space in the paragraphs and ATX headings of an outermost block quote, and leaves a quote alone where a footnote definition makes the columns of its lines matter, and a line alone where it is lazy or has a tab in its marker prefix. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.LintMark/Formatting/Blocks/BlockQuoteFormatter.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/Formatting/Blocks/BlockQuoteFormatterTests.cs");
            designFiles = ("docs/design/lintmark/formatting/blocks/block-quote-formatter.md");
            verificationFiles = ("docs/verification/lintmark/formatting/blocks/block-quote-formatter.md");
            requirementFiles = ("docs/reqstream/lintmark/formatting/blocks/block-quote-formatter.yaml");
        }

        // The formatter implements the block formatter contract, receives its context, and adds edits to the
        // edit set of the document.
        dependency BlockQuoteFormatter to IBlockFormatter;
        dependency BlockQuoteFormatter to FormatContext;
        dependency BlockQuoteFormatter to EditSet;
        dependency BlockQuoteFormatter to TextEdit;

        // It reads the quote and the paragraphs and headings inside it from the parsed document and its blocks,
        // and the source text for the marker prefix of each line.
        dependency BlockQuoteFormatter to ParsedDocument;
        dependency BlockQuoteFormatter to BlockInfo;
        dependency BlockQuoteFormatter to BlockKind;
        dependency BlockQuoteFormatter to SourceDocument;

        // Each edit is tagged with a rule identifier from the rule catalog, and the quote nodes come from the
        // Markdown parser library.
        dependency BlockQuoteFormatter to RuleCatalog;
        dependency BlockQuoteFormatter to Markdig;

        // The lines of a leaf are located by the parser's line-rebuilding helper, and the line arithmetic is
        // shared with the document formatter through the context.
        dependency BlockQuoteFormatter to DocumentParser;
        dependency BlockQuoteFormatter to LeafLine;
        dependency BlockQuoteFormatter to LineLayout;
    }
}
