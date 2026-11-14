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

namespace DemaConsulting.LintMark.Verify.Verification;

/// <summary>
///     One failed check of a file: what failed and the evidence for it.
/// </summary>
/// <param name="Kind">The kind of failure; always a failure kind.</param>
/// <param name="Description">A single line that names the first difference.</param>
/// <param name="TextDiff">The unified diff of the formatted text (empty when not applicable), capped.</param>
/// <param name="HtmlDiff">The unified diff of the normalized HTML of both sides (empty when not applicable), capped.</param>
internal sealed record Finding(OutcomeKind Kind, string Description, IReadOnlyList<string> TextDiff, IReadOnlyList<string> HtmlDiff);

/// <summary>
///     The result of verifying one file under one option set.
/// </summary>
/// <remarks>
///     A pass carries no findings and keeps nothing but its kind, so a run over many thousands of files stays small.
///     Immutable.
/// </remarks>
internal sealed record Outcome
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="Outcome"/> class for a pass.
    /// </summary>
    /// <param name="path">The full path of the file. Must not be blank.</param>
    /// <param name="optionLabel">The label of the option set. Must not be blank.</param>
    /// <param name="kind">The kind of a pass (a failure kind needs findings).</param>
    public Outcome(string path, string optionLabel, OutcomeKind kind)
        : this(path, optionLabel, kind, [])
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="Outcome"/> class.
    /// </summary>
    /// <param name="path">The full path of the file. Must not be blank.</param>
    /// <param name="optionLabel">The label of the option set. Must not be blank.</param>
    /// <param name="kind">The primary kind: the kind of the first finding, or of the pass.</param>
    /// <param name="findings">The findings; empty for a pass.</param>
    public Outcome(string path, string optionLabel, OutcomeKind kind, IReadOnlyList<Finding> findings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(optionLabel);
        ArgumentNullException.ThrowIfNull(findings);
        Path = path;
        OptionLabel = optionLabel;
        Kind = kind;
        Findings = findings;
    }

    /// <summary>
    ///     Gets the full path of the file.
    /// </summary>
    public string Path { get; }

    /// <summary>
    ///     Gets the label of the option set.
    /// </summary>
    public string OptionLabel { get; }

    /// <summary>
    ///     Gets the primary kind of the outcome.
    /// </summary>
    public OutcomeKind Kind { get; }

    /// <summary>
    ///     Gets the findings; empty for a pass.
    /// </summary>
    public IReadOnlyList<Finding> Findings { get; }

    /// <summary>
    ///     Gets a value indicating whether the outcome is a failure.
    /// </summary>
    public bool IsFailure => Kind.IsFailure();

    /// <summary>
    ///     Creates a failure with a single finding.
    /// </summary>
    /// <param name="path">The full path of the file.</param>
    /// <param name="optionLabel">The label of the option set.</param>
    /// <param name="kind">The failure kind.</param>
    /// <param name="description">What went wrong.</param>
    /// <returns>The outcome.</returns>
    public static Outcome Failure(string path, string optionLabel, OutcomeKind kind, string description) =>
        new(path, optionLabel, kind, [new Finding(kind, description, [], [])]);
}
