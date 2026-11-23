package LintMark_WhitespacePasses {
    private import SoftwareItemMetadata::*;
    private import LintMark_DocumentParser::*;
    private import LintMark_EditSet::*;
    private import LintMark_LintMarkConfig::*;
    private import LintMark_RuleCatalog::*;
    private import OtsDependencies::*;

    part def WhitespacePasses {
        doc /* Implements the rules that concern every document and no particular block: the removal of trailing whitespace and the expansion of tabs on the lines of a leaf block, the limit on runs of blank lines and the emptying of whitespace-only lines, the blank line between blocks that must be separated, and how the file ends (also documents the InlineMap, LeafWhitespace, BlankLinePass, BlockSpacingPass, and TrailerPass supporting types). */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.LintMark/Formatting/LeafWhitespace.cs",
                "src/DemaConsulting.LintMark/Formatting/SpacingPasses.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/Formatting/DocumentFormatterPassTests.cs");
            designFiles = ("docs/design/lintmark/formatting/whitespace-passes.md");
            verificationFiles = ("docs/verification/lintmark/formatting/whitespace-passes.md");
            requirementFiles = ("docs/reqstream/lintmark/formatting/whitespace-passes.yaml");
        }

        // The passes read the blocks of the parsed document and the settings that govern them, and read the
        // inline syntax tree of a leaf block from the Markdown parser library.
        dependency WhitespacePasses to DocumentParser;
        dependency WhitespacePasses to LintMarkConfig;
        dependency WhitespacePasses to Markdig;

        // Each pass uses the line arithmetic and the line ownership map of the document formatter, adds its
        // edits to the edit set, and tags them with rule identifiers from the rule catalog.
        dependency WhitespacePasses to LintMark_DocumentFormatter::DocumentFormatter;
        dependency WhitespacePasses to EditSet;
        dependency WhitespacePasses to RuleCatalog;
    }

    // The helpers below are internal to this unit and are shared with the lint rules and the block formatters.
    item def InlineMap {
        doc /* What the inline syntax tree of one leaf block says about its characters: which spans are plain text, which must never be edited, and where the line breaks are (supporting type of the whitespace passes). */
    }

    item def LeafWhitespace {
        doc /* Proposes the edits that remove trailing whitespace and expand tabs on the lines of one leaf block, shared by the document formatter and the heading formatter (supporting type of the whitespace passes). */
    }

    item def BlankLinePass {
        doc /* The document-level pass that limits runs of blank lines between blocks and empties whitespace-only lines (supporting type of the whitespace passes). */
    }

    item def BlockSpacingPass {
        doc /* The document-level pass that puts a blank line between top-level blocks that must be separated: headings, fenced code blocks, lists, and tables, never between a next-line suppression directive and the block it covers (supporting type of the whitespace passes). */
    }

    item def TrailerPass {
        doc /* The document-level pass that decides how the file ends, with exactly one line ending or with none (supporting type of the whitespace passes). */
    }
}
