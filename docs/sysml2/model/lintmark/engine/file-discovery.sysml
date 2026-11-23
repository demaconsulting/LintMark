package LintMark_FileDiscovery {
    private import SoftwareItemMetadata::*;
    private import OtsDependencies::*;
    private import ScalarValues::*;

    attribute def DiscoveredFile {
        doc /* One file selected for processing and whether the command line named it directly. */

        attribute path : String;
        // An explicitly named file is processed even when an ignore pattern matches it.
        attribute isExplicit : Boolean;
    }

    part def FileDiscovery {
        doc /* Expands path, directory, glob and exclusion arguments into the list of Markdown files to process, remembering which were named explicitly, answers whether a configuration's ignore patterns match a file, and prunes the directories that the ignore patterns supplied for a scan root ignore, nested repositories included (also documents the DiscoveredFile and IgnoreScope supporting types). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.LintMark/Engine/FileDiscovery.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/Engine/FileDiscoveryTests.cs");
            designFiles = ("docs/design/lintmark/engine/file-discovery.md");
            verificationFiles = ("docs/verification/lintmark/engine/file-discovery.md");
            requirementFiles = ("docs/reqstream/lintmark/engine/file-discovery.yaml");
        }

        // Glob patterns are expanded and excluded, and ignore patterns are matched, by the globbing library.
        dependency FileDiscovery to FileSystemGlobbing;
    }
}
