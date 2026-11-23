package LintMark_ConfigLoader {
    private import SoftwareItemMetadata::*;
    private import LintMark_LintMarkConfig::*;
    private import LintMark_RuleCatalog::*;
    private import LintMark_SourceDocument::*;
    private import OtsDependencies::*;
    private import ScalarValues::*;

    attribute def ConfigError {
        doc /* One problem found in a configuration source, with the position it is reported at. */

        attribute path : String;
        attribute line : Integer;
        attribute column : Integer;
        // The message property of the C# type is named messageText here because message is a SysML2 keyword.
        attribute messageText : String;
    }

    // The configuration is present exactly when there are no errors.
    item def ConfigLoadResult {
        doc /* The outcome of loading a configuration: either a valid configuration or the list of problems found. */

        part config : LintMarkConfig [0..1];
        attribute sourcePath : String [0..1];
        attribute errors : ConfigError [0..*];
    }

    part def ConfigLoader {
        doc /* Finds, reads and validates the .lintmark.yaml configuration that applies to a Markdown file (the nearest one up the directory tree, an explicit file, or the built-in defaults), caching results per directory and per file, and reports every problem with its position (also documents the ConfigParser, ConfigLoadResult and ConfigError supporting types). */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.LintMark/Configuration/ConfigLoader.cs",
                "src/DemaConsulting.LintMark/Configuration/ConfigParser.cs");
            testFiles = (
                "test/DemaConsulting.LintMark.Tests/Configuration/ConfigLoaderTests.cs",
                "test/DemaConsulting.LintMark.Tests/Configuration/ConfigLoaderParseTests.cs");
            designFiles = ("docs/design/lintmark/configuration/config-loader.md");
            verificationFiles = ("docs/verification/lintmark/configuration/config-loader.md");
            requirementFiles = ("docs/reqstream/lintmark/configuration/config-loader.yaml");
        }

        // The YAML library supplies the events with positions; the loader builds the configuration, its
        // link conversion options and every enumerated setting from them.
        dependency ConfigLoader to YamlDotNet;
        dependency ConfigLoader to LintMarkConfig;

        // The configuration text is decoded with the source document, so that an invalid byte sequence is
        // reported like one in a Markdown file, and its settings use the line ending type of that model.
        dependency ConfigLoader to SourceDocument;

        // Rule names in the rules block are resolved to canonical ids, aliases and dropped aliases by the
        // rule catalog.
        dependency ConfigLoader to RuleCatalog;
    }
}
