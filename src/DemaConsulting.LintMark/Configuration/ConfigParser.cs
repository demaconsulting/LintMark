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

using System.Collections.Immutable;
using System.Globalization;
using DemaConsulting.LintMark.Document;
using DemaConsulting.LintMark.Linting;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace DemaConsulting.LintMark.Configuration;

/// <summary>
///     Turns the text of a <c>.lintmark.yaml</c> file into a validated <see cref="LintMarkConfig"/> or a
///     complete list of positioned problems.
/// </summary>
/// <remarks>
///     <para>
///         Supporting type of <see cref="ConfigLoader"/>. The YAML parser is used at event level only: its
///         object-mapping layer would hide key positions, duplicate keys, scalar styles and null values, all
///         of which the schema must see. The events are first collected into a small tree without recursion
///         (so input nesting cannot exhaust the stack), then validated against the schema, whose own depth
///         is fixed at three levels.
///     </para>
///     <para>
///         Syntax problems and structural problems (a second document, a root that is not a mapping) end
///         the parse; every semantic problem (unknown setting, wrong type, invalid value, duplicate key)
///         is collected so one run reports all of them. Anchors, aliases and tags are rejected because a
///         setting's meaning must be readable where it is written. A flow nesting deeper than
///         <see cref="MaxNestingDepth"/> is rejected before parsing because the parser's running time
///         grows quadratically with it.
///     </para>
///     <para>
///         Thread safety: stateless and pure; safe for concurrent use.
///     </para>
/// </remarks>
internal static class ConfigParser
{
    /// <summary>
    ///     The deepest bracket nesting accepted in a configuration file.
    /// </summary>
    public const int MaxNestingDepth = 64;

    /// <summary>
    ///     The longest value text quoted in a message before it is shortened.
    /// </summary>
    private const int MaxQuotedLength = 40;

    /// <summary>
    ///     Parses configuration text.
    /// </summary>
    /// <param name="path">Path reported in every error. Must not be null or empty.</param>
    /// <param name="text">The decoded text of the file (without byte order mark). Must not be null.</param>
    /// <returns>The valid configuration, or the problems found (never both).</returns>
    /// <remarks>
    ///     An empty file, a file with only comments, a lone document marker and a lone null document all
    ///     mean "no settings" and yield the defaults. Pure: no I/O and no shared state.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> or <paramref name="text"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is empty.</exception>
    public static ConfigLoadResult Parse(string path, string text)
    {
        // Validate input
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(text);

        // Reject pathological nesting before the parser sees it
        var errors = new List<ConfigError>();
        if (FindExcessiveNesting(text) is { } tooDeep)
        {
            errors.Add(new ConfigError(path, tooDeep.Line, tooDeep.Column, $"Nesting is too deep (more than {MaxNestingDepth} levels)"));
            return Failed(path, errors);
        }

        // Collect the document as a tree; syntax and structure problems end the parse here
        var root = ReadTree(path, text, errors, out var fatal);
        if (fatal)
        {
            return Failed(path, errors);
        }

        // No document, or an explicit null document, means no settings
        if (root is null || (root is ScalarNode scalar && IsNull(scalar)))
        {
            return errors.Count == 0 ? Succeeded(path, LintMarkConfig.Default) : Failed(path, errors);
        }

        // The root must be a mapping of settings
        if (root is not MapNode map)
        {
            if (!root.Reported)
            {
                errors.Add(new ConfigError(path, root.Line, root.Column, "The configuration must be a mapping of settings"));
            }

            return Failed(path, errors);
        }

        // Validate every setting, collecting all problems
        var validator = new Validator(path, errors);
        var config = validator.ApplySettings(map);
        return errors.Count == 0 ? Succeeded(path, config) : Failed(path, errors);
    }

    /// <summary>
    ///     Creates the success result.
    /// </summary>
    /// <param name="path">The source path.</param>
    /// <param name="config">The configuration.</param>
    /// <returns>The result.</returns>
    private static ConfigLoadResult Succeeded(string path, LintMarkConfig config) => new(config, path, []);

    /// <summary>
    ///     Creates the failure result from collected errors.
    /// </summary>
    /// <param name="path">The source path.</param>
    /// <param name="errors">The errors; duplicates are removed.</param>
    /// <returns>The result.</returns>
    private static ConfigLoadResult Failed(string path, List<ConfigError> errors) => new(null, path, [.. errors.Distinct()]);

    /// <summary>
    ///     Determines whether a scalar is a YAML null.
    /// </summary>
    /// <param name="scalar">The scalar.</param>
    /// <returns><see langword="true"/> for an empty, <c>~</c>, <c>null</c>, <c>Null</c> or <c>NULL</c> plain scalar.</returns>
    private static bool IsNull(ScalarNode scalar) =>
        scalar.Style == ScalarStyle.Plain && scalar.Value is "" or "~" or "null" or "Null" or "NULL";

    /// <summary>
    ///     Finds the first bracket that exceeds the nesting limit.
    /// </summary>
    /// <param name="text">The text to scan.</param>
    /// <returns>The 1-based position of the offending bracket, or <see langword="null"/> when the nesting is acceptable.</returns>
    /// <remarks>
    ///     The scan is deliberately simple: it counts every <c>[</c> and <c>{</c> as an opener and every
    ///     <c>]</c> and <c>}</c> as a closer, whether or not it sits in a quoted string or comment. A valid
    ///     configuration never has an unbalanced run anywhere near the limit, so the simplification only
    ///     matters for input that is invalid anyway.
    /// </remarks>
    private static (int Line, int Column)? FindExcessiveNesting(string text)
    {
        var depth = 0;
        var line = 1;
        var column = 1;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c is '[' or '{')
            {
                depth++;
                if (depth > MaxNestingDepth)
                {
                    return (line, column);
                }
            }
            else if (c is ']' or '}')
            {
                depth = Math.Max(0, depth - 1);
            }

            // Track the position; a CR directly before an LF belongs to the same line break
            if (c == '\n' || (c == '\r' && (i + 1 >= text.Length || text[i + 1] != '\n')))
            {
                line++;
                column = 1;
            }
            else
            {
                column++;
            }
        }

        return null;
    }

    /// <summary>
    ///     Reads the YAML events into a tree.
    /// </summary>
    /// <param name="path">The path reported in errors.</param>
    /// <param name="text">The text.</param>
    /// <param name="errors">Receives every problem found while reading.</param>
    /// <param name="fatal">Receives <see langword="true"/> when reading had to stop (syntax error or second document).</param>
    /// <returns>The root node of the first document, or <see langword="null"/> when there is none.</returns>
    /// <remarks>
    ///     Containers are tracked with an explicit stack, so the nesting of the input cannot exhaust the call stack.
    ///     The parser can fail with exception types other than its own for some malformed input (an unclosed flow
    ///     sequence is one), so every exception from advancing the parser means "invalid YAML"; the position then
    ///     comes from the last event that was read successfully.
    /// </remarks>
    private static Node? ReadTree(string path, string text, List<ConfigError> errors, out bool fatal)
    {
        fatal = false;
        var parser = new Parser(new StringReader(text));
        var builder = new TreeBuilder(path, errors);
        var documents = 0;
        var lastLine = 1;
        var lastColumn = 1;

        while (true)
        {
            // Advance; any failure ends the parse with a positioned syntax error
            bool advanced;
            try
            {
                advanced = parser.MoveNext();
            }
            catch (YamlException ex)
            {
                errors.Add(new ConfigError(path, ToInt(ex.Start.Line), ToInt(ex.Start.Column), $"Invalid YAML: {ex.Message}"));
                fatal = true;
                return null;
            }

            // Generic catch is justified here: the parser throws exception types other than YamlException for some
            // malformed input (observed: InvalidOperationException for an unclosed flow sequence), and none of them
            // may escape the loader.
            catch (Exception)
            {
                errors.Add(new ConfigError(path, lastLine, lastColumn, "Invalid YAML: the content is malformed"));
                fatal = true;
                return null;
            }

            if (!advanced)
            {
                break;
            }

            // Remember where we are for the fallback error position
            var current = parser.Current!;
            lastLine = ToInt(current.End.Line);
            lastColumn = ToInt(current.End.Column);

            // Only one document is allowed
            if (current is DocumentStart && ++documents > 1)
            {
                errors.Add(new ConfigError(path, ToInt(current.Start.Line), ToInt(current.Start.Column), "Only one YAML document is allowed"));
                fatal = true;
                return builder.Root;
            }

            builder.Add(current);
        }

        return builder.Root;
    }

    /// <summary>
    ///     Converts a parser position component to a 1-based integer.
    /// </summary>
    /// <param name="value">The component.</param>
    /// <returns>The value clamped to at least one.</returns>
    private static int ToInt(long value) => (int)Math.Clamp(value, 1L, int.MaxValue);

    /// <summary>
    ///     A node of the collected document.
    /// </summary>
    private abstract class Node
    {
        /// <summary>
        ///     Gets or sets the 1-based line where the node starts.
        /// </summary>
        public int Line { get; init; }

        /// <summary>
        ///     Gets or sets the 1-based column where the node starts.
        /// </summary>
        public int Column { get; init; }

        /// <summary>
        ///     Gets or sets a value indicating whether a problem was already reported for this node (an alias or a decorated node).
        /// </summary>
        public bool Reported { get; init; }
    }

    /// <summary>
    ///     A scalar value with its style.
    /// </summary>
    private sealed class ScalarNode : Node
    {
        /// <summary>
        ///     Gets the scalar text.
        /// </summary>
        public required string Value { get; init; }

        /// <summary>
        ///     Gets the scalar style (plain, quoted, literal or folded).
        /// </summary>
        public required ScalarStyle Style { get; init; }
    }

    /// <summary>
    ///     A mapping as ordered key/value pairs; keys are nodes because the parser allows non-scalar keys.
    /// </summary>
    private sealed class MapNode : Node
    {
        /// <summary>
        ///     Gets the entries in source order.
        /// </summary>
        public List<(Node Key, Node Value)> Entries { get; } = [];

        /// <summary>
        ///     Gets or sets the key waiting for its value while the mapping is being read.
        /// </summary>
        public Node? PendingKey { get; set; }
    }

    /// <summary>
    ///     A sequence of nodes.
    /// </summary>
    private sealed class SequenceNode : Node
    {
        /// <summary>
        ///     Gets the items in source order.
        /// </summary>
        public List<Node> Items { get; } = [];
    }

    /// <summary>
    ///     A reference to an anchored node; always already reported because aliases are not supported.
    /// </summary>
    private sealed class AliasNode : Node
    {
    }

    /// <summary>
    ///     Builds the node tree from parser events using an explicit stack.
    /// </summary>
    private sealed class TreeBuilder
    {
        /// <summary>
        ///     The path reported in errors.
        /// </summary>
        private readonly string _path;

        /// <summary>
        ///     Receives the problems found.
        /// </summary>
        private readonly List<ConfigError> _errors;

        /// <summary>
        ///     The containers that are currently open, innermost on top.
        /// </summary>
        private readonly Stack<Node> _open = new();

        /// <summary>
        ///     Initializes a new instance of the <see cref="TreeBuilder"/> class.
        /// </summary>
        /// <param name="path">The path reported in errors.</param>
        /// <param name="errors">The list that receives problems.</param>
        public TreeBuilder(string path, List<ConfigError> errors)
        {
            _path = path;
            _errors = errors;
        }

        /// <summary>
        ///     Gets the root node of the first document, or <see langword="null"/> when none was read.
        /// </summary>
        public Node? Root { get; private set; }

        /// <summary>
        ///     Applies one parser event to the tree.
        /// </summary>
        /// <param name="parsingEvent">The event.</param>
        public void Add(ParsingEvent parsingEvent)
        {
            switch (parsingEvent)
            {
                case Scalar scalar:
                    Attach(new ScalarNode
                    {
                        Value = scalar.Value,
                        Style = scalar.Style,
                        Line = ToInt(scalar.Start.Line),
                        Column = ToInt(scalar.Start.Column),
                        Reported = Decorated(scalar, scalar.Anchor.IsEmpty, scalar.Tag.IsEmpty)
                    });
                    break;

                case MappingStart mapping:
                    var map = new MapNode
                    {
                        Line = ToInt(mapping.Start.Line),
                        Column = ToInt(mapping.Start.Column),
                        Reported = Decorated(mapping, mapping.Anchor.IsEmpty, mapping.Tag.IsEmpty)
                    };
                    Attach(map);
                    _open.Push(map);
                    break;

                case SequenceStart sequence:
                    var list = new SequenceNode
                    {
                        Line = ToInt(sequence.Start.Line),
                        Column = ToInt(sequence.Start.Column),
                        Reported = Decorated(sequence, sequence.Anchor.IsEmpty, sequence.Tag.IsEmpty)
                    };
                    Attach(list);
                    _open.Push(list);
                    break;

                case MappingEnd or SequenceEnd:
                    _open.Pop();
                    break;

                case AnchorAlias alias:
                    Report(alias);
                    Attach(new AliasNode { Line = ToInt(alias.Start.Line), Column = ToInt(alias.Start.Column), Reported = true });
                    break;

                default:
                    // Stream and document markers carry no data
                    break;
            }
        }

        /// <summary>
        ///     Reports an anchor or tag on an event.
        /// </summary>
        /// <param name="parsingEvent">The event.</param>
        /// <param name="noAnchor">Whether the event has no anchor.</param>
        /// <param name="noTag">Whether the event has no tag.</param>
        /// <returns><see langword="true"/> when a problem was reported.</returns>
        private bool Decorated(ParsingEvent parsingEvent, bool noAnchor, bool noTag)
        {
            if (noAnchor && noTag)
            {
                return false;
            }

            Report(parsingEvent);
            return true;
        }

        /// <summary>
        ///     Reports that anchors, aliases and tags are not supported at an event.
        /// </summary>
        /// <param name="parsingEvent">The event.</param>
        private void Report(ParsingEvent parsingEvent) =>
            _errors.Add(new ConfigError(
                _path,
                ToInt(parsingEvent.Start.Line),
                ToInt(parsingEvent.Start.Column),
                "Anchors, aliases and tags are not supported"));

        /// <summary>
        ///     Adds a node to the innermost open container, or makes it the root.
        /// </summary>
        /// <param name="node">The node.</param>
        private void Attach(Node node)
        {
            if (_open.Count == 0)
            {
                Root ??= node;
                return;
            }

            switch (_open.Peek())
            {
                case SequenceNode sequence:
                    sequence.Items.Add(node);
                    break;

                case MapNode map when map.PendingKey is null:
                    map.PendingKey = node;
                    break;

                case MapNode map:
                    map.Entries.Add((map.PendingKey!, node));
                    map.PendingKey = null;
                    break;

                default:
                    break;
            }
        }
    }

    /// <summary>
    ///     Checks the schema of a mapping node and builds the configuration.
    /// </summary>
    private sealed class Validator
    {
        /// <summary>
        ///     The path reported in errors.
        /// </summary>
        private readonly string _path;

        /// <summary>
        ///     Receives the problems found.
        /// </summary>
        private readonly List<ConfigError> _errors;

        /// <summary>
        ///     The configuration built so far.
        /// </summary>
        private LintMarkConfig _config = LintMarkConfig.Default;

        /// <summary>
        ///     Initializes a new instance of the <see cref="Validator"/> class.
        /// </summary>
        /// <param name="path">The path reported in errors.</param>
        /// <param name="errors">The list that receives problems.</param>
        public Validator(string path, List<ConfigError> errors)
        {
            _path = path;
            _errors = errors;
        }

        /// <summary>
        ///     Validates the top-level mapping and returns the configuration.
        /// </summary>
        /// <param name="root">The root mapping.</param>
        /// <returns>The configuration; meaningful only when no error was reported.</returns>
        public LintMarkConfig ApplySettings(MapNode root)
        {
            var seen = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var (key, value) in root.Entries)
            {
                if (!TryName(key, seen, string.Empty, out var name))
                {
                    continue;
                }

                ApplySetting(name, key, value);
            }

            return _config;
        }

        /// <summary>
        ///     Applies one top-level setting.
        /// </summary>
        /// <param name="name">The setting name.</param>
        /// <param name="key">The key node.</param>
        /// <param name="value">The value node.</param>
        private void ApplySetting(string name, Node key, Node value)
        {
            switch (name)
            {
                case "line-length" when TryInt(name, key, value, 0, LintMarkConfig.MaxNumber, out var lineLength):
                    _config = _config with { LineLength = lineLength };
                    break;
                case "wrap" when TryEnum<WrapMode>(name, key, value, out var wrap):
                    _config = _config with { Wrap = wrap };
                    break;
                case "end-of-line" when TryEnum<LineEnding>(name, key, value, out var endOfLine):
                    _config = _config with { EndOfLine = endOfLine };
                    break;
                case "final-newline" when TryBool(name, key, value, out var finalNewline):
                    _config = _config with { FinalNewline = finalNewline };
                    break;
                case "heading-style" when TryEnum<HeadingStyle>(name, key, value, out var headings):
                    _config = _config with { Headings = headings };
                    break;
                case "bullet" when TryEnum<BulletMarker>(name, key, value, out var bullet):
                    _config = _config with { Bullet = bullet };
                    break;
                case "list-indent":
                    ApplyListIndent(name, key, value);
                    break;
                case "ordered-style" when TryEnum<OrderedStyle>(name, key, value, out var ordered):
                    _config = _config with { OrderedStyle = ordered };
                    break;
                case "emphasis" when TryEnum<EmphasisMarker>(name, key, value, out var emphasis):
                    _config = _config with { Emphasis = emphasis };
                    break;
                case "strong" when TryEnum<EmphasisMarker>(name, key, value, out var strong):
                    _config = _config with { Strong = strong };
                    break;
                case "code-fence" when TryEnum<FenceMarker>(name, key, value, out var fence):
                    _config = _config with { CodeFence = fence };
                    break;
                case "code-block-style" when TryEnum<CodeBlockStyle>(name, key, value, out var codeBlock):
                    _config = _config with { CodeBlockStyle = codeBlock };
                    break;
                case "hard-break" when TryEnum<HardBreakStyle>(name, key, value, out var hardBreak):
                    _config = _config with { HardBreak = hardBreak };
                    break;
                case "thematic-break" when TryEnum<ThematicBreakStyle>(name, key, value, out var thematic):
                    _config = _config with { ThematicBreak = thematic };
                    break;
                case "bare-urls" when TryEnum<BareUrlStyle>(name, key, value, out var bareUrls):
                    _config = _config with { BareUrls = bareUrls };
                    break;
                case "tables" when TryEnum<TableStyle>(name, key, value, out var tables):
                    _config = _config with { Tables = tables };
                    break;
                case "max-blank-lines" when TryInt(name, key, value, 1, LintMarkConfig.MaxNumber, out var blankLines):
                    _config = _config with { MaxBlankLines = blankLines };
                    break;
                case "trailing-whitespace" when TryEnum<TrailingWhitespaceStyle>(name, key, value, out var trailing):
                    _config = _config with { TrailingWhitespace = trailing };
                    break;
                case "link-refactor":
                    ApplyLinkRefactor(key, value);
                    break;
                case "rules":
                    ApplyRules(key, value);
                    break;
                case "ignore":
                    ApplyIgnore(key, value);
                    break;
                default:
                    // Either a value that was reported by its Try method above, or an unknown setting
                    if (!IsKnownSetting(name))
                    {
                        AddError(key, $"Unknown setting {Quote(name)}");
                    }

                    break;
            }
        }

        /// <summary>
        ///     Determines whether a name is a setting handled by <see cref="ApplySetting"/>.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns><see langword="true"/> for a known setting.</returns>
        private static bool IsKnownSetting(string name) => name is
            "line-length" or "wrap" or "end-of-line" or "final-newline" or "heading-style" or "bullet"
            or "list-indent" or "ordered-style" or "emphasis" or "strong" or "code-fence" or "code-block-style"
            or "hard-break" or "thematic-break" or "bare-urls" or "tables" or "max-blank-lines"
            or "trailing-whitespace" or "link-refactor" or "rules" or "ignore";

        /// <summary>
        ///     Applies the list indent, which accepts only 2 or 4.
        /// </summary>
        /// <param name="name">The setting name.</param>
        /// <param name="key">The key node.</param>
        /// <param name="value">The value node.</param>
        private void ApplyListIndent(string name, Node key, Node value)
        {
            if (!TryScalar(name, key, value, out var scalar))
            {
                return;
            }

            // Only the plain numbers 2 and 4 are accepted; every other value gets the same message
            if (scalar.Style == ScalarStyle.Plain && scalar.Value is "2" or "4")
            {
                _config = _config with { ListIndent = scalar.Value == "2" ? 2 : 4 };
                return;
            }

            var hint = scalar.Style == ScalarStyle.Plain ? string.Empty : " without quotes";
            AddError(value, $"Invalid value {Quote(scalar.Value)} for setting '{name}'; expected one of: 2, 4{hint}");
        }

        /// <summary>
        ///     Applies the nested link-refactor options.
        /// </summary>
        /// <param name="key">The key node.</param>
        /// <param name="value">The value node.</param>
        private void ApplyLinkRefactor(Node key, Node value)
        {
            if (!TryMapping("link-refactor", key, value, "a mapping of options", out var map))
            {
                return;
            }

            var options = _config.LinkRefactor;
            var seen = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var (optionKey, optionValue) in map.Entries)
            {
                if (!TryName(optionKey, seen, "link-refactor.", out var name))
                {
                    continue;
                }

                var scoped = "link-refactor." + name;
                options = name switch
                {
                    "enabled" when TryBool(scoped, optionKey, optionValue, out var enabled) => options with { Enabled = enabled },
                    "threshold" when TryInt(scoped, optionKey, optionValue, 1, LintMarkConfig.MaxNumber, out var threshold) => options with { Threshold = threshold },
                    "in-tables" when TryBool(scoped, optionKey, optionValue, out var inTables) => options with { InTables = inTables },
                    "break-link-text" when TryBool(scoped, optionKey, optionValue, out var breakText) => options with { BreakLinkText = breakText },
                    "label-max-length" when TryInt(scoped, optionKey, optionValue, 1, LintMarkConfig.MaxNumber, out var labelLength) => options with { LabelMaxLength = labelLength },
                    "definitions-marker" => ApplyMarker(options, scoped, optionKey, optionValue),
                    _ => ReportUnknownOption(options, name, scoped, optionKey)
                };
            }

            _config = _config with { LinkRefactor = options };
        }

        /// <summary>
        ///     Applies the definitions marker option.
        /// </summary>
        /// <param name="options">The options so far.</param>
        /// <param name="scoped">The dotted setting name.</param>
        /// <param name="key">The key node.</param>
        /// <param name="value">The value node.</param>
        /// <returns>The options, with the marker when it is valid.</returns>
        private LinkRefactorOptions ApplyMarker(LinkRefactorOptions options, string scoped, Node key, Node value)
        {
            if (!TryScalar(scoped, key, value, out var scalar))
            {
                return options;
            }

            if (!LinkRefactorOptions.IsValidMarker(scalar.Value))
            {
                AddError(value, $"Invalid value {Quote(scalar.Value)} for setting '{scoped}'; expected a single-line HTML comment");
                return options;
            }

            return options with { DefinitionsMarker = scalar.Value };
        }

        /// <summary>
        ///     Reports an unknown link-refactor option unless the name is a known one whose value was already reported.
        /// </summary>
        /// <param name="options">The options so far.</param>
        /// <param name="name">The option name.</param>
        /// <param name="scoped">The dotted setting name.</param>
        /// <param name="key">The key node.</param>
        /// <returns>The unchanged options.</returns>
        private LinkRefactorOptions ReportUnknownOption(LinkRefactorOptions options, string name, string scoped, Node key)
        {
            if (name is not ("enabled" or "threshold" or "in-tables" or "break-link-text" or "label-max-length"))
            {
                AddError(key, $"Unknown setting {Quote(scoped)}");
            }

            return options;
        }

        /// <summary>
        ///     Applies the rule toggles.
        /// </summary>
        /// <param name="key">The key node.</param>
        /// <param name="value">The value node.</param>
        /// <remarks>
        ///     Two entries that name the same rule (by id and alias, or in different letter case) conflict. The
        ///     conflict is reported at every occurrence after the first by position, so the outcome depends
        ///     only on the content of the file, never on the order in which entries are processed.
        /// </remarks>
        private void ApplyRules(Node key, Node value)
        {
            if (!TryMapping("rules", key, value, "a mapping of rule names to true or false", out var map))
            {
                return;
            }

            var seen = new Dictionary<string, (int Line, string Name)>(StringComparer.Ordinal);
            var rules = _config.Rules.ToBuilder();
            foreach (var (ruleKey, ruleValue) in map.Entries)
            {
                if (!TryScalarKey(ruleKey, out var name))
                {
                    continue;
                }

                // Resolve the name; the alias of a dropped rule is accepted and ignored, anything else is unknown
                string canonical;
                var dropped = false;
                if (RuleCatalog.TryResolveConfigurable(name, out var id))
                {
                    canonical = id;
                }
                else if (RuleCatalog.IsDroppedAlias(name))
                {
                    canonical = name.ToUpperInvariant();
                    dropped = true;
                }
                else
                {
                    AddError(ruleKey, $"Unknown rule {Quote(name)}");
                    continue;
                }

                // A second entry for the same rule is a conflict
                if (seen.TryGetValue(canonical, out var first))
                {
                    AddError(ruleKey, $"Rule {Quote(name)} is already configured as {Quote(first.Name)} on line {first.Line}");
                    continue;
                }

                seen.Add(canonical, (ruleKey.Line, name));
                if (TryBool("rules." + name, ruleKey, ruleValue, out var enabled) && !dropped)
                {
                    rules[canonical] = enabled;
                }
            }

            _config = _config with { Rules = rules.ToImmutable() };
        }

        /// <summary>
        ///     Applies the ignore patterns.
        /// </summary>
        /// <param name="key">The key node.</param>
        /// <param name="value">The value node.</param>
        private void ApplyIgnore(Node key, Node value)
        {
            // Null is an error (a half-edited file must not silently revert to the defaults); an empty list is valid
            if (value is ScalarNode nullable && IsNull(nullable))
            {
                if (!nullable.Reported)
                {
                    AddError(key, "Setting 'ignore' must not be empty");
                }

                return;
            }

            if (value is not SequenceNode list)
            {
                if (!value.Reported)
                {
                    AddError(value, "Setting 'ignore' must be a list of patterns");
                }

                return;
            }

            var patterns = new List<string>();
            var failed = false;
            foreach (var item in list.Items)
            {
                if (item.Reported)
                {
                    failed = true;
                    continue;
                }

                if (!TryPattern(item, out var pattern))
                {
                    failed = true;
                    continue;
                }

                // Exact duplicates are dropped, keeping the first; order is preserved
                if (!patterns.Contains(pattern, StringComparer.Ordinal))
                {
                    patterns.Add(pattern);
                }
            }

            if (!failed)
            {
                _config = _config with { Ignore = [.. patterns] };
            }
        }

        /// <summary>
        ///     Validates one ignore pattern and normalizes its separators.
        /// </summary>
        /// <param name="item">The list item.</param>
        /// <param name="pattern">Receives the pattern with forward slashes.</param>
        /// <returns><see langword="true"/> when the item is a valid pattern; otherwise an error was reported.</returns>
        private bool TryPattern(Node item, out string pattern)
        {
            pattern = string.Empty;
            if (item is not ScalarNode scalar)
            {
                AddError(item, "Ignore patterns must be text");
                return false;
            }

            if (IsNull(scalar) || string.IsNullOrWhiteSpace(scalar.Value))
            {
                AddError(item, "Ignore patterns must not be empty or blank");
                return false;
            }

            if (scalar.Value.AsSpan().IndexOfAny(LintMarkConfig.LineBreakCharacters) >= 0)
            {
                AddError(item, "Ignore patterns must be on a single line");
                return false;
            }

            pattern = scalar.Value.Replace('\\', '/');
            return true;
        }

        /// <summary>
        ///     Gets the name of an entry key and checks it for duplicates.
        /// </summary>
        /// <param name="key">The key node.</param>
        /// <param name="seen">The names seen so far in this mapping and the line of their first occurrence; updated.</param>
        /// <param name="scope">Prefix for the name in messages (empty at the top level).</param>
        /// <param name="name">Receives the key text.</param>
        /// <returns><see langword="true"/> when the key is a first occurrence of a scalar key; otherwise an error was reported or the key was skipped.</returns>
        private bool TryName(Node key, Dictionary<string, int> seen, string scope, out string name)
        {
            if (!TryScalarKey(key, out name))
            {
                return false;
            }

            if (seen.TryGetValue(name, out var firstLine))
            {
                AddError(key, $"Duplicate setting {Quote(scope + name)} (first defined on line {firstLine})");
                return false;
            }

            seen.Add(name, key.Line);
            return true;
        }

        /// <summary>
        ///     Gets the text of a scalar key.
        /// </summary>
        /// <param name="key">The key node.</param>
        /// <param name="name">Receives the key text.</param>
        /// <returns><see langword="true"/> for a scalar key; <see langword="false"/> (with an error unless already reported) otherwise.</returns>
        private bool TryScalarKey(Node key, out string name)
        {
            name = string.Empty;
            if (key is ScalarNode scalar && !scalar.Reported)
            {
                name = scalar.Value;
                return true;
            }

            if (!key.Reported)
            {
                AddError(key, "Complex keys are not supported");
            }

            return false;
        }

        /// <summary>
        ///     Expects a mapping value.
        /// </summary>
        /// <param name="setting">The setting name for messages.</param>
        /// <param name="key">The key node.</param>
        /// <param name="value">The value node.</param>
        /// <param name="expectation">What the setting must be, for the message.</param>
        /// <param name="map">Receives the mapping.</param>
        /// <returns><see langword="true"/> for a mapping; otherwise an error was reported.</returns>
        private bool TryMapping(string setting, Node key, Node value, string expectation, out MapNode map)
        {
            map = null!;
            switch (value)
            {
                case MapNode found:
                    map = found;
                    return true;
                case ScalarNode scalar when IsNull(scalar):
                    // A decorated null (an anchor or tag on an empty value) was already reported once
                    if (!scalar.Reported)
                    {
                        AddError(key, $"Setting '{setting}' must not be empty");
                    }

                    return false;
                default:
                    if (!value.Reported)
                    {
                        AddError(value, $"Setting '{setting}' must be {expectation}");
                    }

                    return false;
            }
        }

        /// <summary>
        ///     Expects a single non-null value.
        /// </summary>
        /// <param name="setting">The setting name for messages.</param>
        /// <param name="key">The key node.</param>
        /// <param name="value">The value node.</param>
        /// <param name="scalar">Receives the scalar.</param>
        /// <returns><see langword="true"/> for a non-null scalar; otherwise an error was reported (or the node was already reported).</returns>
        private bool TryScalar(string setting, Node key, Node value, out ScalarNode scalar)
        {
            scalar = null!;
            if (value.Reported)
            {
                return false;
            }

            if (value is not ScalarNode found)
            {
                AddError(value, $"Setting '{setting}' must be a single value");
                return false;
            }

            if (IsNull(found))
            {
                AddError(key, $"Setting '{setting}' must not be empty");
                return false;
            }

            scalar = found;
            return true;
        }

        /// <summary>
        ///     Reads a value of an enumeration setting; the YAML tokens are the lower-case member names.
        /// </summary>
        /// <typeparam name="T">The enumeration type.</typeparam>
        /// <param name="setting">The setting name for messages.</param>
        /// <param name="key">The key node.</param>
        /// <param name="value">The value node.</param>
        /// <param name="result">Receives the member.</param>
        /// <returns><see langword="true"/> when the value is one of the tokens; otherwise an error was reported.</returns>
        private bool TryEnum<T>(string setting, Node key, Node value, out T result)
            where T : struct, Enum
        {
            result = default;
            if (!TryScalar(setting, key, value, out var scalar))
            {
                return false;
            }

            // Tokens are case-sensitive on purpose: one spelling per canonical form
            var members = Enum.GetValues<T>();
            var found = members
                .Select(static m => (Member: m, Token: m.ToString().ToLowerInvariant()))
                .Where(m => string.Equals(m.Token, scalar.Value, StringComparison.Ordinal))
                .Select(static m => (T?)m.Member)
                .FirstOrDefault();
            if (found.HasValue)
            {
                result = found.Value;
                return true;
            }
            var allowed = string.Join(", ", members.Select(static m => m.ToString().ToLowerInvariant()));
            AddError(value, $"Invalid value {Quote(scalar.Value)} for setting '{setting}'; expected one of: {allowed}");
            return false;
        }

        /// <summary>
        ///     Reads a boolean value; only plain <c>true</c> and <c>false</c> are accepted.
        /// </summary>
        /// <param name="setting">The setting name for messages.</param>
        /// <param name="key">The key node.</param>
        /// <param name="value">The value node.</param>
        /// <param name="result">Receives the value.</param>
        /// <returns><see langword="true"/> when the value is a boolean; otherwise an error was reported.</returns>
        private bool TryBool(string setting, Node key, Node value, out bool result)
        {
            result = false;
            if (!TryScalar(setting, key, value, out var scalar))
            {
                return false;
            }

            if (scalar.Style == ScalarStyle.Plain && scalar.Value is "true" or "false")
            {
                result = scalar.Value == "true";
                return true;
            }

            AddError(value, $"Invalid value {Quote(scalar.Value)} for setting '{setting}'; expected true or false without quotes");
            return false;
        }

        /// <summary>
        ///     Reads a whole number within a range; only plain decimal digits are accepted.
        /// </summary>
        /// <param name="setting">The setting name for messages.</param>
        /// <param name="key">The key node.</param>
        /// <param name="value">The value node.</param>
        /// <param name="minimum">The smallest accepted value.</param>
        /// <param name="maximum">The largest accepted value.</param>
        /// <param name="result">Receives the number.</param>
        /// <returns><see langword="true"/> when the value is a whole number in range; otherwise an error was reported.</returns>
        private bool TryInt(string setting, Node key, Node value, int minimum, int maximum, out int result)
        {
            result = 0;
            if (!TryScalar(setting, key, value, out var scalar))
            {
                return false;
            }

            // Plain, optionally negative decimal without leading zeros or a plus sign; a number outside the int range fails TryParse
            var text = scalar.Value;
            var digits = text.StartsWith('-') ? text.AsSpan(1) : text.AsSpan();
            var wellFormed = scalar.Style == ScalarStyle.Plain
                             && digits.Length > 0
                             && digits.IndexOfAnyExceptInRange('0', '9') < 0
                             && (digits.Length == 1 || digits[0] != '0');
            if (wellFormed
                && int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number)
                && number >= minimum
                && number <= maximum)
            {
                result = number;
                return true;
            }

            AddError(value, $"Invalid value {Quote(text)} for setting '{setting}'; expected a whole number without quotes from {minimum} to {maximum}");
            return false;
        }

        /// <summary>
        ///     Shortens and flattens a value for use in a message, with quotes.
        /// </summary>
        /// <param name="value">The text.</param>
        /// <returns>The quoted text.</returns>
        private static string Quote(string value)
        {
            var flat = value.ReplaceLineEndings(" ");
            return flat.Length > MaxQuotedLength ? $"'{flat[..MaxQuotedLength]}...'" : $"'{flat}'";
        }

        /// <summary>
        ///     Adds an error at the start of a node.
        /// </summary>
        /// <param name="node">The node the error is reported at.</param>
        /// <param name="message">The message.</param>
        private void AddError(Node node, string message) => _errors.Add(new ConfigError(_path, node.Line, node.Column, message));
    }
}
