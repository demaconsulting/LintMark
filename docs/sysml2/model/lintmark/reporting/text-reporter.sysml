package LintMark {
    private import SoftwareItemMetadata::*;

    part def TextReporter {
        doc /* Writes diagnostics to the error output as path:line:column: rule message lines, de-duplicated and sorted, with paths shown relative to a base directory, and sets the failing exit status. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.LintMark/Reporting/TextReporter.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/Reporting/TextReporterTests.cs");
            designFiles = ("docs/design/lintmark/reporting/text-reporter.md");
            verificationFiles = ("docs/verification/lintmark/reporting/text-reporter.md");
            requirementFiles = ("docs/reqstream/lintmark/reporting/text-reporter.yaml");
        }

        // The reporter reads each finding as a diagnostic and writes the lines through the context, which
        // also records the exit status.
        dependency TextReporter to Diagnostic;
        dependency TextReporter to Context;
    }
}
