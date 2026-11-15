package LintMark {
    private import SoftwareItemMetadata::*;

    part def HeadingFormatter {
        doc /* Proposes the edits that write headings in the canonical form: ATX with exactly one space after the number signs, no closing number signs, no indentation, no trailing whitespace, and no tabs in the text, converting a single-line setext heading and leaving a multi-line or contained one as written. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.LintMark/Formatting/Blocks/HeadingFormatter.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/Formatting/Blocks/HeadingFormatterTests.cs");
            designFiles = ("docs/design/lintmark/formatting/blocks/heading-formatter.md");
            verificationFiles = ("docs/verification/lintmark/formatting/blocks/heading-formatter.md");
            requirementFiles = ("docs/reqstream/lintmark/formatting/blocks/heading-formatter.yaml");
        }

        // The formatter implements the block formatter contract, receives its context, uses the line
        // arithmetic of the document formatter, and adds edits to the edit set.
        dependency HeadingFormatter to DocumentFormatter;
        dependency HeadingFormatter to EditSet;

        // It reads the heading and its inline spans from the parsed document and the Markdown parser
        // library, shares the whitespace and tab edits of the whitespace passes, and tags each edit with a
        // rule identifier from the rule catalog.
        dependency HeadingFormatter to DocumentParser;
        dependency HeadingFormatter to Markdig;
        dependency HeadingFormatter to WhitespacePasses;
        dependency HeadingFormatter to RuleCatalog;
    }
}
