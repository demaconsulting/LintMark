---
title: Paragraph wrapping
summary: The line-length and wrap settings, and the line-length and paragraph-wrap rules.
aliases:
  - wrap
  - line-length
  - paragraph-wrap
  - sentence
  - semantic
tags:
  - reference
order: 5
see-also:
  - formatting
  - configuration
  - suppression
examples:
  - name: narrow
    kind: source
    language: yaml
    save-as: narrow.yaml
    description: A configuration with a line length of 40.
    content: 'line-length: 40'
  - name: sentences
    kind: source
    language: yaml
    save-as: sentences.yaml
    description: A configuration with one sentence per line.
    content: 'wrap: sentence'
  - name: para
    kind: source
    language: markdown
    save-as: para.markdown
    description: A paragraph of two sentences on one line.
    content: |
      LintMark rewraps a paragraph when a line is too wide. It keeps every word in order.
  - name: fill
    kind: command
    language: shell
    description: The line is wider than 40 columns. Exits 1.
    content: '{{tool}} --config narrow.yaml para.markdown'
    exit: 1
    expect: 'para.markdown:1:41: line-length Line is 83 columns wide; the limit is 40'
  - name: sentence
    kind: command
    language: shell
    description: The sentences share a line. Exits 1.
    content: '{{tool}} --config sentences.yaml para.markdown'
    exit: 1
    expect: 'para.markdown:1:1: paragraph-wrap'
  - name: fix
    kind: command
    language: shell
    description: Wrap the paragraph to 40 columns.
    content: '{{tool}} --config narrow.yaml --fix para.markdown'
---
Paragraphs are the only text that {{tool}} wraps. Two settings control
it: `line-length`, the width in display columns (default 120; 0 is
unlimited), and `wrap`, the method (default `fill`).

- **fill** rewraps only a paragraph that has a line wider than the
  limit, filling each line as far as it goes. A paragraph whose lines
  all fit is left exactly as written, even when its lines are short.
- **sentence** starts every sentence on a new line and fills a sentence
  that is wider than the limit.
- **semantic** starts every sentence on a new line and breaks a sentence
  that is wider than the limit at the latest clause break (a comma,
  semicolon, colon, or dash) that fits.

**Rules.** `line-length` (`MD013`) reports the first line of a paragraph
that is too wide and could be broken, as [[#fill]] shows. `paragraph-wrap`
reports a paragraph that is not laid out as `sentence` or `semantic`
asks, as [[#sentence]] shows; in the `fill` mode it reports nothing. Both
are formatting rules, so `--fix` repairs what they report ([[#fix]]), and
both can be switched off in `rules` or with `--disable`.

**Not wrapped.** Code, HTML, math, tables, headings, link reference
definitions, and front matter keep their lines, and so does a line with
no place to break, such as a single long link or word. Images, code
spans, and URLs are never split, and the text of an inline link only
with the setting `link-refactor.break-link-text: true`.

**Reported but not fixed.** When a paragraph cannot be wrapped safely,
for example a list item with a lazy continuation line, the message ends
with `and the paragraph cannot be wrapped automatically` and the
paragraph stays as written.
