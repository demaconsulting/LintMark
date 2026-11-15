## TestResults

This document describes the integration and usage design for the `TestResults` OTS software item (the
`DemaConsulting.TestResults` package).

### Purpose

TestResults is the DEMA Consulting library that models a set of test results and serializes it to the TRX and JUnit
formats. The `Validation` unit uses it to record the outcome of the built-in self-validation tests and to write the
results file that CI tooling (ReqStream) consumes as test evidence. It is chosen because it is the shared DEMA
Consulting implementation of both formats, which avoids a hand-written serializer and keeps the result files
compatible with the other tools of the toolchain.

### Features Used

- The `TestResults` collection with its `Results` list
- The `TestResult` record: name, outcome, error message, and duration
- The `TestOutcome` values `Passed` and `Failed`
- `TrxSerializer.Serialize` for `.trx` result files
- `JUnitSerializer.Serialize` for `.xml` (JUnit) result files

Not used: reading result files, merging result sets, and any other serializer.

### Integration Pattern

TestResults is consumed as a NuGet package reference of the main project, as a runtime dependency of the shipped tool.
`Validation` creates one `TestResults` collection per `--validate` run, adds one `TestResult` for each self-validation
test, and, when `--results` is given, serializes the collection as a string and writes it to the file chosen by the
extension of the name (`.trx` for TRX, `.xml` for JUnit; any other extension is reported as an error). No
initialization, configuration file, or disposal is required, and the library holds no shared state. A failure to write
the results file is reported by `Validation` as an error and does not affect the other tests. See *Validation Design*.

TestResults has no self-validation mode. The evidence for the behavior LintMark depends on is the integration test
project `test/OtsSoftwareTests`, which serializes results to both formats and checks the content, and the
self-validation tests that run the tool itself.
