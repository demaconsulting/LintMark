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
///     Immutable and shared by every formatter of one document. The line arithmetic and the line ownership map
///     are the same objects that the document-level passes use, so a formatter of a container (a list or a block
///     quote) knows which lines hold code or other protected content. Later phases add the data that wrapping and
///     table formatting need (container prefixes, widths); formatters that do not need it are unaffected.
/// </remarks>
internal sealed record FormatContext
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="FormatContext"/> class with its own line arithmetic and a
    ///     line ownership map in which no block formatter has claimed a line.
    /// </summary>
    /// <param name="document">The parsed document the block belongs to. Never format-unsafe. Must not be null.</param>
    /// <param name="config">The effective configuration of the file. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="document"/> or <paramref name="config"/> is null.</exception>
    public FormatContext(ParsedDocument document, LintMarkConfig config)
        : this(document, config, CreateLayout(document))
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="FormatContext"/> class with the line arithmetic of the document.
    /// </summary>
    /// <param name="document">The parsed document the block belongs to. Never format-unsafe. Must not be null.</param>
    /// <param name="config">The effective configuration of the file. Must not be null.</param>
    /// <param name="layout">The line arithmetic of the document. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    private FormatContext(ParsedDocument document, LintMarkConfig config, LineLayout layout)
        : this(document, config, layout, LineOwnership.Build(document, layout, static _ => false))
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="FormatContext"/> class.
    /// </summary>
    /// <param name="document">The parsed document the block belongs to. Never format-unsafe. Must not be null.</param>
    /// <param name="config">The effective configuration of the file. Must not be null.</param>
    /// <param name="layout">The line arithmetic of the document. Must not be null.</param>
    /// <param name="ownership">Which lines belong to which block, as the document formatter computed it. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public FormatContext(ParsedDocument document, LintMarkConfig config, LineLayout layout, LineOwnership ownership)
    {
        // Validate the arguments so that an incomplete context can never be created
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(ownership);

        Document = document;
        Config = config;
        Layout = layout;
        Ownership = ownership;
    }

    /// <summary>
    ///     Gets the line arithmetic of the document (where a line starts and ends, which lines are blank).
    /// </summary>
    public LineLayout Layout { get; }

    /// <summary>
    ///     Gets which lines belong to which leaf block, which are protected and which lie inside a container.
    /// </summary>
    public LineOwnership Ownership { get; }

    /// <summary>
    ///     Creates the line arithmetic of a document, rejecting a null document.
    /// </summary>
    /// <param name="document">The parsed document.</param>
    /// <returns>The line arithmetic.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="document"/> is null.</exception>
    private static LineLayout CreateLayout(ParsedDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new LineLayout(document.Source);
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
