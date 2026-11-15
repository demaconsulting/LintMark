package LintMark {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::*;

    // The edit is the attribute type TextEdit; the offsets refer to the text the formatter was given, which is the
    // document after the other rules have been applied.
    attribute def ParagraphOutcome {
        doc /* What the paragraph formatter found out about one paragraph: the edit that wraps it (absent when none is proposed), the offset of the first character beyond the line length on its first line that is too wide and could be broken (minus one when there is none), the display width of that line, whether a line that could be broken stays too wide, and the offset of the first changed character of the wrap. */

        attribute edit : TextEdit [0..1];
        attribute overflowOffset : Integer;
        attribute overflowWidth : Integer;
        attribute hasResidualOverflow : Boolean;
        attribute wrapOffset : Integer;
    }

    part def ParagraphFormatter {
        doc /* Decides what to do about the lines of one paragraph that are wider than the configured line length, or that are not laid out the way the sentence or semantic wrap mode writes them: when a line has a place to break, it proposes one edit that fills the paragraph with the container prefix on every line, keeps every inline construct whole, keeps hard line breaks, and never starts a line with block syntax, and reports where the first such line overflows; it leaves a paragraph as written, and reports it where one is due, when the paragraph is in a footnote, holds a tab, has a rule suppressed, has a Pandoc structural line, or cannot be rewritten safely, and it is not a block formatter because it claims no lines (also documents the ParagraphOutcome supporting type, the outcome of one paragraph). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.LintMark/Formatting/Blocks/ParagraphFormatter.cs");
            testFiles = (
                "test/DemaConsulting.LintMark.Tests/Formatting/Blocks/ParagraphFormatterTests.cs",
                "test/DemaConsulting.LintMark.Tests/Formatting/DocumentFormatterWrapTests.cs",
                "test/DemaConsulting.LintMark.Tests/Formatting/DocumentFormatterSentenceWrapTests.cs");
            designFiles = ("docs/design/lintmark/formatting/blocks/paragraph-formatter.md");
            verificationFiles = ("docs/verification/lintmark/formatting/blocks/paragraph-formatter.md");
            requirementFiles = ("docs/reqstream/lintmark/formatting/blocks/paragraph-formatter.yaml");
        }

        // The formatter is given the context shared by the formatters of one run, uses the line arithmetic
        // and the line ownership of the document formatter (a paragraph in a footnote or in a protected block
        // is left alone), and returns its outcome and the one edit that wraps the paragraph.
        dependency ParagraphFormatter to DocumentFormatter;
        dependency ParagraphFormatter to EditSet;

        // It reads the paragraph from the parsed document, the lines of the paragraph and the prefix its
        // container requires on a continuation line from the document parser, and the syntax tree from the
        // Markdown parser library; wrapping is asked for by the wrap mode and the line length of the
        // configuration, and the rule identifiers of the rules that edit a paragraph come from the rule
        // catalog.
        dependency ParagraphFormatter to DocumentParser;
        dependency ParagraphFormatter to Markdig;
        dependency ParagraphFormatter to LintMarkConfig;
        dependency ParagraphFormatter to RuleCatalog;

        // The paragraph is split into atoms and hard-break segments by the tokenizer, each segment is filled
        // by the line wrapper (whose guard recognizes the paragraphs that hold a Pandoc structural line and
        // the words that may not start a line), and lines are measured in display columns.
        dependency ParagraphFormatter to AtomTokenizer;
        dependency ParagraphFormatter to LineWrapper;
        dependency ParagraphFormatter to TextWidth;
    }
}
