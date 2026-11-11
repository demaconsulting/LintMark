package LintMark {
    private import SoftwareItemMetadata::*;

    part def Markdig {
        doc /* Markdig Markdown parsing library (OTS): produces the syntax tree with source ranges for every document. */

        metadata info : SoftwareInfo {
            description = "Markdig Markdown parsing library (OTS): produces the syntax tree with source ranges for every document.";
            designFiles = ("docs/design/ots/markdig.md");
            verificationFiles = ("docs/verification/ots/markdig.md");
            requirementFiles = ("docs/reqstream/ots/markdig.yaml");
        }
    }

    part def FileSystemGlobbing {
        doc /* Microsoft.Extensions.FileSystemGlobbing library (OTS): matches files against include and exclude glob patterns. */

        metadata info : SoftwareInfo {
            description = "Microsoft.Extensions.FileSystemGlobbing library (OTS): matches files against include and exclude glob patterns.";
            designFiles = ("docs/design/ots/filesystemglobbing.md");
            verificationFiles = ("docs/verification/ots/filesystemglobbing.md");
            requirementFiles = ("docs/reqstream/ots/filesystemglobbing.yaml");
        }
    }
}
