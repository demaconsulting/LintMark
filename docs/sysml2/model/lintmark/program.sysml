package LintMark_Program {
    private import SoftwareItemMetadata::*;
    private import LintMark_Context::*;
    private import LintMark_HelpCommand::*;
    private import LintMark_LintMarkEngine::*;
    private import LintMark_Validation::*;

    part def Program {
        doc /* Entry point and execution orchestrator: it hands the help command to the help command unit, otherwise creates the context from the command line, answers the version, help, and self-validation requests, and otherwise hands the file processing to the engine and returns the exit code. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.LintMark/Program.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/ProgramTests.cs");
            designFiles = ("docs/design/lintmark/program.md");
            verificationFiles = ("docs/verification/lintmark/program.md");
            requirementFiles = ("docs/reqstream/lintmark/program.yaml");
        }

        // The entry point answers the help command before the options are parsed and ends the usage text
        // with its pointer to the help topics.
        dependency Program to HelpCommand;

        // The entry point creates and reads the context, dispatches to self-validation when asked,
        // and otherwise hands the file processing to the engine.
        dependency Program to Context;
        dependency Program to Validation;
        dependency Program to LintMarkEngine;
    }
}
