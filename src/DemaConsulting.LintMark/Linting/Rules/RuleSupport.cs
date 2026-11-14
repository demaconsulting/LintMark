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

using System.Text;
using DemaConsulting.LintMark.Document;
using Markdig.Syntax.Inlines;

namespace DemaConsulting.LintMark.Linting.Rules;

/// <summary>
///     Helpers shared by the lint-only rule units: creating a finding at an offset and reading the plain text of an inline tree.
/// </summary>
/// <remarks>
///     Thread safety: static and stateless; safe for concurrent use.
/// </remarks>
internal static class RuleSupport
{
    /// <summary>
    ///     Creates a lint-only finding at an offset of the document.
    /// </summary>
    /// <param name="source">The source document that holds the offset and the path.</param>
    /// <param name="ruleId">The canonical rule identifier.</param>
    /// <param name="offset">The offset into the LF-normalized text; a value outside the text is moved to the nearest end.</param>
    /// <param name="message">The message; line breaks are replaced by blanks so that the report keeps one line per finding.</param>
    /// <returns>The finding.</returns>
    public static Diagnostic Finding(SourceDocument source, string ruleId, int offset, string message)
    {
        var (line, column) = source.PositionOf(Math.Clamp(offset, 0, source.Text.Length));
        return new Diagnostic(ruleId, DiagnosticKind.LintOnly, source.Path, line, column, message.ReplaceLineEndings(" "));
    }

    /// <summary>
    ///     Reads the plain text of an inline tree: the text, the content of code spans and the text of links and emphasis.
    /// </summary>
    /// <param name="root">The inline content, or <see langword="null"/> when there is none.</param>
    /// <param name="includeImages">Whether the alternative text of an image is part of the text; <see langword="false"/> gives an image no text at all.</param>
    /// <returns>The text; line breaks are blanks and inline HTML contributes nothing.</returns>
    public static string PlainText(ContainerInline? root, bool includeImages = true)
    {
        var builder = new StringBuilder();
        if (root is not null)
        {
            Append(root, builder, includeImages);
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Collapses every run of whitespace to one blank and trims the ends.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The normalized text.</returns>
    public static string CollapseWhitespace(string text)
    {
        var builder = new StringBuilder(text.Length);
        var pending = false;
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                pending = builder.Length > 0;
            }
            else
            {
                if (pending)
                {
                    builder.Append(' ');
                    pending = false;
                }

                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Appends the plain text of an inline to a builder.
    /// </summary>
    /// <param name="inline">The inline.</param>
    /// <param name="builder">The builder.</param>
    /// <param name="includeImages">Whether the alternative text of an image is read.</param>
    private static void Append(Inline inline, StringBuilder builder, bool includeImages)
    {
        switch (inline)
        {
            case LiteralInline literal:
                builder.Append(literal.Content.ToString());
                break;
            case CodeInline code:
                builder.Append(code.Content);
                break;
            case LineBreakInline:
                builder.Append(' ');
                break;
            case HtmlEntityInline entity:
                builder.Append(entity.Transcoded.ToString());
                break;
            case LinkInline { IsImage: true } when !includeImages:
                break;
            case ContainerInline container:
                foreach (var child in container)
                {
                    Append(child, builder, includeImages);
                }

                break;
            default:
                break;
        }
    }
}
