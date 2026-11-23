package LintMark_ConfigurationSubsystem {
    private import SoftwareItemMetadata::*;
    public import LintMark_ConfigLoader::*;
    public import LintMark_EditorConfigResolver::*;
    public import LintMark_LintMarkConfig::*;

    part def ConfigurationSubsystem {
        doc /* Finds, reads, and validates the settings that apply to each Markdown file: the nearest .lintmark.yaml, the built-in defaults, and the line ending from .editorconfig, and holds them as one immutable settings object per file. */

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
