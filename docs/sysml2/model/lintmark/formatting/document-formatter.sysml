package LintMark_DocumentFormatter {
    private import SoftwareItemMetadata::*;
    private import LintMark_DocumentParser::*;
    private import LintMark_EditSet::*;
    private import LintMark_InlineNormalizer::*;
    private import LintMark_LinkRefactorer::*;
    private import LintMark_LintMarkConfig::*;
    private import LintMark_ListFormatter::*;
    private import LintMark_ParagraphFormatter::*;
    private import LintMark_RuleCatalog::*;
    private import LintMark_SourceDocument::*;
    private import LintMark_TextWidth::*;
    private import LintMark_WhitespacePasses::*;
    private import OtsDependencies::*;
    private import ScalarValues::*;

    // The block formatter contract (RuleIds, CanFormat, Format) is modeled as an interface definition
    // because it is a C# interface that formatters implement; it has no data of its own.
    interface def IBlockFormatter {
        doc /* The contract of a formatter for one kind of block: it names the rules it applies, says whether it accepts a block, and proposes edits for a block it accepts. */
    }

    attribute def FormatContext {
        doc /* The inputs shared by every block formatter of one run: the parsed document, the effective configuration of its file, and the line arithmetic and line ownership map that the document-level passes use. */

        item document : ParsedDocument;
        part config : LintMarkConfig;
        item layout : LineLayout;
        item ownership : LineOwnership;
    }

    // The edits are the attribute type TextEdit; the diagnostics are the Diagnostic type of the rule catalog.
    item def FormatResult {
        doc /* The outcome of formatting one document: the edits that bring it into canonical form, the line ending to write and the formatting violations found. */

        attribute edits : TextEdit [0..*];
        attribute lineEnding : LineEnding;
        item diagnostics : Diagnostic [0..*];
    }

    part def DocumentFormatter {
        doc /* Turns a parsed document into the edits and line ending that bring it into canonical form without writing anything: each block goes to the first registered block formatter that accepts it unless a rule is suppressed there, each paragraph no block formatter claims goes to the optional inline normalizer, the rules that concern every document (trailing whitespace, tabs, blank lines, spacing between blocks, and the final newline) handle everything else, every edit is reported as a problem, an unsafe document is never touched, and mixed or wrong line endings are reported; the optional link refactorer converts links to reference links and the optional paragraph formatter wraps the paragraphs that are still too wide, each on the text that the other rules produce, with their edits composed with the others and reported at their original positions (also documents the FormatContext, FormatResult, and IBlockFormatter supporting types, and the line arithmetic and line ownership helpers that the passes and the block formatters share). */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.LintMark/Formatting/DocumentFormatter.cs",
                "src/DemaConsulting.LintMark/Formatting/LineLayout.cs",
                "src/DemaConsulting.LintMark/Formatting/LineOwnership.cs");
            testFiles = (
                "test/DemaConsulting.LintMark.Tests/Formatting/DocumentFormatterTests.cs",
                "test/DemaConsulting.LintMark.Tests/Formatting/DocumentFormatterSupportTests.cs",
                "test/DemaConsulting.LintMark.Tests/Formatting/RawTextElementsTests.cs");
            designFiles = ("docs/design/lintmark/formatting/document-formatter.md");
            verificationFiles = ("docs/verification/lintmark/formatting/document-formatter.md");
            requirementFiles = ("docs/reqstream/lintmark/formatting/document-formatter.yaml");
        }

        // The formatter walks the blocks of a parsed document, skips blocks whose rules the suppression
        // map suppresses, and reads the line ending and text from the source document and the configuration
        // it is given.
        dependency DocumentFormatter to DocumentParser;
        dependency DocumentFormatter to SourceDocument;
        dependency DocumentFormatter to LintMarkConfig;

        // Edits are collected conflict-free in an edit set; the text a first list of edits produces is made
        // with the source patcher and the later edits are mapped back with the edit rebaser, both of the
        // edit set unit.
        dependency DocumentFormatter to EditSet;

        // The rules that concern every document are implemented by the whitespace passes.
        dependency DocumentFormatter to WhitespacePasses;

        // Each paragraph that no block formatter claims is given to the inline normalizer, links are
        // converted by the link refactorer, and paragraphs that are still too wide are wrapped last by the
        // paragraph formatter; the list formatter is recognized by type because it runs after the others.
        dependency DocumentFormatter to InlineNormalizer;
        dependency DocumentFormatter to LinkRefactorer;
        dependency DocumentFormatter to ParagraphFormatter;
        dependency DocumentFormatter to ListFormatter;

        // A cheap prefilter measures lines in display columns before a paragraph is wrapped.
        dependency DocumentFormatter to TextWidth;

        // The diagnostics carry rule identifiers and descriptions from the rule catalog, and the inline
        // structure of a leaf block is read from the Markdown parser library.
        dependency DocumentFormatter to RuleCatalog;
        dependency DocumentFormatter to Markdig;
    }

    // The helpers below are internal to the document formatter and are shared with the block formatters of
    // the Blocks subsystem; they are described in the design of the document formatter.
    item def LineLayout {
        doc /* The line arithmetic over the LF-normalized text of one document: where a line starts and ends, whether it is blank, and how tab stops fall (supporting type of the document formatter). */
    }

    item def LineOwnership {
        doc /* Which lines of a document belong to which leaf block, which of them a block formatter has claimed, which must never be edited, and which lie inside a container (supporting type of the document formatter). */
    }

    item def RawTextElements {
        doc /* Finds the HTML elements whose content keeps its whitespace (pre, script, style, and textarea), so that no whitespace inside them is edited (supporting type of the document formatter). */
    }

    item def FrontMatterGuard {
        doc /* Detects the one situation in which writing a dashes line elsewhere would turn the first line of a document into the opening of front matter (supporting type of the document formatter). */
    }
}
