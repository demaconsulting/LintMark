### HtmlEquivalenceChecker

![Safety Structure](SafetyView.svg)

#### Purpose

`HtmlEquivalenceChecker` decides whether two versions of a document render to the same HTML. It is the first safety
check before a rewritten file may be written: a rewrite that changes how the page looks is a defect, however
plausible the new text is.

#### Data Model

`HtmlEquivalenceChecker` is a static partial class with no mutable state.

**Rendering**: Both texts are rendered with `Markdown.ToHtml` and the fixed pipeline of `DocumentParser`, the same
pipeline that parses documents, so the check judges the document the way the tool reads it.

**Normalization rules**: The HTML is normalized so that only differences that can change the rendered page remain:

1. The `id` attribute of a heading (`h1` to `h6`) is dropped.
2. Whitespace outside the verbatim elements below is collapsed: every run of HTML whitespace (space, tab, line feed,
   carriage return, form feed) becomes one space. Only these characters are collapsed, so a changed no-break space is a
   difference.
3. A space next to a block-level tag is removed. The block-level tags are `p`, `li`, `ul`, `ol`, `h1` to `h6`,
   `blockquote`, `table`, `thead`, `tbody`, `tr`, `td`, `th`, `pre`, `hr`, `div`, `section`, `details`, `summary`,
   `dl`, `dt`, and `dd`, opening or closing, compared ignoring case. Whitespace between inline tags is never touched,
   otherwise `*a* *b*` becoming `*a**b*` would go unnoticed.
4. The content of `pre`, `script`, `style`, and `textarea` elements, from the opening tag to the closing tag, is
   compared exactly.
5. The content of inline `code` is compared exactly, except that a line feed counts as a space (the CommonMark rule
   for code spans).
6. Leading and trailing spaces of the whole text are removed.

Verbatim parts are bracketed by a marker character (`U+0001`) in the normalized text so they cannot merge with their
surroundings. Element names match ignoring case. The patterns are generated regular expressions for the heading
identifier, a whitespace run, and the block tag with its surrounding spaces; each has a match timeout of 5000
milliseconds.

**Constants**: `RegexTimeoutMilliseconds` (5000), the marker character, and the number of characters of context shown
before the first difference (20).

#### Key Methods

**Check**: Checks that two versions of a document render to the same page.

- *Parameters*: `string beforeLf` (the original LF-normalized text); `string afterLf` (the rewritten LF-normalized
  text).
- *Returns*: `EquivalenceResult`.
- *Preconditions*: neither text is null and neither contains a carriage return.
- *Postconditions*: the result is equivalent exactly when the normalized HTML of both texts is identical, or the texts
  are identical; any failure to decide yields a result that is not equivalent.

Algorithm: (1) validate the arguments; (2) identical texts are equivalent without rendering; (3) render and normalize
both texts; (4) compare the normalized HTML with an ordinal string comparison; (5) when it differs, find the offset
of the first difference and
describe it as `Rendered output differs at offset N: before "…" after "…"` with excerpts that start 20 characters
before the difference.

**Check** (internal overload): The same check with the function that renders a text to HTML as a third parameter
(`Func<string, string>`). The public overload passes the real renderer, the fixed pipeline of `DocumentParser`; tests
pass a renderer that throws, to prove that a comparison that cannot be completed is reported as a difference. The
arguments are validated in this overload, and a null renderer throws `ArgumentNullException`.

**Normalize** (private): Applies the rules above with a single left-to-right scan. The text is cut into plain parts and
verbatim elements. For each part, the next opening tag of each element kind is found (a tag counts only when the name
is not the prefix of a longer word) and the earliest wins. The element runs from its opening tag to the first closing
tag after it, ignoring case. An element without a closing tag runs to the end of the text, as a browser reads it, so
everything after it is compared exactly. An opening tag that never ends is plain text, and once one such tag is seen
no later tag of the same kind is searched for. The position of the next opening tag of each element kind is
remembered and searched for again only after the scan has passed it. These rules keep the scan linear even for input
made of thousands of unclosed tags or of very many code spans. Plain parts go through the whitespace and block tag
patterns; verbatim parts are kept (with line feeds turned into spaces for inline `code`).

#### Error Handling

`ArgumentNullException` for a null text and `ArgumentException` for a text with a carriage return (the engine always
passes normalized text, so this signals a programming error). Everything else fails closed: the render and the
normalization run inside one `try` block, and any exception, including a regular expression timeout, is caught and
returned as a result that is not equivalent with the description `The rendered output could not be compared
(ExceptionType)`. The result then refuses the rewrite.

#### Dependencies

- **Markdig** (OTS) — rendering to HTML; see *Markdig Integration Design*.
- **DocumentParser** (`Document` subsystem) — the shared Markdown pipeline.
- **EquivalenceResult** (`Safety` subsystem, a unit) — the verdict and its description helpers.
- **.NET BCL** — generated regular expressions and string handling.

#### Callers

- **LintMarkEngine** — runs the check first, on the original and the proposed LF-normalized text of a file whose text
  would change.
