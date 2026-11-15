# Migrating from markdownlint

LintMark keeps the numbers of the markdownlint rules (`MD001` to `MD060`) as aliases, so a rule can be named the way it
is named in markdownlint. This chapter shows how each markdownlint rule maps to LintMark, how to translate a
markdownlint configuration into a `.lintmark.yaml`, and what behaves differently.

## What Changes

- **LintMark fixes as well as reports.** markdownlint reports problems (and can fix some of them). LintMark has 26
  formatting rules that `--fix` always applies, 3 configurable formatting rules, and 24 rules that only report.
- **Most formatting rules are not optional.** A rule that LintMark always applies cannot be switched off in
  `.lintmark.yaml`; each has a setting that selects the canonical form instead (for example `bullet`), and a
  suppression comment leaves a passage alone. The 27 rules that can be switched off are listed under *Rules* in
  *Configuration*.
- **One canonical form instead of `consistent`.** Where markdownlint accepts "the style of the first use in the file"
  (`consistent`), LintMark writes the configured style, which has a default.
- **Rule identifiers are readable names.** Findings are reported with a kebab-case identifier such as
  `heading-increment`; the markdownlint number is accepted wherever a rule is named: in `rules`, in `--enable` and
  `--disable`, and in suppression comments.
- **Both comment families work.** `markdownlint-disable` comments are understood next to `lintmark-disable` comments
  (see *Suppression*), so existing comments keep their meaning.
- **Some rules are dropped.** `MD002`, `MD006`, `MD008`, `MD025`, `MD033`, `MD041`, `MD043`, `MD044`, and `MD057` are
  not implemented. Naming them in `rules`, `--enable`, or `--disable` is accepted and ignored, so a migrated
  configuration stays valid.
- **The exit code is 0 or 1.** `1` means that something was reported, as with markdownlint.

## Rule Mapping

The kind tells what LintMark does with the rule: "fixed" rules are rewritten by `--fix`, and "reported only" rules only
produce a finding. A default of "on" or "off" applies to the rules that a configuration can switch.

| markdownlint | LintMark rule | Kind | Default | Note |
| --- | --- | --- | --- | --- |
| `MD001` | `heading-increment` | reported only | on | |
| `MD002` | none | dropped | | accepted and ignored |
| `MD003` | `heading-style` | fixed, always | | setext headings become ATX headings |
| `MD004` | `bullet` | fixed, always | | one marker, `dash` by default |
| `MD005` | `list-indent` | fixed, always | | |
| `MD006` | none | dropped | | accepted and ignored |
| `MD007` | `list-indent` | fixed, always | | indent `2` or `4` |
| `MD008` | none | dropped | | accepted and ignored |
| `MD009` | `trailing-whitespace` | fixed, always | | a hard break is exactly two spaces |
| `MD010` | `hard-tabs` | fixed, always | | |
| `MD011` | `link-reversed` | reported only | on | |
| `MD012` | `max-blank-lines` | fixed, always | | `max-blank-lines` |
| `MD013` | `line-length` | fixed, can be switched off | on | code, tables, and headings are exempt |
| `MD014` | `dollar-sign-in-shell-code` | reported only | off | |
| `MD018` | `heading-missing-space` | reported only | on | |
| `MD019` | `heading-space` | fixed, always | | |
| `MD020` | `heading-closed-missing-space` | reported only | on | |
| `MD021` | `heading-style` | fixed, always | | closed headings become ATX headings |
| `MD022` | `heading-blank-lines` | fixed, always | | |
| `MD023` | `heading-start-left` | fixed, always | | |
| `MD024` | `heading-duplicate` | reported only | on | |
| `MD025` | none | dropped | | accepted and ignored |
| `MD026` | `heading-trailing-punctuation` | reported only | on | |
| `MD027` | `blockquote-space` | fixed, always | | |
| `MD028` | `blockquote-blank-line` | reported only | on | |
| `MD029` | `ordered-style` | fixed, always | | `incrementing` or `one` |
| `MD030` | `list-marker-space` | fixed, always | | |
| `MD031` | `fence-blank-lines` | fixed, always | | |
| `MD032` | `list-blank-lines` | fixed, always | | |
| `MD033` | none | dropped | | accepted and ignored |
| `MD034` | `bare-urls` | fixed, always | | `bare-urls: leave` keeps bare URLs |
| `MD035` | `thematic-break` | fixed, always | | `thematic-break` |
| `MD036` | `emphasis-as-heading` | reported only | on | |
| `MD037` | `emphasis-space` | reported only | on | |
| `MD038` | `code-span-space` | fixed, always | | |
| `MD039` | `link-space` | reported only | on | |
| `MD040` | `code-fence-language` | reported only | on | |
| `MD041` | none | dropped | | accepted and ignored |
| `MD042` | `link-empty` | reported only | on | |
| `MD043` | none | dropped | | accepted and ignored |
| `MD044` | none | dropped | | accepted and ignored |
| `MD045` | `image-alt-text` | reported only | on | |
| `MD046` | `code-block-style` | fixed, always | | fenced code blocks only |
| `MD047` | `final-newline` | fixed, always | | `final-newline` |
| `MD048` | `code-fence` | fixed, always | | `backtick` or `tilde` |
| `MD049` | `emphasis` | fixed, always | | `asterisk` or `underscore` |
| `MD050` | `strong` | fixed, always | | `asterisk` or `underscore` |
| `MD051` | `link-fragment` | reported only | on | |
| `MD052` | `link-reference-undefined` | reported only | on | |
| `MD053` | `link-definition-unused` | reported only | on | |
| `MD054` | `link-refactor` | fixed, can be switched off | on | only the opt-in conversion to reference links |
| `MD055` | `table-pipe-style` | fixed, always | | pipes at both ends of every line |
| `MD056` | `table-column-count` | reported only | on | |
| `MD057` | none | dropped | | accepted and ignored |
| `MD058` | `table-blank-lines` | fixed, always | | |
| `MD059` | `descriptive-link-text` | reported only | off | |
| `MD060` | `table-column-style` | fixed, always | | `tables: compact` or `aligned` |

markdownlint has no `MD015`, `MD016`, or `MD017`. LintMark has some rules that markdownlint does not have, and that
have no number: `paragraph-wrap` (the `sentence` and `semantic` wrap modes), `table-too-wide`,
`table-grid-unformatted`, `fence-unclosed`, `grid-table-invalid`, and `footnote-unused`.

## Translating a Configuration

Most markdownlint options correspond to one LintMark setting. Settings that LintMark does not have are not needed,
because LintMark always applies one canonical form:

| markdownlint | `.lintmark.yaml` |
| --- | --- |
| `MD003: { style: atx }` | `heading-style: atx` (the only value) |
| `MD004: { style: dash }` | `bullet: dash` (or `asterisk`, `plus`) |
| `MD007: { indent: 4 }` | `list-indent: 4` (`2` or `4`) |
| `MD009: { br_spaces: 2 }` | `trailing-whitespace: strip` (the only value) |
| `MD012: { maximum: 2 }` | `max-blank-lines: 2` |
| `MD013: { line_length: 100 }` | `line-length: 100` (`0` for no limit) |
| `MD013: false` | `rules: { MD013: false }` |
| `MD029: { style: one }` | `ordered-style: one` (`incrementing` is the default) |
| `MD034: false` | `bare-urls: leave` |
| `MD035: { style: "***" }` | `thematic-break: asterisks` (`dashes`, `asterisks`, `underscores`) |
| `MD046: { style: fenced }` | `code-block-style: fenced` (the only value) |
| `MD048: { style: tilde }` | `code-fence: tilde` |
| `MD049: { style: underscore }` | `emphasis: underscore` |
| `MD050: { style: underscore }` | `strong: underscore` |
| `MD060: { style: aligned }` | `tables: aligned` |
| `ignores: [...]` | `ignore: [...]` (replaces the built-in defaults) |
| `MD001: false` (any rule that can be switched) | `rules: { MD001: false }` |

Note these differences in defaults, which matter when a markdownlint configuration relied on markdownlint's own
defaults:

- `line-length` is `120`; markdownlint's `MD013` defaults to 80.
- The style rules have a fixed default instead of `consistent`: `bullet: dash`, `thematic-break: dashes`,
  `code-fence: backtick`, `emphasis: asterisk`, `strong: asterisk`, and `ordered-style: incrementing`.
- `MD025`, `MD033`, and `MD041` do not exist, so a configuration that only disabled them (as in many repositories)
  needs no replacement.
- `bare-urls` is `autolink`, so `--fix` wraps bare URLs in angle brackets unless you set `bare-urls: leave`, and
  `final-newline` is `true`, so a file must end with exactly one line ending (`final-newline: false` requires the
  opposite, which is not the same as disabling `MD047`).

## Comments

LintMark understands the comment forms of markdownlint, with the rule numbers as well as the identifiers:

```markdown
<!-- markdownlint-disable MD013 -->

A passage whose long lines are left as they are.

<!-- markdownlint-enable MD013 -->
```

`lintmark-disable`, `lintmark-enable`, `lintmark-disable-next-line`, `lintmark-disable-line`, and
`lintmark-disable-file` behave the same, and a passage is left exactly as it is when a rule that applies to it is
suppressed (see *Suppression*). `line-ending` and the failure identifiers cannot be suppressed.

## Migration Steps

1. Translate the markdownlint configuration with the tables above. A configuration that matches the defaults of
   LintMark needs no `.lintmark.yaml` at all, apart from the `ignore` patterns.
2. Run `lintmark --check` over the same files as markdownlint and compare the findings. Use `--enable` and `--disable`
   to switch a rule for a single run.
3. Run `lintmark --diff` to review what `--fix` would change, then run `lintmark --fix` and review the change in your
   version control system.
4. Replace the markdownlint command in your build with `lintmark --check`, which exits with `1` when anything is
   reported.

For example, this markdownlint-cli2 configuration:

```yaml
config:
  default: true
  MD003:
    style: atx
  MD007:
    indent: 2
  MD013:
    line_length: 120
  MD025: false
  MD033: false
  MD041: false

ignores:
  - "**/.git/**"
  - "**/node_modules/**"
  - "**/generated/**"
  - "**/AGENT_REPORT_*.md"
```

needs only this `.lintmark.yaml`, because ATX headings, an indent of `2`, and a line length of `120` are the defaults
and the three disabled rules do not exist. The first three ignore patterns have a built-in equivalent, but the `ignore`
list replaces the defaults, so list every pattern that is still wanted:

```yaml
ignore:
  - ".git/**"
  - "node_modules/**"
  - "**/generated/**"
  - "**/AGENT_REPORT_*.md"
```
