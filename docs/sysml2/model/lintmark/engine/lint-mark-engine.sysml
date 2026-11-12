package LintMark {
    private import SoftwareItemMetadata::*;

    part def LintMarkEngine {
        doc /* Discovers the selected files, processes them in parallel, attaches each file's effective configuration and reports failures and configuration problems in a deterministic order (also documents the FileResult and FileFailure supporting types). */

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

        // Each file's configuration comes from the configuration loader, and the line ending from the
        // editor settings is filled in by the engine when the configuration gives none; the results and
        // problems of both are held in the per-file result and reported through the context.
        dependency LintMarkEngine to ConfigLoader;
        dependency LintMarkEngine to EditorConfigResolver;
        dependency LintMarkEngine to LintMarkConfig;
        dependency LintMarkEngine to ConfigLoadResult;
        dependency LintMarkEngine to ConfigError;
        dependency LintMarkEngine to EditorConfigResult;
    }
}
