## EditorConfig Verification

This document provides the verification evidence for the EditorConfig OTS software item (the `editorconfig`
package). Requirements for this OTS item are defined in the EditorConfig OTS Software Requirements document.

### Required Functionality

EditorConfig finds and evaluates `.editorconfig` files. LintMark depends on it to learn, through the raw text of
the `end_of_line` property, which line ending the editor settings give for a Markdown file: in this phase it relies
on the library to expose the raw property text (including `unset`), to ignore malformed lines, and to be used from
many threads. It also relies on it for the layering of files and sections, for the section patterns, and for
resolving a directory chain once and evaluating many files against it; those capabilities are pinned by tests and
required once the formatting phase uses the line ending.

### Verification Approach

EditorConfig has no self-validation mode. It is verified by integration tests in the dedicated OTS test project
`test/OtsSoftwareTests` (`EditorConfigTests.cs`), which reference the editorconfig package directly and not the
LintMark project. Each test creates a real temporary directory tree whose root `.editorconfig` declares
`root = true`, so the upward search never reaches a file outside the tree, resolves the raw `end_of_line` property
of a file, and removes the tree when it ends. The scenarios that record known deviations of the library pin the
behavior that the `EditorConfigResolver` unit compensates for, so that a library update that changes it is noticed:
the typed line-ending accessor still reports a farther file's value when a nearer file sets `unset` or an unknown
word (which is why LintMark interprets the raw text itself), and section patterns are matched with letter case
significant on every platform. The behavior of LintMark's own interpretation of the raw text (for example `cr` as
"not specified") is verified in the `EditorConfigResolver` unit tests.

The scenarios for section patterns, letter case, layering, the root file, chain evaluation, the typed accessor, and
files that do not exist describe pinned library behavior and are not linked to a requirement in this phase,
because the line ending that they lead to is not observable until files are rewritten. The scenarios for the raw
`unset` property, malformed lines, and concurrent use are required.

### Test Scenarios

#### Section patterns match as the specification says

**Scenario**: A root file with one section whose pattern is one of 22 cases is written, and a file is resolved. The
cases cover `*.md` at any depth, `?`, character sets and their negation, alternatives, numeric ranges, folder
patterns, a leading slash, `**` in a folder pattern, and an escaped bracket.

**Expected**: The raw `end_of_line` property is present exactly when the pattern is specified to match the file.

#### Section patterns are case-sensitive on every platform

**Scenario**: Sections `[*.md]` and `[*.TXT]` are written and files whose names differ from the patterns only by
letter case are resolved.

**Expected**: `A.MD` and `a.txt` get no value, and `a.TXT` gets `crlf`.

#### Property names and values are case-insensitive

**Scenario**: The line `END_OF_LINE = Crlf` is written.

**Expected**: The property is found under its lower-case name and its value is reported in lower case.

#### A later section overrides an earlier one

**Scenario**: A `[*.md]` section with `lf` is followed by a `[*]` section with `crlf`.

**Expected**: A Markdown file gets `crlf`, although the earlier section is more specific.

#### A nearer file overrides a farther file property by property

**Scenario**: A parent file sets `end_of_line` and `indent_style`, and a nearer file sets only `end_of_line`.

**Expected**: The nearer value wins for `end_of_line` and the other property is inherited.

#### A file that declares itself the root ends the search

**Scenario**: A farther file sets `crlf`, and a nearer file declares `root = true` and sets another property.

**Expected**: The `end_of_line` property is absent and the nearer file's property is present.

#### A previously resolved chain gives the same result as a direct parse

**Scenario**: The chain of settings files of one directory is resolved once, and three files are evaluated against it
and directly.

**Expected**: The property sets are identical for every file.

#### The raw property of an unset value is the word unset

**Scenario**: A nearer file sets `end_of_line = unset` over a farther `crlf`.

**Expected**: The raw property is `unset`, so a caller can recognize it.

#### The typed line-ending accessor ignores unset and unknown values

**Scenario**: A farther file sets `crlf`; nearer files set `unset` and `banana`.

**Expected**: The raw properties hold the nearer words, while the typed accessor still reports `crlf` for both,
which is why LintMark does not use it.

#### A carriage-return-only value is available in both views

**Scenario**: A section sets `end_of_line = cr`.

**Expected**: The typed accessor reports `cr` and the raw property is `cr`.

#### A file that does not exist is still resolved

**Scenario**: A path in folders that do not exist is resolved against a catch-all section.

**Expected**: The property of the section is reported.

#### Malformed lines are ignored

**Scenario**: A file with `[[[`, a bare property name, a bare equals sign, and a line of text before a valid
`[*.md]` section is resolved.

**Expected**: No exception is thrown and the valid section applies.

#### One shared parser gives identical results on many threads

**Scenario**: Files in two folders under settings files with different sections are resolved 20 times each from 16
threads through one shared parser instance.

**Expected**: Every result equals the sequential result.

#### A parser per thread gives identical results on many threads

**Scenario**: The same kind of tree is resolved from 16 threads, each call with its own parser.

**Expected**: Every result equals the sequential result.
