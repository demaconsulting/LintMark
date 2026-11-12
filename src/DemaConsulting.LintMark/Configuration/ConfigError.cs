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

namespace DemaConsulting.LintMark.Configuration;

/// <summary>
///     One problem found in a configuration source, with the position it is reported at.
/// </summary>
/// <remarks>
///     Supporting type of <see cref="ConfigLoader"/>. Immutable. The message is kept on one line (line
///     breaks are replaced by spaces) so that one problem is always one line of output.
/// </remarks>
internal sealed record ConfigError
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ConfigError"/> class.
    /// </summary>
    /// <param name="path">Path of the file the problem is reported against. Must not be null or empty.</param>
    /// <param name="line">1-based line.</param>
    /// <param name="column">1-based column.</param>
    /// <param name="message">Description of the problem without the path or position. Must not be null or blank.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> or <paramref name="message"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is empty or <paramref name="message"/> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="line"/> or <paramref name="column"/> is less than one.</exception>
    public ConfigError(string path, int line, int column, string message)
    {
        // Validate input
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentOutOfRangeException.ThrowIfLessThan(line, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(column, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Path = path;
        Line = line;
        Column = column;
        Message = message.ReplaceLineEndings(" ");
    }

    /// <summary>
    ///     Gets the path of the file the problem is reported against.
    /// </summary>
    public string Path { get; }

    /// <summary>
    ///     Gets the 1-based line of the problem.
    /// </summary>
    public int Line { get; }

    /// <summary>
    ///     Gets the 1-based column of the problem (UTF-16 code units; a tab counts as one).
    /// </summary>
    public int Column { get; }

    /// <summary>
    ///     Gets the single-line description of the problem.
    /// </summary>
    public string Message { get; }
}
