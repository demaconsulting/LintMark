### SourcePatcher

![Formatting Structure](FormattingView.svg)

#### Purpose

`SourcePatcher` applies an ordered set of edits to the LF-normalized text of a document and returns the patched
text. It is a pure function: it changes nothing else, and text that no edit covers is copied unchanged.

#### Data Model

`SourcePatcher` is a static class with no state. Its input is a `SourceDocument` and an `ImmutableArray<TextEdit>`
in the order that `EditSet.ToImmutable` produces: ascending by start offset and then length, without conflicts.

#### Key Methods

**Apply**: Applies edits to the text of a document.

- *Parameters*: `SourceDocument source`; `ImmutableArray<TextEdit> edits`.
- *Returns*: `string` — the patched text; the very same string instance as `source.Text` when there are no edits.
- *Preconditions*: `source` is not null and `edits` is initialized.
- *Postconditions*: the result is the source text with every edit's range replaced by its replacement, in one ascending
  pass; characters outside every edit's range are unchanged and in their original order.

Algorithm: (1) validate the arguments; (2) return the source text itself for an empty array; (3) walk the edits in
order with a `StringBuilder` of the text length as its initial capacity: first check the edit with `FindProblem`
(see below), then append the untouched text between the end of the previous edit and the start of this one, then the
replacement, and remember the end of the edit; (4) append the text after the last edit. The cost is linear in the
text length plus the total length of the replacements.

**FindProblem** (private): Checks one edit against the text length and the edit before it.

- *Returns*: a description of what is wrong, or `null` when the edit is valid.

An edit is wrong when it is a default instance (no replacement or no rule), when its end lies beyond the text length,
or when it is out of order or conflicts with the previous edit: it starts before the previous edit's end, or both are
insertions at the same offset. A conflict that `EditSet` would have caught is therefore caught again here, because the
patcher does not trust its input.

#### Error Handling

`ArgumentNullException` for a null source. `ArgumentException` (parameter `edits`) for the uninitialized default
array and for any edit that `FindProblem` rejects, with a message that names the offending range. The exceptions
propagate to `LintMarkEngine`, which reports them as a `file-format-error` for that file; no partial text is ever
returned, because the result is built completely before it is returned.

#### Dependencies

- **SourceDocument** (`Document` subsystem) — the LF-normalized text that is patched.
- **TextEdit** (`EditSet` unit) — the edit type.
- **.NET BCL** — `StringBuilder` and immutable arrays.

#### Callers

- **LintMarkEngine** — applies the edits of the `FormatResult` to obtain the proposed text, both for the file itself
  and for the formatting step of the idempotency check.
