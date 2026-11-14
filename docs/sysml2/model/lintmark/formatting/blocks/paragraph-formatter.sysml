package LintMark {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::*;

    // The edit is the attribute type TextEdit; the offsets refer to the text the formatter was given, which is the
    // document after the other rules have been applied.
    attribute def ParagraphOutcome {
        doc /* What the paragraph formatter found out about one paragraph: the edit that wraps it (absent when none is proposed), the offset of the first character beyond the line length on its first line that is too wide and could be broken (minus one when there is none), the display width of that line, and whether a line that could be broken stays too wide. */

        attribute edit : TextEdit [0..1];
        attribute overflowOffset : Integer;
        attribute overflowWidth : Integer;
        attribute hasResidualOverflow : Boolean;
    }

    part def ParagraphFormatter {
        doc /* Decides what to do about the lines of one paragraph that are wider than the configured line length: when a line has a place to break, it proposes one edit that fills the paragraph up to the width with the container prefix on every line, keeps every inline construct whole, keeps hard line breaks, and never starts a line with block syntax, and reports where the first such line overflows; it leaves a paragraph as written, and reports it where one is due, when the paragraph is in a footnote, holds a tab, has a rule suppressed, has a Pandoc structural line, cannot be rewritten safely, or would not render the same afterwards, and it is not a block formatter because it claims no lines (also documents the ParagraphOutcome supporting type, the outcome of one paragraph). */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.LintMark/Formatting/Blocks/ParagraphFormatter.cs",
                "src/DemaConsulting.LintMark/Formatting/Blocks/ParagraphOutcome.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/Formatting/Blocks/ParagraphFormatterTests.cs");
            designFiles = ("docs/design/lintmark/formatting/blocks/paragraph-formatter.md");
            verificationFiles = ("docs/verification/lintmark/formatting/blocks/paragraph-formatter.md");
            requirementFiles = ("docs/reqstream/lintmark/formatting/blocks/paragraph-formatter.yaml");
        }

        // The formatter is given the context shared by the formatters of one run, reads the paragraph from the
        // parsed document (its source text, its blocks and the suppression map that says whether a rule that
        // edits the paragraph is suppressed there), and returns its outcome and the one edit that wraps the
        // paragraph; the line arithmetic and the ownership of lines (a paragraph in a footnote or in a protected
        // block is left alone) come through the context.
        dependency ParagraphFormatter to FormatContext;
        dependency ParagraphFormatter to ParsedDocument;
        dependency ParagraphFormatter to SourceDocument;
        dependency ParagraphFormatter to SuppressionMap;
        dependency ParagraphFormatter to BlockInfo;
        dependency ParagraphFormatter to BlockKind;
        dependency ParagraphFormatter to LineLayout;
        dependency ParagraphFormatter to LineOwnership;
        dependency ParagraphFormatter to ParagraphOutcome;
        dependency ParagraphFormatter to TextEdit;

        // Wrapping is asked for by the wrap mode and the line length of the configuration, and the rule
        // identifiers of the rules that edit a paragraph, which a suppression of any of them protects it from,
        // come from the rule catalog.
        dependency ParagraphFormatter to LintMarkConfig;
        dependency ParagraphFormatter to WrapMode;
        dependency ParagraphFormatter to RuleCatalog;

        // The lines of the paragraph and the prefix its container requires on a continuation line are located by
        // the document parser; the paragraph is split into atoms and hard-break segments by the tokenizer, each
        // segment is filled by the line wrapper, and the block syntax guard recognizes the paragraphs that hold
        // a Pandoc structural line and the words that may not start a line.
        dependency ParagraphFormatter to DocumentParser;
        dependency ParagraphFormatter to LeafLine;
        dependency ParagraphFormatter to AtomTokenizer;
        dependency ParagraphFormatter to TokenizedParagraph;
        dependency ParagraphFormatter to ParagraphSegment;
        dependency ParagraphFormatter to HardBreakKind;
        dependency ParagraphFormatter to LineWrapper;
        dependency ParagraphFormatter to WrappedLines;
        dependency ParagraphFormatter to BlockSyntaxGuard;

        // Lines are measured in display columns, and the wrap stands only when the paragraph, read on its own and
        // inside its list or block quote, renders the same afterwards, which is decided by parsing both with the
        // fixed pipeline of the document parser from the Markdown parser library and comparing the pages with the
        // HTML equivalence checker.
        dependency ParagraphFormatter to TextWidth;
        dependency ParagraphFormatter to Markdig;
        dependency ParagraphFormatter to HtmlEquivalenceChecker;
        dependency ParagraphFormatter to EquivalenceResult;
    }
}
