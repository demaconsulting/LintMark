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
        doc /* Collects the edits proposed for one document, rejects conflicting edits as they are added, and returns them in deterministic source order, and composes the edits computed on the text that a first list of edits produced into edits against the original text and maps an offset of the produced text back to the original (also documents the TextEdit supporting type and the EditRebaser supporting type, the composition of two edit lists). */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.LintMark/Formatting/EditSet.cs",
                "src/DemaConsulting.LintMark/Formatting/TextEdit.cs",
                "src/DemaConsulting.LintMark/Formatting/EditRebaser.cs");
            testFiles = (
                "test/DemaConsulting.LintMark.Tests/Formatting/EditSetTests.cs",
                "test/DemaConsulting.LintMark.Tests/Formatting/EditRebaserTests.cs");
            designFiles = ("docs/design/lintmark/formatting/edit-set.md");
            verificationFiles = ("docs/verification/lintmark/formatting/edit-set.md");
            requirementFiles = ("docs/reqstream/lintmark/formatting/edit-set.yaml");
        }

        // The set holds and returns the edits it collects.
        dependency EditSet to TextEdit;
    }

    // The rebaser is a static helper of the edit set, described in the design of the edit set.
    item def EditRebaser {
        doc /* Composes the edits computed on the text that a first list of edits produced into edits against the original text (edits inside a replaced range are absorbed, edits that start or end inside written text are extended to all of it, overlapping edits merge, and no-op edits are dropped) and maps an offset of the produced text back to the original (supporting type of the edit set). */

        // The rebaser builds its result, and checks each list it is given, through an edit set so that a
        // conflict cannot go unnoticed, and reads and writes text edits.
        dependency EditRebaser to EditSet;
        dependency EditRebaser to TextEdit;
    }
}
