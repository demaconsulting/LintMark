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

namespace DemaConsulting.LintMark.Document;

/// <summary>
///     Describes a file that cannot be turned into a <see cref="SourceDocument"/>.
/// </summary>
/// <remarks>
///     A typed exception (rather than a message) is used so the engine can report the failure
///     as <c>path:line:col: rule-id message</c> without parsing text. The position is 1-based and
///     refers to the first offending byte or character when it is known, otherwise to 1:1.
///     Instances are immutable and safe to share between threads.
/// </remarks>
public sealed class SourceDocumentException : Exception
{
    /// <summary>
    ///     Rule identifier reported when the file is not valid UTF-8 or carries a non-UTF-8 byte order mark.
    /// </summary>
    public const string EncodingRuleId = "file-encoding-error";

    /// <summary>
    ///     Rule identifier reported when the file contains a bare carriage return.
    /// </summary>
    public const string LineEndingRuleId = "file-line-ending-error";

    /// <summary>
    ///     Initializes a new instance of the <see cref="SourceDocumentException"/> class.
    /// </summary>
    /// <param name="ruleId">Kebab-case rule identifier to report the failure under.</param>
    /// <param name="line">1-based line of the first offending character (1 when unknown).</param>
    /// <param name="column">1-based column of the first offending character (1 when unknown).</param>
    /// <param name="message">Human-readable description of the failure (single line, no path).</param>
    public SourceDocumentException(string ruleId, int line, int column, string message)
        : base(message)
    {
        RuleId = ruleId;
        Line = line;
        Column = column;
    }

    /// <summary>
    ///     Gets the kebab-case rule identifier the failure is reported under.
    /// </summary>
    public string RuleId { get; }

    /// <summary>
    ///     Gets the 1-based line of the first offending character.
    /// </summary>
    public int Line { get; }

    /// <summary>
    ///     Gets the 1-based column (UTF-16 code units, tab counted as one) of the first offending character.
    /// </summary>
    public int Column { get; }
}
