package LintMark {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::*;

    enum def WrapMode {
        doc /* How paragraphs are wrapped. */

        enum fill;
        enum sentence;
        enum semantic;
    }

    enum def HeadingStyle {
        doc /* The canonical heading syntax. */

        enum atx;
    }

    enum def BulletMarker {
        doc /* The marker character of bullet list items. */

        enum dash;
        enum asterisk;
        enum plus;
    }

    enum def OrderedStyle {
        doc /* How ordered list items are numbered. */

        enum incrementing;
        enum one;
    }

    enum def EmphasisMarker {
        doc /* The delimiter character of emphasis and strong emphasis. */

        enum asterisk;
        enum underscore;
    }

    enum def FenceMarker {
        doc /* The fence character of fenced code blocks. */

        enum backtick;
        enum tilde;
    }

    enum def CodeBlockStyle {
        doc /* The canonical code block syntax. */

        enum fenced;
    }

    enum def HardBreakStyle {
        doc /* How a hard line break is written. */

        enum spaces;
        enum backslash;
    }

    enum def ThematicBreakStyle {
        doc /* How a thematic break is written. */

        enum dashes;
        enum asterisks;
        enum underscores;
    }

    enum def TableStyle {
        doc /* How table cells are laid out. */

        enum compact;
        enum aligned;
    }

    enum def BareUrlStyle {
        doc /* How bare URLs in text are treated. */

        enum autolink;
        enum leave;
    }

    enum def TrailingWhitespaceStyle {
        doc /* How trailing whitespace is treated. */

        enum strip;
    }

    attribute def LinkRefactorOptions {
        doc /* The options of the opt-in conversion of inline links to reference links. */

        attribute enabled : Boolean;
        attribute threshold : Integer;
        attribute inTables : Boolean;
        attribute breakLinkText : Boolean;
        attribute labelMaxLength : Integer;
        attribute definitionsMarker : String;
    }

    part def LintMarkConfig {
        doc /* The complete, validated, deeply immutable set of settings that governs how one Markdown file is linted and formatted (also documents the RuleIdentifiers supporting table, the LinkRefactorOptions type and the setting enumerations). */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.LintMark/Configuration/LintMarkConfig.cs",
                "src/DemaConsulting.LintMark/Configuration/LinkRefactorOptions.cs",
                "src/DemaConsulting.LintMark/Configuration/RuleIdentifiers.cs",
                "src/DemaConsulting.LintMark/Configuration/WrapMode.cs",
                "src/DemaConsulting.LintMark/Configuration/HeadingStyle.cs",
                "src/DemaConsulting.LintMark/Configuration/BulletMarker.cs",
                "src/DemaConsulting.LintMark/Configuration/OrderedStyle.cs",
                "src/DemaConsulting.LintMark/Configuration/EmphasisMarker.cs",
                "src/DemaConsulting.LintMark/Configuration/FenceMarker.cs",
                "src/DemaConsulting.LintMark/Configuration/CodeBlockStyle.cs",
                "src/DemaConsulting.LintMark/Configuration/HardBreakStyle.cs",
                "src/DemaConsulting.LintMark/Configuration/ThematicBreakStyle.cs",
                "src/DemaConsulting.LintMark/Configuration/TableStyle.cs",
                "src/DemaConsulting.LintMark/Configuration/BareUrlStyle.cs",
                "src/DemaConsulting.LintMark/Configuration/TrailingWhitespaceStyle.cs");
            testFiles = (
                "test/DemaConsulting.LintMark.Tests/Configuration/LintMarkConfigTests.cs",
                "test/DemaConsulting.LintMark.Tests/Configuration/RuleIdentifiersTests.cs");
            designFiles = ("docs/design/lintmark/configuration/lint-mark-config.md");
            verificationFiles = ("docs/verification/lintmark/configuration/lint-mark-config.md");
            requirementFiles = ("docs/reqstream/lintmark/configuration/lint-mark-config.yaml");
        }

        // An unspecified line ending is represented by no value; it is filled in from the editor settings later.
        attribute lineLength : Integer;
        attribute wrap : WrapMode;
        attribute endOfLine : LineEnding [0..1];
        attribute finalNewline : Boolean;
        attribute headings : HeadingStyle;
        attribute bullet : BulletMarker;
        attribute listIndent : Integer;
        attribute orderedStyle : OrderedStyle;
        attribute emphasis : EmphasisMarker;
        attribute strong : EmphasisMarker;
        attribute codeFence : FenceMarker;
        attribute codeBlockStyle : CodeBlockStyle;
        attribute hardBreak : HardBreakStyle;
        attribute thematicBreak : ThematicBreakStyle;
        attribute tables : TableStyle;
        attribute bareUrls : BareUrlStyle;
        attribute maxBlankLines : Integer;
        attribute trailingWhitespace : TrailingWhitespaceStyle;
        attribute linkRefactor : LinkRefactorOptions;

        // The rules map has one entry per configurable rule: rules_key holds the canonical kebab-case rule
        // id and rules_value the matching enabled state, at the same index.
        attribute rules_key : String [0..*];
        attribute rules_value : Boolean [0..*];
        attribute ignore : String [0..*];

        // Every enumerated setting, the link conversion options and the line ending type are used
        // directly as the types of the settings; there is no behavioral call.
        dependency LintMarkConfig to LineEnding;
        dependency LintMarkConfig to WrapMode;
        dependency LintMarkConfig to HeadingStyle;
        dependency LintMarkConfig to BulletMarker;
        dependency LintMarkConfig to OrderedStyle;
        dependency LintMarkConfig to EmphasisMarker;
        dependency LintMarkConfig to FenceMarker;
        dependency LintMarkConfig to CodeBlockStyle;
        dependency LintMarkConfig to HardBreakStyle;
        dependency LintMarkConfig to ThematicBreakStyle;
        dependency LintMarkConfig to TableStyle;
        dependency LintMarkConfig to BareUrlStyle;
        dependency LintMarkConfig to TrailingWhitespaceStyle;
        dependency LintMarkConfig to LinkRefactorOptions;
    }
}
