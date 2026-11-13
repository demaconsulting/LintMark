## YamlDotNet

This document describes the integration and usage design for the `YamlDotNet` OTS software item.

### Purpose

YamlDotNet is the YAML library used by the `Configuration` subsystem to read `.lintmark.yaml` files. It turns YAML
text into a stream of parsing events with exact source positions, which LintMark needs in order to report every
configuration problem at the line and column where it is written, and to see details that an object-mapping layer
hides: which scalars were quoted, which values are null, whether a node carries an anchor or a tag, and where a
second document starts. It is chosen because it is a widely used, maintained .NET YAML implementation that targets
the platforms LintMark supports and exposes its low-level parser.

### Features Used

- The event-level `Parser` over a `StringReader`, advanced one event at a time (`MoveNext` and `Current`); no
  object mapping, serializer, or deserializer
- The start and end marks of every event, 1-based, used as the positions of keys, values, and syntax errors
- Scalar events with their text and style (plain, quoted, literal, or folded), which the schema uses to accept
  only plain numbers and booleans and to recognize null
- Mapping and sequence start and end events, in block and flow style
- The anchor and tag of an event and alias events, which the schema rejects
- Document start events, used to reject a second document and to treat a stream with no document, or a lone
  document marker, as "no settings"
- `YamlException`, whose start mark gives the position of a syntax error and whose message carries no position
  prefix
- CRLF input, which yields the same line numbers as LF input

Not used: object mapping, serialization, deserialization, schema or type converters, merge keys, and any rendering.

Behaviors of the library that the design depends on and compensates for (each is pinned by the integration tests
in `test/OtsSoftwareTests` unless stated otherwise):

- A leading byte order mark is not stripped by the parser; it would become part of the first key. `ConfigLoader`
  decodes the bytes and removes the mark before parsing.
- Duplicate keys are not detected. `ConfigParser` detects them while validating.
- Some malformed input raises an exception that is not a `YamlException` and carries no position (an unclosed flow
  sequence is one). `ConfigParser` treats any exception from advancing the parser as invalid YAML and reports it at
  the end of the last event that was read successfully.
- Tab-indentation errors carry a coarse position (the start of the enclosing collection).
- Running time grows quadratically with the nesting depth of flow collections. `ConfigParser` rejects input nested
  deeper than a fixed limit before the parser sees it. This rationale is not pinned by the OTS tests: the
  `ConfigLoader` unit tests verify that input beyond the limit is rejected, and the subsystem test verifies that a
  file nested far beyond it is rejected promptly. The OTS tests show only that deeply nested input at a bounded size
  is parsed without exhausting the stack.

### Integration Pattern

YamlDotNet is consumed as a NuGet package reference of the main project, as a runtime dependency of the shipped
tool, and by the OTS integration test project. `ConfigParser` creates one `Parser` over the decoded text for each
parse and reads it to the end on one thread; no parser instance is shared, so concurrent parses of different files
are independent. No initialization, configuration file, or disposal is required. Exceptions from the parser never
escape the configuration subsystem: they become positioned `config-error` problems. Because the library is used
only at event level, a library upgrade can change only the events, marks, and exceptions listed above.

YamlDotNet has no self-validation mode. The evidence for the behaviors LintMark depends on is the integration test
project `test/OtsSoftwareTests`, which exercises the library directly: positions of keys and values in block and
flow style, CRLF handling, scalar styles, empty and comment-only streams, null values, a lone document marker,
anchors, aliases and tags, multiple documents, accepted duplicate keys, positioned and non-positioned syntax
errors, tab indentation, deeply nested flow input, the retained byte order mark, and parallel parsing with
independent parsers.

The retained byte order mark is pinned by a test without a requirement: what LintMark needs, that a UTF-8 marker at
the start of a configuration file is accepted, is a requirement of `ConfigLoader`, and the way the loader achieves
it is a design decision described above.
