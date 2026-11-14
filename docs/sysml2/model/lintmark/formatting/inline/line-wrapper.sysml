package LintMark {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::*;

    attribute def WrappedLines {
        doc /* The lines that a run of atoms was filled into, and whether a line that holds more than one atom is still wider than its width because no break that keeps block syntax away from the start of a line exists. */

        attribute lines : String [0..*];
        attribute hasBreakableOverflow : Boolean;
    }

    part def LineWrapper {
        doc /* Fills lines with atoms by a greedy fill on display columns, joining atoms with one space, starting a new line before the atom that does not fit, leaving an atom that is wider than the width alone on its line, moving a break that would put block syntax at the start of a line to the nearest earlier break that is allowed when the line still fits, and never writing trailing whitespace, so that wrapping text that was wrapped this way changes nothing (also documents the WrappedLines supporting type, the lines and the overflow flag). */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.LintMark/Formatting/Inline/LineWrapper.cs",
                "src/DemaConsulting.LintMark/Formatting/Inline/WrappedLines.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/Formatting/Inline/LineWrapperTests.cs");
            designFiles = ("docs/design/lintmark/formatting/inline/line-wrapper.md");
            verificationFiles = ("docs/verification/lintmark/formatting/inline/line-wrapper.md");
            requirementFiles = ("docs/reqstream/lintmark/formatting/inline/line-wrapper.yaml");
        }

        // The width of every atom and line is measured in display columns, and the result carries the lines and
        // the overflow flag.
        dependency LineWrapper to TextWidth;
        dependency LineWrapper to WrappedLines;

        // A break before a word that would be read as the start of another block is not made, as decided by the
        // block syntax guard.
        dependency LineWrapper to BlockSyntaxGuard;
    }
}
