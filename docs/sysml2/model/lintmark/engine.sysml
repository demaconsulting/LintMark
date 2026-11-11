package LintMark {
    private import SoftwareItemMetadata::*;

    part def EngineSubsystem {
        doc /* Selects the Markdown files named on the command line, processes them in parallel and reports failures deterministically. */

        metadata info : SoftwareInfo {
            description = "Selects the Markdown files named on the command line, processes them in parallel and reports failures deterministically.";
            testFiles = ("test/DemaConsulting.LintMark.Tests/Engine/EngineSubsystemTests.cs");
            designFiles = ("docs/design/lintmark/engine.md");
            verificationFiles = ("docs/verification/lintmark/engine.md");
            requirementFiles = ("docs/reqstream/lintmark/engine.yaml");
        }

        part fileDiscovery : FileDiscovery;
        part lintMarkEngine : LintMarkEngine;
    }
}
