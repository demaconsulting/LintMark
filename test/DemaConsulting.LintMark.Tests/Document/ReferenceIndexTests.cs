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

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Unit tests for the ReferenceIndex class.
/// </summary>
/// <remarks>
///     The index is built from the parsed document's blocks, so the tests obtain that input through
///     <see cref="DocumentParser.Parse"/>, which is the production builder of the index.
/// </remarks>
public class ReferenceIndexTests
{
    /// <summary>
    ///     Parses text as a document named <c>t.md</c>.
    /// </summary>
    /// <param name="text">The Markdown text.</param>
    /// <returns>The parsed document.</returns>
    private static ParsedDocument Parse(string text) => DocumentParser.Parse(SourceDocument.FromText("t.md", text));

    /// <summary>
    ///     Test that a document without definitions yields an empty index.
    /// </summary>
    [Fact]
    public void ReferenceIndex_Build_NoDefinitions_IsEmpty()
    {
        // Arrange / Act: a document with a link but no definition
        var document = Parse("Text [a](http://x).\n");

        // Assert: empty index, lookups fail
        Assert.Empty(document.References.Definitions);
        Assert.False(document.References.TryGet("a", out var definition));
        Assert.Null(definition);
    }

    /// <summary>
    ///     Test that definitions carry label, URL, title, span and line.
    /// </summary>
    [Fact]
    public void ReferenceIndex_Build_Definitions_CarryLabelUrlTitleSpanAndLine()
    {
        // Arrange: two definitions, one with a title
        const string text = "See [a] and [b].\n\n[a]: http://a.example\n[b]: http://b.example \"Bee\"\n";

        // Act: parse
        var definitions = Parse(text).References.Definitions;

        // Assert: both definitions with exact data and source order
        Assert.Equal(2, definitions.Count);
        Assert.Equal("a", definitions[0].Label);
        Assert.Equal("http://a.example", definitions[0].Url);
        Assert.Null(definitions[0].Title);
        Assert.Equal(3, definitions[0].Line);
        Assert.Equal("[a]: http://a.example", text.Substring(definitions[0].Start, definitions[0].EndInclusive - definitions[0].Start + 1));
        Assert.Equal("b", definitions[1].Label);
        Assert.Equal("Bee", definitions[1].Title);
        Assert.Equal(4, definitions[1].Line);
    }

    /// <summary>
    ///     Test that lookup ignores case and collapses whitespace.
    /// </summary>
    [Theory]
    [InlineData("foo bar")]
    [InlineData("FOO BAR")]
    [InlineData("  Foo   Bar ")]
    [InlineData("foo\tbar")]
    public void ReferenceIndex_TryGet_CaseAndWhitespaceInsensitive_FindsDefinition(string query)
    {
        // Arrange: a definition with a mixed-case, multi-space label
        var document = Parse("[Foo   Bar]: http://x\n");

        // Act: look up with a variant spelling
        var found = document.References.TryGet(query, out var definition);

        // Assert: found, and the original label is preserved
        Assert.True(found);
        Assert.Equal("Foo Bar", definition!.Label);
        Assert.Equal("foo bar", definition.NormalizedLabel);
    }

    /// <summary>
    ///     Test that when a label is defined twice the first definition wins but both are kept.
    /// </summary>
    [Fact]
    public void ReferenceIndex_TryGet_DuplicateLabels_FirstWinsAndBothRetained()
    {
        // Arrange: the same label defined twice
        var document = Parse("[a]: http://first\n[A]: http://second\n");

        // Act: look up
        var found = document.References.TryGet("a", out var definition);

        // Assert: first wins; both are listed
        Assert.True(found);
        Assert.Equal("http://first", definition!.Url);
        Assert.Equal(2, document.References.Definitions.Count);
    }

    /// <summary>
    ///     Test that a definition inside a block quote is indexed with its exact range, label and line.
    /// </summary>
    [Fact]
    public void ReferenceIndex_Build_DefinitionInBlockQuote_IsIndexed()
    {
        // Arrange: a definition inside a block quote
        const string text = "> [q]: http://q \"Quote\"\n\nOutside.\n";

        // Act: parse
        var definitions = Parse(text).References.Definitions;

        // Assert: found with its own span, label, destination, title and line
        var definition = Assert.Single(definitions);
        Assert.Equal("[q]: http://q \"Quote\"", text.Substring(definition.Start, definition.EndInclusive - definition.Start + 1));
        Assert.Equal("q", definition.Label);
        Assert.Equal("http://q", definition.Url);
        Assert.Equal("Quote", definition.Title);
        Assert.Equal(1, definition.Line);
    }

    /// <summary>
    ///     Test that a definition inside a list item is indexed with its exact range, label and line.
    /// </summary>
    [Fact]
    public void ReferenceIndex_Build_DefinitionInListItem_IsIndexed()
    {
        // Arrange: a definition on the second line, inside a list item
        const string text = "Intro.\n\n- [l]: http://l\n- item\n";

        // Act: parse
        var definitions = Parse(text).References.Definitions;

        // Assert: found with its own span, label, destination and line
        var definition = Assert.Single(definitions);
        Assert.Equal("[l]: http://l", text.Substring(definition.Start, definition.EndInclusive - definition.Start + 1));
        Assert.Equal("l", definition.Label);
        Assert.Equal("http://l", definition.Url);
        Assert.Equal(3, definition.Line);
    }

    /// <summary>
    ///     Test that definitions in top-level, quoted, listed and nested positions are all indexed in source order.
    /// </summary>
    [Fact]
    public void ReferenceIndex_Build_DefinitionInNestedContainers_IsIndexedInSourceOrder()
    {
        // Arrange: a top-level, a quoted, a listed and a quote-list-nested definition, mixed with a link
        const string text = "[a]: http://a\n\n> [b]: http://b\n\n- [c]: http://c\n\n> - [d]: http://d\n>   [e]: http://e\n\nUse [e].\n";

        // Act: parse and look up
        var document = Parse(text);

        // Assert: five definitions in source order and each one is found by its label
        Assert.Equal(["a", "b", "c", "d", "e"], document.References.Definitions.Select(d => d.Label).ToList());
        Assert.Equal([1, 3, 5, 7, 8], document.References.Definitions.Select(d => d.Line).ToList());
        Assert.True(document.References.TryGet("D", out var d));
        Assert.Equal("http://d", d.Url);
    }

    /// <summary>
    ///     Test that footnote definitions are not link reference definitions.
    /// </summary>
    [Fact]
    public void ReferenceIndex_Build_FootnotesAndUnreferencedFootnotes_AreExcluded()
    {
        // Arrange: a referenced footnote, an unreferenced one, and a real definition
        var document = Parse("Text[^1] [a].\n\n[^1]: Used.\n\n[^2]: Unused.\n\n[a]: http://x\n");

        // Act: read the definitions
        var labels = document.References.Definitions.Select(d => d.Label).ToList();

        // Assert: only the link definition is indexed
        Assert.Equal(["a"], labels);
    }

    /// <summary>
    ///     Test that a footnote definition inside a block quote is not indexed as a link reference definition.
    /// </summary>
    [Fact]
    public void ReferenceIndex_Build_FootnoteDefinitionInBlockQuote_IsExcluded()
    {
        // Arrange: a footnote definition in a quote next to a real definition in the same quote
        var document = Parse("> [^q]: note\n>\n> [a]: http://x\n");

        // Act: read the definitions
        var labels = document.References.Definitions.Select(d => d.Label).ToList();

        // Assert: only the link definition is indexed, and the footnote label cannot be looked up
        Assert.Equal(["a"], labels);
        Assert.False(document.References.TryGet("^q", out _));
    }

    /// <summary>
    ///     Test that a footnote definition inside a list item is not indexed as a link reference definition.
    /// </summary>
    [Fact]
    public void ReferenceIndex_Build_FootnoteDefinitionInListItem_IsExcluded()
    {
        // Arrange: a footnote definition in a list item
        var document = Parse("- [^n]: Note\n- b\n");

        // Act / Assert: nothing is indexed
        Assert.Empty(document.References.Definitions);
        Assert.False(document.References.TryGet("^n", out _));
    }

    /// <summary>
    ///     Test that no synthetic definitions are produced for headings.
    /// </summary>
    [Fact]
    public void ReferenceIndex_Build_Headings_ProduceNoSyntheticDefinitions()
    {
        // Arrange / Act: headings only
        var document = Parse("# One\n\n## Two\n");

        // Assert: no definitions
        Assert.Empty(document.References.Definitions);
    }

    /// <summary>
    ///     Test label normalization.
    /// </summary>
    [Theory]
    [InlineData("A", "a")]
    [InlineData("  a   b  ", "a b")]
    [InlineData("A\t\nB", "a b")]
    [InlineData("", "")]
    public void ReferenceIndex_NormalizeLabel_CollapsesWhitespaceAndFoldsCase(string label, string expected)
    {
        // Arrange / Act: normalize
        var normalized = ReferenceIndex.NormalizeLabel(label);

        // Assert: collapsed and lower-cased
        Assert.Equal(expected, normalized);
    }

    /// <summary>
    ///     Test that null arguments are rejected.
    /// </summary>
    [Fact]
    public void ReferenceIndex_Build_NullArguments_Throw()
    {
        // Arrange: a parsed document
        var document = Parse("a\n");

        // Act / Assert: nulls are rejected
        Assert.Throws<ArgumentNullException>(() => ReferenceIndex.Build(null!, document.Blocks));
        Assert.Throws<ArgumentNullException>(() => ReferenceIndex.Build(document.Source, null!));
        Assert.Throws<ArgumentNullException>(() => ReferenceIndex.NormalizeLabel(null!));
        Assert.Throws<ArgumentNullException>(() => document.References.TryGet(null!, out _));
    }
}
