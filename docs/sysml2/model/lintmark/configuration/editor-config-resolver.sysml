package LintMark {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::*;

    attribute def EditorConfigResult {
        doc /* The editor-settings outcome for one Markdown file: the line ending the editor settings specify, or why they could not be read. */

        attribute endOfLine : LineEnding [0..1];
        attribute errorMessage : String [0..1];
    }

    part def EditorConfigResolver {
        doc /* Determines the line ending that the .editorconfig files give for a Markdown file, caching the resolved chain of settings files per directory (also documents the EditorConfigResult supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.LintMark/Configuration/EditorConfigResolver.cs",
                "src/DemaConsulting.LintMark/Configuration/EditorConfigResult.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/Configuration/EditorConfigResolverTests.cs");
            designFiles = ("docs/design/lintmark/configuration/editor-config-resolver.md");
            verificationFiles = ("docs/verification/lintmark/configuration/editor-config-resolver.md");
            requirementFiles = ("docs/reqstream/lintmark/configuration/editor-config-resolver.yaml");
        }

        // The editor-settings library finds and evaluates the settings files, the resolver interprets
        // the raw end_of_line value as the document model's line ending, and returns its outcome as the
        // result type.
        dependency EditorConfigResolver to EditorConfig;
        dependency EditorConfigResolver to LineEnding;
        dependency EditorConfigResolver to EditorConfigResult;
    }
}
