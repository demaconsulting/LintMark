package LintMark {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::*;

    attribute def TextEdit {
        doc /* One replacement of a range of the LF-normalized text, tagged with the rule that proposed it; an insertion has length zero and a deletion an empty replacement. */

        attribute start : Integer;
        attribute length : Integer;
        attribute replacement : String;
        attribute ruleId : String;
    }

    part def EditSet {
        doc /* Collects the edits proposed for one document, rejects conflicting edits as they are added, and returns them in deterministic source order (also documents the TextEdit supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.LintMark/Formatting/EditSet.cs",
                "src/DemaConsulting.LintMark/Formatting/TextEdit.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/Formatting/EditSetTests.cs");
            designFiles = ("docs/design/lintmark/formatting/edit-set.md");
            verificationFiles = ("docs/verification/lintmark/formatting/edit-set.md");
            requirementFiles = ("docs/reqstream/lintmark/formatting/edit-set.yaml");
        }

        // The set holds and returns the edits it collects.
        dependency EditSet to TextEdit;
    }
}
