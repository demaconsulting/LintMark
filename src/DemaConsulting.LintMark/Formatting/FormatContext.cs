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
/// <param name="Document">The parsed document the block belongs to. Never format-unsafe.</param>
/// <param name="Config">The effective configuration of the file.</param>
/// <param name="Layout">The line arithmetic of the document (where a line starts and ends, which lines are blank).</param>
/// <param name="Ownership">Which lines belong to which leaf block, which are protected and which lie inside a container.</param>
/// <remarks>
///     Immutable and shared by every formatter of one document. The line arithmetic and the line ownership map
///     are the same objects that the document-level passes use, so a formatter of a container (a list or a block
///     quote) knows which lines hold code or other protected content.
/// </remarks>
internal sealed record FormatContext(ParsedDocument Document, LintMarkConfig Config, LineLayout Layout, LineOwnership Ownership)
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="FormatContext"/> class with its own line arithmetic and a
    ///     line ownership map in which no block formatter has claimed a line.
    /// </summary>
    /// <param name="document">The parsed document the block belongs to.</param>
    /// <param name="config">The effective configuration of the file.</param>
    public FormatContext(ParsedDocument document, LintMarkConfig config)
        : this(document, config, new LineLayout(document.Source))
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="FormatContext"/> class with the line arithmetic of the document.
    /// </summary>
    /// <param name="document">The parsed document the block belongs to.</param>
    /// <param name="config">The effective configuration of the file.</param>
    /// <param name="layout">The line arithmetic of the document.</param>
    private FormatContext(ParsedDocument document, LintMarkConfig config, LineLayout layout)
        : this(document, config, layout, LineOwnership.Build(document, layout))
    {
    }
}
