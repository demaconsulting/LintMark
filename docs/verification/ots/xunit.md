## xUnit Verification

This document provides the verification evidence for the xUnit OTS software item. Requirements for this OTS item
are defined in the xUnit OTS Software Requirements document.

### Required Functionality

xUnit v3 (`xunit.v3` and `xunit.runner.visualstudio`) is the unit-testing framework used by the project. It
discovers and runs all test methods and writes TRX result files that feed requirements traceability. Passing tests
confirm that the framework is functioning correctly.

### Verification Approach

xUnit is verified by the self-evidence of the CI pipeline. Every test of the product, of the OTS test project, and
of the development tools is discovered, executed, and recorded in a TRX result file by xUnit; ReqStream then
consumes those files with `--enforce`. If xUnit failed to discover or execute tests, or wrote no results, the
requirements would lose their test evidence and the build would fail. The scenarios below name representative tests
that xUnit must discover, execute, and record.

### Test Scenarios

#### Context_Create_NoArguments_ReturnsDefaultContext

**Scenario**: xUnit discovers and runs this test, which verifies default construction of the context.

**Expected**: xUnit executes the test, the test passes, and the result appears in the TRX output.

#### Context_Create_UnknownArgument_ThrowsArgumentException

**Scenario**: xUnit discovers and runs this test, which verifies that an unrecognized argument raises an exception.

**Expected**: xUnit executes the test, the test passes, and the result appears in the TRX output.

#### Program_Run_WithVersionFlag_DisplaysVersionOnly

**Scenario**: xUnit discovers and runs this test, which verifies that the program prints only the version for the
version flag.

**Expected**: xUnit executes the test, the test passes, and the result appears in the TRX output.

#### Validation_Run_WithSilentContext_PrintsSummary

**Scenario**: xUnit discovers and runs this test, which verifies that the validation prints its summary even when the
context is silent.

**Expected**: xUnit executes the test, the test passes, and the result appears in the TRX output.

#### Theory rows are discovered and recorded

**Scenario**: xUnit discovers and runs data-driven tests (for example the marker-check tests of the link
refactoring options).

**Expected**: Every data row is executed and recorded under the name of its test method in the TRX output.
