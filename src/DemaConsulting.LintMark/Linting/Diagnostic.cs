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

namespace DemaConsulting.LintMark.Linting;

/// <summary>
///     One finding about a file: a problem in the file, in its configuration, or in an attempt to rewrite it.
/// </summary>
/// <remarks>
///     <para>
///         The single diagnostic model exists so that every finding of the tool (read and parse failures,
///         configuration problems, formatting violations and safety refusals) is created, ordered,
///         de-duplicated and reported the same way. Whether a diagnostic is
///         <see cref="Fixable"/> is derived from its <see cref="Kind"/> so the two can never disagree.
///     </para>
///     <para>
///         The record is immutable and has value equality over all members, which the reporter uses to report
///         the same finding once. The path is the full path of the file (the reporter makes it relative);
///         positions are 1-based; the message is a single line without the path. Rule identifiers are checked
///         for their shape (kebab-case) but not against <see cref="RuleCatalog"/>, so a unit that reports can
///         be tested without the catalog; a test proves that every identifier the tool emits is registered.
///     </para>
///     <para>
///         Thread safety: instances are immutable and may be shared between threads.
///     </para>
/// </remarks>
internal sealed record Diagnostic
{
    /// <summary>
    ///     The characters that end a line: CR, LF, NEL (U+0085), line separator (U+2028) and paragraph separator (U+2029).
    /// </summary>
    private const string LineBreakCharacters = "\r\n\u0085\u2028\u2029";

    /// <summary>
    ///     Initializes a new instance of the <see cref="Diagnostic"/> class.
    /// </summary>
    /// <param name="ruleId">Kebab-case rule identifier (lowercase letters and digits separated by single hyphens).</param>
    /// <param name="kind">Whether rewriting the file removes the finding.</param>
    /// <param name="path">Full path of the file the finding belongs to. Must not be blank.</param>
    /// <param name="line">1-based line.</param>
    /// <param name="column">1-based column.</param>
    /// <param name="message">Single-line description without the path. Must not be blank and must not contain a line break.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="ruleId"/>, <paramref name="path"/> or <paramref name="message"/> is null.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="ruleId"/> is not kebab-case, <paramref name="path"/> or <paramref name="message"/>
    ///     is blank, or <paramref name="message"/> contains a line break.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="kind"/> is not a defined member, or <paramref name="line"/> or
    ///     <paramref name="column"/> is less than one.
    /// </exception>
    public Diagnostic(string ruleId, DiagnosticKind kind, string path, int line, int column, string message)
    {
        // Validate every argument so that an invalid diagnostic can never be created
        ArgumentNullException.ThrowIfNull(ruleId);
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(message);
        if (!IsKebabCase(ruleId))
        {
            throw new ArgumentException("The rule identifier must be kebab-case (lowercase letters and digits separated by hyphens).", nameof(ruleId));
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a defined DiagnosticKind value.");
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("The path must not be blank.", nameof(path));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(line, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(column, 1);
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("The message must not be blank.", nameof(message));
        }

        if (message.AsSpan().IndexOfAny(LineBreakCharacters) >= 0)
        {
            throw new ArgumentException("The message must be a single line.", nameof(message));
        }

        RuleId = ruleId;
        Kind = kind;
        Path = path;
        Line = line;
        Column = column;
        Message = message;
    }

    /// <summary>
    ///     Gets the kebab-case identifier of the rule the finding is reported under.
    /// </summary>
    public string RuleId { get; }

    /// <summary>
    ///     Gets whether rewriting the file removes the finding.
    /// </summary>
    public DiagnosticKind Kind { get; }

    /// <summary>
    ///     Gets the full path of the file the finding belongs to.
    /// </summary>
    public string Path { get; }

    /// <summary>
    ///     Gets the 1-based line of the finding.
    /// </summary>
    public int Line { get; }

    /// <summary>
    ///     Gets the 1-based column of the finding.
    /// </summary>
    public int Column { get; }

    /// <summary>
    ///     Gets the single-line description of the finding, without the path.
    /// </summary>
    public string Message { get; }

    /// <summary>
    ///     Gets a value indicating whether rewriting the file removes the finding.
    /// </summary>
    /// <remarks>
    ///     Computed from <see cref="Kind"/>, never stored, so the flag and the kind cannot disagree.
    /// </remarks>
    public bool Fixable => Kind == DiagnosticKind.Formatting;

    /// <summary>
    ///     Determines whether a text is a kebab-case identifier.
    /// </summary>
    /// <param name="value">The text to test.</param>
    /// <returns>
    ///     <see langword="true"/> when the text is non-empty, starts with a lowercase letter, and consists of
    ///     lowercase letters and digits separated by single hyphens (no trailing or doubled hyphen).
    /// </returns>
    private static bool IsKebabCase(string value)
    {
        if (value.Length == 0 || !char.IsAsciiLetterLower(value[0]) || value[^1] == '-')
        {
            return false;
        }

        // Every character must be a lowercase letter, a digit, or a hyphen that does not follow another hyphen
        for (var i = 1; i < value.Length; i++)
        {
            var c = value[i];
            var valid = char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || (c == '-' && value[i - 1] != '-');
            if (!valid)
            {
                return false;
            }
        }

        return true;
    }
}
