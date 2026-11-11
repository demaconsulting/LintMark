package TemplateDotNetTool {
    private import SoftwareItemMetadata::*;

    part def Program {
        doc /* Entry point and execution orchestrator. */

        metadata info : SoftwareInfo {
            description = "Entry point and execution orchestrator.";
            sourceFiles = ("src/DemaConsulting.TemplateDotNetTool/Program.cs");
            testFiles = ("test/DemaConsulting.TemplateDotNetTool.Tests/ProgramTests.cs");
            designFiles = ("docs/design/template-dot-net-tool/program.md");
            verificationFiles = ("docs/verification/template-dot-net-tool/program.md");
            requirementFiles = ("docs/reqstream/template-dot-net-tool/program.yaml");
        }
    }
}
