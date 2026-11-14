#### BlockSyntaxGuard

![Inline Structure](InlineView.svg)

##### Purpose

`BlockSyntaxGuard` decides which text must never start a line of a wrapped paragraph, because Markdown or Pandoc would
read the line as the start of another block. A wrap replaces the spaces between the words of a paragraph with line
breaks; a break in the wrong place turns the next word into a list marker, a heading, a quote, a fence, a table row, a
definition, an HTML block, or a Pandoc construct. The guard answers the question for one word at a time and also
recognizes the lines that mark a whole paragraph as Pandoc structure, which is then left as written. It never changes
text: escaping a word would change the document, so a wrap that needs it keeps the word on the previous line instead.

##### Data Model

`BlockSyntaxGuard` is an internal static partial class without mutable state. Its patterns are generated regular
expressions with a match timeout of one second; a pattern that times out counts as a match (the safe answer).

**Words that must not start a line**: `IsGuardedLineStart` tests the first characters of an *atom* (a word, or a longer
unit such as a link or a code span; only its start counts). The test is deliberately wider than what the parser accepts,
because it is applied to the word and never to its context. It can keep a harmless word off the start of a line, never
let a harmful one through. The guarded words are:

| Reading at a line start | Guarded word |
| --- | --- |
| Bullet marker, thematic break, setext underline | `+`, or a word made only of `-`, `*`, `_`, `=` (`-`, `***`, `===`) |
| Ordered list marker | one to nine digits and `.` or `)`, as the whole word (`1.`, `1)`, `2026.`) |
| ATX heading | one to six `#` as the whole word |
| Block quote | any word that starts with `>` |
| Table row | any word that starts with `\|` |
| Table delimiter row | a word of only `-`, `:`, and `\|` that holds at least one `-` |
| Fenced code | a word that starts with three backticks or three tildes |
| Link or footnote definition | a word that starts with `[label]:` or `[^label]:` |
| Block-level HTML | a word that starts with a block-level tag, `<pre`, `<script`, `<style`, `<textarea`, `<!--`, `<?` |
| Block-level HTML, continued | a word that starts with `<![CDATA[` or with `<!` and a letter (a declaration) |
| Pandoc fenced div | a word that starts with `:::` |
| Pandoc definition list | `:` or `~` as the whole word |
| Pandoc LaTeX command | a word that starts with a backslash and a letter |
| Pandoc display math | a word that starts with `$$` |
| Pandoc raw attribute | a word that starts with `{=` |
| Pandoc title block | `%` as the whole word |
| Pandoc grid table border | a word of the form `+`, dashes, equal signs, colons, and plus signs, as the whole word |

The block-level tag list is the CommonMark list of HTML block type 6 (`div`, `p`, `table`, `ul`, `li`, headings `h1` to
`h6`, and the others), opening or closing, followed by a blank, `/`, `>`, or the end of the word; the raw text elements
and the other types match by their start. Words that look similar but are not block syntax are not guarded: `1.5`,
`1)x`, `-foo`, `*foo*`, `#hashtag`, `a>b`, `100%`, and inline HTML such as `<span>` or `<a href>`.

**Pandoc structural lines**: `IsPandocStructuralLine` tests one content line of a paragraph, without its container
prefix and ignoring leading blanks. A line is structural when it starts with `:::`, a backslash and a letter, `$$`,
`{=`, a percent sign followed by a blank or the end of the line, or when it is a grid table border (a plus sign, dashes,
equal signs, colons, plus signs, and blanks, ending in a plus sign). A paragraph with such a line is exempt from
wrapping.

**Invariants**: the guard has no state and the answers depend on the argument only.

##### Key Methods

**IsGuardedLineStart**: Tells whether a word must not start a line.

- *Parameters*: `string atom`, the first word of the line that would be started; a longer atom is tested by its first
  characters.
- *Returns*: `true` when the word is in the table above, `false` otherwise.
- *Preconditions*: the atom is neither null nor empty.
- *Errors*: `ArgumentNullException` for null and `ArgumentException` for an empty atom. A regular expression timeout is
  not an error: the answer is `true`.

Algorithm: the quick tests on the first characters (`>`, `|`, three backticks, three tildes) decide most words; the
remaining patterns (bullet or rule, number marker, heading marker, definition, block-level HTML, table delimiter, Pandoc
word) are tried in turn, and the first match gives `true`.

**IsPandocStructuralLine**: Tells whether a content line marks its paragraph as Pandoc structure.

- *Parameters*: `string line`, the content of one line without container prefix.
- *Returns*: `true` for the lines described above, `false` otherwise.
- *Errors*: `ArgumentNullException` for null. A timeout gives `true`: an undecided line leaves its paragraph alone.

##### Error Handling

`ArgumentNullException` and `ArgumentException` for invalid arguments. The only runtime failure is a regular expression
timeout, which is caught inside each method and turned into the answer that keeps the text unchanged.

##### Dependencies

- **.NET BCL** - generated regular expressions with a match timeout. No other tool unit is used.

##### Callers

- **LineWrapper** - asks about the first atom of every line it starts.
- **ParagraphFormatter** (`Blocks` subsystem) - asks about the first atom of each segment after a hard break, and about
  every content line of a paragraph for Pandoc structure.
