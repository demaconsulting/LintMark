package LintMark_LintMarkEngine {
    private import SoftwareItemMetadata::*;
    private import LintMark_AtomicFileWriter::*;
    private import LintMark_BlockQuoteFormatter::*;
    private import LintMark_CodeBlockFormatter::*;
    private import LintMark_ConfigLoader::*;
    private import LintMark_Context::*;
    private import LintMark_DocumentFormatter::*;
    private import LintMark_DocumentParser::*;
    private import LintMark_EditSet::*;
    private import LintMark_EditorConfigResolver::*;
    private import LintMark_FileDiscovery::*;
    private import LintMark_HeadingFormatter::*;
    private import LintMark_InlineNormalizer::*;
    private import LintMark_LinkRefactorer::*;
    private import LintMark_LintMarkConfig::*;
    private import LintMark_LintRules::*;
    private import LintMark_ListFormatter::*;
    private import LintMark_ParagraphFormatter::*;
    private import LintMark_Reporters::*;
    private import LintMark_RuleCatalog::*;
    private import LintMark_SourceDocument::*;
    private import LintMark_TableFormatter::*;
    private import LintMark_ThematicBreakFormatter::*;
    private import ScalarValues::*;

    // The byte arrays of the C# type are modeled as lists of Integer values (there is no byte scalar).
    attribute def FileChange {
        doc /* A rewrite of one file that is ready to be written, with the exact bytes it was computed from and the resolved target they were read from, so the writer can refuse to replace a file that changed, or a link that was redirected, after it was read; it also carries the findings that remain in the rewritten text. */

        attribute originalLf : String;
        attribute newLf : String;
        attribute originalEndings : LineEndingInfo;
        attribute targetEnding : LineEnding;
        attribute originalBytes : Integer [0..*];
        attribute newBytes : Integer [0..*];
        attribute resolvedTarget : String;
        // Absent when the findings of the rewritten text were not computed.
        item remaining : Diagnostic [0..*];
    }

    // A change is present only for a rewrite that differs from the file and is safe to write; the
    // configuration is absent when the file's configuration is invalid or was not attached.
    item def FileResult {
        doc /* The outcome of processing one file: its diagnostics, configuration problems, the rewrite ready to be written, and whether it was ignored. */

        attribute path : String;
        item diagnostics : Diagnostic [0..*];
        part config : LintMarkConfig [0..1];
        attribute configErrors : ConfigError [0..*];
        attribute change : FileChange [0..1];
        attribute ignored : Boolean;
    }

    part def LintMarkEngine {
        doc /* Discovers the selected files, processes them in parallel, attaches each file's effective configuration, skips ignored files, lints and formats each file, optionally writes the rewrite back or shows it as a diff, and reports diagnostics and configuration problems in a deterministic order (also documents the FileResult and FileChange supporting types). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.LintMark/Engine/LintMarkEngine.cs");
            testFiles = (
                "test/DemaConsulting.LintMark.Tests/Engine/LintMarkEngineTests.cs",
                "test/DemaConsulting.LintMark.Tests/Engine/LintMarkEngineFixTests.cs",
                "test/DemaConsulting.LintMark.Tests/Engine/LintMarkEngineFormattingTests.cs",
                "test/DemaConsulting.LintMark.Tests/Engine/LintMarkEngineIgnoreTests.cs",
                "test/DemaConsulting.LintMark.Tests/Engine/LintMarkEngineLinkRefactorTests.cs",
                "test/DemaConsulting.LintMark.Tests/Engine/LintMarkEngineLintTests.cs",
                "test/DemaConsulting.LintMark.Tests/Engine/LintMarkEngineScalingTests.cs",
                "test/DemaConsulting.LintMark.Tests/Engine/LintMarkEngineTableTests.cs",
                "test/DemaConsulting.LintMark.Tests/Engine/LintMarkEngineWrapTests.cs");
            designFiles = ("docs/design/lintmark/engine/lint-mark-engine.md");
            verificationFiles = ("docs/verification/lintmark/engine/lint-mark-engine.md");
            requirementFiles = ("docs/reqstream/lintmark/engine/lint-mark-engine.yaml");
        }

        // The engine takes its path arguments and the fix and diff options from the context, selects
        // files with the file discovery, and loads and parses each one with the source document and the
        // document parser; a document that the parser flags as unsafe is never rewritten.
        dependency LintMarkEngine to Context;
        dependency LintMarkEngine to FileDiscovery;
        dependency LintMarkEngine to SourceDocument;
        dependency LintMarkEngine to DocumentParser;

        // Each file's configuration comes from the configuration loader, and the line ending from the
        // editor settings is filled in by the engine when the configuration gives none.
        dependency LintMarkEngine to ConfigLoader;
        dependency LintMarkEngine to EditorConfigResolver;
        dependency LintMarkEngine to LintMarkConfig;

        // Findings are carried as diagnostics, and the engine's own failure diagnostics and the rules the
        // command line forces on or off use the rule catalog; the lint-only rules are run by the lint rules.
        dependency LintMarkEngine to RuleCatalog;
        dependency LintMarkEngine to LintRules;

        // A rewrite is computed by the document formatter and applied to the text with the source patcher
        // of the edit set. The shipped tool creates the document formatter with the six block formatters,
        // the inline normalizer, the paragraph formatter, and the link refactorer, so that findings are
        // reported when checking and fixed when fixing.
        dependency LintMarkEngine to DocumentFormatter;
        dependency LintMarkEngine to EditSet;
        dependency LintMarkEngine to HeadingFormatter;
        dependency LintMarkEngine to CodeBlockFormatter;
        dependency LintMarkEngine to ThematicBreakFormatter;
        dependency LintMarkEngine to ListFormatter;
        dependency LintMarkEngine to BlockQuoteFormatter;
        dependency LintMarkEngine to TableFormatter;
        dependency LintMarkEngine to InlineNormalizer;
        dependency LintMarkEngine to ParagraphFormatter;
        dependency LintMarkEngine to LinkRefactorer;

        // Diagnostics and diffs are reported through the reporters, and a rewrite is written to the file
        // with the atomic writer.
        dependency LintMarkEngine to Reporters;
        dependency LintMarkEngine to AtomicFileWriter;
    }
}
