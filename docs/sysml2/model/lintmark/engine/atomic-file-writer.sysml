package LintMark {
    private import SoftwareItemMetadata::*;

    part def AtomicFileWriter {
        doc /* Replaces a file's content atomically through a temporary file in the same directory, refusing when the file changed since it was read or its symbolic link was redirected to another file, and keeping its permissions and symbolic link. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.LintMark/Engine/AtomicFileWriter.cs");
            testFiles = ("test/DemaConsulting.LintMark.Tests/Engine/AtomicFileWriterTests.cs");
            designFiles = ("docs/design/lintmark/engine/atomic-file-writer.md");
            verificationFiles = ("docs/verification/lintmark/engine/atomic-file-writer.md");
            requirementFiles = ("docs/reqstream/lintmark/engine/atomic-file-writer.yaml");
        }
    }
}
