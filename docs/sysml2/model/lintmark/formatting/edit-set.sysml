package LintMark_EditSet {
    private import SoftwareItemMetadata::*;
    private import LintMark_SourceDocument::*;
    private import ScalarValues::*;

    attribute def TextEdit {
        doc /* One replacement of a range of the LF-normalized text, tagged with the rule that proposed it; an insertion has length zero and a deletion an empty replacement. */

        attribute start : Integer;
        attribute length : Integer;
        attribute replacement : String;
        attribute ruleId : String;
    }

    part def EditSet {
        doc /* Collects the edits proposed for one document, rejects conflicting edits as they are added, and returns them in deterministic source order; applies an ordered, non-overlapping list of edits to the text of a source document; and composes the edits computed on the text that a first list of edits produced into edits against the original text and maps an offset of the produced text back to the original (also documents the TextEdit, SourcePatcher, and EditRebaser supporting types). */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.LintMark/Formatting/Edits.cs",
                "src/DemaConsulting.LintMark/Formatting/EditRebaser.cs");
            testFiles = (
                "test/DemaConsulting.LintMark.Tests/Formatting/EditSetTests.cs",
                "test/DemaConsulting.LintMark.Tests/Formatting/EditRebaserTests.cs",
                "test/DemaConsulting.LintMark.Tests/Formatting/EditSetTestExtensions.cs");
            designFiles = ("docs/design/lintmark/formatting/edit-set.md");
            verificationFiles = ("docs/verification/lintmark/formatting/edit-set.md");
            requirementFiles = ("docs/reqstream/lintmark/formatting/edit-set.yaml");
        }

        // The source patcher reads the text to patch from the source document.
        dependency EditSet to SourceDocument;
    }

    item def SourcePatcher {
        doc /* Applies an ordered, non-overlapping array of edits to the text of a source document and returns the patched LF-normalized text (supporting type of the edit set). */
    }

    item def EditRebaser {
        doc /* Composes the edits computed on the text that a first list of edits produced into edits against the original text (edits inside a replaced range are absorbed, edits that start or end inside written text are extended to all of it, overlapping edits merge, and no-op edits are dropped) and maps an offset of the produced text back to the original (supporting type of the edit set). */
    }
}
