package LintMark {
    private import SoftwareItemMetadata::*;

    doc
    /*
     * LintMark is a .NET command-line tool for linting and optionally reformatting
     * markdown files into a preferred format.
     */

    part def LintMarkSystem {
        doc /* The LintMark system as a whole. */

        metadata info : SoftwareInfo {
            description = "The LintMark system as a whole.";
            testFiles = ("test/DemaConsulting.LintMark.Tests/IntegrationTests.cs");
            designFiles = ("docs/design/lintmark.md");
            verificationFiles = ("docs/verification/lintmark.md");
            requirementFiles = ("docs/reqstream/lintmark.yaml");
        }

        part program : Program;
        part cli : CliSubsystem;
        part document : DocumentSubsystem;
        part engine : EngineSubsystem;
        part selfTest : SelfTestSubsystem;
        part utilities : UtilitiesSubsystem;
        part markdig : Markdig;
        part fileSystemGlobbing : FileSystemGlobbing;
    }
}
