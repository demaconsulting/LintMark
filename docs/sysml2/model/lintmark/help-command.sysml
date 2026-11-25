package LintMark_HelpCommand {
    private import SoftwareItemMetadata::*;
    private import OtsDependencies::*;

    part def HelpCommand {
        doc /* Built-in help command: answers the help verb from the help cards embedded in the tool assembly, before the options are parsed. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.LintMark/Cli/HelpCommand.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/Cli/HelpCommandTests.cs");
            designFiles = ("docs/design/lintmark/help-command.md");
            verificationFiles = ("docs/verification/lintmark/help-command.md");
            requirementFiles = ("docs/reqstream/lintmark/help-command.yaml");
        }

        // The help command is served from the cards embedded in the assembly by the help cards library.
        dependency HelpCommand to HelpCards;

        // The version that the help shows comes from the program entry point.
        dependency HelpCommand to LintMark_Program::Program;
    }
}
