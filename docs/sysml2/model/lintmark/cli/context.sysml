package LintMark {
    private import SoftwareItemMetadata::*;

    part def Context {
        doc /* Command-line argument parser and I/O owner for one tool invocation. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.LintMark/Cli/Context.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/Cli/ContextTests.cs");
            designFiles = ("docs/design/lintmark/cli/context.md");
            verificationFiles = ("docs/verification/lintmark/cli/context.md");
            requirementFiles = ("docs/reqstream/lintmark/cli/context.yaml");
        }
    }
}
