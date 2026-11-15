// Copyright (c) DEMA Consulting
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System.Xml.Linq;
using DemaConsulting.TestResults;
using DemaConsulting.TestResults.IO;
using TestResult = DemaConsulting.TestResults.TestResult;

namespace OtsSoftwareTests;

/// <summary>
///     Integration tests of the DemaConsulting.TestResults OTS package: they pin the TRX and JUnit
///     serialization that LintMark's self-validation relies on to write its results files.
/// </summary>
public class TestResultsTests
{
    /// <summary>
    ///     Creates a result collection with one passed and one failed test.
    /// </summary>
    /// <returns>The collection.</returns>
    private static DemaConsulting.TestResults.TestResults CreateResults()
    {
        var results = new DemaConsulting.TestResults.TestResults { Name = "LintMark validation" };
        results.Results.Add(new TestResult
        {
            Name = "LintMark_VersionDisplay",
            ClassName = "Validation",
            CodeBase = "LintMark",
            Outcome = TestOutcome.Passed,
            Duration = TimeSpan.FromMilliseconds(12)
        });
        results.Results.Add(new TestResult
        {
            Name = "LintMark_HelpDisplay",
            ClassName = "Validation",
            CodeBase = "LintMark",
            Outcome = TestOutcome.Failed,
            ErrorMessage = "Usage text not found",
            Duration = TimeSpan.FromMilliseconds(8)
        });
        return results;
    }

    /// <summary>
    ///     Validates that the TRX serializer writes well-formed TRX that names every test and keeps its outcome.
    /// </summary>
    [Fact]
    public void TestResults_TrxSerializer_PassedAndFailedResults_WritesValidTrx()
    {
        // Arrange: a collection of one passed and one failed test
        var results = CreateResults();

        // Act: serialize to TRX and parse the XML back, both as a document and through the package
        var trx = TrxSerializer.Serialize(results);
        var document = XDocument.Parse(trx);
        var roundTrip = TrxSerializer.Deserialize(trx);

        // Assert: a TestRun root with one result per test, and the names and outcomes survive the round trip
        Assert.Equal("TestRun", document.Root?.Name.LocalName);
        var outcomes = document.Descendants().Where(e => e.Name.LocalName == "UnitTestResult")
            .ToDictionary(e => (string)e.Attribute("testName")!, e => (string)e.Attribute("outcome")!);
        Assert.Equal(2, outcomes.Count);
        Assert.Equal("Passed", outcomes["LintMark_VersionDisplay"]);
        Assert.Equal("Failed", outcomes["LintMark_HelpDisplay"]);
        Assert.Equal(
            [("LintMark_HelpDisplay", TestOutcome.Failed), ("LintMark_VersionDisplay", TestOutcome.Passed)],
            roundTrip.Results.Select(r => (r.Name, r.Outcome)).OrderBy(r => r.Name, StringComparer.Ordinal));
    }

    /// <summary>
    ///     Validates that the JUnit serializer writes well-formed JUnit that names every test and marks the failure.
    /// </summary>
    [Fact]
    public void TestResults_JUnitSerializer_PassedAndFailedResults_WritesValidJUnit()
    {
        // Arrange: a collection of one passed and one failed test
        var results = CreateResults();

        // Act: serialize to JUnit and parse the XML back, both as a document and through the package
        var junit = JUnitSerializer.Serialize(results);
        var document = XDocument.Parse(junit);
        var roundTrip = JUnitSerializer.Deserialize(junit);

        // Assert: test cases for both tests, a failure element only for the failed one, outcomes survive the round trip
        var cases = document.Descendants("testcase").ToList();
        Assert.Equal(2, cases.Count);
        Assert.Equal(
            ["LintMark_HelpDisplay"],
            cases.Where(c => c.Elements("failure").Any()).Select(c => (string)c.Attribute("name")!));
        Assert.Contains(cases, c => (string?)c.Attribute("name") == "LintMark_VersionDisplay");
        Assert.Equal(
            [("LintMark_HelpDisplay", TestOutcome.Failed), ("LintMark_VersionDisplay", TestOutcome.Passed)],
            roundTrip.Results.Select(r => (r.Name, r.Outcome)).OrderBy(r => r.Name, StringComparer.Ordinal));
    }
}
