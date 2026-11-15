package LintMark {
    private import SoftwareItemMetadata::*;

    part def Validation {
        doc /* Orchestrates the self-validation test suite, runs each self-test against a temporary directory, reports pass/fail results, and writes them to a TRX or JUnit results file when asked. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.LintMark/SelfTest/Validation.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/SelfTest/ValidationTests.cs");
            designFiles = ("docs/design/lintmark/validation.md");
            verificationFiles = ("docs/verification/lintmark/validation.md");
            requirementFiles = ("docs/reqstream/lintmark/validation.yaml");
        }

        // The self-tests write their output through the context and run the tool itself through the program
        // entry point.
        dependency Validation to Context;
        dependency Validation to Program;

        // The results file is written with the test results library.
        dependency Validation to TestResults;
    }
}
