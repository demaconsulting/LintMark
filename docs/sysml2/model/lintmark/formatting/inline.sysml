package LintMark_InlineSubsystem {
    private import SoftwareItemMetadata::*;
    public import LintMark_AtomTokenizer::*;
    public import LintMark_InlineNormalizer::*;
    public import LintMark_LineWrapper::*;
    public import LintMark_LinkRefactorer::*;

    part def InlineSubsystem {
        doc /* Works on the inline content of paragraphs: it writes emphasis, strong emphasis, bare URLs, and code spans in the canonical form, splits a paragraph into the words a wrap may move, lays those words out as lines under the wrap rules, and converts inline links to reference links. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.LintMark.Tests/Formatting/Inline/InlineSubsystemTests.cs");
            designFiles = ("docs/design/lintmark/formatting/inline.md");
            verificationFiles = ("docs/verification/lintmark/formatting/inline.md");
            requirementFiles = ("docs/reqstream/lintmark/formatting/inline.yaml");
        }

        part inlineNormalizer : InlineNormalizer;
        part atomTokenizer : AtomTokenizer;
        part lineWrapper : LineWrapper;
        part linkRefactorer : LinkRefactorer;
    }
}
