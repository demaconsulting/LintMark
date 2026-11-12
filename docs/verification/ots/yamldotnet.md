## YamlDotNet Verification

This document provides the verification evidence for the YamlDotNet OTS software item. Requirements for this OTS
item are defined in the YamlDotNet OTS Software Requirements document.

### Required Functionality

YamlDotNet is the YAML library used by the `Configuration` subsystem to read `.lintmark.yaml` files. LintMark uses
only its event-level parser: it depends on it to report the exact line and column of every key, value, and syntax
error, to tell plain from quoted scalars, to see anchors, aliases, tags, and document starts so that it can reject
them, to deliver repeated keys so that it can detect duplicates itself, and to parse on many threads with
independent parser instances.

### Verification Approach

YamlDotNet has no self-validation mode. It is verified by integration tests in the dedicated OTS test project
`test/OtsSoftwareTests` (`YamlDotNetTests.cs`), which reference the YamlDotNet package directly and not the
LintMark project. Each scenario describes a behavior that LintMark relies on and that the tests confirm, by reading
the parsing events of small YAML texts and asserting on the events, styles, and positions. No files are needed.
The scenarios that record places where the library does less than the loader needs or fails in an unexpected way
pin the behavior that the `ConfigLoader` unit compensates for, so that a library update that changes it is
noticed: a leading byte order mark is not stripped and becomes part of the first key, repeated keys are delivered
instead of being rejected, and an unclosed flow sequence fails with an exception that is not a YAML exception and
carries no position. The behaviors that belong to the loader's own handling (what is reported for each fault, and
the nesting limit that the loader applies before parsing) are verified in the `ConfigLoader` unit tests.

### Test Scenarios

#### Block mapping keys and values carry 1-based positions

**Scenario**: A two-entry block mapping is read event by event.

**Expected**: The scalars are the keys and values in order, their start positions are 1-based (line 1 column 1, line
1 column 14, line 2 column 1, line 2 column 7), and the end position follows the last character.

#### CRLF input is numbered like LF input

**Scenario**: The same three-line document is read with LF and with CRLF line endings.

**Expected**: Every scalar has the same line and column in both.

#### Flow mapping entries carry positions

**Scenario**: A flow mapping on one line is read.

**Expected**: The mapping is reported as flow style, and the scalars start in columns 2, 5, 8, and 11 of line 1.

#### Plain, quoted, literal, and folded scalars are told apart

**Scenario**: A document with one value of each style is read.

**Expected**: The values are reported as plain, double-quoted, single-quoted, literal, and folded.

#### A key without a value and the spellings of null are plain scalars

**Scenario**: A key with no value, a key with `~`, and a key with `null` are read.

**Expected**: The values are the empty text, `~`, and `null`, each a plain scalar, so that the caller can classify
them as null and reject them.

#### Empty and comment-only input has no document

**Scenario**: Empty text, blank lines, and a comment-only text are read.

**Expected**: No document start event appears.

#### A lone document marker is one document with an empty scalar

**Scenario**: The text `---` alone is read.

**Expected**: One document start and one empty plain scalar are reported.

#### Anchors, aliases, and tags are reported

**Scenario**: A document with an anchor, an alias to it, and a tag is read.

**Expected**: The anchor and the tag are visible on their events, and one alias event appears, so that the caller
can reject them.

#### Several documents are reported as several document starts

**Scenario**: Two documents separated by a document marker are read.

**Expected**: Two document starts appear, and the second is on line 2.

#### Repeated keys are delivered, not rejected

**Scenario**: A mapping with the same key twice is read.

**Expected**: All four scalars arrive, and the second occurrence has its own position on line 2, so the caller must
detect the duplicate.

#### Malformed input throws a YAML exception with a position

**Scenario**: A quoted scalar that is never closed, on the second line, is read.

**Expected**: A YAML exception is thrown whose start is on line 2 or later and in a column of at least 1, and whose
message is not empty.

#### An unclosed flow sequence throws an exception that is not a YAML exception

**Scenario**: A flow sequence that is never closed is read.

**Expected**: An `InvalidOperationException` is thrown instead of a YAML exception, so a caller must treat any
exception from the parser as invalid input.

#### Tab indentation throws a YAML exception

**Scenario**: A nested mapping indented with a tab is read.

**Expected**: A YAML exception is thrown and its start has a line of at least 1.

#### Deeply nested flow input parses without exhausting the stack

**Scenario**: A flow sequence nested 3,000 levels deep is read on a thread with a 256 KiB stack, so that any
recursion in the parser would fail.

**Expected**: Reading finishes within the time allowed without an exception, and the deepest level seen is 3,000.
The nesting limit that LintMark applies protects against the running time that grows steeply with depth, not
against a crash.

#### A leading byte order mark becomes part of the first key

**Scenario**: A text that starts with the byte order mark character followed by `line-length: 100` is read.

**Expected**: The first key is the mark followed by `line-length`, which is why the loader removes the mark before
parsing.

#### Independent parsers give identical results on many threads

**Scenario**: The same document is parsed by independent parser instances from 16 threads, 200 times.

**Expected**: Every result, as a list of values with their positions, equals the sequential result.
