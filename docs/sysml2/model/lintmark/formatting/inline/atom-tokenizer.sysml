package LintMark_AtomTokenizer {
    private import SoftwareItemMetadata::*;
    private import LintMark_DocumentParser::*;
    private import LintMark_LinkRefactorer::*;
    private import OtsDependencies::*;
    private import ScalarValues::*;

    enum def HardBreakKind {
        doc /* How a run of words of a paragraph ends: with no hard line break (the end of the paragraph), with two or more spaces before the line end, or with a backslash before the line end. */

        enum none;
        enum spaces;
        enum backslash;
    }

    // The attribute is named endsWith because end is a keyword of the modeling language (the C# property is End).
    attribute def ParagraphSegment {
        doc /* The words of one run of a paragraph between hard line breaks, each word being an atom that may not be split, and how the run ends. */

        attribute atoms : String [0..*];
        attribute endsWith : HardBreakKind;
    }

    attribute def TokenizedParagraph {
        doc /* A paragraph split into its segments between hard line breaks, with the number of places each of its source lines holds where a line break could be written. */

        attribute segments : ParagraphSegment [0..*];
        attribute breakOpportunitiesPerLine : Integer [0..*];
    }

    part def AtomTokenizer {
        doc /* Splits the text of one paragraph into the words that a wrap may separate, keeping every link, image, code span, autolink, bare URL, inline HTML tag, entity, math span, footnote reference, task marker, and Pandoc attribute or citation whole, cutting the words into segments at hard line breaks, never allowing a break after a backslash, counting the places each source line could be broken, and refusing a paragraph whose multi-line construct cannot be rejoined safely or whose spans do not fit its lines (also documents the TokenizedParagraph, ParagraphSegment and HardBreakKind supporting types, the tokenized paragraph, its segments, and how a segment ends). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.LintMark/Formatting/Inline/AtomTokenizer.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/Formatting/Inline/AtomTokenizerTests.cs");
            designFiles = ("docs/design/lintmark/formatting/inline/atom-tokenizer.md");
            verificationFiles = ("docs/verification/lintmark/formatting/inline/atom-tokenizer.md");
            requirementFiles = ("docs/reqstream/lintmark/formatting/inline/atom-tokenizer.yaml");
        }

        // The tokenizer rebuilds the content of the paragraph from its lines, which the document parser
        // locates as leaf lines.
        dependency AtomTokenizer to DocumentParser;

        // The constructs that must stay whole, and the hard line breaks that the document parser agrees
        // with, are read from the inline syntax tree of the Markdown parser library.
        dependency AtomTokenizer to Markdig;

        // The tail of an inline link (its destination and title) is read with the helper of the link
        // refactorer, so that both units agree on where a link ends.
        dependency AtomTokenizer to LinkRefactorer;
    }
}
