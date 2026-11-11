package TemplateDotNetTool {
    private import SoftwareItemMetadata::*;

    part def Context {
        doc /* Command-line argument parser and I/O owner for one tool invocation. */

        metadata info : SoftwareInfo {
            description = "Command-line argument parser and I/O owner for one tool invocation.";
            sourceFiles = ("src/DemaConsulting.TemplateDotNetTool/Cli/Context.cs");
            testFiles = ("test/DemaConsulting.TemplateDotNetTool.Tests/Cli/ContextTests.cs");
            designFiles = ("docs/design/template-dot-net-tool/cli/context.md");
            verificationFiles = ("docs/verification/template-dot-net-tool/cli/context.md");
            requirementFiles = ("docs/reqstream/template-dot-net-tool/cli/context.yaml");
        }
    }
}
