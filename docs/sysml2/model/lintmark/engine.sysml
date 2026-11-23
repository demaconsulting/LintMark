package LintMark_EngineSubsystem {
    private import SoftwareItemMetadata::*;
    public import LintMark_AtomicFileWriter::*;
    public import LintMark_FileDiscovery::*;
    public import LintMark_LintMarkEngine::*;
    public import LintMark_Reporters::*;

    part def EngineSubsystem {
        doc /* Runs one invocation over the selected files: discovers the files, processes them in parallel, attaches each file's settings, lints and formats each one, writes or shows the rewrites, and reports the findings and configuration problems in a deterministic order. */

        metadata info : SoftwareInfo {
            testFiles = (
                "test/DemaConsulting.LintMark.Tests/Engine/EngineSubsystemTests.cs",
                "test/DemaConsulting.LintMark.Tests/Engine/LintMarkEngineTestExtensions.cs");
            designFiles = ("docs/design/lintmark/engine.md");
            verificationFiles = ("docs/verification/lintmark/engine.md");
            requirementFiles = ("docs/reqstream/lintmark/engine.yaml");
        }

        part fileDiscovery : FileDiscovery;
        part lintMarkEngine : LintMarkEngine;
        part atomicFileWriter : AtomicFileWriter;
        part reporters : Reporters;
    }
}
