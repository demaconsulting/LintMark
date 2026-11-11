package LintMark {
    private import SoftwareItemMetadata::*;

    part def Program {
        doc /* Entry point and execution orchestrator. */

        metadata info : SoftwareInfo {
            description = "Entry point and execution orchestrator.";
            sourceFiles = ("src/DemaConsulting.LintMark/Program.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/ProgramTests.cs");
            designFiles = ("docs/design/lintmark/program.md");
            verificationFiles = ("docs/verification/lintmark/program.md");
            requirementFiles = ("docs/reqstream/lintmark/program.yaml");
        }

        // The entry point creates and reads the context, dispatches to self-validation when asked,
        // and otherwise hands the file processing to the engine.
        dependency Program to Context;
        dependency Program to Validation;
        dependency Program to LintMarkEngine;
    }
}
