## HelpCommand

### Verification Approach

`HelpCommand` is verified with unit tests in `HelpCommandTests.cs`. Each test creates a help command with
`HelpCommand.Create`, which writes to in-memory writers instead of the console, or builds the catalog from the options
that `HelpCommand.CreateOptions` gives. No mocks are used: the tests use the real help cards embedded in the tool
assembly and the real HelpCards library, a documented dependency of the unit.

### Test Environment

N/A - standard test environment. No files, external services, or network access are needed; the cards are read from
the tool assembly.

### Acceptance Criteria

- All unit tests pass with zero failures.
- The help verb prints the topic index with every root topic and exits with 0.
- The shipped catalog passes validation with warnings treated as errors.
- Every card prints in lines of at most 120 characters.

### Test Scenarios

#### Normal operation

**The help verb prints the topic index**: `TryRun` with `["help"]` answers with exit code 0, and the output lists the
command of every root topic (`HelpCommand_TryRun_HelpVerb_PrintsTopicIndexAndReturnsZero`).

**The shipped catalog is valid**: The catalog configuration from `CreateOptions`, validated with warnings treated as
errors, reports no defect. This proves that the root cards are exactly `RootCardIds`, that every card carries one
purpose tag, and that every command example parses as a command line of the tool
(`HelpCommand_CreateOptions_ShippedCatalog_PassesValidation`).

#### Boundary conditions

**Every card fits the line width**: Every card of the shipped catalog is printed with its links and tokens expanded
and with a fixed, long prerelease version in place of the version of the build; each is answered with exit code 0
and has no line longer than 120 characters (`HelpCommand_TryRun_EveryCard_FitsMaximumLineWidth`).

#### Error handling

N/A - wrong help arguments and unknown topics are handled by HelpCards; the exit code 2 of an unknown topic is
verified at system level (`LintMark_HelpTopics_UnknownTopic_ReturnsUsageError`).
