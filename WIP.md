- F5 debugging is deferred.
- Usage, packaging, and compiler-behavior references are documented in README.md.

Things either blocked or defensive-copied by the presence of `readonly` on a field:

- Assignment expression (incl compound)
- `(readonlyField, _) =`
- `ref readonlyField`
- `readonlyField.NonReadOnlyMember()`
  - Incl implicitly through `foreach` and other constructs but only if they don't _always_ copy regardless of readonly
  - `new Action(readonlyField.NonReadOnlyMember)` is a copy but is not a defensive copy (boxing is a copy regardless of readonly)
    - Check boxing on `using (readOnlyField)` and all other lang constructs
  - Incl implicit `ToString()` in `p + ""` etc.
    - Make sure to test that enums are seen as readonly
- `readonlyField = ref`
  - whether it's a `ref readonly` or not

Allow taking pointer, that's allowed on readonly fields

The above also applies recursively to:

- `readOnlyField.NestedField`
  - However, if nested field is a ref field, this applies to `= ref` and taking a writeable ref back, and nothing else, because nothing else mutates the ref itself
- `readOnlyInlineArray[...]`

For readonly inline array:
- conversion to `Span<T>` is prohibited while conversion to `ReadOnlySpan<T>` remains available
- slicing produces `ReadOnlySpan<T>` rather than `Span<T>`
- foreach over inline array produces non-writable ref iteration variable
  - maybe analyze the iteration variable instead?

Ref-like typed primary parameters cannot be captured by instance members. Their uses in member initializers and base constructor arguments must still be analyzed.

class B(int p);
class D(InlineArray3<int> a) : B(a[0] = 3);

- `[ReadOnly] ref` and `[ReadOnly] ref readonly` parameters protect `= ref` reassignment, not mutations after reading the ref. The language's own `ref readonly` restrictions still apply.

## Fixers

- Introduce explicit defensive copy using a temporary, not an identity cast
- Inline-array ref iteration variables are analyzed recursively, rather than casting the collection before foreach.
  - Update point 5 in XML docs on the attribute definition to reflect the chosen approach
