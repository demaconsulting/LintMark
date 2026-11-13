package LintMark {
    private import SoftwareItemMetadata::*;

    part def ReportingSubsystem {
        doc /* Writes the results for people and tools: diagnostics as sorted path:line:column lines and proposed rewrites as unified diffs, in a deterministic order independent of scheduling. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.LintMark.Tests/Reporting/ReportingSubsystemTests.cs");
            designFiles = ("docs/design/lintmark/reporting.md");
            verificationFiles = ("docs/verification/lintmark/reporting.md");
            requirementFiles = ("docs/reqstream/lintmark/reporting.yaml");
        }

        part textReporter : TextReporter;
        part diffReporter : DiffReporter;
    }
}
