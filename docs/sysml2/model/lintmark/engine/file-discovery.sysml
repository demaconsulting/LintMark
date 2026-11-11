package LintMark {
    private import SoftwareItemMetadata::*;

    part def FileDiscovery {
        doc /* Expands path, directory, glob and exclusion arguments into the list of Markdown files to process. */

        metadata info : SoftwareInfo {
            description = "Expands path, directory, glob and exclusion arguments into the list of Markdown files to process.";
            sourceFiles = ("src/DemaConsulting.LintMark/Engine/FileDiscovery.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/Engine/FileDiscoveryTests.cs");
            designFiles = ("docs/design/lintmark/engine/file-discovery.md");
            verificationFiles = ("docs/verification/lintmark/engine/file-discovery.md");
            requirementFiles = ("docs/reqstream/lintmark/engine/file-discovery.yaml");
        }

        // Glob patterns are expanded and excluded by the globbing library.
        dependency FileDiscovery to FileSystemGlobbing;
    }
}
