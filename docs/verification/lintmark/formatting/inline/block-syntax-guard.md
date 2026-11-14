#### BlockSyntaxGuard

##### Verification Approach

`BlockSyntaxGuard` is verified with unit tests in `BlockSyntaxGuardTests.cs`. Nothing is mocked or replaced by a
double. The guard is pure and stateless: it takes one word (or one line) and returns a yes or no answer, with no file,
parser, or document involved, so every test calls its two methods directly and asserts the answer. The tests do not
parse the words with the Markdown parser; the guard is deliberately wider than the parser, and the scenarios fix the
exact set of words it answers yes for. Most scenarios are theories with one row per word, each with the expected
answer. The test for concurrent use asks about ten words with both answers once on one thread, then asks eight threads
to ask the same words two hundred times each; every answer is compared with the single-threaded answer.

##### Test Environment

N/A - standard test environment.

##### Acceptance Criteria

- All unit tests pass with zero failures.
- A null word is rejected with an argument null exception and an empty word with an argument exception, each naming the
  parameter `atom`; a null line is rejected with an argument null exception naming the parameter `line`.
- The words `-`, `+`, and `*` are guarded as bullet markers.
- A number of one to nine digits followed by a period or a closing parenthesis is guarded as an ordered list marker, a
  year-like number included; a number of ten digits, a number with text after the marker, and a number without a marker
  are not guarded.
- One to six number signs are guarded as a heading marker; seven number signs and words that only contain a number sign
  are not guarded.
- Every word that starts with `>` is guarded as a quote.
- Words that start with three backticks or three tildes are guarded as code fences; code spans and strikethrough are
  not.
- A word that starts with a bracketed label and a colon is guarded as a definition; a link, a reference, a task marker,
  and a footnote reference are not.
- The starts of block-level HTML (tags of the block list, raw text elements, comments, processing instructions, CDATA
  sections, and declarations) are guarded, in either case; inline HTML tags and links in angle brackets are not.
- A run of dashes, asterisks, underscores, or equal signs is guarded as a thematic break or a setext underline.
- A word that starts with a pipe, and a word of only dashes, colons, and pipes with at least one dash, is guarded as a
  table row or delimiter.
- The starts of Pandoc constructs (fenced div, lone colon or tilde, LaTeX command, display math fence, raw attribute,
  lone percent sign, and grid table border) are guarded.
- Ordinary text, emphasis, signed numbers, dollar amounts, escapes, entities, and words in other scripts are not
  guarded.
- A content line that starts a Pandoc construct is recognized as a Pandoc structural line, also when it is indented; an
  escape, a lone backslash, a percent sign inside a word, a bullet item, a construct that is not at the start, and an
  empty line are not.
- Every thread gets the same answer for the same word.

##### Test Scenarios

###### Normal operation

**Bullet markers are guarded**: The words `-`, `+`, and `*` are each guarded.

**Ordered list markers are guarded**: The words `1.`, `1)`, `2026.`, `2026)`, `0.`, and `123456789.` (nine digits) are
each guarded, a number that looks like a year included.

**Numbers that are not markers are not guarded**: The words `1.5`, `1)x`, `2026.5`, `1234567890.` (ten digits), `v1.`,
`1,`, and `1.x` are not guarded.

**Heading markers are guarded only as markers**: The words `#`, `##`, and `######` are guarded; `#######` (seven),
`#hashtag`, and `C#` are not.

**Quote markers are guarded**: The words `>`, `>quote`, and `>=` are each guarded, so every word that starts with a
greater-than sign is.

**Code fences are guarded and code spans are not**: Six words that start with three backticks or three tildes are
guarded: three backticks alone, three backticks followed by `csharp`, four backticks, `~~~`, `~~~~`, and three backticks
followed by `{.python}`. A code span with two backticks on each side, a code span with one backtick on each side (both
around the word `code`), and `~~strike~~` are not guarded.

**Definitions are guarded and links are not**: The words `[label]:`, `[label]:/url`, and `[^note]:` are guarded. The
words `[label]`, `[label](url)`, `[x]` (a task marker), and `[^1]` (a footnote reference) are not.

**Block-level HTML starts are guarded**: The words `<div>`, `<DIV` (upper case), `</div>`, `<p>`, `<table>`, `<h1>`,
`<details>`, `<!--`, `<?php`, `<![CDATA[`, `<!DOCTYPE`, `<pre>`, `<script>`, `<style`, and `<textarea` are each guarded.

**Inline HTML and links in angle brackets are not guarded**: The words `<span>`, `<a`, `<em>`, `<b>`, `<br>`,
`<http://x.org>`, and `<mailto:a@b.com>` are not guarded.

**Thematic breaks and setext underlines are guarded**: The words `---`, `--`, `***`, `___`, `_`, `===`, and `=` are each
guarded.

**Table rows and delimiters are guarded**: The words `|`, `|a|b|`, `---|---`, `:--`, `--:`, `:-:`, and `|---|` are each
guarded.

**Pandoc constructs are guarded**: The words `:::`, `:::note`, `:` (alone), `~` (alone), the page-break command,
`\cite{x}`, `$$`, `$$x`, `{=html}`, `%` (alone), `+---+`, `+===+`, and `+-+` are each guarded.

**Ordinary text is not guarded**: The words `text`, `*emphasis*`, `**bold**`, `_x_`, `-foo`, `+foo`, `+1`, `a-b`, `a.b`,
`...`, `::`, `\` (alone), `\*`, `\1`, `$5`, `100%`, `{x}`, `&amp;`, `日本語`, and `😀` are not guarded.

**Pandoc structural lines are classified**: Lines are tested for whether they start a Pandoc construct. These are
structural: `:::`, `::: {.note}`, the page-break command, `\begin{center}`, `$$`, `{=html}`, `% title`, `%`,
`+---+---+`, and `+===+`, and the page-break command indented by two blanks. These are not: `text`, `\*escaped`, `\`
(alone), `100% sure`, `+ item`, `a ::: b`, and the empty line.

###### Error handling

**A null or empty word is rejected**: Asking about a null word throws an argument null exception that names `atom`;
asking about an empty word throws an argument exception that names `atom`.

**A null line is rejected**: Asking whether a null line is a Pandoc structural line throws an argument null exception
that names `line`.

**Concurrent calls give the same answers**: The ten words `-`, `2026.`, `text`, `<div>`, `<span>`, `|a`, `:::`, `1.5`,
`[x]:`, and `日本` are tested once on one thread. Eight threads then test the same ten words two hundred times each;
every set of answers equals the single-threaded answers.
