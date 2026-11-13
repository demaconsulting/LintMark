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

using DemaConsulting.LintMark.Configuration;
using DemaConsulting.LintMark.Document;

namespace DemaConsulting.LintMark.Formatting;

/// <summary>
///     What a block formatter needs to know about the document and the settings it formats under.
/// </summary>
/// <remarks>
///     Immutable and shared by every formatter of one document. Later phases add the data that wrapping and
///     table formatting need (container prefixes, widths); formatters that do not need it are unaffected.
/// </remarks>
internal sealed record FormatContext
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="FormatContext"/> class.
    /// </summary>
    /// <param name="document">The parsed document the block belongs to. Never format-unsafe. Must not be null.</param>
    /// <param name="config">The effective configuration of the file. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="document"/> or <paramref name="config"/> is null.</exception>
    public FormatContext(ParsedDocument document, LintMarkConfig config)
    {
        // Validate the arguments so that an incomplete context can never be created
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(config);

        Document = document;
        Config = config;
    }

    /// <summary>
    ///     Gets the parsed document the block belongs to.
    /// </summary>
    public ParsedDocument Document { get; }

    /// <summary>
    ///     Gets the effective configuration of the file.
    /// </summary>
    public LintMarkConfig Config { get; }
}
