package LintMark {
    private import SoftwareItemMetadata::*;

    part def ConfigurationSubsystem {
        doc /* Decides which settings apply to a Markdown file: finds and validates the configuration file and determines the line ending given by the editor settings. */

        metadata info : SoftwareInfo {
            testFiles = (
                "test/DemaConsulting.LintMark.Tests/Configuration/ConfigurationSubsystemTests.cs",
                "test/DemaConsulting.LintMark.Tests/Configuration/ConfigurationSamples.cs");
            designFiles = ("docs/design/lintmark/configuration.md");
            verificationFiles = ("docs/verification/lintmark/configuration.md");
            requirementFiles = ("docs/reqstream/lintmark/configuration.yaml");
        }

        part lintMarkConfig : LintMarkConfig;
        part configLoader : ConfigLoader;
        part editorConfigResolver : EditorConfigResolver;
    }
}
