package LintMark_Reporters {
    private import SoftwareItemMetadata::*;
    private import LintMark_Context::*;
    private import LintMark_RuleCatalog::*;
    private import LintMark_SourceDocument::*;
    private import ScalarValues::*;

    attribute def FileDiff {
        doc /* What would change in one file, for display: its path, the LF-normalized text before and after, the line terminators it has now and the line ending it would be written with. */

        attribute path : String;
        attribute beforeLf : String;
        attribute afterLf : String;
        attribute originalEndings : LineEndingInfo;
        attribute targetEnding : LineEnding;
    }

    part def Reporters {
        doc /* Writes the results of a run: the text reporter writes diagnostics to the error output as path:line:column: rule message lines, de-duplicated and sorted, with paths shown relative to a base directory, and sets the failing exit status; the diff reporter writes what the fix option would change as a unified diff with three lines of context, merged hunks, no-newline markers and a note when only the line endings change, in display-path order (also documents the FileDiff supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.LintMark/Reporting/TextReporter.cs",
                "src/DemaConsulting.LintMark/Reporting/DiffReporter.cs");
            testFiles = (
                "test/DemaConsulting.LintMark.Tests/Reporting/TextReporterTests.cs",
                "test/DemaConsulting.LintMark.Tests/Reporting/DiffReporterTests.cs",
                "test/DemaConsulting.LintMark.Tests/Reporting/PatchApplier.cs");
            designFiles = ("docs/design/lintmark/engine/reporters.md");
            verificationFiles = ("docs/verification/lintmark/engine/reporters.md");
            requirementFiles = ("docs/reqstream/lintmark/engine/reporters.yaml");
        }

        // The reporters write every line through the context, which also records the exit status.
        dependency Reporters to Context;

        // The text reporter reads each finding as a diagnostic.
        dependency Reporters to RuleCatalog;

        // The diff reporter names the line endings with the document model's types.
        dependency Reporters to SourceDocument;
    }
}
