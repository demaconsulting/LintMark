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

using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace OtsSoftwareTests;

/// <summary>
///     Integration tests of the YamlDotNet OTS package: they pin the event-level parsing behavior that
///     LintMark's configuration loader relies on, including the places where the library does less than
///     the loader needs (byte order marks, duplicate keys) and where it fails in unexpected ways.
/// </summary>
public class YamlDotNetTests
{
    /// <summary>
    ///     The byte order mark character (code point 65279) as a string.
    /// </summary>
    private static readonly string ByteOrderMark = char.ConvertFromUtf32(65279);

    /// <summary>
    ///     Reads every event of a YAML text.
    /// </summary>
    /// <param name="yaml">The YAML text.</param>
    /// <returns>The events in stream order.</returns>
    private static List<ParsingEvent> ReadEvents(string yaml)
    {
        var parser = new Parser(new StringReader(yaml));
        var events = new List<ParsingEvent>();
        while (parser.MoveNext())
        {
            events.Add(parser.Current!);
        }

        return events;
    }

    /// <summary>
    ///     Selects the scalar events of a YAML text.
    /// </summary>
    /// <param name="yaml">The YAML text.</param>
    /// <returns>The scalars in stream order.</returns>
    private static List<Scalar> ReadScalars(string yaml) => [.. ReadEvents(yaml).OfType<Scalar>()];

    /// <summary>
    ///     Validates that keys and values of a block mapping carry 1-based line and column positions.
    /// </summary>
    [Fact]
    public void YamlDotNet_Parser_BlockMapping_ReportsKeysAndValuesWithOneBasedPositions()
    {
        // Arrange: a two-entry block mapping
        const string yaml = "line-length: 100\nwrap: fill\n";

        // Act: read the scalars
        var scalars = ReadScalars(yaml);

        // Assert: key and value starts are 1-based and the end follows the last character
        Assert.Equal(["line-length", "100", "wrap", "fill"], scalars.Select(s => s.Value));
        Assert.Equal(
            [(1, 1), (1, 14), (2, 1), (2, 7)],
            scalars.Select(s => ((int)s.Start.Line, (int)s.Start.Column)));
        Assert.Equal((1, 12), ((int)scalars[0].End.Line, (int)scalars[0].End.Column));
    }

    /// <summary>
    ///     Validates that carriage-return line feed input is numbered the same as line feed input.
    /// </summary>
    [Fact]
    public void YamlDotNet_Parser_CrlfInput_ReportsSameLinesAsLf()
    {
        // Arrange: the same document with both line endings
        const string lf = "a: 1\nb: 2\nc: 3\n";
        var crlf = lf.Replace("\n", "\r\n", StringComparison.Ordinal);

        // Act: read the scalar positions of both
        var lfPositions = ReadScalars(lf).Select(s => ((int)s.Start.Line, (int)s.Start.Column)).ToList();
        var crlfPositions = ReadScalars(crlf).Select(s => ((int)s.Start.Line, (int)s.Start.Column)).ToList();

        // Assert: identical positions
        Assert.Equal(lfPositions, crlfPositions);
    }

    /// <summary>
    ///     Validates that entries of a flow mapping carry positions and that the mapping style is reported.
    /// </summary>
    [Fact]
    public void YamlDotNet_Parser_FlowMapping_ReportsPositions()
    {
        // Arrange: a flow mapping on one line
        const string yaml = "{a: 1, b: 2}\n";

        // Act: read the events
        var events = ReadEvents(yaml);
        var scalars = events.OfType<Scalar>().ToList();

        // Assert: the mapping is flow style and the scalars are positioned within the line
        Assert.Equal(MappingStyle.Flow, events.OfType<MappingStart>().Single().Style);
        Assert.Equal([2, 5, 8, 11], scalars.Select(s => (int)s.Start.Column));
        Assert.All(scalars, s => Assert.Equal(1, (int)s.Start.Line));
    }

    /// <summary>
    ///     Validates that plain, quoted and block scalars are distinguishable by style.
    /// </summary>
    [Fact]
    public void YamlDotNet_Parser_ScalarStyles_ReportPlainAndQuoted()
    {
        // Arrange: one value of every style
        const string yaml = "a: plain\nb: \"double\"\nc: 'single'\nd: |\n  literal\ne: >\n  folded\n";

        // Act: read the value scalars (every second scalar)
        var values = ReadScalars(yaml).Where((_, index) => index % 2 == 1).ToList();

        // Assert: the styles are reported
        Assert.Equal(
            [ScalarStyle.Plain, ScalarStyle.DoubleQuoted, ScalarStyle.SingleQuoted, ScalarStyle.Literal, ScalarStyle.Folded],
            values.Select(v => v.Style));
    }

    /// <summary>
    ///     Validates that an empty stream and a comment-only stream contain no document.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("\n\n")]
    [InlineData("# only a comment\n")]
    public void YamlDotNet_Parser_EmptyAndCommentOnlyStreams_ProduceNoDocument(string yaml)
    {
        // Arrange: the stream is supplied by the data

        // Act: read the events
        var events = ReadEvents(yaml);

        // Assert: no document is present
        Assert.DoesNotContain(events, e => e is DocumentStart);
    }

    /// <summary>
    ///     Validates that a key without a value reports an empty plain scalar.
    /// </summary>
    [Fact]
    public void YamlDotNet_Parser_EmptyValue_IsEmptyPlainScalar()
    {
        // Arrange: a key with no value, and keys with the null spellings
        const string yaml = "a:\nb: ~\nc: null\n";

        // Act: read the value scalars
        var values = ReadScalars(yaml).Where((_, index) => index % 2 == 1).ToList();

        // Assert: all three are plain scalars the caller can classify as null
        Assert.Equal(["", "~", "null"], values.Select(v => v.Value));
        Assert.All(values, v => Assert.Equal(ScalarStyle.Plain, v.Style));
    }

    /// <summary>
    ///     Validates that a document marker alone produces a document holding one empty scalar.
    /// </summary>
    [Fact]
    public void YamlDotNet_Parser_DocumentMarkerOnly_ProducesEmptyScalarDocument()
    {
        // Arrange: a lone document start marker
        const string yaml = "---\n";

        // Act: read the events
        var events = ReadEvents(yaml);

        // Assert: one document containing one empty plain scalar
        Assert.Single(events.OfType<DocumentStart>());
        var scalar = Assert.Single(events.OfType<Scalar>());
        Assert.Equal(string.Empty, scalar.Value);
        Assert.Equal(ScalarStyle.Plain, scalar.Style);
    }

    /// <summary>
    ///     Validates that anchors, aliases and tags are reported so that a caller can reject them.
    /// </summary>
    [Fact]
    public void YamlDotNet_Parser_AnchorAliasAndTag_AreReported()
    {
        // Arrange: an anchor, an alias and a tag
        const string yaml = "a: &x 1\nb: *x\nc: !!str 2\n";

        // Act: read the events
        var events = ReadEvents(yaml);

        // Assert: each construct is visible on its event
        Assert.False(events.OfType<Scalar>().First(s => s.Value == "1").Anchor.IsEmpty);
        Assert.Single(events.OfType<AnchorAlias>());
        Assert.False(events.OfType<Scalar>().First(s => s.Value == "2").Tag.IsEmpty);
    }

    /// <summary>
    ///     Validates that several documents in one stream are reported as several document starts.
    /// </summary>
    [Fact]
    public void YamlDotNet_Parser_MultipleDocuments_AreReported()
    {
        // Arrange: two documents
        const string yaml = "a: 1\n---\nb: 2\n";

        // Act: read the events
        var documents = ReadEvents(yaml).OfType<DocumentStart>().ToList();

        // Assert: two starts, the second on line 2
        Assert.Equal(2, documents.Count);
        Assert.Equal(2, (int)documents[1].Start.Line);
    }

    /// <summary>
    ///     Pins that the parser does not reject duplicate keys, so the caller must detect them.
    /// </summary>
    [Fact]
    public void YamlDotNet_Parser_DuplicateKeys_AreNotRejected()
    {
        // Arrange: the same key twice
        const string yaml = "a: 1\na: 2\n";

        // Act: read the scalars
        var scalars = ReadScalars(yaml);

        // Assert: both occurrences are delivered with their own positions
        Assert.Equal(["a", "1", "a", "2"], scalars.Select(s => s.Value));
        Assert.Equal(2, (int)scalars[2].Start.Line);
    }

    /// <summary>
    ///     Validates that malformed input throws a YAML exception that carries a position.
    /// </summary>
    [Fact]
    public void YamlDotNet_Parser_MalformedInput_ThrowsYamlExceptionWithPosition()
    {
        // Arrange: a quoted scalar that is never closed, on the second line
        const string yaml = "a: 1\nb: \"never closed\n";

        // Act: read until the failure
        var exception = Assert.ThrowsAny<YamlException>(() => ReadEvents(yaml));

        // Assert: the position is exactly where the bad scalar starts (1-based, line 2 column 4)
        Assert.Equal((2L, 4L), (exception.Start.Line, exception.Start.Column));
        Assert.False(string.IsNullOrWhiteSpace(exception.Message));
    }

    /// <summary>
    ///     Pins that an unclosed flow sequence fails with a non-YAML exception, so the caller must treat any
    ///     exception from the parser as invalid input.
    /// </summary>
    [Fact]
    public void YamlDotNet_Parser_UnclosedFlowSequence_ThrowsInvalidOperationException()
    {
        // Arrange: a flow sequence that is never closed
        const string yaml = "a: [1, 2\nb: c\n";

        // Act: read until the failure
        var exception = Record.Exception(() => ReadEvents(yaml));

        // Assert: the failure is an InvalidOperationException rather than a YAML exception
        Assert.IsType<InvalidOperationException>(exception);
    }

    /// <summary>
    ///     Validates that tab indentation is rejected with a YAML exception.
    /// </summary>
    [Fact]
    public void YamlDotNet_Parser_TabIndentation_ThrowsYamlException()
    {
        // Arrange: a nested mapping indented with a tab
        const string yaml = "a:\n\tb: 1\n";

        // Act: read until the failure
        var exception = Assert.ThrowsAny<YamlException>(() => ReadEvents(yaml));

        // Assert: a position is reported
        Assert.True(exception.Start.Line >= 1);
    }

    /// <summary>
    ///     Validates that a deeply nested flow collection is parsed without exhausting the stack, and pins
    ///     the depth used by the tests.
    /// </summary>
    [Fact]
    public void YamlDotNet_Parser_DeeplyNestedFlowInput_ParsesWithoutStackExhaustion()
    {
        // Arrange: a flow sequence nested 3000 levels, parsed on a thread with a small stack (256 KiB)
        // so that any recursion in the parser would fail here
        const int depth = 3000;
        var yaml = "a: " + new string('[', depth) + new string(']', depth) + "\n";
        var maximumDepth = 0;
        Exception? failure = null;
        var thread = new Thread(
            () =>
            {
                try
                {
                    var level = 0;
                    foreach (var parsingEvent in ReadEvents(yaml))
                    {
                        level += parsingEvent switch { SequenceStart => 1, SequenceEnd => -1, _ => 0 };
                        maximumDepth = Math.Max(maximumDepth, level);
                    }
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            },
            256 * 1024)
        {
            // A parser that never finishes must not keep the test host alive after the join times out
            IsBackground = true
        };

        // Act: run to completion
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "Parsing nested input did not finish in time");

        // Assert: the whole nesting was parsed
        Assert.Null(failure);
        Assert.Equal(depth, maximumDepth);
    }

    /// <summary>
    ///     Pins that a leading byte order mark is not removed by the parser, so the caller must strip it.
    /// </summary>
    [Fact]
    public void YamlDotNet_Parser_LeadingByteOrderMark_IsPartOfFirstKey()
    {
        // Arrange: a document whose text starts with the byte order mark character
        var yaml = ByteOrderMark + "line-length: 100\n";

        // Act: read the scalars
        var scalars = ReadScalars(yaml);

        // Assert: the mark is part of the first key
        Assert.Equal(ByteOrderMark + "line-length", scalars[0].Value);
    }

    /// <summary>
    ///     Validates that independent parser instances used from many threads produce identical results.
    /// </summary>
    [Fact]
    public void YamlDotNet_Parser_IndependentParsers_ParallelParse_IdenticalResults()
    {
        // Arrange: a document and its sequential result
        const string yaml = "line-length: 100\nrules:\n  line-length: false\nignore:\n  - a/**\n  - b/**\n";
        var expected = ReadScalars(yaml).Select(s => $"{s.Value}@{s.Start.Line}:{s.Start.Column}").ToList();

        // Act: parse the same document from many threads
        var results = new List<string>[200];
        Parallel.For(
            0,
            results.Length,
            new ParallelOptions { MaxDegreeOfParallelism = 16 },
            index => results[index] = [.. ReadScalars(yaml).Select(s => $"{s.Value}@{s.Start.Line}:{s.Start.Column}")]);

        // Assert: every result equals the sequential one
        Assert.All(results, r => Assert.Equal(expected, r));
    }
}
