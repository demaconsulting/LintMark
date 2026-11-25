package OtsDependencies {
    private import SoftwareItemMetadata::*;

    // Runtime OTS items: libraries that ship with the tool and are referenced as parts of the system.

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

    part def TestResults {
        doc /* DemaConsulting.TestResults library (OTS): writes the self-validation results as TRX or JUnit result files. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/testresults.md");
            verificationFiles = ("docs/verification/ots/testresults.md");
            requirementFiles = ("docs/reqstream/ots/testresults.yaml");
        }
    }

    part def HelpCards {
        doc /* DemaConsulting.HelpCards library (OTS): embeds the help cards in the tool assembly, serves the help command from them, and supplies the test helpers that validate the cards and run their examples. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/help-cards.md");
            verificationFiles = ("docs/verification/ots/help-cards.md");
            requirementFiles = ("docs/reqstream/ots/help-cards.yaml");
        }
    }

    // Tooling OTS items: build, documentation, and quality tools used by the pipeline. They do not ship with
    // the tool, so they are not parts of the system.

    part def BuildMark {
        doc /* BuildMark build-notes tool (OTS): generates the build notes document from the repository history. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/buildmark.md");
            verificationFiles = ("docs/verification/ots/buildmark.md");
            requirementFiles = ("docs/reqstream/ots/buildmark.yaml");
        }
    }

    part def FileAssert {
        doc /* FileAssert document assertion tool (OTS): checks that the generated documents and diagrams exist and have the expected content. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/fileassert.md");
            verificationFiles = ("docs/verification/ots/fileassert.md");
            requirementFiles = ("docs/reqstream/ots/fileassert.yaml");
        }
    }

    part def Pandoc {
        doc /* Pandoc document converter (OTS): converts the Markdown documents to HTML. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/pandoc.md");
            verificationFiles = ("docs/verification/ots/pandoc.md");
            requirementFiles = ("docs/reqstream/ots/pandoc.yaml");
        }
    }

    part def ReqStream {
        doc /* ReqStream requirements traceability tool (OTS): checks that every requirement is satisfied by passing tests and produces the requirements and trace matrix documents. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/reqstream.md");
            verificationFiles = ("docs/verification/ots/reqstream.md");
            requirementFiles = ("docs/reqstream/ots/reqstream.yaml");
        }
    }

    part def ReviewMark {
        doc /* ReviewMark file review enforcement tool (OTS): checks that every file that needs review is covered by a current review and produces the review plan and report. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/reviewmark.md");
            verificationFiles = ("docs/verification/ots/reviewmark.md");
            requirementFiles = ("docs/reqstream/ots/reviewmark.yaml");
        }
    }

    part def SarifMark {
        doc /* SarifMark SARIF conversion tool (OTS): converts the code scanning results to a readable report. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/sarifmark.md");
            verificationFiles = ("docs/verification/ots/sarifmark.md");
            requirementFiles = ("docs/reqstream/ots/sarifmark.yaml");
        }
    }

    part def SonarMark {
        doc /* SonarMark quality report tool (OTS): produces the SonarCloud quality report document. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/sonarmark.md");
            verificationFiles = ("docs/verification/ots/sonarmark.md");
            requirementFiles = ("docs/reqstream/ots/sonarmark.yaml");
        }
    }

    part def SysML2Tools {
        doc /* SysML2Tools architecture model tool (OTS): lints the SysML2 model, answers queries about it, and renders the design diagrams. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/sysml2tools.md");
            verificationFiles = ("docs/verification/ots/sysml2tools.md");
            requirementFiles = ("docs/reqstream/ots/sysml2tools.yaml");
        }
    }

    part def VersionMark {
        doc /* VersionMark tool-version documentation tool (OTS): records the versions of the tools used by the pipeline. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/versionmark.md");
            verificationFiles = ("docs/verification/ots/versionmark.md");
            requirementFiles = ("docs/reqstream/ots/versionmark.yaml");
        }
    }

    part def WeasyPrint {
        doc /* WeasyPrint HTML-to-PDF converter (OTS): produces the PDF documents from the generated HTML. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/weasyprint.md");
            verificationFiles = ("docs/verification/ots/weasyprint.md");
            requirementFiles = ("docs/reqstream/ots/weasyprint.yaml");
        }
    }

    part def XUnit {
        doc /* xUnit unit-testing framework (OTS): discovers and runs the automated tests and reports their results. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/xunit.md");
            verificationFiles = ("docs/verification/ots/xunit.md");
            requirementFiles = ("docs/reqstream/ots/xunit.yaml");
        }
    }
}
