## HelpCommand

![LintMark Structure](LintMarkView.svg)

### Purpose

`HelpCommand` answers the `lintmark help` command from the help cards that are embedded in the tool assembly. Its
single responsibility is to hand the arguments of a `help` invocation to the HelpCards command-line helper, configured
with the tool name, the version, and the expected set of cards. It is the only unit that references the HelpCards
library.

### Data Model

**ToolName**: `string` constant, `lintmark`, the name of the tool as the user types it. The usage lines, the index
preamble, and the `{{tool}}` token of the cards are derived from it, and `Program.PrintHelp` uses it for its usage
lines.

**RootCardIds**: `string[]`, the ids of the root cards, which are the topics that `lintmark help` lists:
`getting-started`, `command-line`, `configuration`, `formatting`, `wrapping`, `tables-and-links`, `rules`,
`suppression`, `self-validation`, and `troubleshooting`. It is the one list of topics; a missing or extra root card
fails the catalog.

**PurposeTags**: `string[]` (private), `tutorial`, `reference`, and `troubleshooting`; the closed tag vocabulary. Every
card carries one of these tags and no other tag.

**Cards**: `HelpSource` (private, static), the cards embedded in the tool assembly. Creating the source reads nothing.

**Default**: `HelpCommand` (static), the instance used by the tool; it writes to the console.

**_cli**: `HelpCli`, the command-line helper of HelpCards, created from `CreateOptions`. It is immutable and keeps no
state between calls.

**Help cards**: the Markdown files under `src/DemaConsulting.LintMark/Help/`, embedded by the HelpCards build targets.
The rules for writing them are in the *Help Cards* section of `CONTRIBUTING.md`.

### Key Methods

**TryRun(args, out exitCode)**: runs the help command when the first argument is `help`.

- *Parameters*: `string[] args`, the command-line arguments of the tool; `out int exitCode`, which receives the exit
  code when the command was answered.
- *Returns*: `bool`, `true` when the command was answered and `false` when the tool should continue with its own
  option parser.
- *Postconditions*: when `false` is returned, nothing was written.

It delegates to `HelpCli.TryRun`, which builds the catalog only after the verb was recognized.

**Footer**: gets the one-line pointer to the help command that ends the usage text, without building the catalog.

**CreateOptions(output, error)**: creates the options of the command-line helper.

- *Parameters*: optional `TextWriter` output and error writers (`null` for the console).
- *Returns*: `HelpCliOptions` with the tool name, the version from `Program.Version`, the catalog configuration (the
  embedded cards, `PurposeTags`, and `RootCardIds`), the usage lines, the index preamble, and the writers.
- *Postconditions*: `GetCatalogConfiguration` of the options gives the tests the composition that the tool ships.

**Create(output, error)**: creates a help command that writes to the given writers; used by the tests.

### Error Handling

`TryRun` does not throw for user input; the library writes errors to the error writer and sets the exit code (see
*Help command* in the system design). The validation test reports every card defect at build time, so a defective
catalog is not shipped.

The command is answered before the options are parsed, so no `Context` exists and the output and error routing of
`Context` is not used: routing the answers through a `Context` would mean parsing the options a second time just for
help.

### Dependencies

- **HelpCards** (OTS): `HelpCli`, `HelpCliOptions`, `HelpSource`, and `HelpCatalogBuilder`; see *HelpCards*.
- **Program**: `Program.Version` supplies the version shown in the index and the `{{version}}` token.

### Callers

`Program.Main` calls `HelpCommand.Default.TryRun` before `Context.Create`, and `Program.PrintHelp` ends the usage text
with `HelpCommand.Default.Footer`.
