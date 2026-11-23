package LintMark_CodeBlockFormatter {
    private import SoftwareItemMetadata::*;
    private import LintMark_DocumentFormatter::*;
    private import LintMark_DocumentParser::*;
    private import LintMark_EditSet::*;
    private import LintMark_LintMarkConfig::*;
    private import LintMark_RuleCatalog::*;
    private import OtsDependencies::*;

    part def CodeBlockFormatter {
        doc /* Proposes the edits that write code blocks in the canonical form: fenced with the configured fence character (lengthened when the content would close it) and never indented at the top level, without changing the content of a block, and leaving an unclosed fence, a braced fence, and an indented block inside a container as written. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.LintMark/Formatting/Blocks/CodeBlockFormatter.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/Formatting/Blocks/CodeBlockFormatterTests.cs");
            designFiles = ("docs/design/lintmark/formatting/blocks/code-block-formatter.md");
            verificationFiles = ("docs/verification/lintmark/formatting/blocks/code-block-formatter.md");
            requirementFiles = ("docs/reqstream/lintmark/formatting/blocks/code-block-formatter.yaml");
        }

        // The formatter implements the block formatter contract, receives its context, uses the line
        // arithmetic, the line ownership, and the front matter guard of the document formatter, and adds
        // edits to the edit set.
        dependency CodeBlockFormatter to DocumentFormatter;
        dependency CodeBlockFormatter to EditSet;

        // It reads the block from the parsed document and the Markdown parser library, the fence character
        // from the configuration, and tags each edit with a rule identifier from the rule catalog.
        dependency CodeBlockFormatter to DocumentParser;
        dependency CodeBlockFormatter to Markdig;
        dependency CodeBlockFormatter to LintMarkConfig;
        dependency CodeBlockFormatter to RuleCatalog;
    }
}
