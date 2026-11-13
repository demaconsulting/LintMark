package LintMark {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::*;

    attribute def FileDiff {
        doc /* What would change in one file, for display: its path, the LF-normalized text before and after, the line terminators it has now and the line ending it would be written with. */

        attribute path : String;
        attribute beforeLf : String;
        attribute afterLf : String;
        attribute originalEndings : LineEndingInfo;
        attribute targetEnding : LineEnding;
    }

    part def DiffReporter {
        doc /* Writes what the fix option would change as a unified diff with three lines of context, merged hunks, no-newline markers and a note when only the line endings change, in display-path order (also documents the FileDiff supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.LintMark/Reporting/DiffReporter.cs",
                "src/DemaConsulting.LintMark/Reporting/FileDiff.cs");
            testFiles = (
                "test/DemaConsulting.LintMark.Tests/Reporting/DiffReporterTests.cs",
                "test/DemaConsulting.LintMark.Tests/Reporting/PatchApplier.cs");
            designFiles = ("docs/design/lintmark/reporting/diff-reporter.md");
            verificationFiles = ("docs/verification/lintmark/reporting/diff-reporter.md");
            requirementFiles = ("docs/reqstream/lintmark/reporting/diff-reporter.yaml");
        }

        // The reporter takes each changed file as a file diff, names the line endings with the document
        // model's types, writes the lines through the context, and shows paths the way the text reporter does.
        dependency DiffReporter to FileDiff;
        dependency DiffReporter to LineEnding;
        dependency DiffReporter to LineEndingInfo;
        dependency DiffReporter to Context;
        dependency DiffReporter to TextReporter;
    }
}
