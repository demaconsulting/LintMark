package LintMark {
    private import SoftwareItemMetadata::*;

    part def InlineNormalizer {
        doc /* Proposes the edits that write the emphasis, strong emphasis, bare URLs, and code spans of a paragraph in the canonical form: the configured delimiter character, URLs wrapped in angle brackets, and code spans without redundant padding, leaving a construct as written where the change could alter its meaning (also documents the InlineCandidates supporting type, the inline constructs found in one paragraph). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.LintMark/Formatting/Inline/InlineNormalizer.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/Formatting/Inline/InlineNormalizerTests.cs");
            designFiles = ("docs/design/lintmark/formatting/inline/inline-normalizer.md");
            verificationFiles = ("docs/verification/lintmark/formatting/inline/inline-normalizer.md");
            requirementFiles = ("docs/reqstream/lintmark/formatting/inline/inline-normalizer.yaml");
        }

        // The normalizer reads the paragraph from the parsed document and the inline syntax tree from the
        // Markdown parser library, the delimiter and URL styles from the configuration, adds edits to the
        // edit set, and tags each edit with a rule identifier from the rule catalog.
        dependency InlineNormalizer to DocumentParser;
        dependency InlineNormalizer to Markdig;
        dependency InlineNormalizer to LintMarkConfig;
        dependency InlineNormalizer to EditSet;
        dependency InlineNormalizer to RuleCatalog;
    }
}
