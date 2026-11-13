package LintMark {
    private import SoftwareItemMetadata::*;

    part def HtmlEquivalenceChecker {
        doc /* Decides whether a rewrite renders the same HTML as the original by rendering both texts with the fixed Markdown pipeline and comparing the normalized output, collapsing only HTML whitespace and reporting the first difference. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.LintMark/Safety/HtmlEquivalenceChecker.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/Safety/HtmlEquivalenceCheckerTests.cs");
            designFiles = ("docs/design/lintmark/safety/html-equivalence-checker.md");
            verificationFiles = ("docs/verification/lintmark/safety/html-equivalence-checker.md");
            requirementFiles = ("docs/reqstream/lintmark/safety/html-equivalence-checker.yaml");
        }

        // Both texts are rendered by the Markdown library with the pipeline owned by the document parser,
        // and the verdict is returned as an equivalence result.
        dependency HtmlEquivalenceChecker to DocumentParser;
        dependency HtmlEquivalenceChecker to Markdig;
        dependency HtmlEquivalenceChecker to EquivalenceResult;
    }
}
