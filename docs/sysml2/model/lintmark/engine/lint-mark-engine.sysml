package LintMark {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::*;

    // The byte arrays of the C# type are modeled as lists of Integer values (there is no byte scalar).
    attribute def FileChange {
        doc /* A rewrite of one file that passed every safety check and is ready to be written, with the exact bytes it was computed from and the resolved target they were read from, so the writer can refuse to replace a file that changed, or a link that was redirected, after it was read. */

        attribute originalLf : String;
        attribute newLf : String;
        attribute originalEndings : LineEndingInfo;
        attribute targetEnding : LineEnding;
        attribute originalBytes : Integer [0..*];
        attribute newBytes : Integer [0..*];
        attribute resolvedTarget : String;
    }

    // A change is present only for a rewrite that passed every safety check; the configuration is absent
    // when the file's configuration is invalid or was not attached.
    item def FileResult {
        doc /* The outcome of processing one file: its diagnostics, configuration problems, safety verdict, the rewrite ready to be written, and whether it was ignored. */

        attribute path : String;
        part diagnostics : Diagnostic [0..*];
        part config : LintMarkConfig [0..1];
        attribute configErrors : ConfigError [0..*];
        attribute safety : SafetyStatus;
        attribute change : FileChange [0..1];
        attribute ignored : Boolean;
    }

    part def LintMarkEngine {
        doc /* Discovers the selected files, processes them in parallel, attaches each file's effective configuration, skips ignored files, formats and safety-checks each rewrite, optionally writes it back or shows it as a diff, and reports diagnostics and configuration problems in a deterministic order (also documents the FileResult and FileChange supporting types). */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.LintMark/Engine/LintMarkEngine.cs",
                "src/DemaConsulting.LintMark/Engine/FileResult.cs",
                "src/DemaConsulting.LintMark/Engine/FileChange.cs");
            testFiles = (
                "test/DemaConsulting.LintMark.Tests/Engine/LintMarkEngineTests.cs",
                "test/DemaConsulting.LintMark.Tests/Engine/LintMarkEngineFixTests.cs",
                "test/DemaConsulting.LintMark.Tests/Engine/LintMarkEngineFormattingTests.cs",
                "test/DemaConsulting.LintMark.Tests/Engine/LintMarkEngineIgnoreTests.cs");
            designFiles = ("docs/design/lintmark/engine/lint-mark-engine.md");
            verificationFiles = ("docs/verification/lintmark/engine/lint-mark-engine.md");
            requirementFiles = ("docs/reqstream/lintmark/engine/lint-mark-engine.yaml");
        }

        // The engine takes its path arguments and the fix and diff options from the context, selects
        // files with FileDiscovery (each as a discovered file), and loads and parses each one with
        // SourceDocument and DocumentParser; the parsed document and its grid fallback decide whether a
        // file may be rewritten, and the suppression map decides whether the grid diagnostic is shown.
        dependency LintMarkEngine to Context;
        dependency LintMarkEngine to FileDiscovery;
        dependency LintMarkEngine to DiscoveredFile;
        dependency LintMarkEngine to SourceDocument;
        dependency LintMarkEngine to DocumentParser;
        dependency LintMarkEngine to ParsedDocument;
        dependency LintMarkEngine to GridTableFallback;
        dependency LintMarkEngine to SuppressionMap;

        // Each file's configuration comes from the configuration loader, and the line ending from the
        // editor settings is filled in by the engine when the configuration gives none; the results and
        // problems of both are held in the per-file result and reported through the context.
        dependency LintMarkEngine to ConfigLoader;
        dependency LintMarkEngine to EditorConfigResolver;
        dependency LintMarkEngine to LintMarkConfig;
        dependency LintMarkEngine to ConfigLoadResult;
        dependency LintMarkEngine to ConfigError;
        dependency LintMarkEngine to EditorConfigResult;

        // Findings are carried as diagnostics, and the engine's own failure diagnostics use rule
        // identifiers registered in the rule catalog.
        dependency LintMarkEngine to Diagnostic;
        dependency LintMarkEngine to DiagnosticKind;
        dependency LintMarkEngine to RuleCatalog;

        // A rewrite is computed by the document formatter, applied to the text with the source patcher,
        // and carried as a file change only when it is safe. The shipped tool creates the document
        // formatter with the five block formatters and the inline normalizer.
        dependency LintMarkEngine to DocumentFormatter;
        dependency LintMarkEngine to FormatResult;
        dependency LintMarkEngine to SourcePatcher;
        dependency LintMarkEngine to FileChange;
        dependency LintMarkEngine to HeadingFormatter;
        dependency LintMarkEngine to CodeBlockFormatter;
        dependency LintMarkEngine to ThematicBreakFormatter;
        dependency LintMarkEngine to ListFormatter;
        dependency LintMarkEngine to BlockQuoteFormatter;
        dependency LintMarkEngine to InlineNormalizer;

        // Every rewrite passes the three safety checks, whose verdicts and status are held in the result.
        dependency LintMarkEngine to HtmlEquivalenceChecker;
        dependency LintMarkEngine to StructuralTokenChecker;
        dependency LintMarkEngine to IdempotencyChecker;
        dependency LintMarkEngine to EquivalenceResult;
        dependency LintMarkEngine to SafetyStatus;

        // Diagnostics and diffs are reported through the reporters, and a safe rewrite is written to the
        // file with the atomic writer.
        dependency LintMarkEngine to TextReporter;
        dependency LintMarkEngine to DiffReporter;
        dependency LintMarkEngine to FileDiff;
        dependency LintMarkEngine to AtomicFileWriter;
    }
}
