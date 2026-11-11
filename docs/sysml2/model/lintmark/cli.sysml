package LintMark {
    private import SoftwareItemMetadata::*;

    part def CliSubsystem {
        doc /* Command-line argument parsing, output-channel management, and exit-code tracking. */

        metadata info : SoftwareInfo {
            description = "Command-line argument parsing, output-channel management, and exit-code tracking.";
            testFiles = ("test/DemaConsulting.LintMark.Tests/Cli/CliSubsystemTests.cs");
            designFiles = ("docs/design/lintmark/cli.md");
            verificationFiles = ("docs/verification/lintmark/cli.md");
            requirementFiles = ("docs/reqstream/lintmark/cli.yaml");
        }

        part context : Context;
    }
}
