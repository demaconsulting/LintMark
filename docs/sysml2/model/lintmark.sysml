package LintMark {
    private import SoftwareItemMetadata::*;

    doc
    /*
     * LintMark is a .NET command-line tool for linting and optionally reformatting
     * markdown files into a preferred format.
     */

    part def LintMarkSystem {
        doc /* The LintMark system as a whole: it finds the Markdown files it is asked to check, reports the problems it finds, and with the fix option rewrites the files into the preferred format. */

        metadata info : SoftwareInfo {
            testFiles = (
                "test/DemaConsulting.LintMark.Tests/IntegrationTests.cs",
                "test/DemaConsulting.LintMark.Tests/IntegrationWrapTests.cs",
                "test/DemaConsulting.LintMark.Tests/IntegrationOptionTests.cs",
                "test/DemaConsulting.LintMark.Tests/IntegrationRuleTests.cs",
                "test/DemaConsulting.LintMark.Tests/IntegrationLintTests.cs",
                "test/DemaConsulting.LintMark.Tests/Runner.cs",
                "test/DemaConsulting.LintMark.Tests/TemporaryDirectory.cs",
                "test/DemaConsulting.LintMark.Tests/AssemblyInfo.cs");
            designFiles = ("docs/design/lintmark.md");
            verificationFiles = ("docs/verification/lintmark.md");
            requirementFiles = (
                "docs/reqstream/lintmark.yaml",
                "docs/reqstream/lintmark/platform-requirements.yaml");
        }

        part program : Program;
        part context : Context;
        part validation : Validation;
        part configuration : ConfigurationSubsystem;
        part document : DocumentSubsystem;
        part engine : EngineSubsystem;
        part linting : LintingSubsystem;
        part formatting : FormattingSubsystem;
        part markdig : Markdig;
        part fileSystemGlobbing : FileSystemGlobbing;
        part yamlDotNet : YamlDotNet;
        part editorConfig : EditorConfig;
        part testResults : TestResults;
    }
}
