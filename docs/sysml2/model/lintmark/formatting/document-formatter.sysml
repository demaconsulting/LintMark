package LintMark {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::*;

    // The block formatter contract (RuleIds, CanFormat, Format) is modeled as an interface definition
    // because it is a C# interface that formatters implement; it has no data of its own.
    interface def IBlockFormatter {
        doc /* The contract of a formatter for one kind of block: it names the rules it applies, says whether it accepts a block, and proposes edits for a block it accepts. */
    }

    attribute def FormatContext {
        doc /* The inputs shared by every block formatter of one run: the parsed document and the effective configuration of its file. */

        item document : ParsedDocument;
        part config : LintMarkConfig;
    }

    // The edits are the attribute type TextEdit; the diagnostics are the Diagnostic unit.
    item def FormatResult {
        doc /* The outcome of formatting one document: the edits that bring it into canonical form, the line ending to write and the formatting violations found. */

        attribute edits : TextEdit [0..*];
        attribute lineEnding : LineEnding;
        part diagnostics : Diagnostic [0..*];
    }

    part def DocumentFormatter {
        doc /* Turns a parsed document into the edits and line ending that bring it into canonical form without writing anything: each block goes to the first registered block formatter that accepts it unless a rule is suppressed there, the rules that concern every document (trailing whitespace, tabs, blank lines, spacing between blocks, and the final newline) handle everything else, every edit of such a rule is reported as a problem, an unsafe document is never touched, and mixed or wrong line endings are reported (also documents the FormatResult and FormatContext supporting types, the IBlockFormatter contract modeled as an interface definition, and the internal helpers that the passes and the block formatters share). */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.LintMark/Formatting/DocumentFormatter.cs",
                "src/DemaConsulting.LintMark/Formatting/FormatResult.cs",
                "src/DemaConsulting.LintMark/Formatting/FormatContext.cs",
                "src/DemaConsulting.LintMark/Formatting/IBlockFormatter.cs",
                "src/DemaConsulting.LintMark/Formatting/LeafWhitespace.cs",
                "src/DemaConsulting.LintMark/Formatting/InlineMap.cs",
                "src/DemaConsulting.LintMark/Formatting/LineLayout.cs",
                "src/DemaConsulting.LintMark/Formatting/LineOwnership.cs",
                "src/DemaConsulting.LintMark/Formatting/FrontMatterGuard.cs",
                "src/DemaConsulting.LintMark/Formatting/BlankLinePass.cs",
                "src/DemaConsulting.LintMark/Formatting/BlockSpacingPass.cs",
                "src/DemaConsulting.LintMark/Formatting/TrailerPass.cs");
            testFiles = (
                "test/DemaConsulting.LintMark.Tests/Formatting/DocumentFormatterTests.cs",
                "test/DemaConsulting.LintMark.Tests/Formatting/DocumentFormatterPassTests.cs",
                "test/DemaConsulting.LintMark.Tests/Formatting/DocumentFormatterSupportTests.cs",
                "test/DemaConsulting.LintMark.Tests/Formatting/TestBlockFormatters.cs");
            designFiles = ("docs/design/lintmark/formatting/document-formatter.md");
            verificationFiles = ("docs/verification/lintmark/formatting/document-formatter.md");
            requirementFiles = ("docs/reqstream/lintmark/formatting/document-formatter.yaml");
        }

        // The formatter walks the blocks of a parsed document, skips blocks whose rules the suppression
        // map suppresses, and reads the line ending and counts from the source document and the
        // configuration it is given.
        dependency DocumentFormatter to ParsedDocument;
        dependency DocumentFormatter to BlockInfo;
        dependency DocumentFormatter to BlockKind;
        dependency DocumentFormatter to SuppressionMap;
        dependency DocumentFormatter to SourceDocument;
        dependency DocumentFormatter to LineEnding;
        dependency DocumentFormatter to LineEndingInfo;
        dependency DocumentFormatter to LintMarkConfig;

        // Edits are collected conflict-free in an edit set, and each block formatter is handed its context
        // and the edit set to add to.
        dependency DocumentFormatter to EditSet;
        dependency DocumentFormatter to TextEdit;
        dependency DocumentFormatter to IBlockFormatter;
        dependency DocumentFormatter to FormatContext;

        // The result carries the edits, the line ending and the diagnostics, whose rule identifiers and
        // descriptions come from the rule catalog.
        dependency DocumentFormatter to FormatResult;
        dependency DocumentFormatter to Diagnostic;
        dependency DocumentFormatter to DiagnosticKind;
        dependency DocumentFormatter to RuleCatalog;

        // The rules that concern every document are implemented by the helpers of this unit: the line
        // arithmetic, the ownership of lines (claimed by a block formatter, protected, or free), the inline
        // structure of a leaf block (read from the Markdown parser library), the whitespace and tab edits,
        // the front matter guard, and the passes for blank lines, spacing between blocks, and the end of
        // the file.
        dependency DocumentFormatter to LineLayout;
        dependency DocumentFormatter to LineOwnership;
        dependency DocumentFormatter to InlineMap;
        dependency DocumentFormatter to LeafWhitespace;
        dependency DocumentFormatter to FrontMatterGuard;
        dependency DocumentFormatter to BlankLinePass;
        dependency DocumentFormatter to BlockSpacingPass;
        dependency DocumentFormatter to TrailerPass;
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

    item def InlineMap {
        doc /* What the inline syntax tree of one leaf block says about its characters: which spans are plain text, which must never be edited, and where the line breaks are (supporting type of the document formatter). */
    }

    item def LeafWhitespace {
        doc /* Proposes the edits that remove trailing whitespace and expand tabs on the lines of one leaf block, shared by the document formatter and the heading formatter (supporting type of the document formatter). */
    }

    item def FrontMatterGuard {
        doc /* Detects the one situation in which writing a dashes line elsewhere would turn the first line of a document into the opening of front matter (supporting type of the document formatter). */
    }

    item def BlankLinePass {
        doc /* The document-level pass that limits runs of blank lines between blocks and empties whitespace-only lines (supporting type of the document formatter). */
    }

    item def BlockSpacingPass {
        doc /* The document-level pass that puts a blank line between top-level blocks that must be separated: headings, fenced code blocks, lists, and tables (supporting type of the document formatter). */
    }

    item def TrailerPass {
        doc /* The document-level pass that decides how the file ends, with exactly one line ending or with none (supporting type of the document formatter). */
    }
}
