package LintMark {
    private import SoftwareItemMetadata::*;

    part def InlineNormalizer {
        doc /* Proposes the edits that write emphasis and strong emphasis with the configured delimiter and wrap bare URLs in angle brackets inside one paragraph, skipping a paragraph where a rule is suppressed, where a link reference label could stop matching its definition, or where the paragraph would not render the same afterwards (also documents the InlineCandidates and ParagraphProof supporting types, the walk of the inline syntax tree that finds the candidate edits and the stand-alone render check that vets them). */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.LintMark/Formatting/Inline/InlineNormalizer.cs",
                "src/DemaConsulting.LintMark/Formatting/Inline/InlineCandidates.cs",
                "src/DemaConsulting.LintMark/Formatting/Inline/ParagraphProof.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/Formatting/Inline/InlineNormalizerTests.cs");
            designFiles = ("docs/design/lintmark/formatting/inline/inline-normalizer.md");
            verificationFiles = ("docs/verification/lintmark/formatting/inline/inline-normalizer.md");
            requirementFiles = ("docs/reqstream/lintmark/formatting/inline/inline-normalizer.yaml");
        }

        // The normalizer is asked by the document formatter for one paragraph at a time; it reads the paragraph
        // from the parsed document and its blocks (and the source text and the reference definitions that its
        // labels must keep matching), skips a paragraph in which the suppression map suppresses a rule, and
        // reads the delimiters and the bare URL style it writes from the configuration.
        dependency InlineNormalizer to ParsedDocument;
        dependency InlineNormalizer to BlockInfo;
        dependency InlineNormalizer to BlockKind;
        dependency InlineNormalizer to SourceDocument;
        dependency InlineNormalizer to ReferenceIndex;
        dependency InlineNormalizer to SuppressionMap;
        dependency InlineNormalizer to LintMarkConfig;
        dependency InlineNormalizer to EmphasisMarker;
        dependency InlineNormalizer to BareUrlStyle;

        // The edits are added to the edit set of the document, and each is tagged with a rule identifier from
        // the rule catalog.
        dependency InlineNormalizer to EditSet;
        dependency InlineNormalizer to TextEdit;
        dependency InlineNormalizer to RuleCatalog;

        // The inline syntax tree is read from the Markdown parser library, and the same fixed pipeline of the
        // document parser is used to render the paragraph before and after, from the lines of the paragraph
        // that the parser's line-rebuilding helper locates.
        dependency InlineNormalizer to Markdig;
        dependency InlineNormalizer to DocumentParser;
        dependency InlineNormalizer to LeafLine;
    }
}
