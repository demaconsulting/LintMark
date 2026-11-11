### ReferenceIndex

![Document Structure](DocumentView.svg)

#### Purpose

`ReferenceIndex` lists the link reference definitions of a document (`[label]: url "title"`) and
resolves a reference to its definition by label, the way Markdown does.

#### Data Model

**Definitions**: `IReadOnlyList<ReferenceDefinition>` — every definition in source order, including
duplicates.

**ReferenceDefinition**: A record `(string Label, string NormalizedLabel, string Url, string? Title, int Start,
int EndInclusive, int Line)`. `Label` is the label as the parser reports it (case kept, internal whitespace
already collapsed); `NormalizedLabel` has whitespace collapsed and the case folded and is used for
matching; `Start` and `EndInclusive` are offsets of the definition's own range; `Line` is 1-based.

Definitions come from the definition blocks' own ranges, never from the parser's group shells or tree
order (quirk Q-3), so positions are exact and in source order. Footnote definitions, referenced or not,
are not link reference definitions and are excluded.

#### Key Methods

**Build**: Builds the index from the flattened blocks of a document.

- *Parameters*: `SourceDocument source`, `IReadOnlyList<BlockInfo> blocks`.
- *Returns*: `ReferenceIndex` (empty when there are no definitions).
- *Preconditions*: arguments are not null.

`Build` takes the `SourceDocument` to compute line numbers. It records every `LinkReferenceDefinition`
block and registers the first definition of each normalized label for lookup (the first definition wins,
as in CommonMark); duplicates stay visible in `Definitions`.

**NormalizeLabel**: Static. Trims the label, collapses every run of whitespace to one space, and
lower-cases it with the invariant culture (the closest available approximation of case folding).

**TryGet**: Looks a definition up by label.

- *Parameters*: `string label` — case and whitespace insensitive; `out ReferenceDefinition? definition`.
- *Returns*: `bool`.

#### Error Handling

`ArgumentNullException` for null arguments. A label or URL that the parser leaves null becomes an empty
string. The index is immutable after `Build`.

#### Dependencies

- **SourceDocument** — line numbers.
- **Markdig** — the `LinkReferenceDefinition` block type.
- `BlockInfo` and `BlockKind` — plain value types that the `DocumentParser` unit declares and produces.
  They are inputs only, so there is no call back into the parser and no dependency cycle.

#### Callers

- **DocumentParser** — builds an index for every parsed document. In the unit tests the inputs are built
  with `DocumentParser.Parse`, which is a test-time dependency only.
