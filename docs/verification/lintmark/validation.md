## Validation

### Verification Approach

`Validation` is verified with unit tests in `ValidationTests.cs`. It runs the self-validation tests of the tool and
writes a summary and an optional results file, so the tests use a real `Context` (silent, so nothing is printed
to the test console) and real files in a `TemporaryDirectory`. The self-validation tests that `Validation` runs
drive the real tool, so no mocks are used. The writing of the TRX and JUnit result files is delegated to the
TestResults package, whose behavior is verified separately as an OTS item.

### Test Environment

Results-file scenarios create and remove temporary directories. The self-validation executes the tool in process
and needs no network or external service.

### Acceptance Criteria

- All unit tests pass with zero failures.
- A null context is refused with `ArgumentNullException`.
- The summary is printed even when the context is silent, and the exit status stays 0 when every self-test passes.
- A results file with a `.trx` extension is a valid TRX file and one with an `.xml` extension is a JUnit file.
- An unsupported results extension produces no file.

### Test Scenarios

#### Normal operation

**Silent context still prints the summary**: The summary of the self-tests is written although output is silent.

**Passing self-tests leave the exit status at 0**: The context exit status is 0 after a run.

**A TRX results file is written**: Requesting a `.trx` results file creates it.

**An XML results file is written**: Requesting an `.xml` results file creates it.

#### Boundary conditions

N/A - the self-validation has no numeric or size limits.

#### Error handling

**A null context is refused**: `Validation.Run(null)` throws `ArgumentNullException`.

**An unsupported results format writes no file**: A results file with another extension is not created and an
error is written to the context.
