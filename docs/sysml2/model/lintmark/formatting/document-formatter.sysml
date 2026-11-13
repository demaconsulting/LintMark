package LintMark {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::*;

    // The block formatter contract (RuleIds, CanFormat, Format) is modeled as an interface definition
    // because it is a C# interface that formatters implement; it has no data of its own.
    interface def IBlockFormatter {
        doc /* The contract of a formatter for one kind of block: it names the rules it applies, says whether it accepts a block, and proposes edits for a block it accepts. */
    }

    attribute def FormatContext {
        doc /* The inputs shared by every block formatter of one run: the parsed document and the effective configuration of its file. */

        item document : ParsedDocument;
        part config : LintMarkConfig;
    }

    // The edits are the attribute type TextEdit; the diagnostics are the Diagnostic unit.
    item def FormatResult {
        doc /* The outcome of formatting one document: the edits that bring it into canonical form, the line ending to write and the formatting violations found. */

        attribute edits : TextEdit [0..*];
        attribute lineEnding : LineEnding;
        part diagnostics : Diagnostic [0..*];
    }

    part def DocumentFormatter {
        doc /* Turns a parsed document into the edits and line ending that bring it into canonical form without writing anything: each block goes to the first registered block formatter that accepts it unless a rule is suppressed there, an unsafe document is never touched, and mixed or wrong line endings are reported (also documents the FormatResult and FormatContext supporting types and the IBlockFormatter contract, modeled as an interface definition). */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.LintMark/Formatting/DocumentFormatter.cs",
                "src/DemaConsulting.LintMark/Formatting/FormatResult.cs",
                "src/DemaConsulting.LintMark/Formatting/FormatContext.cs",
                "src/DemaConsulting.LintMark/Formatting/IBlockFormatter.cs");
            testFiles = (
                "test/DemaConsulting.LintMark.Tests/Formatting/DocumentFormatterTests.cs",
                "test/DemaConsulting.LintMark.Tests/Formatting/TestBlockFormatters.cs");
            designFiles = ("docs/design/lintmark/formatting/document-formatter.md");
            verificationFiles = ("docs/verification/lintmark/formatting/document-formatter.md");
            requirementFiles = ("docs/reqstream/lintmark/formatting/document-formatter.yaml");
        }

        // The formatter walks the blocks of a parsed document, skips blocks whose rules the suppression
        // map suppresses, and reads the line ending and counts from the source document and the
        // configuration it is given.
        dependency DocumentFormatter to ParsedDocument;
        dependency DocumentFormatter to BlockInfo;
        dependency DocumentFormatter to SuppressionMap;
        dependency DocumentFormatter to SourceDocument;
        dependency DocumentFormatter to LineEnding;
        dependency DocumentFormatter to LineEndingInfo;
        dependency DocumentFormatter to LintMarkConfig;

        // Edits are collected conflict-free in an edit set, and each block formatter is handed its context
        // and the edit set to add to.
        dependency DocumentFormatter to EditSet;
        dependency DocumentFormatter to TextEdit;
        dependency DocumentFormatter to IBlockFormatter;
        dependency DocumentFormatter to FormatContext;

        // The result carries the edits, the line ending and the line-ending diagnostics, whose rule id
        // comes from the rule catalog.
        dependency DocumentFormatter to FormatResult;
        dependency DocumentFormatter to Diagnostic;
        dependency DocumentFormatter to DiagnosticKind;
        dependency DocumentFormatter to RuleCatalog;
    }
}
