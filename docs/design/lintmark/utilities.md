## Utilities

![Utilities Structure](UtilitiesView.svg)

### Overview

The `Utilities` subsystem provides shared utility functions for the LintMark. It
supplies reusable, independently testable helpers consumed by other subsystems. Its first responsibility is safe
file-path manipulation, protecting callers from path-traversal vulnerabilities when constructing paths from
caller-supplied inputs (`PathHelpers`). Its second is measuring how many terminal columns a text occupies, which the
line wrapping and table layout of later phases need (`TextWidth`). The `Utilities` subsystem contains two units:
`PathHelpers` and `TextWidth`.

### Interfaces

**PathHelpers.SafePathCombine**: Combines a base path and a relative path, rejecting any result
that escapes the base directory.

- *Type*: In-process .NET static method.
- *Role*: Provider.
- *Contract*: Accepts `string basePath` and `string relativePath`. Returns the combined path
  produced by `Path.Combine(basePath, relativePath)` after verifying that the resolved result
  remains within `basePath`. Preserves the caller's relative/absolute style in the return value.
- *Constraints*: Throws `ArgumentNullException` for null inputs; throws `ArgumentException`
  when the combined path escapes the base directory; may propagate `NotSupportedException` or
  `PathTooLongException` from underlying BCL path operations.

**TextWidth.Of**: Measures the display width of a text.

- *Type*: In-process .NET static method.
- *Role*: Provider.
- *Contract*: Accepts a `string` and returns the number of terminal columns it occupies, counting grapheme clusters:
  East Asian wide and full-width characters and emoji take two columns, combining marks, joiners, variation selectors,
  and control characters take none, and everything else takes one. Zero for an empty text.
- *Constraints*: Throws `ArgumentNullException` for a null text. Independent of the current culture.

### Design

The `Utilities` subsystem contains the two independent units `PathHelpers` and `TextWidth`. Neither has dependencies
on other tool units or subsystems; they use only .NET BCL types (`Path`, `ArgumentNullException`, `StringInfo`,
`Rune`).

`PathHelpers.SafePathCombine` is a pure utility method: it performs no file-system I/O, holds
no state, and throws immediately on invalid input. All calls to `SafePathCombine` in the
codebase originate from the `SelfTest` subsystem (`Validation`), which uses it to construct
log and result file paths inside temporary directories created during self-validation test
execution.

`TextWidth.Of` is likewise pure and stateless over an immutable table of wide character ranges. It has no production
caller yet: the line wrapper and the table formatter that are added later are its consumers. It exists now
because the width model is independent of any formatter and can be verified alone.
