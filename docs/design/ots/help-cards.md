## HelpCards

This document describes the integration and usage design for the `HelpCards` OTS software item (the
`DemaConsulting.HelpCards` package).

### Purpose

HelpCards is the DEMA Consulting library for built-in help. It turns Markdown help cards embedded in the tool assembly
into a validated catalog, serves `lintmark help` from that catalog for people and AI agents, and supplies the test
helpers that prove the cards are valid and that their examples work. It is chosen because it is the shared DEMA
Consulting implementation of built-in help, which keeps the help command, its exit codes, and its JSON output the same
across the tools of the toolchain.

Like the other DEMA Consulting tools that LintMark uses, HelpCards is produced by another repository with its own
requirements and release lifecycle, and LintMark does not change it, so it is an OTS software item.

### Features Used

- `HelpSource.FromAssembly`: the `Help` folder that the package build targets embed in the tool assembly.
- `HelpCli` and `HelpCliOptions`: the command-line helper that answers the `help` command, and the pointer sentence
  (`Footer`) for the usage text. `GetCatalogConfiguration` gives the tests the composition that the tool ships.
- `HelpCatalogBuilder`: `AddSource`, `TagVocabulary`, and `ExpectedRoots` fix the cards, the allowed tags, and the set
  of topics.
- `HelpCardValidator` and `HelpExampleRunner`: test helpers that validate the cards and run every command example
  against the built tool.
- The MSBuild targets shipped in the package, which embed `Help/**` as resources of the tool assembly.

Not used: merging card sources of other libraries, translations, and the agent knowledge tool.

### Integration Pattern

HelpCards is consumed as a NuGet package reference of the main project, as a runtime dependency of the shipped tool;
the test project receives the test helpers through its project reference to the tool. `HelpCommand` is the only unit
that uses the package in the shipped tool. Creating its `HelpSource` and `HelpCli` performs no I/O and needs no
disposal, and the package builds the catalog only when `Program.Main` hands it a first argument `help`. See
*HelpCommand*.

The package is a pre-release, so its exact version is pinned in the project file; upgrading it requires re-running
the help tests and a review of this design.

HelpCards has no self-validation mode, and it is not qualified in `test/OtsSoftwareTests`. The evidence for the
behavior LintMark depends on is the help tests of the tool itself, which use the real package with the cards that the
tool ships.
