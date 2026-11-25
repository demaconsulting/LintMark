---
title: Rules
summary: The kinds of rule, how to name them, and how to switch them on or off.
aliases:
  - lint
  - lint-rules
  - rule-ids
  - enable
  - disable
  - markdownlint
tags:
  - reference
order: 7
see-also:
  - formatting
  - suppression
  - configuration
examples:
  - name: doc
    kind: source
    language: markdown
    save-as: doc.markdown
    description: A heading that skips a level, and an image without text.
    content: |
      # Title

      ### Section

      ![](logo.png)
  - name: check
    kind: command
    language: shell
    description: Report the lint-only findings. Exits 1.
    content: '{{tool}} doc.markdown'
    exit: 1
    expect: |
      doc.markdown:3:1: heading-increment Heading level increases from 1 to 3; expected at most 2
      doc.markdown:5:1: image-alt-text
  - name: disable
    kind: command
    language: shell
    description: Switch both rules off for one run.
    content: '{{tool}} --disable "heading-increment,MD045" doc.markdown'
  - name: always-applied
    kind: command
    language: shell
    description: A formatting rule cannot be switched. Exits 1.
    content: '{{tool}} --enable bullet doc.markdown'
    exit: 1
    expect: "Error: --enable cannot switch rule 'bullet'; it is always applied"
---
{{tool}} has 60 rule identifiers of three kinds:

- 26 formatting rules that always apply; see [[formatting]].
- 27 rules that can be switched: 24 lint-only rules and the formatting
  rules `line-length`, `paragraph-wrap`, and `link-refactor`.
- 7 identifiers that label failures, such as `file-read-error`; see
  [[troubleshooting]].

A lint-only rule only reports, because every repair would change what the
document says. Its message describes the problem, as [[#check]] shows,
and `--fix` leaves it, so the run still exits 1.

**Naming.** A rule is named by its identifier or by its markdownlint
number, such as `MD001`, in any letter case. The numbers of rules that
{{tool}} no longer has (`MD002`, `MD006`, `MD008`, `MD025`, `MD033`,
`MD041`, `MD043`, `MD044`, `MD057`) are accepted and ignored.

**Switching.** The `rules` setting of a configuration maps names to
`true` or `false`. `--enable <ids>` and `--disable <ids>` take a
comma-separated list, can be repeated, and win over `rules` for one run,
as [[#disable]] shows. Naming a rule that always applies, an unknown
rule, or one rule in both options is an error ([[#always-applied]]).
Three rules are off by default: `descriptive-link-text` (`MD059`),
`dollar-sign-in-shell-code` (`MD014`), and `table-grid-unformatted`.

The 24 lint-only rules:

- **Headings:** `heading-increment` (`MD001`), `heading-duplicate`
  (`MD024`), `heading-trailing-punctuation` (`MD026`),
  `emphasis-as-heading` (`MD036`), `heading-missing-space` (`MD018`),
  `heading-closed-missing-space` (`MD020`).
- **Links and images:** `image-alt-text` (`MD045`), `link-empty`
  (`MD042`), `link-fragment` (`MD051`), `link-reference-undefined`
  (`MD052`), `link-definition-unused` (`MD053`), `link-reversed`
  (`MD011`), `link-space` (`MD039`), `descriptive-link-text` (`MD059`).
- **Code, quotes, emphasis, and footnotes:** `code-fence-language`
  (`MD040`), `fence-unclosed`, `blockquote-blank-line` (`MD028`),
  `dollar-sign-in-shell-code` (`MD014`), `emphasis-space` (`MD037`),
  `footnote-unused`.
- **Tables:** `table-too-wide`, `table-column-count` (`MD056`),
  `table-grid-unformatted`, `grid-table-invalid`; see
  [[tables-and-links]].
