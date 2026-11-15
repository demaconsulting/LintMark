## TestResults Verification

This document provides the verification evidence for the TestResults OTS software item (the
`DemaConsulting.TestResults` package). Requirements for this OTS item are defined in the TestResults OTS Software
Requirements document.

### Required Functionality

TestResults provides the in-memory model of test results and the serializers that write them as TRX and JUnit XML.
The `Validation` unit depends on it to write the results file of the self-validation (`--results`) so that CI can
use the validation of each platform as requirements evidence. LintMark relies on the serializers to produce
well-formed XML that names every test and keeps its outcome and failure message.

### Verification Approach

TestResults has no self-validation mode. It is verified by integration tests in the dedicated OTS test project
`test/OtsSoftwareTests` (`TestResultsTests.cs`), which reference the package directly and not the LintMark project.
Each test builds a result collection with one passed and one failed test, serializes it, parses the produced XML as a
document, reads it back through the package, and asserts on the structure, the names, and the outcomes. No files are
needed. How `Validation` uses the serializers (the choice by file extension, and the error for an unsupported one) is
verified in the `Validation` unit.

### Test Scenarios

#### TRX output is well-formed and keeps names and outcomes

**Scenario**: A collection with one passed and one failed test is serialized to TRX and read back.

**Expected**: The document root is a test run with one result per test, the passed test is reported as passed and the
failed one as failed, and the names and outcomes survive the round trip through the package.

#### JUnit output is well-formed and marks the failure

**Scenario**: The same collection is serialized to JUnit XML and read back.

**Expected**: The document has one test case per test, only the failed test has a failure element, and the names and
outcomes survive the round trip through the package.
