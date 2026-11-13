package LintMark {
    private import SoftwareItemMetadata::*;

    part def Markdig {
        doc /* Markdig Markdown parsing library (OTS): produces the syntax tree with source ranges for every document. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/markdig.md");
            verificationFiles = ("docs/verification/ots/markdig.md");
            requirementFiles = ("docs/reqstream/ots/markdig.yaml");
        }
    }

    part def FileSystemGlobbing {
        doc /* Microsoft.Extensions.FileSystemGlobbing library (OTS): matches files against include and exclude glob patterns. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/filesystemglobbing.md");
            verificationFiles = ("docs/verification/ots/filesystemglobbing.md");
            requirementFiles = ("docs/reqstream/ots/filesystemglobbing.yaml");
        }
    }

    part def YamlDotNet {
        doc /* YamlDotNet YAML parsing library (OTS): its event-level parser reads .lintmark.yaml with the position of every key and value. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/yamldotnet.md");
            verificationFiles = ("docs/verification/ots/yamldotnet.md");
            requirementFiles = ("docs/reqstream/ots/yamldotnet.yaml");
        }
    }

    part def EditorConfig {
        doc /* editorconfig library (OTS): finds and evaluates .editorconfig files so that the end_of_line setting can be read for a file. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/editorconfig.md");
            verificationFiles = ("docs/verification/ots/editorconfig.md");
            requirementFiles = ("docs/reqstream/ots/editorconfig.yaml");
        }
    }
}
