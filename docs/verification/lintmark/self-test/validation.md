### Validation

#### Verification Approach

`Validation` is verified with unit tests defined in `ValidationTests.cs`. Tests supply a real
`Context` object (not mocked) with controlled arguments and assert on exit codes, output
content, and result files. `Program` and `PathHelpers` also execute their real implementations;
no test doubles are introduced. Temporary directories are used for result file paths to keep
tests isolated.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- `Validation.Run` throws `ArgumentNullException` for a null context argument.
- The validation summary output contains "Total Tests:", "Passed:", and "Failed:".
- `context.ExitCode` is 0 when all sub-tests pass.
- TRX and JUnit XML result files are created with the correct XML root elements.
- An unsupported result file extension produces no file and an error message on the context.

#### Test Scenarios

##### Normal operation

**Summary is printed for a silent context**: `Validation.Run` is called with a silent context
(output captured separately); the summary contains "Total Tests:", "Passed:", and "Failed:",
confirming the summary is always produced.

**Exit code is zero when all sub-tests pass**: `Validation.Run` is called with a silent context;
`context.ExitCode` is 0 after the run, confirming all sub-tests pass in the standard environment.

**TRX results file is written**: `Validation.Run` is called with a context whose `ResultsFile`
points to a temporary `.trx` path; a file is created at the specified path and it contains a
`<TestRun` XML element.

**JUnit XML results file is written**: `Validation.Run` is called with a context whose
`ResultsFile` points to a temporary `.xml` path; a file is created at the specified path and it
contains a `<testsuites` XML element.

##### Boundary conditions

N/A - the unit has no numeric or size limits; its null-argument boundary is covered under error
handling.

##### Error handling

**Null context is rejected**: `Validation.Run` is called with a null context argument; an
`ArgumentNullException` is thrown, confirming the null guard at the unit boundary.

**Unsupported results format is reported and no file is written**: `Validation.Run` is called
with a context whose `ResultsFile` has a `.json` extension (unsupported); no file is created, no
exception is thrown, and an error message indicating the unsupported format is written to the
context.
