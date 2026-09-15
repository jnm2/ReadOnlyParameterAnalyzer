# Instrumental.ReadOnlyParameterAnalyzer

A C# analyzer that enforces readonly intent on parameters, including primary
constructor parameters. It reports **IRP0001** (an error by default) when code
could mutate a parameter's storage or would need a defensive copy if that
storage were a readonly field.

## Install

```xml
<PackageReference Include="Instrumental.ReadOnlyParameterAnalyzer" Version="1.0.0"
                  PrivateAssets="all" />
```

The package includes the analyzer, IDE code fixes, and an internal
`Instrumental.Annotations.ReadOnlyAttribute` source file. There is no runtime
package dependency. The analyzer requires a host supporting Roslyn 5.9 or later;
the examples using primary constructors require C# 12 or later.

The attribute source is packaged in the NuGet package's `build` folder and
added as a `Compile` item during project evaluation. It uses `Visible="false"`
to stay hidden in the consuming project's Solution Explorer.

## Usage

```csharp
using Instrumental.Annotations;

class Counter([ReadOnly] int initialValue)
{
    public int Value => initialValue;

    void Reset()
    {
        initialValue = 0; // IRP0001
    }
}
```

The attribute also works on ordinary method, constructor, local-function, and
lambda parameters. `[ReadOnly]` and `[ReadOnly(true)]` enable the rule;
`[ReadOnly(false)]` leaves the parameter mutable.

Primary constructor parameters are checked in member initializers and base
constructor arguments as well as instance member bodies. Ref-like and
by-reference primary parameters cannot be captured by instance members (a C#
restriction), but their uses in initializers and base arguments are still
checked.

Readonly is **shallow**, just like a readonly field: a reference-typed parameter
cannot be reassigned, but its referenced object's members can be changed.
Likewise, storing a reference in a struct does not make the referenced object
immutable.

The analyzer checks assignments (including compound assignments and
deconstruction), increments/decrements, writable references, and non-readonly
struct member calls. These rules follow nested value-type fields and inline
array elements. Readonly members and operations that already work on independent
copies are allowed. Readonly property/indexer setters are allowed too; a compound
write can still require a defensive copy for a non-readonly getter.

Implicit calls are checked too, such as enumeration and awaiter acquisition.
Delegate creation, `using` expressions, and pattern matching already operate on
copies of struct values and are not treated as mutations merely because they
invoke a non-readonly member. Enumeration follows Roslyn's actual lowering:
the specification's cast-based expansion does not imply that every `foreach`
already copies its collection.

For `[ReadOnly] ref` and `[ReadOnly] ref readonly` parameters, the annotation
protects the **reference binding** against `= ref` reassignment. It does not add
readonly restrictions to the referent. C#'s own `ref readonly` restrictions still
apply. Ref fields similarly separate changing the reference from changing the
value it references. Unsafe pointer access is not prohibited by this analyzer.

Inline arrays cannot expose a writable `Span<T>` from protected storage;
`ReadOnlySpan<T>` conversions remain valid. Slicing and by-reference iteration
are checked so that they do not provide a writable path back to the parameter.
An inferred slice is still a writable span and is diagnosed, even if its
immediate use only reads `Length`; use an explicit `ReadOnlySpan<T>` destination.
By-value iteration copies elements and is allowed. Writable ref iteration
variables are analyzed recursively at their uses rather than rejected at the
foreach declaration.

## Explicit defensive copies

C# ordinarily introduces defensive copies when non-readonly members are invoked
on readonly struct storage. This analyzer makes that choice explicit:

```csharp
void Read([ReadOnly] MutableValue value)
{
    var valueCopy = value;
    valueCopy.Read(); // Any mutation of the struct affects only this local.
}
```

The IDE fix introduces a temporary rather than relying on an identity cast.
It is offered only for compiler defensive-copy scenarios and only where the
rewrite can preserve evaluation order and control flow. It is not offered for
assignments or writable-reference escapes that a readonly field would reject.
In complex expressions, introduce the copy manually at the intended evaluation
point. A copy is shallow and does not isolate objects referenced by the struct.

Common statements, expression-bodied members and delegate lambdas, event
subscriptions, and foreach collections support the fix. It deliberately avoids
hoisting across argument evaluation, conditional branches, or loop conditions,
and does not rewrite expression trees or ref returns. Ref-like temporaries are
checked for compiler escape-scope errors and are not introduced in async or
iterator contexts.

## Attribute emission and friend assemblies

The packaged attribute is internal. Both the attribute type and its applications
are emitted into the consuming assembly.

`InternalsVisibleTo` can expose it to a friend assembly that also generates its
own copy, producing C# warning CS0436. In the friend project, reuse the accessible
attribute by disabling generation:

```xml
<PropertyGroup>
  <GenerateReadOnlyParameterAttribute>false</GenerateReadOnlyParameterAttribute>
</PropertyGroup>
```

The same option supports supplying your own attribute with the exact namespace,
name, and constructors. The analyzer checks annotated source declarations, not
callers in other assemblies. A friend project's declaration is still analyzed
when it uses the shared attribute type.

## Configuration

Standard diagnostic configuration applies:

```ini
[*.cs]
dotnet_diagnostic.IRP0001.severity = warning
```

No project-wide default readonly policy is applied; parameters must be annotated.

## Build and test

Use the .NET SDK selected by `global.json`. The test executable currently targets
.NET Framework 4.7.2, so running it requires Windows with .NET Framework installed.

```powershell
dotnet build Instrumental.ReadOnlyParameterAnalyzer.slnx -c Release
dotnet test --project src\Instrumental.ReadOnlyParameterAnalyzer.Tests
dotnet pack src\Instrumental.ReadOnlyParameterAnalyzer.Package -c Release
```

The NuGet package is written to the package project's `bin\Release` directory.

## Language references

- [Readonly fields](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/readonly)
- [Ref fields and ref reassignment](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-11.0/low-level-struct-improvements.md)
- [Inline array conversions, slicing, and iteration](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/inline-arrays.md)
- [Primary constructor capture restrictions](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/primary-constructors.md#semantics)
- [Identity casts and defensive copies](https://github.com/dotnet/roslyn/issues/84307)

Licensed under the [MIT license](LICENSE.txt).
