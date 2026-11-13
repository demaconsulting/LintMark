package LintMark {
    private import SoftwareItemMetadata::*;
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
        doc /* Finds, reads and validates the .lintmark.yaml configuration that applies to a Markdown file, caching results per directory and per file (also documents the ConfigParser, ConfigLoadResult and ConfigError supporting types). */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.LintMark/Configuration/ConfigLoader.cs",
                "src/DemaConsulting.LintMark/Configuration/ConfigParser.cs",
                "src/DemaConsulting.LintMark/Configuration/ConfigLoadResult.cs",
                "src/DemaConsulting.LintMark/Configuration/ConfigError.cs");
            testFiles = (
                "test/DemaConsulting.LintMark.Tests/Configuration/ConfigLoaderTests.cs",
                "test/DemaConsulting.LintMark.Tests/Configuration/ConfigLoaderParseTests.cs");
            designFiles = ("docs/design/lintmark/configuration/config-loader.md");
            verificationFiles = ("docs/verification/lintmark/configuration/config-loader.md");
            requirementFiles = ("docs/reqstream/lintmark/configuration/config-loader.yaml");
        }

        // The YAML library supplies the events with positions; the loader builds the configuration, its
        // link conversion options and every enumerated setting from them, and the line ending is the
        // document model's type.
        dependency ConfigLoader to YamlDotNet;
        dependency ConfigLoader to LintMarkConfig;
        dependency ConfigLoader to LinkRefactorOptions;
        dependency ConfigLoader to LineEnding;

        // Type-level use of the setting enumerations when reading and checking each setting.
        dependency ConfigLoader to WrapMode;
        dependency ConfigLoader to HeadingStyle;
        dependency ConfigLoader to BulletMarker;
        dependency ConfigLoader to OrderedStyle;
        dependency ConfigLoader to EmphasisMarker;
        dependency ConfigLoader to FenceMarker;
        dependency ConfigLoader to CodeBlockStyle;
        dependency ConfigLoader to HardBreakStyle;
        dependency ConfigLoader to ThematicBreakStyle;
        dependency ConfigLoader to TableStyle;
        dependency ConfigLoader to BareUrlStyle;
        dependency ConfigLoader to TrailingWhitespaceStyle;

        // The loader returns its outcome and every problem as these value types.
        dependency ConfigLoader to ConfigLoadResult;
        dependency ConfigLoader to ConfigError;

        // Rule names in the rules block are resolved to canonical ids, aliases and dropped aliases by the
        // rule catalog (the configuration parser belongs to this unit).
        dependency ConfigLoader to RuleCatalog;
    }
}
