package TemplateDotNetTool {
    private import SoftwareItemMetadata::*;

    part def CliSubsystem {
        doc /* Command-line argument parsing, output-channel management, and exit-code tracking. */

        metadata info : SoftwareInfo {
            description = "Command-line argument parsing, output-channel management, and exit-code tracking.";
            testFiles = ("test/DemaConsulting.TemplateDotNetTool.Tests/Cli/CliSubsystemTests.cs");
            designFiles = ("docs/design/template-dot-net-tool/cli.md");
            verificationFiles = ("docs/verification/template-dot-net-tool/cli.md");
            requirementFiles = ("docs/reqstream/template-dot-net-tool/cli.yaml");
        }

        part context : Context;
    }
}
