## HelpCards Verification

This document provides the verification evidence for the HelpCards OTS software item (the `DemaConsulting.HelpCards`
package). Requirements for this OTS item are defined in the HelpCards OTS Software Requirements document.

### Required Functionality

HelpCards serves the built-in `help` command of LintMark from the Markdown help cards embedded in the tool assembly,
and supplies the test helpers that validate the cards and run their command examples. LintMark relies on the package
to embed the cards at build time, to answer `lintmark help` from them, to report every defect of a card, and to report
every command example whose exit code or output differs from what its card states.

### Verification Approach

HelpCards has no self-validation mode, and it is not verified in `test/OtsSoftwareTests`: its features are only used
with the cards that the tool ships, so the evidence comes from the help tests of the tool itself, which use the real
package with those cards. No mocking is applied. The unit tests of `HelpCommand` drive the package in process with
in-memory writers; the system tests start the built tool, and the example runner of the package gives each card a
fresh scratch folder in the temporary directory and calls the test for each command example.

### Test Scenarios

#### The help command is answered from the embedded cards

**Scenario**: The help verb is run in process through `HelpCommand`, and by the built tool.

**Expected**: The topic index lists every root topic and the exit code is 0
(`HelpCommand_TryRun_HelpVerb_PrintsTopicIndexAndReturnsZero` and `LintMark_HelpTopics_HelpVerb_ListsTopics`).

#### The test helpers validate the cards and run the examples

**Scenario**: The validator checks the shipped catalog with warnings treated as errors, and the example runner runs
every command example of the cards against the built tool.

**Expected**: The validator reports no defect, and every example gives the exit code and output that its card states
(`HelpCommand_CreateOptions_ShippedCatalog_PassesValidation` and
`LintMark_HelpExamples_AllCommandExamples_GiveStatedResults`).
