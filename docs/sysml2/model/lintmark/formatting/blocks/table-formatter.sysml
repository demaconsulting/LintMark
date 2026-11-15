package LintMark {
    private import SoftwareItemMetadata::*;

    part def TableFormatter {
        doc /* Proposes the edits that write GFM pipe tables in the canonical layout: a pipe at both ends of every line, one blank inside each pipe, and a minimal delimiter row (compact, the default) or every column padded to its widest cell in display columns (aligned), leaving grid tables and tables that cannot be rewritten safely as written (also documents the PipeTable supporting type, the reading of a pipe table into rows and cells, which the table lint rules and the link refactorer share). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.LintMark/Formatting/Blocks/TableFormatter.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/Formatting/Blocks/TableFormatterTests.cs");
            designFiles = ("docs/design/lintmark/formatting/blocks/table-formatter.md");
            verificationFiles = ("docs/verification/lintmark/formatting/blocks/table-formatter.md");
            requirementFiles = ("docs/reqstream/lintmark/formatting/blocks/table-formatter.yaml");
        }

        // The formatter implements the block formatter contract, receives its context, uses the line
        // arithmetic and the line ownership of the document formatter, and adds edits to the edit set.
        dependency TableFormatter to DocumentFormatter;
        dependency TableFormatter to EditSet;

        // It reads the table and its cells from the parsed document and the Markdown parser library, the
        // table style from the configuration, and tags each edit with a rule identifier from the rule
        // catalog.
        dependency TableFormatter to DocumentParser;
        dependency TableFormatter to Markdig;
        dependency TableFormatter to LintMarkConfig;
        dependency TableFormatter to RuleCatalog;

        // Cell widths are measured in display columns.
        dependency TableFormatter to TextWidth;
    }
}
