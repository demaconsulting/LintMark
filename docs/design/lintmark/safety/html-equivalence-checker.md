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

**Normalization rules**: The HTML is normalized so that only differences that can change the rendered page remain.
The text is cut into tags and the text between them. A tag is a construct that starts with `<` and a letter, `</` and
a letter, `<!`, or `<?`; any other `<` is text. A comment ends at `-->`, a processing instruction at `?>`, and any
other tag at the first `>` that is not inside a quoted attribute value (quotes are `"` and `'`), so a value may
contain `>`. The rules are:

1. **Tags are copied exactly.** Attribute names, attribute values (including `id`, `href`, `title`, `alt`, `class`, and
   `style`), quotes, the case of tag names, and the spacing inside a tag are compared as rendered. In particular the
   `id` of a heading is not dropped: the pipeline never generates heading identifiers (no automatic identifiers, no
   generic attributes), so an identifier in the HTML was written by the author as raw HTML, and changing it changes
   where a fragment link leads. A comment is copied exactly, as a tag is.
2. Whitespace in the text between tags is collapsed: every run of HTML whitespace (space, tab, line feed, carriage
   return, form feed) becomes one space. Only these characters are collapsed, so a changed no-break space is a
   difference, and character references (`&amp;`, `&#160;`) are not interpreted or compared by value.
3. A space between a block-level tag and text is removed (the space that follows a block-level tag and the space that
   precedes one). The block-level tags are `p`, `li`, `ul`, `ol`, `h1` to `h6`, `blockquote`, `table`, `thead`,
   `tbody`, `tr`, `td`, `th`, `pre`, `hr`, `div`, `section`, `details`, `summary`, `dl`, `dt`, and `dd`, opening or
   closing, whose names are recognized ignoring case. A name that only starts like one of these (`param`, `p-x`) is not
   block-level. Whitespace between inline tags is never touched, otherwise `*a* *b*` becoming `*a**b*` would go
   unnoticed. The spaces removed are outside the tag, never inside it.
4. The content of `pre`, `script`, `style`, and `textarea` elements, from the opening tag to the closing tag, is
   compared exactly. The names are recognized ignoring case.
5. The content of inline `code` is compared exactly, except that a line feed counts as a space (the CommonMark rule
   for code spans).
6. A tag that never ends (no `>` outside quotes, a quote or comment that is never closed) is not text, so the rest of
   the part of the text it is in is compared exactly. An opening tag of a verbatim element that never ends makes the
   rest of the whole text verbatim.
7. Leading and trailing spaces of the whole text are removed.

Verbatim parts are bracketed by a marker character (`U+0001`) in the normalized text so they cannot merge with their
surroundings. The only regular expression is the whitespace run; it has a match timeout of 5000 milliseconds, and a
timeout is a failure that refuses the rewrite (see *Error Handling*). Tags are found by a hand-written scanner, not by
patterns, which keeps the scan linear.

**Justification of each normalization**: Every rule that ignores a difference needs a reason why a reader cannot see
it; every other part of the HTML is compared. Each item names the rule and whether it was removed, kept, or is not
applied.

- **Heading `id` dropped — removed.** Only the spike pipeline made identifiers (automatic identifiers with
  context-dependent suffixes). The production pipeline makes none, so an identifier is the author's, and a change
  breaks fragment links.
- **Whitespace collapsed inside tags — removed.** Whitespace in `title`, `alt`, `href`, and `class` values is part of
  the value; collapsing it hid real changes.
- **Whitespace in comments collapsed — removed.** Tools read comments (directives, markers); a comment is not text.
- **Whitespace runs in text collapsed — kept.** HTML shows any run of HTML whitespace as one space, which is what
  makes rewrapping a paragraph harmless.
- **Only HTML whitespace collapsed — kept.** A no-break space and other Unicode spaces are visible, so they are
  compared.
- **Space next to a block-level tag removed — kept.** A browser shows no space at the edge of a block or between
  blocks. Only the space outside the tag is removed. The list of blocks is deliberately short; a block that is not
  listed only keeps its spaces, which is the safe outcome.
- **Tag names recognized ignoring case — kept.** HTML element names are case-insensitive, so `<PRE>` must be treated
  as verbatim. Only the recognition ignores case; the tag text itself is compared exactly.
- **Inline `code` line feed counts as a space — kept.** CommonMark turns a line ending inside a code span into a
  space, and the renderer keeps the line feed for LF text, so a rewrap of a code span would otherwise be refused.
  Nothing else in `code` is normalized.
- **Verbatim `pre`, `script`, `style`, `textarea` — kept exact.** Whitespace in them is shown or executed.
- **Character references — not normalized.** `&amp;` and `&#38;` are compared as written; the renderer writes one
  form, so a different form is a change of the source worth refusing.
- **Leading and trailing spaces of the text — kept.** A browser shows no space at the edge of the page.

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

**CheckOnDisk**: Checks that two versions of a document render to the same page when each keeps the line endings it
has on disk.

- *Parameters*: `string before` (the original text with its real line endings); `string after` (the rewritten text with
  its real line endings).
- *Returns*: `EquivalenceResult`.
- *Preconditions*: neither text is null. Carriage returns are allowed.
- *Postconditions*: as `Check`.

Algorithm: as `Check`, without the carriage return validation: identical texts are equivalent, otherwise both texts are
rendered as they are, normalized, and compared. It exists because the Markdown parser renders some constructs (such as a
code span whose content is framed by line breaks) differently for CRLF and LF text, so converting the line endings can
change the page. A carriage return in the rendered HTML is HTML whitespace and is collapsed like a line feed outside the
verbatim elements. `CheckOnDisk` has an internal overload with a renderer parameter, like `Check`, and shares the
comparison and the fail-closed handling with it.

**Normalize** (private): Applies the rules above with a single left-to-right scan. The text is cut into plain parts and
verbatim elements. For each part, the next opening tag of each element kind is found (a tag counts only when the name
is not the prefix of a longer word) and the earliest wins. The element runs from its opening tag to the first closing
tag after it, ignoring case. An element without a closing tag runs to the end of the text, as a browser reads it, so
everything after it is compared exactly; so does an opening tag that never ends (rule 6). The position of the next
opening tag of each element kind is remembered and searched for again only after the scan has passed it. These rules
keep the scan linear even for input made of thousands of unclosed tags or of very many code spans. Verbatim parts are
kept (with line feeds turned into spaces for inline `code`). Plain parts go through `NormalizePlain`.

**NormalizePlain** (private): Scans a plain part from left to right, alternating between the text up to the next tag
(whitespace runs collapse; the space that follows a block-level tag is dropped) and the tag itself (found by
`FindTagStart` and `FindTagEnd`, copied exactly; the space that precedes a block-level tag, decided by `IsBlockTag`,
is dropped). A tag that never ends copies the rest of the part exactly. Every character is looked at a bounded number
of times, so the scan is linear, also for very many tags with attributes and for very many tags whose quote or
comment never ends.

#### Error Handling

`ArgumentNullException` for a null text and, for `Check`, `ArgumentException` for a text with a carriage return (the
engine passes normalized text to it, so this signals a programming error); `CheckOnDisk` accepts carriage returns.
Everything else fails closed: the render and the
normalization run inside one `try` block, and any exception, including a regular expression timeout, is caught and
returned as a result that is not equivalent with the description `The rendered output could not be compared
(ExceptionType)`. The result then refuses the rewrite.

#### Dependencies

- **Markdig** (OTS) — rendering to HTML; see *Markdig Integration Design*.
- **DocumentParser** (`Document` subsystem) — the shared Markdown pipeline.
- **EquivalenceResult** (`Safety` subsystem, a unit) — the verdict and its description helpers.
- **.NET BCL** — generated regular expressions and string handling.

#### Callers

- **LintMarkEngine** — runs `Check` first, on the original and the proposed LF-normalized text of a file whose text
  would change, and `CheckOnDisk` on the original and the proposed bytes, decoded, of every file whose bytes would
  change, also when only the line endings change.
