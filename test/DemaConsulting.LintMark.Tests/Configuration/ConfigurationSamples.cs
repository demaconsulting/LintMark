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

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Configuration file content shared by the tests of the Configuration subsystem and its units.
/// </summary>
internal static class ConfigurationSamples
{
    /// <summary>
    ///     A configuration file that gives every setting, including every link conversion option, three rules
    ///     (one of them by its legacy number) and two ignore patterns.
    /// </summary>
    public const string FullSample =
        "line-length: 100\n"
        + "wrap: sentence\n"
        + "end-of-line: crlf\n"
        + "final-newline: false\n"
        + "heading-style: atx\n"
        + "bullet: asterisk\n"
        + "list-indent: 4\n"
        + "ordered-style: one\n"
        + "emphasis: underscore\n"
        + "strong: underscore\n"
        + "code-fence: tilde\n"
        + "code-block-style: fenced\n"
        + "hard-break: backslash\n"
        + "thematic-break: asterisks\n"
        + "bare-urls: leave\n"
        + "tables: aligned\n"
        + "max-blank-lines: 2\n"
        + "trailing-whitespace: strip\n"
        + "link-refactor:\n"
        + "  enabled: true\n"
        + "  threshold: 80\n"
        + "  in-tables: false\n"
        + "  break-link-text: true\n"
        + "  label-max-length: 25\n"
        + "  definitions-marker: \"<!-- Links -->\"\n"
        + "rules:\n"
        + "  MD013: false\n"
        + "  heading-increment: false\n"
        + "  descriptive-link-text: true\n"
        + "  MD025: false\n"
        + "ignore:\n"
        + "  - \"docs/generated/**\"\n"
        + "  - \"**/vendor/**\"\n";
}
