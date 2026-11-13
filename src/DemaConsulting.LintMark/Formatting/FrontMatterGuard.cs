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

using DemaConsulting.LintMark.Document;

namespace DemaConsulting.LintMark.Formatting;

/// <summary>
///     Detects the one situation in which writing a <c>---</c> line elsewhere in a document would turn the
///     document's first line into the opening of YAML front matter.
/// </summary>
/// <remarks>
///     <para>
///         The parser reads a first line of <c>---</c> as the start of front matter only when a closing
///         <c>---</c> line follows; without one it is a thematic break. A rewrite that creates such a line (a break
///         converted to dashes, an indented code block whose content has an unindented <c>---</c> line once it is
///         fenced) would silently swallow everything in between into front matter. The formatters that can create
///         such a line ask this guard first and leave the block as written.
///     </para>
///     <para>
///         Thread safety: static and stateless; safe for concurrent use.
///     </para>
/// </remarks>
internal static class FrontMatterGuard
{
    /// <summary>
    ///     The line that opens (and closes) YAML front matter.
    /// </summary>
    private const string Fence = "---";

    /// <summary>
    ///     Determines whether a new <c>---</c> line could close a front matter block that the first line opens.
    /// </summary>
    /// <param name="document">The parsed document. Must not be null.</param>
    /// <returns>
    ///     <see langword="true"/> when the first line is <c>---</c> and the document has no front matter (so the
    ///     first line is a thematic break that a later <c>---</c> line would turn into front matter).
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="document"/> is null.</exception>
    public static bool CouldOpenFrontMatter(ParsedDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var text = document.Source.Text;
        var end = text.IndexOf('\n', StringComparison.Ordinal);
        var firstLine = end < 0 ? text : text[..end];
        return firstLine.TrimEnd(' ', '\t') == Fence && !document.Blocks.Any(static block => block.Kind == BlockKind.FrontMatter);
    }
}
