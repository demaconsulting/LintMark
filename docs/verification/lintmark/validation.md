## Validation

### Verification Approach

`Validation` is verified with unit tests in `ValidationTests.cs`. It runs the self-validation tests of the tool and
writes a summary and an optional results file, so the tests use a real `Context` (silent, so nothing is printed
to the test console) and real files in a `TemporaryDirectory`. The self-validation tests that `Validation` runs
drive the real tool, so no mocks are used. The writing of the TRX and JUnit result files is delegated to the
TestResults package, whose behavior is verified separately as an OTS item.

The functional self-tests (`LintMark_CleanFileNoFindings`, `LintMark_ViolationsReported`,
`LintMark_FixRewritesToCanonicalForm`, `LintMark_DiffPreviewsFixes`, `LintMark_ConfigurationHonored`) compare exact exit
codes, findings, and file contents, so a run in which every self-test passes is itself evidence that checking, fixing,
previewing, and configuration discovery work. The unit tests therefore assert that every self-test is reported as
passed, rather than repeating the fixtures.

### Test Environment

Results-file scenarios create and remove temporary directories. The self-validation executes the tool in process
and needs no network or external service.

### Acceptance Criteria

- All unit tests pass with zero failures.
- A null context is refused with `ArgumentNullException`.
- The summary is printed even when the context is silent, and the exit status stays 0 when every self-test passes.
- All seven self-tests, including the five functional tests, are reported as passed.
- The current directory is the same after the run as before it.
- A results file with a `.trx` extension is a valid TRX file and one with an `.xml` extension is a JUnit file.
- An unsupported results extension produces no file.

### Test Scenarios

#### Normal operation

**Silent context still prints the summary**: The summary of the self-tests is written although output is silent.

**Passing self-tests leave the exit status at 0**: The context exit status is 0 after a run.

**Every self-test runs and passes**: The log reports each of the seven self-tests as passed and the totals as seven
passed and zero failed (`Validation_Run_WithSilentContext_RunsAndPassesEverySelfTest`).

**The current directory is restored**: The functional self-tests switch the current directory to their workspace; after
the run it is the same as before (`Validation_Run_WithSilentContext_RestoresCurrentDirectory`).

**A TRX results file is written**: Requesting a `.trx` results file creates it, and it names the functional
self-tests.

**An XML results file is written**: Requesting an `.xml` results file creates it.

**The heading depth is honored**: With a depth of two the summary is headed by a second-level heading
(`Validation_Run_DepthTwo_HeadsTheSummaryAtThatDepth`).

#### Boundary conditions

N/A - the self-validation has no numeric or size limits.

#### Error handling

**A null context is refused**: `Validation.Run(null)` throws `ArgumentNullException`.

**An unsupported results format writes no file**: A results file with another extension is not created and an
error is written to the context.
