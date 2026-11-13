package LintMark {
    private import SoftwareItemMetadata::*;

    part def CodeBlockFormatter {
        doc /* Proposes the edits that write each code block in the canonical form, fenced with the configured fence character and never indented, with no trailing whitespace on the fence lines, and never changes the content of the code. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.LintMark/Formatting/Blocks/CodeBlockFormatter.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/Formatting/Blocks/CodeBlockFormatterTests.cs");
            designFiles = ("docs/design/lintmark/formatting/blocks/code-block-formatter.md");
            verificationFiles = ("docs/verification/lintmark/formatting/blocks/code-block-formatter.md");
            requirementFiles = ("docs/reqstream/lintmark/formatting/blocks/code-block-formatter.yaml");
        }

        // The formatter implements the block formatter contract, receives its context, and adds edits to the
        // edit set of the document.
        dependency CodeBlockFormatter to IBlockFormatter;
        dependency CodeBlockFormatter to FormatContext;
        dependency CodeBlockFormatter to EditSet;
        dependency CodeBlockFormatter to TextEdit;

        // It reads the code block from the parsed document and its blocks, and the fence character it writes
        // from the configuration.
        dependency CodeBlockFormatter to ParsedDocument;
        dependency CodeBlockFormatter to BlockInfo;
        dependency CodeBlockFormatter to BlockKind;
        dependency CodeBlockFormatter to SourceDocument;
        dependency CodeBlockFormatter to LintMarkConfig;
        dependency CodeBlockFormatter to FenceMarker;

        // Each edit is tagged with a rule identifier from the rule catalog, and the fenced and indented code
        // nodes come from the Markdown parser library.
        dependency CodeBlockFormatter to RuleCatalog;
        dependency CodeBlockFormatter to Markdig;

        // The line arithmetic and line ownership shared with the document formatter, and the guard against
        // creating front matter.
        dependency CodeBlockFormatter to LineLayout;
        dependency CodeBlockFormatter to LineOwnership;
        dependency CodeBlockFormatter to FrontMatterGuard;
    }
}
