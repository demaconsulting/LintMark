package LintMark {
    private import SoftwareItemMetadata::*;

    part def LintMarkEngine {
        doc /* Discovers the selected files, processes them in parallel and reports failures in a deterministic order (also documents the FileResult and FileFailure supporting types). */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.LintMark/Engine/LintMarkEngine.cs",
                "src/DemaConsulting.LintMark/Engine/FileResult.cs",
                "src/DemaConsulting.LintMark/Engine/FileFailure.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/Engine/LintMarkEngineTests.cs");
            designFiles = ("docs/design/lintmark/engine/lint-mark-engine.md");
            verificationFiles = ("docs/verification/lintmark/engine/lint-mark-engine.md");
            requirementFiles = ("docs/reqstream/lintmark/engine/lint-mark-engine.yaml");
        }

        // The engine takes its path arguments from the context and reports failures through it, selects
        // files with FileDiscovery, and loads and parses each one with SourceDocument and DocumentParser.
        dependency LintMarkEngine to Context;
        dependency LintMarkEngine to FileDiscovery;
        dependency LintMarkEngine to SourceDocument;
        dependency LintMarkEngine to DocumentParser;
    }
}
