# LintMark

## Verification Approach

The LintMark system is verified at four levels:

- **Unit tests** verify each unit in isolation with real collaborators and no mocks.
- **Subsystem tests** (`*SubsystemTests.cs`, one class per subsystem) drive a subsystem from its outer boundary.
- **System tests** (`IntegrationTests.cs`, `IntegrationWrapTests.cs`, `IntegrationOptionTests.cs`,
  `IntegrationRuleTests.cs`, and `IntegrationLintTests.cs`) start the built tool as a separate process through the
  `Runner` helper, with real files in a `TemporaryDirectory`, and assert on the output text, the exit code, and the
  files on disk. They are the end-to-end tests of the command line contract.
- **Self-validation** (`--validate`) runs two built-in tests of the shipped binary on every platform and runtime in
  CI. It provides the platform evidence for Windows, Linux, macOS, .NET 10, and .NET 11.

Tests are written with xUnit in the `DemaConsulting.LintMark.Tests` project; the libraries that LintMark depends on
are verified as OTS items in `OtsSoftwareTests`. No mocks are used anywhere in the product tests: the tool has no
network or database dependency, and every collaborator is cheap and deterministic. The few injectable seams (a file
writer and a formatter in the engine, a hook in the atomic writer) exist only to provoke failures that the file system
cannot produce on demand.

### Correctness of formatting is verified at development time

The tool contains no run-time proof that a rewrite is correct: it does not render the text before and after, compare
structure, or format twice. Instead, correctness of the formatting is established before release by the corpus
harness `tools/LintMark.Verify`, a non-shipping development tool that is not part of the system, the package, or
the review structure. The harness formats every file of a curated corpus in memory, with the product's formatter and
the full set of fixes, and checks three properties for each file and option set:

- **HTML equivalence**: the formatted text renders to the same HTML as the original, also with the line endings the
  file has on disk (the two renderings differ for some code spans, so both are compared).
- **Structure**: the Pandoc structure tokens of the formatted text equal those of the original.
- **Idempotency**: formatting the formatted text changes nothing, byte for byte.

A refusal by design (an invalid grid table, an ignored file, an undecodable file) counts as a pass; a crash, a
timeout, or a verifier error is a failure. The corpus is fetched by `tools/LintMark.Verify/fetch-corpus.ps1` from the
manifest `corpus-sources.json` into the folder `.corpus` and has these groups: specification examples (CommonMark,
GitHub Flavored Markdown, and Markdig extensions), adversarial fixtures from other Markdown tools, small real-world
samples, and the DEMA Consulting repositories. Diversity is preferred over quantity. Each file is run with the default
configuration and with an option matrix of every combination of the wrap modes (`fill`, `sentence`, `semantic`), line
lengths (120, 80, 40), table styles (`compact`, `aligned`), and link refactoring (off, on). A run is started with
`dotnet run --project tools/LintMark.Verify -- <paths> [--matrix]` and writes a Markdown report; the exit code is 0
when every check passed. The harness has its own tests (`tools/LintMark.Verify.Tests`) that run in CI with
`dotnet test`. Its results are interpreted as follows: any failure kind is a defect of the product to fix before the
change is released, never a reason to weaken a check.

## Test Environment

- **Operating systems**: Windows, Linux, and macOS (the CI matrix); .NET 10 and .NET 11 runtimes.
- **Process runner**: system tests start the compiled tool with `dotnet <dll> <arguments>` and capture the standard
  output, standard error, and exit code. `Runner.Run` accepts a working directory for scenarios that depend on it.
- **Files**: each test creates a `TemporaryDirectory` with an empty `.git` folder, so no configuration or settings file
  above the tree is read, and removes it afterward.
- **Concurrency**: tests that capture the console, change the current directory, or start the tool are in the
  `Sequential` collection and run one after another.
- **Test oracle**: the unit and subsystem tests of the `Formatting` subsystem use the HTML-equivalence and structure
  checks of the non-shipping development tool `tools/LintMark.Verify` (referenced by the test project) as an oracle.
- **Corpus harness**: needs network access once to fetch the corpus; its structure check reads the Pandoc structure
  tokens in process and needs no Pandoc installation. It is run by a developer before a release and not by the unit
  test run.
- No external service, database, or credentials are needed for the tests.

## Acceptance Criteria

- Every unit, subsystem, system, and OTS test passes with zero failures, and the self-validation passes on every
  CI platform and runtime.
- The ReqStream trace matrix shows every requirement covered by passing tests (`reqstream --enforce`).
- Before a release the corpus harness passes over the whole corpus with the default configuration and the full option
  matrix: no HTML mismatch, structure mismatch, non-idempotent result, crash, or timeout.
- Exit code 0 means nothing was reported; exit code 1 means a finding, an error, or a usage problem.
- Findings are `path:line:col: rule-id message`, in a stable order.

## Test Scenarios

The scenarios are grouped by the feature of the requirements; each is an end-to-end run of the tool.

### Command line

**Version prints only the version**: `--version` or `-v` prints the version and exits with 0.

**Help lists every option**: `-?`, `-h`, and `--help` print the usage with every option.

**Default run lints every Markdown file below the current directory**: With no paths, the banner is printed and every
`.md` file is checked except those in the folders that the default `ignore` setting skips (`.git`, `node_modules`,
`.venv`, `generated`, `thirdparty`, and `.agent-logs`).

**Paths, directories, globs, and exclusions select files**: Named files are always processed, each file once, and a
`!` pattern removes matches.

**Check reports and never writes**: `--check` (the default) reports findings and leaves every file unchanged.

**Fix rewrites to canonical form**: `--fix` rewrites a CRLF file to LF, a second run changes nothing, and findings that
cannot be fixed are still reported.

**Diff shows the change and writes nothing**: `--diff` prints a unified diff of the rewrite.

**Conflicting and unknown options are usage errors**: An unknown option, `--fix` with `--check`, and a bad value
exit with 1.

**Explicit and disabled configuration**: `--config <file>` replaces discovery; `--no-config` uses the defaults and
ignores `.editorconfig`.

**Rule switches win over configuration**: `--enable` and `--disable` by id or `MDxxx` number; an unknown rule is a
usage error.

**Silent and log output**: `--silent` suppresses the console; `--log <file>` also writes the output to a file.

**Self-validation writes results**: `--validate` prints the summary; `--results` (or `--result`) writes TRX or JUnit,
and `--depth 2` makes the report heading a second-level (`##`) heading. A `--depth` without a value is a usage error.

**Exit codes**: 0 when nothing is reported; 1 for any finding, error, or usage problem. An unexpected internal error
is the one case outside this contract (the tool reports it and re-throws it, so the runtime sets the failure code); it
cannot be provoked from the command line and is not covered by a system scenario.

**Problem lines have a fixed format**: Each is `path:line:col: rule-id message`, in path then position order, also for
configuration errors.

### Configuration and files

**Nearest configuration applies per file**: Configuration files at different depths each apply to their own files
without merging, also when the nearest file is invalid.

**Every documented setting has its default and its effect**: A sample file changes the behavior each setting names.

**Invalid configuration is reported once and the file is never rewritten**: A bad value, an unknown key, and bad
syntax are reported with positions; `--fix` leaves the Markdown file unchanged.

**Ignore patterns skip files**: Globs in `ignore` skip files; an explicitly named file is still processed.

**Line ending follows the configuration, then `.editorconfig`, then the majority**: A BOM and the text are kept;
unreadable editor settings are reported.

**Rule aliases are accepted everywhere**: `MDxxx` works wherever a rule id does, retired aliases are ignored, and
findings always show ids.

**Suppression comments leave a passage alone**: Disable and enable comments, in either family's spelling, silence the
named rules over the range they cover.

**Unreadable, non-UTF-8, bare-CR, and unparsable files are reported without stopping the run**: Other files are still
processed.

### Safe rewriting

**Rewrites are atomic and conservative**: No temporary file remains, a file that changed after reading is never
overwritten, and nothing is written for a format-unsafe document (an invalid grid table).

**Fix is stable and protective**: A second fix finds nothing; code, HTML, math, and front matter are never changed.

**Fix keeps lint-only findings**: `--fix` makes no edit for a lint-only finding and reports it at its position in the
written file.

### Formatting

**Whitespace**: Trailing whitespace, tabs, runs of blank lines, and the final newline are normalized.

**Headings, block spacing, code blocks, thematic breaks, lists, and block quotes**: Each is rewritten to the
configured style; lists are never merged or split.

**Inline style**: Emphasis, strong emphasis, bare URLs, and code-span padding are normalized.

**Tables**: Pipes, and the `compact` (default) or `aligned` layout, are applied.

**Link refactor**: When enabled, long inline links become reference links in one definitions block.

**Wrapping fills overflow only**: With `line-length` 120 (0 = unlimited), only paragraphs with a breakable overflow are
rewrapped, and paragraphs within the width stay byte-identical.

**Sentence and semantic wrapping**: The two modes, and the `paragraph-wrap` rule that selects them, break lines at
sentences and clauses.

**Wrapping is safe**: Links, code spans, HTML, hard breaks, and container prefixes stay whole, no line starts block
syntax, and a paragraph that cannot be fixed is reported.

**Width counts display columns**: Wide characters count as two columns and combining marks add none. A tab has no
width of its own (positions count it as one column and the whitespace rules expand it to four-column tab stops), so a
paragraph that holds a tab is kept as written.

### Lint checks

**Heading, link, code, table, and miscellaneous checks report their findings**: Each rule family reports its
violations at the documented position; the rule ids are those of the rule catalog.

### Platforms

**Self-validation passes on each platform and runtime**: The version and help displays run on Windows, Linux, macOS,
.NET 10, and .NET 11.
