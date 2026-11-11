package LintMark {
    private import SoftwareItemMetadata::*;

    part def Program {
        doc /* Entry point and execution orchestrator. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.LintMark/Program.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/ProgramTests.cs");
            designFiles = ("docs/design/lintmark/program.md");
            verificationFiles = ("docs/verification/lintmark/program.md");
            requirementFiles = ("docs/reqstream/lintmark/program.yaml");
        }
    }
}
