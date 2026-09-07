- NuGet package to contain internal .g.cs attribute with `[Conditional("NEVER")]`
  - Check that [Conditional] is sufficient to not need an optout for IVT
- Add F5 debug
- Build out readme
- Build out package

Things either blocked or defensive-copied by the presence of `readonly` on a field:

- Assignment expression (incl compound)
- `(readonlyField, _) =`
- `ref readonlyField`
- `readonlyField.NonReadOnlyMember()`
  - Incl implicitly through `foreach` and other constructs but only if they don't _always_ copy regardless of readonly
  - `new Action(readonlyField.NonReadOnlyMember)` is a copy but is not a defensive copy (boxing is a copy regardless of readonly)
    - Check boxing on `using (readOnlyField)` and all other lang constructs
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

For ref-like typed primary parameters, they can only be 

class B(int p);
class D(InlineArray3<int> a) : B(a[0] = 3);

- `[ReadOnly] ref` and `[ReadOnly] readonly ref` allowed, ensure that what is blocked is `= ref` reassignment and that mutations after reading the ref are not blocked

## Fixers

- Introduce explicit defensive copy (TODO: use temps instead of casts)
- Cast inline array to ROS before foreach? (preferable: analyze iteration variable recursively)
