#### InlineNormalizer

![Inline Structure](InlineView.svg)

##### Purpose

`InlineNormalizer` writes the emphasis, strong emphasis, bare URLs and code spans of a paragraph in the canonical form
and proposes the edits that do it. It enforces four rules: `emphasis` (`_x_` and `*x*` are the same emphasis; the
`emphasis` setting decides which is written), `strong` (the same for `__x__` and `**x**`, setting `strong`),
`bare-urls` (a bare `http://`, `https://` or `ftp://` URL becomes `<url>`; the setting value `leave` switches the rule
off) and `code-span-space` (a code span padded with one space on each side loses both spaces; the rule has no setting).

##### Data Model

`InlineNormalizer` is an internal sealed class without state. Its property `RuleIds` is an immutable array of the four
rule identifiers from `RuleCatalog`.

It is not a block formatter: it claims no lines, so the whitespace rules keep the lines of the paragraph. Its edits
(`TextEdit`) replace characters inside the text (a delimiter run, the first and the last character of a URL, which are
replaced by themselves plus an angle bracket) or delete one character (the padding of a code span). They never touch
the whitespace at the end of a line and never insert at a position, so they cannot conflict with those rules.

**InlineCandidates** (internal static class in the same file) holds the walk over the inline tree. Constants: `Schemes`
(`http://`, `https://`, `ftp://`), and the character sets `LiteralDelimiters` (`*` and `_`) and `LiteralAngle` (`<`).

##### Key Methods

**AddBlockEdits**: Adds the edits of one paragraph to an `EditSet`.

- *Parameters*: `ParsedDocument document`, `BlockInfo block`, `LintMarkConfig config`, `EditSet edits`.
- *Preconditions*: `block` is a block of `document`; a block that is not a paragraph with inline content adds nothing.
- *Postconditions*: each edit is attributed to the rule it enforces. A rule that is suppressed anywhere in the paragraph
  is not applied to it, and when none is left nothing is added.

The paragraph is left alone when the document defines link references and the paragraph holds a square bracket,
because a rewritten label could stop matching its definition. Otherwise the candidates come from `InlineCandidates`.

**InlineCandidates.Find**: Finds the edits for a paragraph, ordered by start offset and without overlaps.

1. When a literal `<` that the parser read as text exists anywhere in the paragraph, return no edit: a bracket written
   by a rewrite could close it and turn the text after it into an HTML construct.
2. Record whether any literal holds `*` or `_` (an unpaired or escaped delimiter changes how the others pair).
3. Walk the tree (`Visit`). Equal edits proposed twice stand once; edits that overlap without being equal mean the tree
   cannot be trusted, so none stands.

Candidate rules (everything the walk does not recognize is left as written):

- *Emphasis* (`AddEmphasis`): exactly one (`emphasis`) or two (`strong`) delimiters; the source starts and ends with
  exactly them; no other emphasis inside or around, no literal delimiter in the paragraph, no delimiter character in
  the content or next to the node; the neighbors are plain text, a line break, a code span or a link; a target of `_`
  needs a non-word character on both sides.
- *Bare URL* (`AddUrl`): an autolink outside the text of another link whose source has a supported scheme and none of
  blank, `<`, `>`, `|`, `{`, `}`; it stands after a blank, the start, or `(`, and before a blank, the end, or closing
  punctuation; it is not the destination after `](`. `www.` addresses, e-mail addresses, and `mailto:` links stay.
- *Code span* (`AddCodeSpan`): one line, exactly one space on each side with something else next to it, no backtick
  at the edges of the content, no tab; two edits delete the spaces. Padding of other widths stays.

Headings and table cells are never visited, because only paragraph blocks are given to the normalizer.

##### Error Handling

`ArgumentNullException` is thrown for a null argument. A situation where a rewrite would not be safe is not an error:
no edit is added. `EditSet.Add` throws `InvalidOperationException` when an edit conflicts with another one; that is a
defect, not a property of a document. Nothing is caught.

##### Dependencies

- **EditSet** and **TextEdit** (`Formatting` subsystem) - the edits.
- **ParsedDocument**, **BlockInfo** (`Document` subsystem) - the text, the references, and the suppressions.
- **LintMarkConfig**, **EmphasisMarker**, **BareUrlStyle** (`Configuration` subsystem) - the settings.
- **RuleCatalog** (`Linting` subsystem) - the rule identifiers.
- **Markdig** (OTS) - the inline syntax tree.

##### Callers

- **DocumentFormatter** - asks for the edits of each paragraph that no block formatter claims and that is not
  protected, while it collects the leaf edits.
- **LintMarkEngine** (`Engine` subsystem) - creates the instance that `DocumentFormatter` is given.
