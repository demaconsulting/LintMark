### StructuralTokenChecker

![Safety Structure](SafetyView.svg)

#### Purpose

`StructuralTokenChecker` decides whether a rewrite kept the Pandoc structure of a document. It is the second safety
check and sees damage that comparing rendered HTML cannot: the Markdown parser knows nothing of Pandoc's fenced divs,
raw LaTeX commands, display math fences, attributes, and citations and renders them as ordinary text, so a rewrite
that joins two `:::` fence lines into prose renders the same HTML and would pass the HTML check.

#### Data Model

`StructuralTokenChecker` is a static partial class with no mutable state.

**Structural line tokens** (compared as a *sequence*, in order). A line is first stripped: leading spaces, tabs, and
block quote markers (`>`) are removed, and so are trailing spaces and tabs. A stripped line is a token when it

- starts with `:::` (a fenced div line);
- starts with a backslash followed by an ASCII letter (a raw LaTeX command such as a page break command);
- is exactly `$$` (a display math fence);
- starts with a code fence with braces, three backticks followed by `{`;
- is a grid table border line: it starts with `+`, has at least two characters, and matches `^\+[-=:+ ]*\+$`.

**Attribute groups** (compared as a *multiset*): text that matches `\{[#.][^{}\n]*\}`, for example `{#id}` or
`{.class key=value}`, counted by exact text. Only groups that start with `#` or `.` are recognized. Other forms, such
as `{width=50%}` or `{-}`, are not compared.

**Citations** (compared as a *multiset*): text that matches `\[-?@[^\]\n]+\]`, for example `[@key]` or
`[-@key, p. 3]`, counted by exact text. Only bracketed citations are recognized. Narrative citations such as `@key`
outside brackets are not compared.

All three regular expressions are generated with a match timeout of 5000 milliseconds.

A difference in any of the three comparisons is a failure. The check is a backstop, not a substitute for careful
formatters: it is deliberately strict, so it can refuse a harmless rewrite but never accept a harmful one that
changes the recognized tokens. Tokens of the forms that are not recognized, and the nesting of a line inside a block
quote or an indented code block, are not seen by this check. Indentation and block quote prefixes do not matter, which
is why a list that is re-indented around a fence still passes, and also why a fence line that moves into or out of a
block quote, or becomes indented code, is not detected here; the HTML check is the safeguard for those changes.

#### Key Methods

**Check**: Checks that two versions of a document have the same Pandoc structural tokens.

- *Parameters*: `string beforeLf`; `string afterLf` (LF-normalized).
- *Returns*: `EquivalenceResult`.
- *Preconditions*: neither text is null and neither contains a carriage return.
- *Postconditions*: the result is equivalent exactly when the token sequences and both occurrence counts are equal, or
  the texts are identical.

Algorithm: (1) validate; (2) identical texts are equivalent; (3) collect the line tokens of both texts and compare the
sequences position by position, reporting the first position where they differ, or the first extra token at the end of
the longer sequence (`Pandoc structural line N differs: before "…" after "…"`, with `(none)` for a missing token);
(4) when the sequences are equal, count the attribute groups of both texts by exact text and visit the union of the
keys in ordinal order, reporting the first group whose count differs (`Attribute group "…" occurs N time(s) before and
M after`); (5) do the same for citations; (6) otherwise equivalent. The group or citation reported is the first in
ordinal order of its text, not the first in document order. Tokens inside the descriptions are shortened and escaped
with the shared excerpt helper.

**CollectLineTokens** (private): Strips each line and returns, in order, those that are structural tokens.
**IsStructural** (private): Decides whether a stripped line is a token. **CompareSequences** (private): Compares two
token sequences and describes the first difference. **CompareOccurrences** (private): Compares the occurrence counts of
the matches of one pattern, visiting the union of the keys in ordinal order. **Count** (private): Counts the matches of
a pattern by matched text.

**Check** (internal overloads): The same check with the function that collects the line tokens of a text as a third
parameter (`Func<string, List<string>>`), and with the function that counts the matches of a pattern as a fourth
(`Func<Regex, string, Dictionary<string, int>>`). The public overload passes the real functions; tests pass ones that
throw, to prove that a comparison that cannot be completed is reported as a difference, on the line token path and on
the occurrence count path. The arguments are validated in these overloads, and a null function throws
`ArgumentNullException`.

#### Error Handling

`ArgumentNullException` for a null text and `ArgumentException` for a text with a carriage return. Everything else
fails closed: the comparison runs inside one `try` block and any exception, including a regular expression timeout, is
returned as a result that is not equivalent with the description `The Pandoc structure could not be compared
(ExceptionType)`, which refuses the rewrite.

#### Dependencies

- **EquivalenceResult** (`Safety` subsystem, a unit) — the verdict and its description helpers.
- **.NET BCL** — generated regular expressions, collections, and string handling.

#### Callers

- **LintMarkEngine** — runs the check second, on the original and the proposed LF-normalized text of a file whose text
  would change.
