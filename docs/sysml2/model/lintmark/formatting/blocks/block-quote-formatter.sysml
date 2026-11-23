package LintMark_BlockQuoteFormatter {
    private import SoftwareItemMetadata::*;
    private import LintMark_DocumentFormatter::*;
    private import LintMark_DocumentParser::*;
    private import LintMark_EditSet::*;
    private import LintMark_RuleCatalog::*;
    private import OtsDependencies::*;

    part def BlockQuoteFormatter {
        doc /* Proposes the edits that write block quotes in the canonical form, with exactly one space after each quote marker on the paragraph and ATX heading lines of a quote, claiming no lines and leaving a line as written where the change could alter its meaning (a tab in the prefix, a code-indent run of spaces, a lazy continuation, or a quote inside a list or footnote). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.LintMark/Formatting/Blocks/BlockQuoteFormatter.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/Formatting/Blocks/BlockQuoteFormatterTests.cs");
            designFiles = ("docs/design/lintmark/formatting/blocks/block-quote-formatter.md");
            verificationFiles = ("docs/verification/lintmark/formatting/blocks/block-quote-formatter.md");
            requirementFiles = ("docs/reqstream/lintmark/formatting/blocks/block-quote-formatter.yaml");
        }

        // The formatter implements the block formatter contract, receives its context, uses the line
        // arithmetic of the document formatter, and adds edits to the edit set.
        dependency BlockQuoteFormatter to DocumentFormatter;
        dependency BlockQuoteFormatter to EditSet;

        // It reads the quote and the leaves inside it from the parsed document (and the document parser's
        // line helpers) and the Markdown parser library, and tags each edit with a rule identifier from the
        // rule catalog.
        dependency BlockQuoteFormatter to DocumentParser;
        dependency BlockQuoteFormatter to Markdig;
        dependency BlockQuoteFormatter to RuleCatalog;
    }
}
