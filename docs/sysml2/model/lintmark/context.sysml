package LintMark_Context {
    private import SoftwareItemMetadata::*;
    private import LintMark_RuleCatalog::*;

    part def Context {
        doc /* Command-line argument parser and I/O owner for one tool invocation: it parses the arguments and options, owns the standard output and error writers and the optional log file, and records the exit status. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.LintMark/Cli/Context.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/Cli/ContextTests.cs");
            designFiles = ("docs/design/lintmark/context.md");
            verificationFiles = ("docs/verification/lintmark/context.md");
            requirementFiles = ("docs/reqstream/lintmark/context.yaml");
        }

        // The rules forced on or off by the enable and disable options are carried as the rule overrides of
        // the rule catalog.
        dependency Context to RuleCatalog;
    }
}
